using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Vernier.Core.Security;
using Vernier.Core.Settings;

namespace Vernier.Core.Providers;

/// <summary>Claude through the official Anthropic C# SDK (Messages API, streaming).</summary>
public sealed class AnthropicProvider(ProviderSettings settings, ISecretStore secrets) : IChatProvider
{
    public string Id => settings.Id;
    public string DisplayName => settings.DisplayName;
    public string Model => settings.Model;

    public ProviderCapabilities Capabilities => new(
        Vision: true, Streaming: true, IsLocal: false, ReportsUsage: true,
        Notes: "Remote. Images and text are sent to Anthropic. Thinking is adaptive (model default). Calls Vernier's tools natively.",
        Tools: true);

    /// <summary>Most model ↔ tool round trips in one answer. Each step is a full request; the executor's call limit applies as well.</summary>
    public const int MaxToolSteps = 8;

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var client = CreateClient();
        var messages = request.Messages.Select(ToParam).ToList();
        var tools = request.Tools;
        long input = 0, output = 0;

        for (var step = 0; ; step++)
        {
            var parameters = new MessageCreateParams
            {
                Model = string.IsNullOrWhiteSpace(settings.Model) ? "claude-opus-5-5" : settings.Model,
                MaxTokens = request.MaxTokens,
                System = request.System ?? "",
                Messages = messages,
            };
            // On the last allowed step no tools are offered, so the model has to finish in words.
            if (tools is not null && step < MaxToolSteps)
                parameters = parameters with { Tools = [.. tools.Definitions.Select(ToSdkTool)] };

            var blocks = new SortedDictionary<long, StreamedBlock>();
            var results = new List<ContentBlockParam>();
            string? stopReason = null;
            var stream = client.Messages.CreateStreaming(parameters, cancellationToken: ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    RawMessageStreamEvent ev;
                    try
                    {
                        if (!await stream.MoveNextAsync().ConfigureAwait(false)) break;
                        ev = stream.Current;
                    }
                    catch (Exception e) when (e is not OperationCanceledException) { throw Translate(e); }

                    if (ev.TryPickContentBlockStart(out var start))
                    {
                        var block = new StreamedBlock();
                        if (start.ContentBlock.TryPickToolUse(out var use))
                        {
                            (block.ToolId, block.ToolName) = (use.ID, use.Name);
                            yield return new ChatChunk(null, Tool: new ToolCallNote(use.Name, use.ID, false));
                        }
                        else if (start.ContentBlock.TryPickThinking(out var thinking)) { block.IsThinking = true; block.Text.Append(thinking.Thinking); block.Signature = thinking.Signature; }
                        else if (start.ContentBlock.TryPickRedactedThinking(out var redacted)) block.Redacted = redacted.Data;
                        else if (start.ContentBlock.TryPickText(out var text)) block.Text.Append(text.Text);
                        blocks[start.Index] = block;
                    }
                    else if (ev.TryPickContentBlockDelta(out var delta) && blocks.TryGetValue(delta.Index, out var open))
                    {
                        if (delta.Delta.TryPickText(out var text))
                        {
                            open.Text.Append(text.Text);
                            yield return new ChatChunk(text.Text);
                        }
                        else if (delta.Delta.TryPickInputJson(out var json)) open.Json.Append(json.PartialJson);
                        else if (delta.Delta.TryPickThinking(out var thinking)) open.Text.Append(thinking.Thinking);
                        else if (delta.Delta.TryPickSignature(out var signature)) open.Signature = signature.Signature;
                    }
                    else if (ev.TryPickContentBlockStop(out var stop) && blocks.TryGetValue(stop.Index, out var done) && done.ToolId is not null && tools is not null)
                    {
                        // Run the tool the moment its arguments are complete: a mark appears while the answer is still being written.
                        var args = Providers.ProviderHttp.TryParse(done.Json.Length == 0 ? "{}" : done.Json.ToString());
                        var result = args is JsonObject
                            ? await tools.RunAsync(done.ToolName!, args, ct).ConfigureAwait(false)
                            : Tools.ToolResult.Fail("The arguments were not a complete JSON object. Send the call again with valid JSON.");
                        done.Input = args as JsonObject ?? [];
                        results.Add(ToResultBlock(done.ToolId, result));
                        yield return new ChatChunk(null, Tool: new ToolCallNote(done.ToolName!, done.ToolId, true, result.IsError));
                    }
                    else if (ev.TryPickStart(out var messageStart))
                        input += messageStart.Message.Usage.InputTokens;
                    else if (ev.TryPickDelta(out var messageDelta))
                    {
                        output += messageDelta.Usage.OutputTokens;
                        stopReason = messageDelta.Delta.StopReason?.Raw();
                    }
                }
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            // A refusal is HTTP 200 with partial or empty text; surface it instead of presenting it as an answer.
            if (stopReason == "refusal")
                throw new ProviderException(ProviderErrorKind.Refused, "Claude declined this request (stop_reason: refusal). No other model was substituted.");
            if (stopReason != "tool_use" || results.Count == 0) break;

            // Echo the assistant turn exactly (thinking with its signature, text, tool calls), then one result per call.
            var echo = new List<ContentBlockParam>();
            foreach (var b in blocks.Values)
            {
                if (b.Redacted is not null) echo.Add(new RedactedThinkingBlockParam { Data = b.Redacted });
                else if (b.IsThinking) echo.Add(new ThinkingBlockParam { Thinking = b.Text.ToString(), Signature = b.Signature ?? "" });
                else if (b.ToolId is not null) echo.Add(new ToolUseBlockParam { ID = b.ToolId, Name = b.ToolName!, Input = ToSdkInput(b.Input) });
                else if (b.Text.Length > 0) echo.Add(new TextBlockParam { Text = b.Text.ToString() });
            }
            messages = [.. messages, new MessageParam { Role = Role.Assistant, Content = echo }, new MessageParam { Role = Role.User, Content = results }];
        }
        yield return new ChatChunk(null, new TokenUsage(input, output));
    }

    private sealed class StreamedBlock
    {
        public StringBuilder Text { get; } = new();
        public StringBuilder Json { get; } = new();
        public bool IsThinking { get; set; }
        public string? Signature { get; set; }
        public string? Redacted { get; set; }
        public string? ToolId { get; set; }
        public string? ToolName { get; set; }
        public JsonObject Input { get; set; } = [];
    }

    private static Tool ToSdkTool(Tools.ToolDefinition d) => new()
    {
        Name = d.Name,
        Description = d.Description,
        // Arguments stream as they are generated; the executor validates them against the schema before anything runs.
        EagerInputStreaming = true,
        InputSchema = new()
        {
            Properties = (d.InputSchema["properties"] as JsonObject ?? []).ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value)),
            Required = [.. (d.InputSchema["required"] as JsonArray ?? []).Select(r => r!.GetValue<string>())],
        },
    };

    private static Dictionary<string, JsonElement> ToSdkInput(JsonObject input) =>
        input.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));

    private static ToolResultBlockParam ToResultBlock(string id, Tools.ToolResult result)
    {
        if (!result.Content.OfType<Tools.ToolImage>().Any())
            return new ToolResultBlockParam { ToolUseID = id, Content = result.Text, IsError = result.IsError };
        var parts = new List<Block>();
        foreach (var c in result.Content)
            if (c is Tools.ToolImage img) parts.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(img.Data), MediaType = img.MediaType } });
            else if (c is Tools.ToolText t) parts.Add(new TextBlockParam { Text = t.Text });
        return new ToolResultBlockParam { ToolUseID = id, Content = parts, IsError = result.IsError };
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        try
        {
            var page = await CreateClient().Models.List(cancellationToken: ct).ConfigureAwait(false);
            return page.Items.Select(m => m.ID).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException) { throw Translate(e); }
    }

    private IAnthropicClient CreateClient()
    {
        var key = settings.ApiKeyName is null ? null : secrets.Get(settings.ApiKeyName);
        if (string.IsNullOrWhiteSpace(key))
            throw new ProviderException(ProviderErrorKind.NotConfigured,
                $"No API key found. Set {settings.ApiKeyName} as an environment variable or save it in Settings.");
        // Retries are owned by ChatRunner so they stay bounded and visible in one place.
        IAnthropicClient client = new AnthropicClient { ApiKey = key, MaxRetries = 0 };
        return string.IsNullOrWhiteSpace(settings.BaseUrl) ? client : client.WithOptions(o => o with { BaseUrl = settings.BaseUrl });
    }

    private static MessageParam ToParam(ChatMessage m)
    {
        var role = m.Role == "assistant" ? Role.Assistant : Role.User;
        if (m.Images is not { Count: > 0 }) return new MessageParam { Role = role, Content = m.Text };
        var blocks = new List<ContentBlockParam>();
        foreach (var img in m.Images)
            blocks.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = Convert.ToBase64String(img.Data), MediaType = img.MediaType },
            });
        blocks.Add(new TextBlockParam { Text = m.Text });
        return new MessageParam { Role = role, Content = blocks };
    }

    private static ProviderException Translate(Exception e) => e switch
    {
        ProviderException p => p,
        AnthropicRateLimitException => new ProviderException(ProviderErrorKind.RateLimited, "Anthropic rate limit reached.", inner: e),
        AnthropicUnauthorizedException or AnthropicForbiddenException => new ProviderException(ProviderErrorKind.Auth, "Anthropic rejected the API key.", inner: e),
        AnthropicBadRequestException or AnthropicNotFoundException or AnthropicUnprocessableEntityException
            => new ProviderException(ProviderErrorKind.BadRequest, "Anthropic rejected the request: " + SecretRedactor.Redact(e.Message), inner: e),
        Anthropic5xxException => new ProviderException(ProviderErrorKind.Server, "Anthropic had a server error.", inner: e),
        AnthropicIOException or HttpRequestException or IOException => new ProviderException(ProviderErrorKind.Network, "Could not reach Anthropic.", inner: e),
        _ => new ProviderException(ProviderErrorKind.Unavailable, "Anthropic call failed: " + SecretRedactor.Redact(e.Message), inner: e),
    };
}
