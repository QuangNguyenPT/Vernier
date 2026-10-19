using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vernier.Core.Security;

namespace Vernier.Core.Tools;

/// <summary>
/// What a tool can do to the learner's computer. Observe reads; Annotate draws on Vernier's own
/// glass; Control makes a reversible change the learner can undo with one key (pause or seek a
/// video); Act sends input or changes another application; Consequential writes files, notes or
/// external services. Act and Consequential always need the learner's approval.
/// </summary>
public enum ToolRisk { Observe, Annotate, Control, Act, Consequential }

/// <param name="Name">Lower-case letters, digits and underscores; stable across a session so prompts stay cacheable.</param>
/// <param name="InputSchema">JSON Schema object (type, properties, required, enum). Validated before the tool runs.</param>
/// <param name="UntrustedOutput">The result carries text read from the screen, files, notes or the network; it is fenced before the model sees it.</param>
public sealed record ToolDefinition(string Name, string Description, JsonObject InputSchema, ToolRisk Risk, bool UntrustedOutput = false);

public abstract record ToolContent;
public sealed record ToolText(string Text) : ToolContent;
public sealed record ToolImage(byte[] Data, string MediaType) : ToolContent;

public sealed record ToolResult(IReadOnlyList<ToolContent> Content, bool IsError)
{
    public static ToolResult Ok(string text) => new([new ToolText(text)], false);
    public static ToolResult Fail(string text) => new([new ToolText(text)], true);
    public string Text => string.Join("\n", Content.OfType<ToolText>().Select(t => t.Text));
}

public interface ITool
{
    ToolDefinition Definition { get; }
    /// <summary>Throw a <see cref="ToolException"/> (or let a specific exception escape) on failure; the executor turns it into an error result the model can read.</summary>
    Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct);

    /// <summary>
    /// For tools that need approval: work out exactly what the call would do and say it in the learner's words, before they
    /// are asked. Throw a <see cref="ToolException"/> to refuse without asking. Null: the arguments are shown instead.
    /// </summary>
    Task<string?> PreviewAsync(JsonObject args, ToolTurn turn, CancellationToken ct) => Task.FromResult<string?>(null);

    /// <summary>The learner did not allow a previewed call: undo whatever the preview left behind (an on-screen mark, a plan).</summary>
    void Declined(JsonObject args, ToolTurn turn) { }
}

/// <summary>A failure the model should be told about in plain words, e.g. "no text 'F = ma' on screen; candidates: …".</summary>
public sealed class ToolException(string message) : Exception(message);

/// <summary>Decision for one Act or Consequential call. Anything not explicitly allowed is refused.</summary>
public enum ApprovalResolution { AllowOnce, Deny, Timeout, Cancelled }

public sealed record ApprovalRequest(ToolDefinition Tool, JsonObject Args, string Summary, ToolTurn Turn);

public interface IToolApprover
{
    Task<ApprovalResolution> ApproveAsync(ApprovalRequest request, CancellationToken ct);
}

/// <summary>One tool call as it happened, for the activity list the learner can inspect.</summary>
public sealed record ToolEvent(string TurnId, string Tool, string Arguments, bool IsError, string Outcome, TimeSpan Took, DateTimeOffset At);

/// <summary>
/// The authority for one learner question. Tools may only be called while it is active, only
/// those it allows, and it is revoked when the turn ends or the learner presses Esc.
/// </summary>
public sealed class ToolTurn
{
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentQueue<ToolEvent> _events = new();
    private int _calls;

    public ToolTurn(IEnumerable<string> allowed, CancellationToken outer, int maxCalls = 40)
    {
        Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Allowed = allowed.ToHashSet(StringComparer.Ordinal);
        MaxCalls = maxCalls;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
    }

    public string Id { get; }
    /// <summary>Bearer secret for the loopback MCP server. Never logged or shown.</summary>
    public string Token { get; }
    public IReadOnlySet<string> Allowed { get; }
    public int MaxCalls { get; }
    public bool IsActive => !_cts.IsCancellationRequested;
    public CancellationToken Cancellation => _cts.Token;
    /// <summary>Set once a tool returned text from the screen, files or the network; later Act calls in this turn always ask.</summary>
    public bool Tainted { get; internal set; }
    public IReadOnlyList<ToolEvent> Events => [.. _events];
    /// <summary>Per-turn state tools share (e.g. the current observation). Keys are tool-defined.</summary>
    public ConcurrentDictionary<string, object> State { get; } = new();
    /// <summary>Held by the executor across preview, approval and execution of an Act or Consequential call.</summary>
    internal SemaphoreSlim ActLock { get; } = new(1, 1);

    public event Action<ToolEvent>? EventAdded;

    internal bool TryCount() => Interlocked.Increment(ref _calls) <= MaxCalls;

    internal void Add(ToolEvent e)
    {
        _events.Enqueue(e);
        EventAdded?.Invoke(e);
    }

    public void End() => _cts.Cancel();
}

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.Ordinal);

    public void Add(ITool tool)
    {
        var name = tool.Definition.Name;
        if (name.Length is 0 or > 48 || !name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_') || !char.IsLetter(name[0]))
            throw new ArgumentException($"Tool name '{name}' must be lower-case letters, digits and underscores.");
        if (!_tools.TryAdd(name, tool)) throw new ArgumentException($"Tool '{name}' is registered twice.");
    }

    public ITool? Resolve(string name) => _tools.GetValueOrDefault(name);
    public IReadOnlyList<ToolDefinition> Definitions => [.. _tools.Values.Select(t => t.Definition).OrderBy(d => d.Name, StringComparer.Ordinal)];
}

/// <summary>
/// The one place a tool call is checked and run, whichever transport it arrived by (native
/// function calling or the MCP server): resolve → grant → validate → gate → approve → execute →
/// fence untrusted output. Failures become error results; they never throw into the model loop.
/// </summary>
/// <param name="gate">Settings check per tool, e.g. capture off. Returns a refusal the model can read, or null to allow.</param>
public sealed class ToolExecutor(ToolRegistry registry, IToolApprover approver, Func<ToolDefinition, string?>? gate = null, DiagnosticLogSink? log = null)
{
    public const string DenialText = "Denied by the learner. The tool call did not run. This denial is final for this call: do not call it again or ask for approval again; continue without it.";

    public ToolRegistry Registry => registry;

    public async Task<ToolResult> RunAsync(string name, JsonNode? arguments, ToolTurn turn, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var args = arguments as JsonObject ?? [];
        ToolResult result;
        try
        {
            result = await RunCoreAsync(name, args, turn, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!turn.IsActive || ct.IsCancellationRequested)
        {
            result = ToolResult.Fail("The learner stopped this turn. Nothing more will run.");
        }
        catch (ToolException e)
        {
            result = ToolResult.Fail(e.Message);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or TimeoutException)
        {
            log?.Invoke($"Tool {name} failed: {e.GetType().Name}");
            result = ToolResult.Fail($"The tool failed: {e.Message}");
        }
        turn.Add(new ToolEvent(turn.Id, name, Summarise(args), result.IsError, Summarise(result.Text), clock.Elapsed, DateTimeOffset.UtcNow));
        return result;
    }

    private async Task<ToolResult> RunCoreAsync(string name, JsonObject args, ToolTurn turn, CancellationToken ct)
    {
        if (!turn.IsActive) return ToolResult.Fail("This turn has ended; tools can no longer be called.");
        if (registry.Resolve(name) is not { } tool || !turn.Allowed.Contains(name))
            return ToolResult.Fail($"There is no tool '{name}' available in this turn. Available: {string.Join(", ", turn.Allowed.Order())}.");
        if (!turn.TryCount()) return ToolResult.Fail($"Tool call limit for this turn ({turn.MaxCalls}) reached. Finish your answer without more tools.");
        args = SchemaCheck.Repair(tool.Definition.InputSchema, args); // what small models commonly get wrong is fixed, not refused
        if (SchemaCheck.Validate(tool.Definition.InputSchema, args) is { } problem)
            return ToolResult.Fail($"Invalid arguments for {name}: {problem}. Call {name} again with arguments shaped like: {SchemaCheck.Example(tool.Definition.InputSchema, args)}");
        if (gate?.Invoke(tool.Definition) is { } refusal) return ToolResult.Fail(refusal);

        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct, turn.Cancellation);
        ToolResult result;
        if (tool.Definition.Risk is ToolRisk.Act or ToolRisk.Consequential)
        {
            // One effectful call at a time per turn: preview, approval and execution of a call are never interleaved with
            // another's, so what runs is what was previewed.
            await turn.ActLock.WaitAsync(run.Token).ConfigureAwait(false);
            try
            {
                var summary = await tool.PreviewAsync(args, turn, run.Token).ConfigureAwait(false) ?? $"{name} {Summarise(args)}";
                ApprovalResolution decision;
                try { decision = await approver.ApproveAsync(new ApprovalRequest(tool.Definition, args, summary, turn), run.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { decision = ApprovalResolution.Cancelled; }
                if (decision != ApprovalResolution.AllowOnce)
                {
                    tool.Declined(args, turn);
                    if (!turn.IsActive || ct.IsCancellationRequested) throw new OperationCanceledException();
                    return ToolResult.Fail(decision == ApprovalResolution.Timeout ? "The learner did not answer the approval in time, so the call did not run. Do not retry it." : DenialText);
                }
                result = await tool.ExecuteAsync(args, turn, run.Token).ConfigureAwait(false);
            }
            finally { turn.ActLock.Release(); }
        }
        else result = await tool.ExecuteAsync(args, turn, run.Token).ConfigureAwait(false);
        if (!tool.Definition.UntrustedOutput || result.IsError) return result;
        turn.Tainted = true;
        return result with
        {
            Content = [.. result.Content.Select(c => c is ToolText t ? new ToolText(Untrusted.Wrap("tool:" + name, t.Text, 16000)) : c)],
        };
    }

    private static string Summarise(JsonObject args) => Summarise(args.ToJsonString());

    private static string Summarise(string text)
    {
        text = SecretRedactor.Redact(text.Replace('\n', ' ').Replace('\r', ' '));
        return text.Length <= 160 ? text : text[..160] + "…";
    }
}

public delegate void DiagnosticLogSink(string message);

/// <summary>Reads JSON numbers whatever they were built from: parsed text gives JsonElement values, C# literals give int or double ones.</summary>
public static class Json
{
    public static double? Number(JsonNode? node) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.Number
            ? double.Parse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)
            : null;

    public static int? Int(JsonNode? node) => Number(node) is { } d ? (int)Math.Round(d) : null;
}

/// <summary>
/// Enough of JSON Schema for tool arguments: object, required, property types (string, number,
/// integer, boolean, array, object), enum, minimum/maximum, maxLength, and no unknown properties
/// when additionalProperties is false. Arguments come from a model, so they are untrusted.
/// </summary>
public static class SchemaCheck
{
    public static string? Validate(JsonObject schema, JsonObject args) => Check(schema, args, "");

    /// <summary>
    /// Fixes the shapes models (small ones most) get wrong, before validation: a number written as a string, a JSON object
    /// or list sent as a string, one value where a list is wanted, a point as {x, y} where a list is wanted, an enum in the
    /// wrong case, true/false as text, and unknown keys (dropped). What cannot be fixed is left for <see cref="Validate"/> to refuse.
    /// </summary>
    public static JsonObject Repair(JsonObject schema, JsonObject args) => Fix(schema, args.DeepClone()) as JsonObject ?? args;

    private static JsonNode? Fix(JsonObject schema, JsonNode? value)
    {
        if (value is null) return null;
        var type = schema["type"]?.GetValue<string>();
        if (value is JsonValue text && text.GetValueKind() == JsonValueKind.String && type is "object" or "array")
        {
            try { if (JsonNode.Parse(text.GetValue<string>()) is { } parsed) value = parsed; }
            catch (System.Text.Json.JsonException) { }
        }
        switch (type)
        {
            case "object" when value is JsonObject obj:
            {
                var props = schema["properties"] as JsonObject ?? [];
                var closed = schema["additionalProperties"]?.GetValue<bool>() == false;
                var fixedObj = new JsonObject();
                foreach (var (key, sub) in obj.ToList())
                {
                    var match = props.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (match.Value is JsonObject subSchema) fixedObj[match.Key] = Fix(subSchema, sub?.DeepClone());
                    else if (!closed) fixedObj[key] = sub?.DeepClone();
                }
                return fixedObj;
            }
            case "array":
            {
                var items = schema["items"] as JsonObject;
                if (value is JsonObject point && items?["type"]?.GetValue<string>() is "number" or "integer" && point.ContainsKey("x"))
                    value = new JsonArray(new[] { "x", "y", "z" }.Where(point.ContainsKey).Select(k => point[k]?.DeepClone()).ToArray());
                if (value is not JsonArray arr) arr = [value.DeepClone()];
                return items is null ? arr : new JsonArray(arr.Select(x => Fix(items, x?.DeepClone())).ToArray());
            }
            case "number" or "integer" when value is JsonValue v && v.GetValueKind() == JsonValueKind.String:
                return double.TryParse(v.GetValue<string>().Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)
                    ? (type == "integer" && Math.Abs(d - Math.Round(d)) < 1e-9 ? JsonValue.Create((long)Math.Round(d)) : JsonValue.Create(d)) : value;
            case "string" when value is JsonValue v && v.GetValueKind() == JsonValueKind.Number:
                return JsonValue.Create(v.ToJsonString());
            case "string" when value is JsonValue v && v.GetValueKind() == JsonValueKind.String && schema["enum"] is JsonArray options:
                var s = v.GetValue<string>().Trim();
                return options.Select(o => o?.GetValue<string>()).FirstOrDefault(o => string.Equals(o, s, StringComparison.OrdinalIgnoreCase)) is { } canonical ? JsonValue.Create(canonical) : value;
            case "boolean" when value is JsonValue v && v.GetValueKind() == JsonValueKind.String:
                return bool.TryParse(v.GetValue<string>().Trim(), out var b) ? JsonValue.Create(b) : value;
        }
        return value;
    }

    /// <summary>A minimal valid call for a schema (required arguments with placeholder values): shown when arguments are refused.</summary>
    /// <summary>A call that passes validation, built from what the model sent (its own values kept where they fit) with every missing required argument filled in.</summary>
    public static string Example(JsonObject schema, JsonNode? given = null) => (Sample(schema, given) ?? new JsonObject()).ToJsonString();

    private static JsonNode? Sample(JsonObject schema, JsonNode? given = null) => schema["type"]?.GetValue<string>() switch
    {
        "object" => SampleObject(schema, given as JsonObject),
        "array" when given is JsonArray { Count: > 0 } ga && schema["items"] is JsonObject gi => new JsonArray([.. ga.Select(g => Sample(gi, g))]),
        "array" => new JsonArray(Enumerable.Repeat(0, Math.Max(1, (int)(Json.Number(schema["minItems"]) ?? 1))).Select(_ => schema["items"] is JsonObject i ? Sample(i) : null).ToArray()),
        "number" or "integer" when given is JsonValue gv && gv.TryGetValue<double>(out _) => given.DeepClone(),
        "string" when given is JsonValue gs && gs.TryGetValue<string>(out var str) && (schema["enum"] is not JsonArray en || en.Any(e => e?.GetValue<string>() == str)) => given.DeepClone(),
        "boolean" when given is JsonValue gb && gb.TryGetValue<bool>(out _) => given.DeepClone(),
        "string" => schema["enum"] is JsonArray e ? e[0]?.DeepClone() : JsonValue.Create("…"),
        "number" or "integer" => JsonValue.Create(0),
        "boolean" => JsonValue.Create(false),
        _ => null,
    };

    private static JsonObject SampleObject(JsonObject schema, JsonObject? given)
    {
        var props = schema["properties"] as JsonObject ?? [];
        var o = new JsonObject();
        foreach (var (key, value) in given ?? [])
            if (props[key] is JsonObject p && Sample(p, value) is { } v) o[key] = v;
        foreach (var r in (schema["required"] as JsonArray ?? []).Select(r => r?.GetValue<string>()))
            if (r is not null && !o.ContainsKey(r) && props[r] is JsonObject p) o[r] = Sample(p);
        return o;
    }

    private static string? Check(JsonObject schema, JsonNode? value, string path)
    {
        var at = path.Length == 0 ? "arguments" : path;
        var type = schema["type"]?.GetValue<string>();
        if (value is null) return type is null ? null : $"{at} is missing";
        switch (type)
        {
            case "object":
                if (value is not JsonObject obj) return $"{at} must be an object";
                var props = schema["properties"] as JsonObject ?? [];
                foreach (var req in (schema["required"] as JsonArray ?? []).Select(r => r?.GetValue<string>()))
                    if (req is not null && (!obj.ContainsKey(req) || obj[req] is null)) return $"{Join(path, req)} is required";
                if (schema["additionalProperties"]?.GetValue<bool>() == false)
                    foreach (var key in obj.Select(p => p.Key))
                        if (!props.ContainsKey(key)) return $"{Join(path, key)} is not an allowed argument";
                foreach (var (key, sub) in obj)
                    if (props[key] is JsonObject subSchema && Check(subSchema, sub, Join(path, key)) is { } p) return p;
                break;
            case "array":
                if (value is not JsonArray arr) return $"{at} must be an array";
                if (Json.Number(schema["maxItems"]) is { } maxItems && arr.Count > maxItems) return $"{at} has more than {maxItems} items";
                if (schema["items"] is JsonObject itemSchema)
                    for (var i = 0; i < arr.Count; i++)
                        if (Check(itemSchema, arr[i], $"{at}[{i}]") is { } p) return p;
                break;
            case "string":
                if (value is not JsonValue sv || sv.GetValueKind() != JsonValueKind.String) return $"{at} must be a string";
                var s = sv.GetValue<string>();
                if (Json.Number(schema["maxLength"]) is { } maxLength && s.Length > maxLength) return $"{at} is longer than {maxLength} characters";
                if (schema["enum"] is JsonArray options && !options.Any(o => o?.GetValue<string>() == s))
                    return $"{at} must be one of: {string.Join(", ", options.Select(o => o?.GetValue<string>()))}";
                break;
            case "number" or "integer":
                if (value is not JsonValue nv || nv.GetValueKind() != JsonValueKind.Number) return $"{at} must be a number";
                var d = Json.Number(nv)!.Value;
                if (type == "integer" && Math.Abs(d - Math.Round(d)) > 1e-9) return $"{at} must be a whole number";
                if (double.IsNaN(d) || double.IsInfinity(d)) return $"{at} must be finite";
                if (Json.Number(schema["minimum"]) is { } min && d < min) return $"{at} must be at least {min}";
                if (Json.Number(schema["maximum"]) is { } max && d > max) return $"{at} must be at most {max}";
                break;
            case "boolean":
                if (value is not JsonValue bv || bv.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False)) return $"{at} must be true or false";
                break;
        }
        return null;
    }

    private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;
}
