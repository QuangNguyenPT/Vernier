using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Providers;
using Vernier.Core.Tools;

namespace Vernier.Tests;

internal sealed class EchoTool(string name, ToolRisk risk = ToolRisk.Observe, bool untrusted = false, Exception? fail = null) : ITool
{
    public int Runs { get; private set; }
    public ToolDefinition Definition { get; } = new(name, "Echoes text.",
        (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"text":{"type":"string","maxLength":20},"n":{"type":"integer","minimum":1,"maximum":3},"kind":{"type":"string","enum":["a","b"]},"box":{"type":"object","properties":{"x":{"type":"number"}},"required":["x"],"additionalProperties":false}},"required":["text"],"additionalProperties":false}""")!,
        risk, untrusted);

    public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
    {
        Runs++;
        if (fail is not null) throw fail;
        return Task.FromResult(ToolResult.Ok("echo " + args["text"]!.GetValue<string>()));
    }
}

internal sealed class PreviewTool(string? preview, bool refuse) : ITool
{
    public int Runs { get; private set; }
    public ToolDefinition Definition { get; } = new("act", "Acts.", new JsonObject { ["type"] = "object" }, ToolRisk.Act);
    public Task<string?> PreviewAsync(JsonObject args, ToolTurn turn, CancellationToken ct) =>
        refuse ? throw new ToolException("refused before asking") : Task.FromResult(preview);
    public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct) { Runs++; return Task.FromResult(ToolResult.Ok("acted")); }
}

internal sealed class FixedApprover(ApprovalResolution answer) : IToolApprover
{
    public List<ApprovalRequest> Asked { get; } = [];
    public Task<ApprovalResolution> ApproveAsync(ApprovalRequest request, CancellationToken ct) { Asked.Add(request); return Task.FromResult(answer); }
}

public class ToolExecutorTests
{
    private static (ToolExecutor Executor, EchoTool Tool, FixedApprover Approver) Make(ToolRisk risk = ToolRisk.Observe, bool untrusted = false, ApprovalResolution answer = ApprovalResolution.AllowOnce, Exception? fail = null, Func<ToolDefinition, string?>? gate = null)
    {
        var registry = new ToolRegistry();
        var tool = new EchoTool("echo", risk, untrusted, fail);
        registry.Add(tool);
        var approver = new FixedApprover(answer);
        return (new ToolExecutor(registry, approver, gate), tool, approver);
    }

    private static JsonNode Args(string json) => JsonNode.Parse(json)!;

    [Theory]
    [InlineData("Click the marked spot in Notepad.", false)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public async Task Approval_shows_the_tools_own_preview_and_a_refusing_preview_never_asks(string? preview, bool refuse)
    {
        var registry = new ToolRegistry();
        var tool = new PreviewTool(preview, refuse);
        registry.Add(tool);
        var approver = new FixedApprover(ApprovalResolution.AllowOnce);
        var result = await new ToolExecutor(registry, approver).RunAsync("act", new JsonObject(), new ToolTurn(["act"], default), default);
        if (refuse)
        {
            Assert.True(result.IsError);
            Assert.Equal("refused before asking", result.Text);
            Assert.Empty(approver.Asked);
            Assert.Equal(0, tool.Runs);
            return;
        }
        Assert.Equal(preview ?? "act {}", Assert.Single(approver.Asked).Summary);
        Assert.Equal(1, tool.Runs);
    }

    [Fact]
    public void Tool_names_are_checked_when_registered()
    {
        var registry = new ToolRegistry();
        Assert.Throws<ArgumentException>(() => registry.Add(new EchoTool("Bad-Name")));
        registry.Add(new EchoTool("ok_name"));
        Assert.Throws<ArgumentException>(() => registry.Add(new EchoTool("ok_name")));
    }

    [Fact]
    public async Task Arguments_are_validated_and_problems_come_back_as_results_the_model_can_read()
    {
        var (exec, tool, _) = Make();
        var turn = new ToolTurn(["echo"], default);
        Assert.Contains("text is required", (await exec.RunAsync("echo", Args("{}"), turn, default)).Text);
        Assert.Contains("must be a string", (await exec.RunAsync("echo", Args("""{"text":5}"""), turn, default)).Text);
        Assert.Contains("longer than 20", (await exec.RunAsync("echo", Args("""{"text":"012345678901234567890"}"""), turn, default)).Text);
        Assert.Contains("must be one of: a, b", (await exec.RunAsync("echo", Args("""{"text":"x","kind":"c"}"""), turn, default)).Text);
        Assert.Contains("whole number", (await exec.RunAsync("echo", Args("""{"text":"x","n":1.5}"""), turn, default)).Text);
        Assert.Contains("at most 3", (await exec.RunAsync("echo", Args("""{"text":"x","n":9}"""), turn, default)).Text);
        Assert.Contains("not an allowed argument", (await exec.RunAsync("echo", Args("""{"text":"x","evil":1}"""), turn, default)).Text);
        Assert.Contains("box.x is required", (await exec.RunAsync("echo", Args("""{"text":"x","box":{}}"""), turn, default)).Text);
        Assert.Equal(0, tool.Runs);
        var ok = await exec.RunAsync("echo", Args("""{"text":"hi","n":2,"kind":"a","box":{"x":1}}"""), turn, default);
        Assert.False(ok.IsError);
        Assert.Equal("echo hi", ok.Text);
        Assert.Equal(9, turn.Events.Count);
    }

    [Fact]
    public void Numbers_validate_whether_they_came_from_json_text_or_csharp_literals()
    {
        // Found live: a schema written in C# (minimum = 1) and a model's integer argument made every box mark fail.
        var schema = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["w"] = new JsonObject { ["type"] = "number", ["minimum"] = 1 } } };
        Assert.Null(SchemaCheck.Validate(schema, (JsonObject)JsonNode.Parse("""{"w":67}""")!));
        Assert.Null(SchemaCheck.Validate(schema, new JsonObject { ["w"] = 66.5 }));
        Assert.Null(SchemaCheck.Validate(schema, new JsonObject { ["w"] = 3 }));
        Assert.Contains("at least 1", SchemaCheck.Validate(schema, new JsonObject { ["w"] = 0 }));
        Assert.Equal(67, Json.Number(JsonNode.Parse("67")));
        Assert.Equal(2, Json.Int(JsonValue.Create(2.0)));
    }

    [Fact]
    public async Task Only_granted_tools_run_and_nothing_runs_after_the_turn_ends_or_past_the_limit()
    {
        var (exec, tool, _) = Make();
        var noGrant = new ToolTurn([], default);
        Assert.Contains("no tool 'echo'", (await exec.RunAsync("echo", Args("""{"text":"a"}"""), noGrant, default)).Text);
        Assert.Contains("no tool 'nope'", (await exec.RunAsync("nope", null, new ToolTurn(["echo"], default), default)).Text);

        var limited = new ToolTurn(["echo"], default, maxCalls: 2);
        await exec.RunAsync("echo", Args("""{"text":"a"}"""), limited, default);
        await exec.RunAsync("echo", Args("""{"text":"a"}"""), limited, default);
        Assert.Contains("limit", (await exec.RunAsync("echo", Args("""{"text":"a"}"""), limited, default)).Text);

        var ended = new ToolTurn(["echo"], default);
        ended.End();
        Assert.Contains("has ended", (await exec.RunAsync("echo", Args("""{"text":"a"}"""), ended, default)).Text);
        Assert.Equal(2, tool.Runs);
    }

    [Theory]
    [InlineData(ApprovalResolution.Deny, "Denied by the learner")]
    [InlineData(ApprovalResolution.Timeout, "did not answer")]
    [InlineData(ApprovalResolution.Cancelled, "Denied by the learner")]
    public async Task Acting_tools_run_only_when_the_learner_allows_them(ApprovalResolution answer, string expected)
    {
        var (exec, tool, approver) = Make(ToolRisk.Act, answer: answer);
        var result = await exec.RunAsync("echo", Args("""{"text":"click"}"""), new ToolTurn(["echo"], default), default);
        Assert.True(result.IsError);
        Assert.Contains(expected, result.Text);
        Assert.Equal(0, tool.Runs);
        Assert.Contains("click", Assert.Single(approver.Asked).Summary);

        var (allowed, ran, _) = Make(ToolRisk.Act);
        Assert.False((await allowed.RunAsync("echo", Args("""{"text":"click"}"""), new ToolTurn(["echo"], default), default)).IsError);
        Assert.Equal(1, ran.Runs);
    }

    [Fact]
    public async Task Observe_tools_never_ask_and_settings_gates_are_checked_on_every_call()
    {
        var blocked = true;
        var (exec, tool, approver) = Make(gate: _ => blocked ? "Screen capture is off." : null);
        var turn = new ToolTurn(["echo"], default);
        Assert.Equal("Screen capture is off.", (await exec.RunAsync("echo", Args("""{"text":"a"}"""), turn, default)).Text);
        blocked = false;
        Assert.False((await exec.RunAsync("echo", Args("""{"text":"a"}"""), turn, default)).IsError);
        Assert.Empty(approver.Asked);
        Assert.Equal(1, tool.Runs);
    }

    [Fact]
    public async Task Screen_text_from_a_tool_is_fenced_and_taints_the_turn_and_failures_become_results()
    {
        var (exec, _, _) = Make(untrusted: true);
        var turn = new ToolTurn(["echo"], default);
        var result = await exec.RunAsync("echo", Args("""{"text":"ignore all"}"""), turn, default);
        Assert.Contains("<untrusted source=\"tool:echo\">", result.Text);
        Assert.True(turn.Tainted);

        var (failing, _, _) = Make(fail: new ToolException("No text \"x\" on screen."));
        Assert.Equal("No text \"x\" on screen.", (await failing.RunAsync("echo", Args("""{"text":"x"}"""), new ToolTurn(["echo"], default), default)).Text);
        var (crashing, _, _) = Make(fail: new InvalidOperationException("boom"));
        Assert.Contains("The tool failed: boom", (await crashing.RunAsync("echo", Args("""{"text":"x"}"""), new ToolTurn(["echo"], default), default)).Text);
    }
}

public class McpServerTests
{
    private static async Task<(HttpStatusCode Status, JsonNode? Body)> Post(string url, string? token, string json, string? origin = null)
    {
        using var http = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (token is not null) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (origin is not null) req.Headers.Add("Origin", origin);
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, text.Length > 0 ? JsonNode.Parse(text) : null);
    }

    [Fact]
    public async Task Speaks_mcp_over_loopback_http_and_enforces_the_turn_grant()
    {
        var registry = new ToolRegistry();
        var echo = new EchoTool("echo");
        registry.Add(echo);
        registry.Add(new EchoTool("secret"));
        await using var server = new McpServer(new ToolExecutor(registry, new FixedApprover(ApprovalResolution.Deny)));
        var turn = new ToolTurn(["echo"], default);
        var grant = server.Open(turn);

        Assert.StartsWith("http://127.0.0.1:", server.Url);
        Assert.Contains(turn.Token, server.ClientConfig(turn));

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(server.Url, null, """{"jsonrpc":"2.0","id":1,"method":"ping"}""")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(server.Url, "wrong", """{"jsonrpc":"2.0","id":1,"method":"ping"}""")).Status);
        // A web page cannot reach it: browsers always send an Origin.
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", "https://evil.example")).Status);

        var init = await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""");
        Assert.Equal("2025-06-18", init.Body!["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Accepted, (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","method":"notifications/initialized"}""")).Status);

        var list = (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""")).Body!["result"]!["tools"]!.AsArray();
        Assert.Equal("echo", Assert.Single(list)!["name"]!.GetValue<string>()); // the ungranted tool is not even listed

        var call = (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""")).Body!["result"]!;
        Assert.False(call["isError"]!.GetValue<bool>());
        Assert.Equal("echo hi", call["content"]![0]!["text"]!.GetValue<string>());
        var refused = (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"secret","arguments":{"text":"hi"}}}""")).Body!["result"]!;
        Assert.True(refused["isError"]!.GetValue<bool>());
        Assert.Equal(-32601, (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":5,"method":"server/discover"}""")).Body!["error"]!["code"]!.GetValue<int>());

        grant.Dispose(); // the turn ended (answer done, or Esc)
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"echo","arguments":{"text":"late"}}}""")).Status);
        Assert.Equal(1, echo.Runs);
        Assert.False(turn.IsActive);
    }

    private static async Task<(HttpStatusCode Status, JsonNode? Body, string Headers)> Send(
        string url, string token, HttpMethod method, string? json = null, string? protocolVersion = null)
    {
        using var http = new HttpClient();
        using var req = new HttpRequestMessage(method, url);
        if (json is not null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.ParseAdd("application/json, text/event-stream"); // what a Streamable HTTP client sends
        if (protocolVersion is not null) req.Headers.Add("MCP-Protocol-Version", protocolVersion);
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, text.Length > 0 && text[0] is '{' or '[' ? JsonNode.Parse(text) : null, res.Headers + res.Content.Headers.ToString());
    }

    [Fact]
    public async Task Follows_the_streamable_http_rules_of_the_spec()
    {
        var registry = new ToolRegistry();
        registry.Add(new EchoTool("look", ToolRisk.Observe, untrusted: true));
        registry.Add(new EchoTool("write", ToolRisk.Act));
        await using var server = new McpServer(new ToolExecutor(registry, new FixedApprover(ApprovalResolution.Deny)));
        var turn = new ToolTurn(["look", "write"], default);
        using var grant = server.Open(turn);
        var t = turn.Token;

        // A reply to something a server asked (Vernier asks nothing) is acknowledged; a message with no method is malformed.
        Assert.Equal(HttpStatusCode.Accepted, (await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":9,"result":{}}""")).Status);
        var noMethod = await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":9}""");
        Assert.Equal(HttpStatusCode.BadRequest, noMethod.Status);
        Assert.Equal(-32600, noMethod.Body!["error"]!["code"]!.GetValue<int>());
        // Batches were removed from the protocol; they are refused with a reason, not misreported as a parse error.
        var batch = await Send(server.Url, t, HttpMethod.Post, """[{"jsonrpc":"2.0","id":1,"method":"ping"}]""");
        Assert.Equal(HttpStatusCode.BadRequest, batch.Status);
        Assert.Equal(-32600, batch.Body!["error"]!["code"]!.GetValue<int>());
        Assert.Equal(-32700, (await Send(server.Url, t, HttpMethod.Post, "{not json")).Body!["error"]!["code"]!.GetValue<int>());

        // Protocol version: negotiated at initialize, and a header naming one we do not speak is a 400.
        var newest = (await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2099-01-01"}}""")).Body!["result"]!;
        Assert.Equal("2025-11-25", newest["protocolVersion"]!.GetValue<string>()); // offers its own newest instead
        Assert.Equal("vernier", newest["serverInfo"]!["name"]!.GetValue<string>());
        Assert.NotNull(newest["capabilities"]!["tools"]);
        Assert.Equal(OperatorGuide.McpInstructions, newest["instructions"]!.GetValue<string>()); // the server says how to use it
        Assert.Equal(HttpStatusCode.OK, (await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":2,"method":"ping"}""", "2025-06-18")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":2,"method":"ping"}""", "1999-01-01")).Status);

        // A malformed version does not break the handshake: the client is offered ours, and answered under its own id.
        var odd = await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":77,"method":"initialize","params":{"protocolVersion":{"x":1}}}""");
        Assert.Equal(77, odd.Body!["id"]!.GetValue<int>());
        Assert.Equal("2025-11-25", odd.Body["result"]!["protocolVersion"]!.GetValue<string>());

        // No server-sent event stream and no sessions: GET and DELETE are 405 and say what is allowed.
        var get = await Send(server.Url, t, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.Status);
        Assert.Contains("Allow: POST", get.Headers);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Send(server.Url, t, HttpMethod.Delete)).Status);
        var denied = await Send(server.Url, "wrong", HttpMethod.Post, """{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.Status);
        Assert.Contains("WWW-Authenticate: Bearer", denied.Headers);

        // Tools carry hints a client can act on (read-only tools may run in parallel; effectful ones are marked).
        var tools = (await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":3,"method":"tools/list"}""")).Body!["result"]!["tools"]!.AsArray();
        var look = tools.Single(x => x!["name"]!.GetValue<string>() == "look")!;
        var write = tools.Single(x => x!["name"]!.GetValue<string>() == "write")!;
        Assert.True(look["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.True(look["annotations"]!["openWorldHint"]!.GetValue<bool>());
        Assert.False(write["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.True(write["annotations"]!["destructiveHint"]!.GetValue<bool>());
        Assert.Equal("object", look["inputSchema"]!["type"]!.GetValue<string>());

        // A call with no tool name is a protocol error (-32602); an unknown or denied tool is a tool error the model can read.
        Assert.Equal(-32602, (await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{}}""")).Body!["error"]!["code"]!.GetValue<int>());
        Assert.True((await Send(server.Url, t, HttpMethod.Post, """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"write","arguments":{"text":"x"}}}""")).Body!["result"]!["isError"]!.GetValue<bool>());
    }

    [Fact]
    public async Task An_unexpected_failure_inside_a_tool_is_still_answered_at_once()
    {
        var registry = new ToolRegistry();
        registry.Add(new EchoTool("boom", fail: new KeyNotFoundException("not a type the executor expects")));
        var logged = new List<string>();
        await using var server = new McpServer(new ToolExecutor(registry, new FixedApprover(ApprovalResolution.Deny)), logged.Add);
        var turn = new ToolTurn(["boom"], default);
        using var grant = server.Open(turn);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var (status, body) = await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"boom","arguments":{"text":"x"}}}""");
        Assert.True(clock.ElapsedMilliseconds < 1000);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(-32603, body!["error"]!["code"]!.GetValue<int>());
        Assert.Contains(logged, l => l.Contains("KeyNotFoundException"));
    }

    [Fact]
    public async Task A_grant_with_a_deadline_stops_working_by_itself()
    {
        await using var server = new McpServer(new ToolExecutor(new ToolRegistry(), new FixedApprover(ApprovalResolution.Deny)));
        var turn = new ToolTurn([], new CancellationTokenSource(TimeSpan.FromMilliseconds(50)).Token);
        using var grant = server.Open(turn);
        await Task.Delay(150);
        Assert.False(turn.IsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(server.Url, turn.Token, """{"jsonrpc":"2.0","id":1,"method":"ping"}""")).Status);
    }

    [Fact]
    public void Images_in_results_are_sent_as_mcp_image_content()
    {
        var mcp = McpServer.ToMcp(new ToolResult([new ToolImage([1, 2, 3], "image/png"), new ToolText("ok")], false));
        Assert.Equal("image", mcp["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("AQID", mcp["content"]![0]!["data"]!.GetValue<string>());
        Assert.Equal("ok", mcp["content"]![1]!["text"]!.GetValue<string>());
    }
}

public class ScreenIndexTests
{
    // 1000x500 screenshot of a 2000x1000 screen region: every image pixel is 2 physical pixels.
    private static readonly CaptureGeometry Geo = new(new PxRect(100, 50, 2000, 1000), 1000, 500);

    private static ExtractedText Line(string text, double x, double y, params (string Word, double X, double W)[] words) =>
        new(text, new PxRect(x, y, words.Length == 0 ? text.Length * 8 : words[^1].X + words[^1].W - x, 16), 0.7, "ocr")
        {
            Words = [.. words.Select(w => new ExtractedText(w.Word, new PxRect(w.X, y, w.W, 16), 0.7, "ocr"))],
        };

    private static ScreenIndex Index() => new("o1", Geo, [
        Line("Newton's second law: F = m a", 10, 10, ("Newton's", 10, 60), ("second", 75, 50), ("law:", 130, 30), ("F", 170, 8), ("=", 182, 8), ("m", 194, 10), ("a", 208, 8)),
        Line("Find the acceleration when F − 10 = 0", 10, 40),
        Line("Find the acceleration when F − 10 = 0", 10, 300),
        new ExtractedText("Extrude", new PxRect(600, 20, 60, 20), 0.97, "accessibility") { Role = "button" },
        new ExtractedText("Extrude", new PxRect(604, 22, 50, 14), 0.7, "ocr"),
    ], DateTimeOffset.UtcNow);

    [Fact]
    public void A_run_of_words_inside_a_line_is_marked_on_its_own_box_in_physical_pixels()
    {
        var target = Index().Resolve(null, "F = m a", null, null);
        Assert.Equal(ScreenItemKind.Phrase, target.Item!.Kind);
        Assert.Equal(new PxRect(100 + 170 * 2, 50 + 10 * 2, (216 - 170) * 2, 32), target.Bounds);
        Assert.Equal("text found by OCR", target.Method);
    }

    [Fact]
    public void Ambiguous_text_is_refused_with_candidates_and_occurrence_or_id_picks_one()
    {
        var index = Index();
        var lines = index.Items.Where(i => i.Kind == ScreenItemKind.Line && i.Text.StartsWith("Find")).Select(i => i.Id).ToList();
        var e = Assert.Throws<ToolException>(() => index.Resolve(null, "find the acceleration when F - 10 = 0", null, null));
        Assert.Contains("matches 2 places", e.Message);
        Assert.Contains(lines[0] + " ", e.Message);
        Assert.Contains(lines[1] + " ", e.Message);
        Assert.Equal(50 + 300 * 2, index.Resolve(null, "find the acceleration when F - 10 = 0", 2, null).Bounds.Y); // minus sign folded
        Assert.Equal(50 + 300 * 2, index.Resolve(lines[1], null, null, null).Bounds.Y);
    }

    [Fact]
    public void A_control_and_its_ocr_text_in_the_same_place_are_one_target_and_the_control_wins()
    {
        var target = Index().Resolve(null, "extrude", null, null);
        Assert.Equal("accessibility element", target.Method);
        Assert.Equal("button", target.Item!.Role);
    }

    [Fact]
    public void Stale_ids_missing_text_and_bad_boxes_are_explained_and_boxes_are_estimates()
    {
        var index = Index();
        Assert.Contains("no longer valid", Assert.Throws<ToolException>(() => index.Resolve("t99", null, null, null)).Message);
        Assert.Contains("No text matching", Assert.Throws<ToolException>(() => index.Resolve(null, "Bernoulli", null, null)).Message);
        Assert.Contains("outside the screenshot", Assert.Throws<ToolException>(() => index.Resolve(null, null, null, new PxRect(2000, 2000, 5, 5))).Message);
        Assert.Contains("most of the screen", Assert.Throws<ToolException>(() => index.Resolve(null, null, null, new PxRect(0, 0, 990, 490))).Message);
        var estimate = index.Resolve(null, null, null, new PxRect(400, 200, 50, 25));
        Assert.True(estimate.Confidence < 0.5); // drawn dashed
        Assert.Equal(new PxRect(900, 450, 100, 50), estimate.Bounds);
    }

    [Fact]
    public void The_prompt_list_puts_what_the_learner_marked_first_and_uses_screenshot_pixels()
    {
        var index = Index();
        var marked = index.Items.Last(i => i.Kind == ScreenItemKind.Line && i.Text.StartsWith("Find")).Id;
        var listing = index.Describe(focusPhysical: new PxRect(100 + 10 * 2, 50 + 300 * 2, 300, 32));
        Assert.StartsWith(marked + " ", listing);
        Assert.Contains("[button] \"Extrude\" (600,20 60x20)", listing);
    }

    [Fact]
    public void An_id_from_an_earlier_capture_in_the_same_turn_is_refused_not_reused()
    {
        var ids = new IdSequence();
        var first = new ScreenIndex("o1", Geo, [Line("old line", 10, 10)], DateTimeOffset.UtcNow, ids: ids);
        var second = new ScreenIndex("o2", Geo, [Line("new line", 10, 10)], DateTimeOffset.UtcNow, ids: ids);
        var oldId = first.Items.Single(i => i.Kind == ScreenItemKind.Line).Id;
        Assert.NotEqual(oldId, second.Items.Single(i => i.Kind == ScreenItemKind.Line).Id);
        Assert.Contains("no longer valid", Assert.Throws<ToolException>(() => second.Resolve(oldId, null, null, null)).Message);
    }

    [Fact]
    public void Parallel_tool_calls_get_distinct_ids_and_searching_is_safe()
    {
        var ids = new IdSequence();
        var made = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 200, _ => made.Add(ids.Next('m')));
        Assert.Equal(200, made.Distinct().Count());
        var index = Index();
        Parallel.For(0, 50, i => index.Find(i % 2 == 0 ? "F = m a" : "Extrude"));
        Assert.Single(index.Items.Where(i => i.Kind == ScreenItemKind.Phrase).Select(i => i.Text).Distinct());
    }

    [Fact]
    public void Ocr_symbol_confusions_are_folded()
    {
        Assert.Equal(ScreenIndex.Normalise("F − 10 × 2"), ScreenIndex.Normalise("f - 10 * 2"));
        Assert.Equal("x2", ScreenIndex.Normalise("x^2"));
    }
}

public class OverlayDataTests
{
    [Fact]
    public void Simplified_strokes_keep_their_shape_with_far_fewer_points()
    {
        var wave = Enumerable.Range(0, 1500).Select(k => new PxPoint(k * 0.5, Math.Sin(k / 20.0) * 5)).ToArray();
        var simple = PolylineMath.Simplify(wave, 0.75);
        Assert.True(simple.Count < 150, $"{simple.Count} points kept");
        Assert.Equal(wave[0], simple[0]);
        Assert.Equal(wave[^1], simple[^1]);
        Assert.Equal(2, PolylineMath.Simplify([new(0, 0), new(5, 0.1), new(10, 0)], 0.75).Count);
    }

    [Fact]
    public void Removing_the_tutors_marks_keeps_the_learners_ink_as_one_undo_step()
    {
        var store = new AnnotationStore();
        store.Add(new Annotation { Kind = AnnotationKind.Freehand, Points = [new(0, 0), new(1, 1)], Source = "user" });
        store.Add(new Annotation { Kind = AnnotationKind.Circle, Points = [new(0, 0), new(9, 9)], Source = "tutor" });
        store.RemoveWhere(a => a.Source == "tutor");
        Assert.Equal("user", Assert.Single(store.Items).Source);
        store.Undo();
        Assert.Equal(2, store.Items.Count);
    }
}

public class ClaudeCodeInvocationTests
{
    [Fact]
    public void The_cli_runs_with_its_own_tools_off_and_only_the_granted_vernier_tools()
    {
        var args = ClaudeCodeProvider.Arguments("sys.txt", "mcp.json", ["overlay_mark", "look"], "vernier", "opus", "high");
        string After(string flag) => args[args.IndexOf(flag) + 1];
        Assert.Contains("--restricted", args);
        Assert.Contains("--disable-slash-commands", args);
        Assert.Contains("--strict-mcp-config", args);
        Assert.Equal("", After("--tools"));
        Assert.Equal("", After("--setting-sources"));
        Assert.Equal("sys.txt", After("--system-prompt-file"));
        Assert.Equal("mcp.json", After("--mcp-config"));
        Assert.Equal("mcp__vernier__overlay_mark,mcp__vernier__look", After("--allowedTools"));
        Assert.Equal("opus", After("--model"));
        Assert.Equal("high", After("--effort"));
        Assert.DoesNotContain("Read", args);
        Assert.DoesNotContain("--system-prompt", args);
        Assert.DoesNotContain("--safe-mode", args); // it would also switch off the MCP config

        var noTools = ClaudeCodeProvider.Arguments("sys.txt", null, [], "vernier", "", null);
        Assert.DoesNotContain("--mcp-config", noTools);
        Assert.DoesNotContain("--allowedTools", noTools);
        Assert.DoesNotContain("--model", noTools);
    }

    [Fact]
    public void Screenshots_go_inline_in_the_stream_json_message_not_through_files()
    {
        var request = new ChatRequest("s", [ChatMessage.User("earlier"), ChatMessage.Assistant("answer"), ChatMessage.User("look", new ImageInput([1, 2, 3], "image/png"))]);
        var message = JsonNode.Parse(ClaudeCodeProvider.UserMessage(request))!;
        Assert.Equal("user", message["type"]!.GetValue<string>());
        var content = message["message"]!["content"]!.AsArray();
        Assert.Contains("[Learner]\nearlier", content[0]!["text"]!.GetValue<string>());
        Assert.Contains("[Tutor]\nanswer", content[0]!["text"]!.GetValue<string>());
        Assert.Equal("image", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("AQID", content[1]!["source"]!["data"]!.GetValue<string>());
        Assert.Contains("look", content[2]!["text"]!.GetValue<string>());
    }
}
