using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Security;
using Vernier.Core.Settings;
using Vernier.Core.Tools;

namespace Vernier.Core.Providers;

public sealed record ClaudeCodeStatus(bool Installed, string? Path, string? Version, string Guidance);

/// <summary>Finds the Claude Code CLI on PATH. No install location is assumed.</summary>
public static class ClaudeCodeLocator
{
    public const string SetupGuidance =
        "Claude Code was not found on PATH. Install it from https://code.claude.com/docs, run `claude` once to sign in, then press Re-check. " +
        "Vernier works without it: choose any other provider.";

    public static string? FindOnPath(string? pathVariable = null, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        pathVariable ??= Environment.GetEnvironmentVariable("PATH") ?? "";
        string[] names = OperatingSystem.IsWindows() ? ["claude.exe", "claude.cmd", "claude.bat"] : ["claude"];
        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var name in names)
            {
                string candidate;
                try { candidate = Path.Combine(dir, name); }
                catch (ArgumentException) { continue; }
                if (exists(candidate)) return candidate;
            }
        return null;
    }

    public static async Task<ClaudeCodeStatus> DetectAsync(CancellationToken ct)
    {
        var path = FindOnPath();
        if (path is null) return new ClaudeCodeStatus(false, null, null, SetupGuidance);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var (exit, stdout, _) = await ProcessRunner.RunAsync(path, ["--version"], null, null, timeout.Token).ConfigureAwait(false);
            return exit == 0
                ? new ClaudeCodeStatus(true, path, stdout.Trim(), "Detected. Sign-in state is checked when you press Test connection.")
                : new ClaudeCodeStatus(false, path, null, "Claude Code was found but `claude --version` failed. Reinstall or repair it.");
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            return new ClaudeCodeStatus(false, path, null, "Claude Code was found but could not be started: " + e.Message);
        }
    }
}

public static class ProcessRunner
{
    /// <summary>Runs a process with an argument list (no shell, so nothing is interpolated into a command line).</summary>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string file, IReadOnlyList<string> args, string? stdin, string? workingDirectory, CancellationToken ct)
    {
        using var p = Start(file, args, workingDirectory, stdin is not null);
        if (stdin is not null)
        {
            await p.StandardInput.WriteAsync(stdin.AsMemory(), ct).ConfigureAwait(false);
            p.StandardInput.Close();
        }
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        try
        {
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(p);
            throw;
        }
        return (p.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    public static Process Start(string file, IReadOnlyList<string> args, string? workingDirectory, bool redirectStdin, IReadOnlyDictionary<string, string>? environment = null)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectStdin,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory ?? "",
        };
        if (redirectStdin) psi.StandardInputEncoding = new UTF8Encoding(false);
        foreach (var (k, v) in environment ?? new Dictionary<string, string>()) psi.Environment[k] = v;
        // .cmd/.bat shims cannot be started directly without a shell; cmd /c with ArgumentList keeps quoting correct.
        if (file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(file);
        }
        else psi.FileName = file;
        foreach (var a in args) psi.ArgumentList.Add(a);
        return Process.Start(psi) ?? throw new IOException($"Could not start {Path.GetFileName(file)}.");
    }

    public static void Kill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }
}

/// <summary>
/// Drives the documented headless mode (`claude -p`, stream-json in and out) using the user's
/// existing Claude Code sign-in. Built-in tools are off (`--restricted --tools ""`), user and
/// project settings, CLAUDE.md files, skills and slash commands are not loaded, and screenshots
/// are sent inline, so the CLI reads nothing on disk. When the turn has tools, the CLI is pointed
/// at Vernier's loopback MCP server and may call only the tools granted to that turn.
/// </summary>
public sealed class ClaudeCodeProvider(ProviderSettings settings, string tempRoot) : IChatProvider
{
    public string Id => settings.Id;
    public string DisplayName => settings.DisplayName;
    public string Model => string.IsNullOrWhiteSpace(settings.Model) ? "(Claude Code default)" : settings.Model;

    public ProviderCapabilities Capabilities => new(
        Vision: true, Streaming: true, IsLocal: false, ReportsUsage: true,
        Notes: "Remote, via your Claude Code sign-in and plan. Screenshots are sent inline; the CLI runs with its own tools switched off and can call only Vernier's tools.",
        Tools: true);

    /// <summary>
    /// Starts the CLI ahead of the question so its start-up (several seconds) overlaps with the
    /// learner typing. The process waits on stdin; <see cref="StreamAsync"/> uses it if the
    /// question arrives with the same system prompt, model and tools, and discards it otherwise.
    /// </summary>
    public void Prewarm(string? system, ToolSession? tools)
    {
        if (ClaudeCodeLocator.FindOnPath() is not { } exe) return;
        Warm.Replace(Launch(exe, system, tools));
    }

    public static readonly TimeSpan WarmLifetime = TimeSpan.FromMinutes(2);

    /// <summary>A CLI is waiting for a question.</summary>
    public static bool HasWarm => Warm.Exists;

    /// <summary>Whether the most recent turn used a CLI started ahead of time (diagnostics and tests).</summary>
    public static bool LastRunWasWarm { get; private set; }

    /// <summary>Discards a waiting CLI (bar closed, provider changed, paused).</summary>
    public static void DiscardWarm() => Warm.Clear();

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var exe = ClaudeCodeLocator.FindOnPath()
            ?? throw new ProviderException(ProviderErrorKind.NotConfigured, ClaudeCodeLocator.SetupGuidance);

        var warm = Warm.Take(Key(request.System, request.Tools));
        LastRunWasWarm = warm is not null;
        using var run = warm ?? Launch(exe, request.System, request.Tools);
        var process = run.Process ?? throw new ProviderException(ProviderErrorKind.Unavailable, "Claude Code could not be started: " + run.StartError);
        try
        {
            await process.StandardInput.WriteLineAsync(UserMessage(request).AsMemory(), ct).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        catch (IOException e)
        {
            throw new ProviderException(ProviderErrorKind.Unavailable, "Claude Code stopped before the question could be sent: " + e.Message, inner: e);
        }

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var sawDelta = false;
        var sawResult = false;
        while (await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            // CLI output is untrusted input: parse defensively, ignore anything unrecognised.
            if (ProviderHttp.TryParse(line) is not JsonObject ev) continue;
            switch (ev["type"]?.GetValue<string>())
            {
                case "stream_event":
                    var inner = ev["event"];
                    var kind = inner?["type"]?.GetValue<string>();
                    if (kind == "content_block_delta" && inner?["delta"]?["type"]?.GetValue<string>() == "text_delta" &&
                        inner["delta"]?["text"]?.GetValue<string>() is { Length: > 0 } delta)
                    {
                        sawDelta = true;
                        yield return new ChatChunk(delta);
                    }
                    else if (kind == "content_block_start" && inner?["content_block"]?["type"]?.GetValue<string>() == "tool_use")
                        yield return new ChatChunk(null, Tool: new ToolCallNote(
                            ToolName(inner["content_block"]?["name"]?.GetValue<string>() ?? ""), inner["content_block"]?["id"]?.GetValue<string>() ?? "", false));
                    break;
                case "user":
                    // Tool results come back to the CLI as a user message: the call has finished.
                    foreach (var block in ev["message"]?["content"] as JsonArray ?? [])
                        if (block?["type"]?.GetValue<string>() == "tool_result")
                            yield return new ChatChunk(null, Tool: new ToolCallNote("", block["tool_use_id"]?.GetValue<string>() ?? "", true, block["is_error"]?.GetValue<bool>() == true));
                    break;
                case "result":
                    sawResult = true;
                    if (ev["is_error"]?.GetValue<bool>() == true)
                        throw ClassifyFailure(ev["result"]?.ToString() ?? "Claude Code reported an error.");
                    // Without partial messages (older CLI) the whole answer arrives here.
                    if (!sawDelta && ev["result"]?.GetValue<string>() is { Length: > 0 } full)
                        yield return new ChatChunk(full);
                    if (ev["usage"] is JsonObject u)
                        yield return new ChatChunk(null, new TokenUsage(
                            (u["input_tokens"]?.GetValue<long>() ?? 0) + (u["cache_read_input_tokens"]?.GetValue<long>() ?? 0) + (u["cache_creation_input_tokens"]?.GetValue<long>() ?? 0),
                            u["output_tokens"]?.GetValue<long>() ?? 0));
                    break;
            }
        }

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        if (!sawResult)
        {
            var stderr = await stderrTask.ConfigureAwait(false);
            throw ClassifyFailure(string.IsNullOrWhiteSpace(stderr) ? $"Claude Code exited with code {process.ExitCode} and no result." : stderr);
        }
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(["(Claude Code default)", "opus", "sonnet", "haiku"]);

    private string Key(string? system, ToolSession? tools) => $"{settings.Model}|{settings.Effort}|{tools?.Turn.Token}|{system}";

    private static string ToolName(string wire)
    {
        if (!wire.StartsWith("mcp__", StringComparison.Ordinal)) return wire;
        var at = wire.IndexOf("__", 5, StringComparison.Ordinal);
        return at < 0 ? wire : wire[(at + 2)..];
    }

    /// <summary>The argument list. Nothing private goes on the command line: the system prompt and the MCP config are files in a private folder.</summary>
    public static List<string> Arguments(string systemFile, string? mcpFile, IEnumerable<string> toolNames, string serverName, string? model, string? effort)
    {
        var args = new List<string>
        {
            "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
            "--no-session-persistence", "--restricted", "--setting-sources", "", "--disable-slash-commands",
            "--strict-mcp-config", "--tools", "", "--system-prompt-file", systemFile,
        };
        var names = toolNames.ToList();
        if (mcpFile is not null && names.Count > 0)
        {
            args.Add("--mcp-config"); args.Add(mcpFile);
            args.Add("--allowedTools"); args.Add(string.Join(",", names.Select(n => $"mcp__{serverName}__{n}")));
        }
        if (!string.IsNullOrWhiteSpace(model)) { args.Add("--model"); args.Add(model); }
        if (!string.IsNullOrWhiteSpace(effort)) { args.Add("--effort"); args.Add(effort); }
        return args;
    }

    private WarmRun Launch(string exe, string? system, ToolSession? tools)
    {
        var workDir = Path.Combine(tempRoot, "cc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var systemFile = Path.Combine(workDir, "system.txt");
        File.WriteAllText(systemFile, system ?? "You are a careful tutor.");
        string? mcpFile = null;
        if (tools?.McpConfig is { } config)
        {
            mcpFile = Path.Combine(workDir, "mcp.json");
            File.WriteAllText(mcpFile, config);
        }
        var args = Arguments(systemFile, mcpFile, tools?.Definitions.Select(d => d.Name) ?? [], tools?.McpServerName ?? "vernier", settings.Model, settings.Effort);
        try
        {
            // A long MCP tool timeout leaves the learner time to answer an approval card inside a call.
            var process = ProcessRunner.Start(exe, args, workDir, redirectStdin: true, new Dictionary<string, string> { ["MCP_TOOL_TIMEOUT"] = "180000" });
            return new WarmRun(Key(system, tools), process, workDir, null);
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
        {
            return new WarmRun(Key(system, tools), null, workDir, e.Message);
        }
    }

    /// <summary>One user message for stream-json input: earlier turns as text, then this turn's text with its images inline.</summary>
    public static string UserMessage(ChatRequest request)
    {
        var content = new JsonArray();
        var sb = new StringBuilder();
        var many = request.Messages.Count > 1;
        foreach (var m in request.Messages)
        {
            if (many) sb.Append(m.Role == "assistant" ? "[Tutor]\n" : "[Learner]\n");
            foreach (var img in m.Images ?? [])
            {
                if (sb.Length > 0) { content.Add(new JsonObject { ["type"] = "text", ["text"] = sb.ToString() }); sb.Clear(); }
                content.Add(new JsonObject
                {
                    ["type"] = "image",
                    ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = img.MediaType, ["data"] = Convert.ToBase64String(img.Data) },
                });
            }
            sb.Append(m.Text).Append("\n\n");
        }
        if (sb.Length > 0) content.Add(new JsonObject { ["type"] = "text", ["text"] = sb.ToString() });
        return new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = content },
        }.ToJsonString();
    }

    private static ProviderException ClassifyFailure(string message)
    {
        message = SecretRedactor.Redact(message.Trim());
        if (message.Length > 400) message = message[..400] + "…";
        var lower = message.ToLowerInvariant();
        if (lower.Contains("login") || lower.Contains("authenticat") || lower.Contains("api key") || lower.Contains("credential"))
            return new ProviderException(ProviderErrorKind.Auth, "Claude Code is not signed in. Run `claude` in a terminal and sign in. Details: " + message);
        if (lower.Contains("rate limit") || lower.Contains("usage limit") || lower.Contains("overloaded"))
            return new ProviderException(ProviderErrorKind.RateLimited, "Claude Code reported a limit: " + message);
        return new ProviderException(ProviderErrorKind.Unavailable, "Claude Code failed: " + message);
    }

    /// <summary>A started CLI and its private folder. Disposing kills the process and deletes the folder.</summary>
    private sealed record WarmRun(string Key, Process? Process, string WorkDir, string? StartError) : IDisposable
    {
        public void Dispose()
        {
            if (Process is not null)
            {
                ProcessRunner.Kill(Process);
                Process.Dispose();
            }
            try { Directory.Delete(WorkDir, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>At most one waiting CLI. An unused one is discarded after two minutes.</summary>
    private static class Warm
    {
        private static readonly object Gate = new();
        private static WarmRun? _run;
        private static Timer? _expiry;

        public static void Replace(WarmRun run)
        {
            WarmRun? old;
            lock (Gate)
            {
                old = _run;
                _run = run;
                _expiry?.Dispose();
                _expiry = new Timer(_ => Clear(), null, WarmLifetime, Timeout.InfiniteTimeSpan);
            }
            old?.Dispose();
        }

        public static bool Exists { get { lock (Gate) return _run is { Process.HasExited: false }; } }

        public static void Clear()
        {
            WarmRun? old;
            lock (Gate) { old = _run; _run = null; }
            old?.Dispose();
        }

        public static WarmRun? Take(string key)
        {
            WarmRun? run, stale = null;
            lock (Gate)
            {
                run = _run;
                _run = null;
                if (run is not null && (run.Key != key || run.Process is not { HasExited: false })) { stale = run; run = null; }
            }
            stale?.Dispose();
            return run;
        }
    }
}

public sealed class ProviderFactory(ISecretStore secrets, HttpClient http, string tempRoot)
{
    public IChatProvider Create(ProviderSettings s) => s.Kind switch
    {
        ProviderKinds.Anthropic => new AnthropicProvider(s, secrets),
        ProviderKinds.Gemini => new GeminiProvider(s, secrets, http),
        ProviderKinds.ClaudeCode => new ClaudeCodeProvider(s, tempRoot),
        ProviderKinds.CodexCli => new CodexCliProvider(s, tempRoot),
        ProviderKinds.OpenAiCompatible => new OpenAiCompatibleProvider(s, secrets, http),
        _ => throw new ProviderException(ProviderErrorKind.NotConfigured, $"Unknown provider kind '{s.Kind}'."),
    };
}
