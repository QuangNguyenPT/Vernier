using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Security;
using Vernier.Core.Settings;

namespace Vernier.Core.Providers;

/// <summary>
/// "Sign in with your account" for AI services, done the only supported way: through each
/// vendor's own command-line tool, which owns the browser sign-in and the token. Vernier never
/// sees a password or a token; it only asks the tool whether it is signed in, and runs it.
/// </summary>
public sealed record CliAccount(string Id, string Name, string Service, string ProviderId, string[] Executables, string[] StatusArgs, string[] LoginArgs, string InstallHint);

public sealed record CliAccountStatus(CliAccount Account, bool Installed, bool SignedIn, string? Path, string Detail);

public static class CliAccounts
{
    public static readonly CliAccount Claude = new("claude", "Claude account", "Claude Pro / Max, via Claude Code", "claude-code",
        ["claude.exe", "claude.cmd", "claude.bat"], ["auth", "status"], ["auth", "login"],
        "Install Claude Code from https://code.claude.com/docs, then press Sign in.");

    public static readonly CliAccount ChatGpt = new("chatgpt", "ChatGPT account", "ChatGPT Plus / Pro, via Codex CLI", "codex",
        ["codex.exe", "codex.cmd", "codex.bat"], ["login", "status"], ["login"],
        "Install the Codex CLI from https://developers.openai.com/codex/cli, then press Sign in.");

    public static readonly IReadOnlyList<CliAccount> All = [Claude, ChatGpt];

    public static string? Find(CliAccount account, string? pathVariable = null, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        pathVariable ??= Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var name in account.Executables)
            {
                string candidate;
                try { candidate = Path.Combine(dir, name); }
                catch (ArgumentException) { continue; }
                if (exists(candidate)) return candidate;
            }
        return null;
    }

    public static async Task<CliAccountStatus> StatusAsync(CliAccount account, CancellationToken ct)
    {
        var path = Find(account);
        if (path is null) return new(account, false, false, null, account.InstallHint);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var (exit, stdout, stderr) = await ProcessRunner.RunAsync(path, account.StatusArgs, null, null, timeout.Token).ConfigureAwait(false);
            return Interpret(account, path, exit, stdout + "\n" + stderr);
        }
        catch (OperationCanceledException) { return new(account, true, false, path, "The sign-in check timed out."); }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
        {
            return new(account, true, false, path, "The tool could not be started: " + e.Message);
        }
    }

    /// <summary>Reads the tool's own answer. The output is untrusted text: only its meaning is taken, and it is redacted before display.</summary>
    public static CliAccountStatus Interpret(CliAccount account, string path, int exitCode, string output)
    {
        var lower = output.ToLowerInvariant();
        var negative = lower.Contains("not logged in") || lower.Contains("not signed in") || lower.Contains("\"loggedin\": false") || lower.Contains("\"loggedin\":false") || lower.Contains("no credentials");
        var positive = lower.Contains("logged in") || lower.Contains("\"loggedin\": true") || lower.Contains("\"loggedin\":true") || lower.Contains("signed in");
        var signedIn = !negative && exitCode == 0 && positive;
        return new(account, true, signedIn, path, signedIn ? "Signed in." : "Not signed in. Press Sign in: a terminal opens and the tool takes you through your browser.");
    }

    /// <summary>The command the user's terminal should run to sign in. Vernier launches it visibly and takes no part in it.</summary>
    public static (string File, string[] Args)? LoginCommand(CliAccount account) =>
        Find(account) is { } path ? (path, account.LoginArgs) : null;
}

/// <summary>
/// Drives `codex exec` using the user's ChatGPT sign-in (Codex CLI). Runs read-only and
/// ephemeral in a private temp folder; the final message is read from the file the CLI writes.
/// Codex has no system-prompt flag, so the tutor's instructions lead the prompt. Not streamed.
/// </summary>
public sealed class CodexCliProvider(ProviderSettings settings, string tempRoot) : IChatProvider
{
    public string Id => settings.Id;
    public string DisplayName => settings.DisplayName;
    public string Model => string.IsNullOrWhiteSpace(settings.Model) ? "(Codex default)" : settings.Model;

    public ProviderCapabilities Capabilities => new(
        Vision: true, Streaming: false, IsLocal: false, ReportsUsage: true,
        Notes: "Remote, via your ChatGPT sign-in in the Codex CLI. The answer arrives all at once rather than streaming. Screenshots are written to a private temp file for the CLI, then deleted.");

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var exe = CliAccounts.Find(CliAccounts.ChatGpt)
            ?? throw new ProviderException(ProviderErrorKind.NotConfigured, "The Codex CLI was not found on PATH. " + CliAccounts.ChatGpt.InstallHint);

        var workDir = Path.Combine(tempRoot, "cx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var answerFile = Path.Combine(workDir, "answer.txt");
            var args = new List<string> { "exec", "--json", "--ephemeral", "--skip-git-repo-check", "--sandbox", "read-only", "--color", "never", "-C", workDir, "-o", answerFile };
            if (!string.IsNullOrWhiteSpace(settings.Model)) { args.Add("-m"); args.Add(settings.Model); }

            var prompt = new StringBuilder();
            if (!string.IsNullOrEmpty(request.System)) prompt.Append("[Instructions for you, the tutor]\n").Append(request.System).Append("\n\n");
            var imageIndex = 0;
            var many = request.Messages.Count > 1;
            foreach (var m in request.Messages)
            {
                if (many) prompt.Append(m.Role == "assistant" ? "[Tutor]\n" : "[Learner]\n");
                foreach (var img in m.Images ?? [])
                {
                    var file = Path.Combine(workDir, $"screen-{++imageIndex}{(img.MediaType == "image/jpeg" ? ".jpg" : ".png")}");
                    File.WriteAllBytes(file, img.Data);
                    args.Add("-i");
                    args.Add(file);
                }
                prompt.Append(m.Text).Append("\n\n");
            }
            args.Add("-"); // prompt from stdin, so nothing of it lands on a command line

            int exit;
            string stdout, stderr;
            try
            {
                (exit, stdout, stderr) = await ProcessRunner.RunAsync(exe, args, prompt.ToString(), workDir, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
            {
                throw new ProviderException(ProviderErrorKind.Unavailable, "The Codex CLI could not be started: " + e.Message, inner: e);
            }

            TokenUsage? usage = null;
            string? reported = null;
            foreach (var line in stdout.Split('\n'))
            {
                if (ProviderHttp.TryParse(line.Trim()) is not JsonObject ev) continue;
                if (ev["usage"] is JsonObject u)
                    usage = new TokenUsage(u["input_tokens"]?.GetValue<long>() ?? 0, u["output_tokens"]?.GetValue<long>() ?? 0);
                if (ev["type"]?.GetValue<string>() is "error" or "turn.failed")
                    reported = ev["message"]?.ToString() ?? ev["error"]?["message"]?.ToString() ?? reported;
            }

            var answer = File.Exists(answerFile) ? File.ReadAllText(answerFile).Trim() : "";
            if (exit != 0 || answer.Length == 0)
                throw Classify(reported ?? (string.IsNullOrWhiteSpace(stderr) ? $"codex exited with code {exit} and no answer." : stderr));

            yield return new ChatChunk(answer);
            if (usage is not null) yield return new ChatChunk(null, usage);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(["(Codex default)"]);

    public static ProviderException Classify(string message)
    {
        message = SecretRedactor.Redact(message.Trim());
        if (message.Length > 400) message = message[^400..];
        var lower = message.ToLowerInvariant();
        if (lower.Contains("not logged in") || lower.Contains("login") || lower.Contains("unauthorized") || lower.Contains("401") || lower.Contains("credential"))
            return new ProviderException(ProviderErrorKind.Auth, "Your ChatGPT account is not signed in to the Codex CLI. Use Settings › Accounts › Sign in. Details: " + message);
        if (lower.Contains("rate limit") || lower.Contains("usage limit") || lower.Contains("429"))
            return new ProviderException(ProviderErrorKind.RateLimited, "Codex reported a limit: " + message);
        return new ProviderException(ProviderErrorKind.Unavailable, "Codex failed: " + message);
    }
}
