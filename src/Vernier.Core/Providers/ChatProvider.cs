using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Tools;

namespace Vernier.Core.Providers;

public sealed record ImageInput(byte[] Data, string MediaType);

public sealed record ChatMessage(string Role, string Text, IReadOnlyList<ImageInput>? Images = null)
{
    public static ChatMessage User(string text, params ImageInput[] images) => new("user", text, images.Length == 0 ? null : images);
    public static ChatMessage Assistant(string text) => new("assistant", text);
}

/// <param name="Tools">Tools the model may call during this turn. Null: a plain answer.</param>
public sealed record ChatRequest(string? System, IReadOnlyList<ChatMessage> Messages, int MaxTokens = 8000, ToolSession? Tools = null)
{
    public bool HasImages => Messages.Any(m => m.Images is { Count: > 0 });
}

/// <summary>
/// Tools for one turn. Providers with native function calling run calls through
/// <see cref="RunAsync"/>; CLI providers point their MCP client at <see cref="McpConfig"/>.
/// Either way the same executor checks and runs every call.
/// </summary>
public sealed class ToolSession(IReadOnlyList<ToolDefinition> definitions, ToolTurn turn, ToolExecutor executor, McpServer? mcp = null)
{
    public IReadOnlyList<ToolDefinition> Definitions => definitions;
    public ToolTurn Turn => turn;
    public Task<ToolResult> RunAsync(string name, JsonNode? arguments, CancellationToken ct) => executor.RunAsync(name, arguments, turn, ct);
    /// <summary>Config for an MCP client, carrying the turn's secret. Null when no MCP server is running.</summary>
    public string? McpConfig => mcp?.ClientConfig(turn);
    public string McpServerName => McpServer.ServerName;
}

public sealed record TokenUsage(long Input, long Output);

/// <summary>What the model did with a tool, for the learner to see ("drawing…", "looking closer…").</summary>
public sealed record ToolCallNote(string Name, string Id, bool Finished, bool IsError = false);

/// <summary>A streamed piece of text, the final usage report (Text null), or a tool-call note.</summary>
public sealed record ChatChunk(string? Text, TokenUsage? Usage = null, ToolCallNote? Tool = null);

/// <param name="Tools">The provider can call tools mid-answer (native function calling, or MCP for CLI providers).</param>
public sealed record ProviderCapabilities(bool Vision, bool Streaming, bool IsLocal, bool ReportsUsage, string Notes, bool Tools = false);

public enum ProviderErrorKind { NotConfigured, Auth, RateLimited, BadRequest, Refused, Server, Network, Timeout, Unavailable }

public sealed class ProviderException(ProviderErrorKind kind, string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(message, inner)
{
    public ProviderErrorKind Kind { get; } = kind;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public bool IsTransient => Kind is ProviderErrorKind.RateLimited or ProviderErrorKind.Server or ProviderErrorKind.Network;
}

public interface IChatProvider
{
    string Id { get; }
    string DisplayName { get; }
    string Model { get; }
    ProviderCapabilities Capabilities { get; }
    IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, CancellationToken ct);
    /// <summary>Capability discovery: asks the service which models it offers.</summary>
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct);
}

public sealed record ChatResult(string Text, TokenUsage? Usage, int Attempts);

public static class ChatRunner
{
    public const int MaxAttempts = 3;

    /// <summary>
    /// Streams with bounded retries. A retry only happens before any text has been shown, so the
    /// learner never sees an answer restart. Images are refused up front for text-only models
    /// rather than silently dropped.
    /// </summary>
    public static async IAsyncEnumerable<ChatChunk> StreamWithRetryAsync(
        IChatProvider provider, ChatRequest request, TimeSpan timeout,
        [EnumeratorCancellation] CancellationToken ct, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        if (request.HasImages && !provider.Capabilities.Vision)
            throw new ProviderException(ProviderErrorKind.BadRequest,
                $"{provider.DisplayName} is not configured as an image-capable model. Use extracted text only, or pick a vision model.");

        delay ??= Task.Delay;
        for (var attempt = 1; ; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            var yielded = false;
            ProviderException? failure = null;

            var stream = provider.StreamAsync(request, timeoutCts.Token).GetAsyncEnumerator(timeoutCts.Token);
            try
            {
                while (true)
                {
                    ChatChunk chunk;
                    try
                    {
                        if (!await stream.MoveNextAsync().ConfigureAwait(false)) break;
                        chunk = stream.Current;
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        failure = new ProviderException(ProviderErrorKind.Timeout, $"{provider.DisplayName} did not finish within {timeout.TotalSeconds:0}s.");
                        break;
                    }
                    catch (ProviderException e)
                    {
                        failure = e;
                        break;
                    }
                    // A tool call counts as visible output: retrying after it would draw or act twice.
                    yielded |= !string.IsNullOrEmpty(chunk.Text) || chunk.Tool is not null;
                    yield return chunk;
                }
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            if (failure is null) yield break;
            if (yielded || !failure.IsTransient || attempt >= MaxAttempts) throw failure;

            var wait = failure.RetryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
            if (wait > TimeSpan.FromSeconds(30)) throw failure; // do not sit on a long rate-limit window silently
            await delay(wait, ct).ConfigureAwait(false);
        }
    }

    public static async Task<ChatResult> CompleteAsync(IChatProvider provider, ChatRequest request, TimeSpan timeout, CancellationToken ct)
    {
        var sb = new StringBuilder();
        TokenUsage? usage = null;
        await foreach (var c in StreamWithRetryAsync(provider, request, timeout, ct).ConfigureAwait(false))
        {
            if (c.Text is not null) sb.Append(c.Text);
            if (c.Usage is not null) usage = c.Usage;
        }
        return new ChatResult(sb.ToString(), usage, 1);
    }

    /// <summary>The small, user-triggered connection test.</summary>
    public static async Task<(bool Ok, string Message)> TestAsync(IChatProvider provider, CancellationToken ct)
    {
        try
        {
            var r = await CompleteAsync(provider,
                new ChatRequest(null, [ChatMessage.User("Reply with the single word: ready")], MaxTokens: 64),
                TimeSpan.FromSeconds(45), ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(r.Text)
                ? (false, "The provider answered with an empty response.")
                : (true, $"Connected. Model replied: \"{r.Text.Trim()}\"" + (r.Usage is { } u ? $" ({u.Input} in / {u.Output} out tokens)" : ""));
        }
        catch (ProviderException e) { return (false, $"{e.Kind}: {e.Message}"); }
        catch (OperationCanceledException) { return (false, "Cancelled."); }
    }
}
