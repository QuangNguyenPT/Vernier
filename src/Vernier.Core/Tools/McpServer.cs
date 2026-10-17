using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Vernier.Core.Tools;

/// <summary>
/// A loopback Model Context Protocol server (Streamable HTTP, JSON responses, no server-sent
/// events) so CLI providers such as Claude Code can call Vernier's tools. Every call goes through
/// the same <see cref="ToolExecutor"/> as native tool calling.
/// </summary>
/// <remarks>
/// Defence in depth: bound to 127.0.0.1 only; the Host header must be exactly that address and
/// port; any Origin header is refused (browsers always send one, so a web page cannot reach it
/// through DNS rebinding); every request needs the bearer token of a live turn; a turn only sees
/// and runs the tools it was granted, and nothing runs after it ends.
/// </remarks>
public sealed class McpServer : IAsyncDisposable
{
    public const string ServerName = "vernier";
    private static readonly string[] Versions = ["2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05"];
    private readonly ToolExecutor _executor;
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, ToolTurn> _turns = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    private readonly DiagnosticLogSink? _log;

    public McpServer(ToolExecutor executor, DiagnosticLogSink? log = null)
    {
        _executor = executor;
        _log = log;
        var random = new Random();
        for (var attempt = 0; ; attempt++)
        {
            Port = random.Next(49152, 65500);
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/{PathSegment}/");
            try { _listener.Start(); break; }
            catch (HttpListenerException) when (attempt < 20) { }
        }
        _loop = Task.Run(AcceptAsync);
    }

    public int Port { get; private set; }
    private const string PathSegment = "mcp";
    public string Url => $"http://127.0.0.1:{Port}/{PathSegment}/";

    /// <summary>Lets this turn's token call its granted tools. Ending the turn revokes it.</summary>
    public IDisposable Open(ToolTurn turn)
    {
        _turns[turn.Token] = turn;
        return new Revoker(() => { turn.End(); _turns.TryRemove(turn.Token, out _); });
    }

    /// <summary>The config file content Claude Code reads with --mcp-config. Contains the turn token: write it to a private file.</summary>
    public string ClientConfig(ToolTurn turn) => new JsonObject
    {
        ["mcpServers"] = new JsonObject
        {
            [ServerName] = new JsonObject
            {
                ["type"] = "http", ["url"] = Url,
                ["headers"] = new JsonObject { ["Authorization"] = "Bearer " + turn.Token },
            },
        },
    }.ToJsonString();

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        JsonNode? requestId = null; // known once the body is parsed, so even an internal failure is answered to the right request
        try
        {
            if (req.Headers["Host"] != $"127.0.0.1:{Port}") { await Reply(res, 403, null); return; }
            if (!string.IsNullOrEmpty(req.Headers["Origin"])) { await Reply(res, 403, null); return; }
            var auth = req.Headers["Authorization"] ?? "";
            ToolTurn? turn = null;
            if (!auth.StartsWith("Bearer ", StringComparison.Ordinal) || !_turns.TryGetValue(auth[7..], out turn) || !turn.IsActive)
            {
                if (turn is { IsActive: false }) _turns.TryRemove(auth[7..], out _); // expired or ended: forget it
                res.AddHeader("WWW-Authenticate", "Bearer");
                await Reply(res, 401, null);
                return;
            }
            // A client that names a protocol version we do not speak is told so (spec: 400), rather than answered in another dialect.
            if (req.Headers["MCP-Protocol-Version"] is { Length: > 0 } header && !Versions.Contains(header)) { await Reply(res, 400, Error(null, -32600, $"Unsupported MCP-Protocol-Version {header}. Supported: {string.Join(", ", Versions)}.")); return; }
            switch (req.HttpMethod)
            {
                case "GET": res.AddHeader("Allow", "POST"); await Reply(res, 405, null); return; // no server-initiated stream
                case "DELETE": await Reply(res, 405, null); return; // no sessions to end
                case "POST": break;
                default: res.AddHeader("Allow", "POST"); await Reply(res, 405, null); return;
            }
            if (req.ContentLength64 > MaxBody) { await Reply(res, 413, null); return; }
            var body = await ReadBoundedAsync(req.InputStream).ConfigureAwait(false);
            if (body is null) { await Reply(res, 413, null); return; }
            var parsed = Providers.ProviderHttp.TryParse(body);
            if (parsed is JsonArray) { await Reply(res, 400, Error(null, -32600, "Batched messages are not supported (MCP 2025-06-18 and later do not use them).")); return; }
            if (parsed is not JsonObject msg) { await Reply(res, 400, Error(null, -32700, "Parse error")); return; }
            var id = msg["id"]?.DeepClone();
            requestId = id?.DeepClone();
            if (msg["method"] is not JsonValue methodValue || methodValue.TryGetValue<string>(out var method) is false)
            {
                // No method: a response to something a server asked (we ask nothing) is acknowledged; anything else is malformed.
                if (msg["result"] is not null || msg["error"] is not null) { await Reply(res, 202, null); return; }
                await Reply(res, 400, Error(id, -32600, "Invalid Request: no method.")); return;
            }
            if (id is null) { await Reply(res, 202, null); return; } // notification (initialized, cancelled, ...)
            JsonNode response = method switch
            {
                "initialize" => Result(id, Initialize(msg["params"] as JsonObject)),
                "ping" => Result(id, new JsonObject()),
                "tools/list" => Result(id, new JsonObject { ["tools"] = ListTools(turn) }),
                "tools/call" => msg["params"]?["name"] is JsonValue { } nameValue && nameValue.TryGetValue<string>(out var toolName) && toolName.Length > 0
                    ? Result(id, await CallAsync(msg["params"] as JsonObject, turn).ConfigureAwait(false))
                    : Error(id, -32602, "Invalid params: tools/call needs params.name."),
                _ => Error(id, -32601, $"Method not found: {method}"),
            };
            await Reply(res, 200, response);
        }
        catch (Exception e) when (e is HttpListenerException or IOException or ObjectDisposedException)
        {
            try { res.Abort(); } catch (ObjectDisposedException) { }
        }
        catch (Exception e)
        {
            // The last line of a server: an unanswered request would leave the CLI waiting minutes for a tool. Logged and answered.
            _log?.Invoke($"MCP request failed: {e.GetType().Name}");
            try { await Reply(res, 200, Error(requestId, -32603, "Internal error in Vernier's tool server.")); }
            catch (Exception inner) when (inner is HttpListenerException or IOException or ObjectDisposedException or InvalidOperationException) { }
        }
    }

    private const int MaxBody = 4 * 1024 * 1024;

    // Chunked requests have no length up front; read at most MaxBody bytes.
    private static async Task<string?> ReadBoundedAsync(Stream input)
    {
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        int n;
        while ((n = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            if (ms.Length + n > MaxBody) return null;
            ms.Write(buffer, 0, n);
        }
        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }

    private static JsonObject Initialize(JsonObject? p)
    {
        var asked = p?["protocolVersion"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        return new JsonObject
        {
            ["protocolVersion"] = Versions.Contains(asked) ? asked : Versions[0],
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = ServerName, ["title"] = "Vernier", ["version"] = typeof(McpServer).Assembly.GetName().Version?.ToString(3) ?? "1" },
            ["instructions"] = "Vernier's tools for the learner's current question: look at and mark the screen, recall the session and context, operate the application or notes only after the learner approves each step.",
        };
    }

    private JsonArray ListTools(ToolTurn turn) =>
        [.. _executor.Registry.Definitions.Where(d => turn.Allowed.Contains(d.Name)).Select(d => (JsonNode)new JsonObject
        {
            ["name"] = d.Name, ["description"] = d.Description, ["inputSchema"] = d.InputSchema.DeepClone(),
            ["annotations"] = new JsonObject
            {
                ["readOnlyHint"] = d.Risk is ToolRisk.Observe,
                ["destructiveHint"] = d.Risk is ToolRisk.Act or ToolRisk.Consequential,
                ["openWorldHint"] = d.UntrustedOutput,
            },
        })];

    private async Task<JsonObject> CallAsync(JsonObject? p, ToolTurn turn)
    {
        var name = p?["name"]?.GetValue<string>() ?? "";
        var result = await _executor.RunAsync(name, p?["arguments"], turn, turn.Cancellation).ConfigureAwait(false);
        return ToMcp(result);
    }

    public static JsonObject ToMcp(ToolResult result) => new()
    {
        ["content"] = new JsonArray([.. result.Content.Select(c => (JsonNode)(c switch
        {
            ToolImage img => new JsonObject { ["type"] = "image", ["data"] = Convert.ToBase64String(img.Data), ["mimeType"] = img.MediaType },
            ToolText t => new JsonObject { ["type"] = "text", ["text"] = t.Text },
            _ => new JsonObject { ["type"] = "text", ["text"] = "" },
        }))]),
        ["isError"] = result.IsError,
    };

    private static JsonObject Result(JsonNode id, JsonNode result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
    private static JsonObject Error(JsonNode? id, int code, string message) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

    private static async Task Reply(HttpListenerResponse res, int status, JsonNode? body)
    {
        res.StatusCode = status;
        if (body is null) { res.ContentLength64 = 0; res.Close(); return; }
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
        res.ContentType = "application/json";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        res.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        foreach (var t in _turns.Values) t.End();
        _turns.Clear();
        try { _listener.Stop(); _listener.Close(); } catch (ObjectDisposedException) { }
        try { await _loop.ConfigureAwait(false); } catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { }
        _stop.Dispose();
    }

    private sealed class Revoker(Action revoke) : IDisposable
    {
        private int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) revoke(); }
    }
}
