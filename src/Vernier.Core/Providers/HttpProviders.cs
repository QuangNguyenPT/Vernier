using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vernier.Core.Security;
using Vernier.Core.Settings;
using Vernier.Core.Tools;

namespace Vernier.Core.Providers;

public static class ProviderHttp
{
    /// <summary>Remote endpoints must be TLS. Plain HTTP is only accepted for loopback (local model servers).</summary>
    public static Uri RequireSafeBase(string? baseUrl, string providerName)
    {
        if (!Uri.TryCreate(baseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out var uri))
            throw new ProviderException(ProviderErrorKind.NotConfigured, $"{providerName}: the endpoint URL is missing or invalid.");
        if (uri.Scheme == Uri.UriSchemeHttps) return uri;
        if (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) return uri;
        throw new ProviderException(ProviderErrorKind.NotConfigured, $"{providerName}: only https:// endpoints are allowed (http:// is accepted for localhost only).");
    }

    public static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpRequestMessage request, string providerName, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new ProviderException(ProviderErrorKind.Network, $"Could not reach {providerName} at {request.RequestUri?.GetLeftPart(UriPartial.Authority)}.", inner: e);
        }
        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            var body = SecretRedactor.Redact(await ReadBounded(response, ct).ConfigureAwait(false));
            var status = (int)response.StatusCode;
            var kind = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ProviderErrorKind.Auth,
                HttpStatusCode.TooManyRequests => ProviderErrorKind.RateLimited,
                >= HttpStatusCode.InternalServerError => ProviderErrorKind.Server,
                _ => ProviderErrorKind.BadRequest,
            };
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } d ? d - DateTimeOffset.UtcNow : null);
            throw new ProviderException(kind, $"{providerName} returned HTTP {status}. {body}".Trim(), retryAfter);
        }
    }

    public static async IAsyncEnumerable<string> ReadSseDataAsync(HttpResponseMessage response, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data.Length > 0) yield return data;
        }
    }

    /// <summary>Most model ↔ tool round trips in one answer; on the last step no tools are offered, so the model finishes in words.</summary>
    public const int MaxToolSteps = 8;

    /// <summary>Tool schemas for APIs that accept only a subset of JSON Schema (Gemini): unsupported keys are dropped, validation still happens in the executor.</summary>
    public static JsonNode Simplify(JsonNode schema, IReadOnlySet<string> keep) => schema switch
    {
        JsonObject o => new JsonObject(o.Where(p => keep.Contains(p.Key))
            .Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : p.Key == "properties"
                ? (JsonNode)new JsonObject(((JsonObject)p.Value).Select(q => KeyValuePair.Create(q.Key, q.Value is null ? null : Simplify(q.Value, keep))))
                : Simplify(p.Value, keep)))),
        JsonArray a => new JsonArray([.. a.Select(x => x?.DeepClone())]),
        _ => schema.DeepClone(),
    };

    public static JsonNode? TryParse(string json)
    {
        try { return JsonNode.Parse(json); }
        catch (JsonException) { return null; } // a malformed keep-alive line is skipped, not fatal
    }

    private static async Task<string> ReadBounded(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return text.Length > 400 ? text[..400] + "…" : text;
        }
        catch (Exception e) when (e is HttpRequestException or IOException) { return ""; }
    }
}

/// <summary>
/// Chat Completions wire format: OpenAI, Moonshot/Kimi, and local servers (Ollama, LM Studio,
/// llama.cpp, vLLM) that implement the same endpoint.
/// </summary>
public sealed class OpenAiCompatibleProvider(ProviderSettings settings, ISecretStore secrets, HttpClient http) : IChatProvider
{
    public string Id => settings.Id;
    public string DisplayName => settings.DisplayName;
    public string Model => settings.Model;

    public ProviderCapabilities Capabilities => new(
        Vision: settings.SupportsVision, Streaming: true, IsLocal: settings.IsLocal, ReportsUsage: true,
        Notes: settings.IsLocal
            ? "Local server. Nothing leaves this machine. Image support depends on the loaded model and is off unless you enable it; usage reporting depends on the server."
            : "Remote. Text" + (settings.SupportsVision ? " and images are" : " is") + " sent to the configured endpoint.",
        Tools: settings.SupportsTools);

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Model))
            throw new ProviderException(ProviderErrorKind.NotConfigured, $"{DisplayName}: choose a model in Settings (use \"List models\" to see what the endpoint offers).");

        var messages = new JsonArray();
        if (!string.IsNullOrEmpty(request.System))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.System });
        foreach (var m in request.Messages)
        {
            if (m.Images is not { Count: > 0 })
            {
                messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Text });
                continue;
            }
            var parts = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = m.Text } };
            foreach (var img in m.Images)
                parts.Add(new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = $"data:{img.MediaType};base64,{Convert.ToBase64String(img.Data)}" },
                });
            messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = parts });
        }

        var tools = request.Tools;
        TokenUsage? usage = null;
        for (var step = 0; ; step++)
        {
            var body = new JsonObject
            {
                ["model"] = settings.Model,
                ["messages"] = messages.DeepClone(),
                ["stream"] = true,
                ["stream_options"] = new JsonObject { ["include_usage"] = true },
                [settings.MaxTokensField] = request.MaxTokens,
            };
            if (tools is not null && step < ProviderHttp.MaxToolSteps)
                body["tools"] = new JsonArray([.. tools.Definitions.Select(d => (JsonNode)new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = d.Name, ["description"] = d.Description, ["parameters"] = d.InputSchema.DeepClone() },
                })]);

            using var req = Build(HttpMethod.Post, "chat/completions");
            req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await ProviderHttp.SendAsync(http, req, DisplayName, ct).ConfigureAwait(false);

            // Tool calls stream as fragments keyed by index; a call is complete when the next index starts or the choice finishes.
            var calls = new SortedDictionary<int, (string Id, string Name, StringBuilder Args)>();
            var ran = new Dictionary<int, ToolResult>();
            var said = new StringBuilder();
            string? finish = null;
            await foreach (var data in ProviderHttp.ReadSseDataAsync(response, ct).ConfigureAwait(false))
            {
                if (data == "[DONE]") break;
                var node = ProviderHttp.TryParse(data);
                if (node is null) continue;
                if (node["usage"] is JsonObject u)
                    usage = Add(usage, new TokenUsage(u["prompt_tokens"]?.GetValue<long>() ?? 0, u["completion_tokens"]?.GetValue<long>() ?? 0));
                if (node["choices"] is not JsonArray { Count: > 0 } choices) continue;
                var delta = choices[0]?["delta"];
                if (delta?["content"] is JsonValue content && content.TryGetValue<string>(out var text) && text.Length > 0)
                {
                    said.Append(text);
                    yield return new ChatChunk(text);
                }
                foreach (var tc in delta?["tool_calls"] as JsonArray ?? [])
                {
                    var index = Json.Int(tc?["index"]) ?? 0;
                    if (!calls.ContainsKey(index))
                    {
                        foreach (var (i, c) in calls.Where(c => !ran.ContainsKey(c.Key)).ToList())
                        {
                            ran[i] = await Run(tools, c.Name, c.Args.ToString(), ct).ConfigureAwait(false);
                            yield return new ChatChunk(null, Tool: new ToolCallNote(c.Name, c.Id, true, ran[i].IsError));
                        }
                        calls[index] = (tc?["id"]?.GetValue<string>() ?? $"call_{index}", tc?["function"]?["name"]?.GetValue<string>() ?? "", new StringBuilder());
                        yield return new ChatChunk(null, Tool: new ToolCallNote(calls[index].Name, calls[index].Id, false));
                    }
                    if (tc?["function"]?["arguments"]?.GetValue<string>() is { } part) calls[index].Args.Append(part);
                }
                if (choices[0]?["finish_reason"]?.GetValue<string>() is { } f) finish = f;
            }
            if (finish != "tool_calls" || calls.Count == 0 || tools is null) break;
            foreach (var (i, c) in calls.Where(c => !ran.ContainsKey(c.Key)).ToList())
            {
                ran[i] = await Run(tools, c.Name, c.Args.ToString(), ct).ConfigureAwait(false);
                yield return new ChatChunk(null, Tool: new ToolCallNote(c.Name, c.Id, true, ran[i].IsError));
            }

            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = said.Length > 0 ? said.ToString() : null,
                ["tool_calls"] = new JsonArray([.. calls.Values.Select(c => (JsonNode)new JsonObject
                {
                    ["id"] = c.Id, ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Args.Length == 0 ? "{}" : c.Args.ToString() },
                })]),
            });
            var pictures = new JsonArray();
            foreach (var (i, c) in calls)
            {
                messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = c.Id, ["content"] = (ran[i].IsError ? "ERROR: " : "") + ran[i].Text });
                // Tool messages carry text only; a picture a tool returned follows as a user message the model can see.
                foreach (var img in ran[i].Content.OfType<ToolImage>())
                    if (settings.SupportsVision)
                        pictures.Add(new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = $"data:{img.MediaType};base64,{Convert.ToBase64String(img.Data)}" } });
            }
            if (pictures.Count > 0)
            {
                pictures.Insert(0, new JsonObject { ["type"] = "text", ["text"] = "The image returned by the tool call above:" });
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = pictures });
            }
        }
        if (usage is not null) yield return new ChatChunk(null, usage);
    }

    private static async Task<ToolResult> Run(ToolSession? tools, string name, string arguments, CancellationToken ct) =>
        tools is null ? ToolResult.Fail("No tools are available.")
        : ProviderHttp.TryParse(arguments.Length == 0 ? "{}" : arguments) is JsonObject args ? await tools.RunAsync(name, args, ct).ConfigureAwait(false)
        : ToolResult.Fail("The arguments were not valid JSON. Send the call again with a JSON object.");

    private static TokenUsage Add(TokenUsage? a, TokenUsage b) => a is null ? b : new TokenUsage(a.Input + b.Input, a.Output + b.Output);

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        using var req = Build(HttpMethod.Get, "models");
        using var response = await ProviderHttp.SendAsync(http, req, DisplayName, ct).ConfigureAwait(false);
        var node = ProviderHttp.TryParse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return node?["data"] is JsonArray data
            ? data.Select(m => m?["id"]?.GetValue<string>()).Where(id => id is not null).Select(id => id!).Order().ToArray()
            : [];
    }

    private HttpRequestMessage Build(HttpMethod method, string path)
    {
        var baseUri = ProviderHttp.RequireSafeBase(settings.BaseUrl, DisplayName);
        var req = new HttpRequestMessage(method, new Uri(baseUri, path));
        var key = settings.ApiKeyName is null ? null : secrets.Get(settings.ApiKeyName);
        if (!string.IsNullOrWhiteSpace(key))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        else if (!settings.IsLocal)
            throw new ProviderException(ProviderErrorKind.NotConfigured,
                $"No API key found. Set {settings.ApiKeyName} as an environment variable or save it in Settings.");
        return req;
    }
}

/// <summary>Gemini generateContent REST API (v1beta), SSE streaming.</summary>
public sealed class GeminiProvider(ProviderSettings settings, ISecretStore secrets, HttpClient http) : IChatProvider
{
    public string Id => settings.Id;
    public string DisplayName => settings.DisplayName;
    public string Model => settings.Model;

    public ProviderCapabilities Capabilities => new(
        Vision: settings.SupportsVision, Streaming: true, IsLocal: false, ReportsUsage: true,
        Notes: "Remote. Text and images are sent to Google. Calls Vernier's tools natively.",
        Tools: true);

    private static readonly HashSet<string> SchemaKeys = ["type", "properties", "required", "description", "enum", "items", "minimum", "maximum", "maxItems", "maxLength"];

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Model))
            throw new ProviderException(ProviderErrorKind.NotConfigured, "Gemini: choose a model in Settings (use \"List models\").");

        var contents = new JsonArray();
        foreach (var m in request.Messages)
        {
            var parts = new JsonArray { new JsonObject { ["text"] = m.Text } };
            foreach (var img in m.Images ?? [])
                parts.Add(new JsonObject
                {
                    ["inlineData"] = new JsonObject { ["mimeType"] = img.MediaType, ["data"] = Convert.ToBase64String(img.Data) },
                });
            contents.Add(new JsonObject { ["role"] = m.Role == "assistant" ? "model" : "user", ["parts"] = parts });
        }
        var model = settings.Model.StartsWith("models/", StringComparison.Ordinal) ? settings.Model : "models/" + settings.Model;
        var tools = request.Tools;
        TokenUsage? usage = null;
        for (var step = 0; ; step++)
        {
            var body = new JsonObject
            {
                ["contents"] = contents.DeepClone(),
                ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = request.MaxTokens },
            };
            if (!string.IsNullOrEmpty(request.System))
                body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = request.System } } };
            if (tools is not null && step < ProviderHttp.MaxToolSteps)
                body["tools"] = new JsonArray(new JsonObject
                {
                    ["functionDeclarations"] = new JsonArray([.. tools.Definitions.Select(d => (JsonNode)new JsonObject
                    {
                        ["name"] = d.Name, ["description"] = d.Description, ["parameters"] = ProviderHttp.Simplify(d.InputSchema, SchemaKeys),
                    })]),
                });

            using var req = Build(HttpMethod.Post, $"{model}:streamGenerateContent?alt=sse");
            req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await ProviderHttp.SendAsync(http, req, DisplayName, ct).ConfigureAwait(false);

            // Gemini sends each function call whole. The model's parts are echoed back unchanged: they can carry thought signatures it requires.
            var modelParts = new JsonArray();
            var answers = new JsonArray();
            await foreach (var data in ProviderHttp.ReadSseDataAsync(response, ct).ConfigureAwait(false))
            {
                var node = ProviderHttp.TryParse(data);
                if (node is null) continue;
                if (node["usageMetadata"] is JsonObject u)
                    usage = new TokenUsage(u["promptTokenCount"]?.GetValue<long>() ?? 0, u["candidatesTokenCount"]?.GetValue<long>() ?? 0);
                if (node["candidates"] is not JsonArray { Count: > 0 } candidates) continue;
                if (candidates[0]?["content"]?["parts"] is not JsonArray parts) continue;
                foreach (var p in parts)
                {
                    if (p is null) continue;
                    modelParts.Add(p.DeepClone());
                    if (p["text"] is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0 && p["thought"]?.GetValue<bool>() != true)
                        yield return new ChatChunk(text);
                    if (p["functionCall"] is JsonObject call && tools is not null)
                    {
                        var name = call["name"]?.GetValue<string>() ?? "";
                        var id = call["id"]?.GetValue<string>() ?? name;
                        yield return new ChatChunk(null, Tool: new ToolCallNote(name, id, false));
                        var result = await tools.RunAsync(name, call["args"] as JsonObject ?? [], ct).ConfigureAwait(false);
                        yield return new ChatChunk(null, Tool: new ToolCallNote(name, id, true, result.IsError));
                        var reply = new JsonObject { ["name"] = name, ["response"] = new JsonObject { [result.IsError ? "error" : "content"] = result.Text } };
                        if (call["id"] is not null) reply["id"] = call["id"]!.DeepClone();
                        answers.Add(new JsonObject { ["functionResponse"] = reply });
                        foreach (var img in result.Content.OfType<ToolImage>())
                            if (settings.SupportsVision) answers.Add(new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = img.MediaType, ["data"] = Convert.ToBase64String(img.Data) } });
                    }
                }
            }
            if (answers.Count == 0) break;
            contents.Add(new JsonObject { ["role"] = "model", ["parts"] = modelParts });
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = answers });
        }
        if (usage is not null) yield return new ChatChunk(null, usage);
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        using var req = Build(HttpMethod.Get, "models?pageSize=200");
        using var response = await ProviderHttp.SendAsync(http, req, DisplayName, ct).ConfigureAwait(false);
        var node = ProviderHttp.TryParse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        if (node?["models"] is not JsonArray models) return [];
        return models
            .Where(m => m?["supportedGenerationMethods"] is JsonArray methods && methods.Any(x => x?.GetValue<string>() == "generateContent"))
            .Select(m => m?["name"]?.GetValue<string>()?.Replace("models/", ""))
            .Where(n => n is not null).Select(n => n!).Order().ToArray();
    }

    private HttpRequestMessage Build(HttpMethod method, string path)
    {
        var baseUri = ProviderHttp.RequireSafeBase(settings.BaseUrl, DisplayName);
        var key = settings.ApiKeyName is null ? null : secrets.Get(settings.ApiKeyName);
        if (string.IsNullOrWhiteSpace(key))
            throw new ProviderException(ProviderErrorKind.NotConfigured,
                $"No API key found. Set {settings.ApiKeyName} as an environment variable or save it in Settings.");
        var req = new HttpRequestMessage(method, new Uri(baseUri, path));
        req.Headers.Add("x-goog-api-key", key); // header, never the ?key= query string, so it cannot land in a URL log
        return req;
    }
}
