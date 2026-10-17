using System.Net.Http;
using System.Text.Json.Nodes;
using Vernier.Core.Connectors;
using Vernier.Core.Tools;

namespace Vernier.App.Agent;

/// <summary>
/// The learner's "Vernier notes" Google Doc: read it (untrusted), or add to it after the learner
/// approves the exact text. Vernier creates the document on first use and can open no other
/// (the drive.file permission). Offered only while Google Docs is connected.
/// </summary>
public static class GoogleTools
{
    public const string DocTitle = "Vernier notes";
    /// <summary>Short enough to read in full on the approval card, which always shows all of it.</summary>
    public const int MaxText = 4000;
    public static readonly string[] ReadNames = ["gdocs_read"], WriteNames = ["gdocs_append"];
    public static IEnumerable<ITool> All() => [new Read(), new Append()];

    private static async Task<string> DocIdAsync(GoogleDocsClient docs, bool create, CancellationToken ct)
    {
        if (AppServices.Settings.GoogleNotesDocId is { Length: > 0 } id) return id;
        if (!create) throw new ToolException($"There is no \"{DocTitle}\" document yet; it is created the first time something is added.");
        id = await docs.CreateAsync(DocTitle, ct).ConfigureAwait(false);
        AppServices.Settings.GoogleNotesDocId = id;
        AppServices.SaveSettings();
        return id;
    }

    private static async Task<ToolResult> Guard(Func<Task<ToolResult>> call)
    {
        try { return await call().ConfigureAwait(false); }
        catch (ConnectorException e) { throw new ToolException(e.Message); }
        catch (HttpRequestException e) { throw new ToolException("Google could not be reached: " + e.Message); }
    }

    private sealed class Read : ITool
    {
        public ToolDefinition Definition { get; } = new("gdocs_read",
            $"Reads the learner's \"{DocTitle}\" Google Doc, where earlier explanations were saved.",
            new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false },
            ToolRisk.Observe, UntrustedOutput: true);

        public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct) => Guard(async () =>
        {
            var docs = AppServices.Google.Docs();
            var text = await docs.ReadTextAsync(await DocIdAsync(docs, create: false, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            return ToolResult.Ok(text.Length == 0 ? "The document is empty." : text.Length > 12000 ? text[^12000..] + "\n[earlier part not shown]" : text);
        });
    }

    private sealed class Append : ITool
    {
        public ToolDefinition Definition { get; } = new("gdocs_append",
            $"Adds text to the end of the learner's \"{DocTitle}\" Google Doc, only when they ask to save something there. They approve the exact text first.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["text"] = new JsonObject { ["type"] = "string", ["maxLength"] = MaxText, ["description"] = "Plain text to add; start with a short heading line." } },
                ["required"] = new JsonArray("text"), ["additionalProperties"] = false,
            },
            ToolRisk.Act);

        public Task<string?> PreviewAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var text = args["text"]!.GetValue<string>();
            return Task.FromResult<string?>($"Add to your Google Doc \"{DocTitle}\" (all of it, exactly):\n\n{text}");
        }

        public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct) => Guard(async () =>
        {
            var docs = AppServices.Google.Docs();
            await docs.AppendAsync(await DocIdAsync(docs, create: true, ct).ConfigureAwait(false), args["text"]!.GetValue<string>(), ct).ConfigureAwait(false);
            AppServices.Log.Info("Added to the Google notes document.");
            return ToolResult.Ok($"Added to \"{DocTitle}\".");
        });
    }
}
