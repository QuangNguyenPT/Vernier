using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Knowledge;
using Vernier.Core.Security;
using Vernier.Core.Tools;

namespace Vernier.App.Agent;

/// <summary>
/// The learner's Obsidian vault as tools: search and read notes (fenced as untrusted), and write a
/// concept note. A write is planned before approval (one note per concept, the index updated with
/// it), shown as a diff, and applied as one transaction that aborts if any file changed since the
/// preview. Offered only when a vault is connected and notes may go to the chosen model.
/// </summary>
public static class NoteTools
{
    public static readonly string[] Names = ["notes_search", "notes_read", "notes_write"];
    public static IEnumerable<ITool> All() => [new Search(), new ReadNote(), new WriteNote()];

    private static ObsidianVault Vault() =>
        AppServices.Settings.VaultPath is { Length: > 0 } path ? new ObsidianVault(path) : throw new ToolException("No Obsidian vault is connected (Notes & files).");

    private static JsonObject Str(string description, int maxLength) => new() { ["type"] = "string", ["description"] = description, ["maxLength"] = maxLength };

    private static JsonObject Schema(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray([.. required.Select(r => (JsonNode)r)]), ["additionalProperties"] = false,
    };

    private sealed class Search : ITool
    {
        public ToolDefinition Definition { get; } = new("notes_search",
            "Searches the learner's own notes (their Obsidian vault) for a topic and returns matching notes with a line from each. Use it before explaining something they may already have notes on, or before writing a note, to link to what exists.",
            Schema(new JsonObject { ["query"] = Str("Words to look for.", 120) }, "query"), ToolRisk.Observe, UntrustedOutput: true);

        public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct) => Task.Run(() =>
        {
            var hits = Vault().Search(args["query"]!.GetValue<string>(), 8, ct);
            if (hits.Count == 0) return ToolResult.Ok("No notes match.");
            var sb = new StringBuilder();
            foreach (var h in hits) sb.Append(CultureInfo.InvariantCulture, $"{h.Note.RelativePath} ({h.Note.Title}): {h.Snippet}\n");
            return ToolResult.Ok(sb.ToString());
        }, ct);
    }

    private sealed class ReadNote : ITool
    {
        public ToolDefinition Definition { get; } = new("notes_read",
            "Reads one of the learner's notes by its path from notes_search.",
            Schema(new JsonObject { ["path"] = Str("The note's path inside the vault, e.g. Vernier/Concepts/Torque.md.", 260) }, "path"), ToolRisk.Observe, UntrustedOutput: true);

        public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct) => Task.Run(() =>
        {
            var path = args["path"]!.GetValue<string>();
            if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) throw new ToolException("Only Markdown notes can be read.");
            try
            {
                var text = Vault().Read(path);
                return ToolResult.Ok(text.Length > 12000 ? text[..12000] + "\n[note truncated]" : text);
            }
            catch (FileNotFoundException) { throw new ToolException($"There is no note at {path}."); }
            catch (PathGuardException e) { throw new ToolException(e.Message); }
        }, ct);
    }

    private sealed class WriteNote : ITool
    {
        public ToolDefinition Definition { get; } = new("notes_write",
            "Writes up a concept in the learner's vault, only when they ask for a note: a summary in their words, the key formula, what it builds on, a worked example and the mistake to watch. " +
            "If a note on the concept exists it is added to under a dated heading, never replaced. The learner sees the exact change and approves it.",
            Schema(new JsonObject
            {
                ["concept"] = Str("The concept's name, e.g. Torque.", 80),
                ["subject"] = Str("The subject, e.g. Physics.", 60),
                ["summary"] = Str("A short explanation, a few sentences.", 2000),
                ["formula"] = Str("The key formula as TeX, if there is one.", 300),
                ["builds_on"] = new JsonObject { ["type"] = "array", ["maxItems"] = 8, ["items"] = Str("A prerequisite concept.", 80) },
                ["example"] = Str("A short worked example.", 2000),
                ["mistake"] = Str("A mistake to watch for.", 400),
            }, "concept", "subject", "summary"),
            ToolRisk.Act);

        private static string Key(JsonObject args) => "notes:" + args.ToJsonString();

        private static IReadOnlyList<FileChange> Plan(JsonObject args)
        {
            var vault = Vault();
            string? Opt(string name) => args[name]?.GetValue<string>() is { Length: > 0 } s ? s : null;
            try
            {
                var note = vault.ProposeConceptNote(args["subject"]!.GetValue<string>(), args["concept"]!.GetValue<string>(), args["summary"]!.GetValue<string>(),
                    Opt("formula"), args["builds_on"] is JsonArray a ? a.Select(x => x?.GetValue<string>() ?? "") : [], Opt("mistake"), Opt("example"));
                var index = vault.ProposeIndex([note]);
                return index.After == index.Before ? [note] : [note, index];
            }
            catch (PathGuardException e) { throw new ToolException(e.Message); }
        }

        public Task<string?> PreviewAsync(JsonObject args, ToolTurn turn, CancellationToken ct) => Task.Run<string?>(() =>
        {
            var changes = Plan(args);
            turn.State[Key(args)] = changes;
            // The whole change, never a prefix: what is approved is exactly what is written (the card scrolls).
            return string.Join("\n\n", changes.Select(c => c.Summary + "\n" + c.Diff)) + "\n\nNothing else in the vault is touched.";
        }, ct);

        public void Declined(JsonObject args, ToolTurn turn) => turn.State.TryRemove(Key(args), out _);

        public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct) => Task.Run(() =>
        {
            var changes = turn.State.TryRemove(Key(args), out var planned) && planned is IReadOnlyList<FileChange> p ? p : throw new ToolException("This note change was not previewed, so nothing was written.");
            try { Vault().ApplyAll(changes); }
            catch (IOException e) { throw new ToolException(e.Message); }
            AppServices.Log.Info($"Note tool wrote {changes.Count} file(s).");
            return ToolResult.Ok("Saved: " + string.Join(", ", changes.Select(c => c.RelativePath)) + ".");
        }, ct);
    }
}
