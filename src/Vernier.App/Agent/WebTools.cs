using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Research;
using Vernier.Core.Tools;

namespace Vernier.App.Agent;

/// <summary>
/// Research on the web, run by Vernier itself so it works the same with every provider and the harness can count what was
/// read: a turn that had to be grounded (<see cref="ResearchPolicy"/>) is checked against the sources this tool found.
/// </summary>
public static class WebTools
{
    public static readonly string[] Names = ["web_research"];

    public static IEnumerable<ITool> All(WebResearcher researcher) => [new WebResearch(researcher)];

    private sealed class WebResearch(WebResearcher researcher) : ITool
    {
        public const int MaxCallsPerTurn = 4;

        public ToolDefinition Definition { get; } = new("web_research",
            $"Researches on the web: searches, then reads pages until several load with readable text (dead links and empty pages do not count), and returns the passages that matter with their addresses. " +
            $"Required before explaining a web page, software and its tools (e.g. what a Blender modifier or panel does in the current version), current facts, or anything you are not sure of; an answer that had to be researched needs at least {ResearchPolicy.MinSources} working sources. " +
            "Give a specific query: the app and version, the exact tool or term. Never put names, emails, numbers or other personal details from the screen in it. " +
            "Use what you read to get the facts right, then explain in your own words for this learner and what is on their screen; do not list or paraphrase the sources one by one. End the answer with a short Sources line.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["query"] = new JsonObject { ["type"] = "string", ["maxLength"] = QuerySanitizer.MaxLength, ["description"] = "What to search for, e.g. \"Blender 4.2 bevel modifier segments amount\"." },
                    ["why"] = new JsonObject { ["type"] = "string", ["maxLength"] = 120, ["description"] = "What you need to find out (shown to the learner)." },
                },
                ["required"] = new JsonArray("query"),
                ["additionalProperties"] = false,
            },
            ToolRisk.Observe, UntrustedOutput: true);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var ledger = (ResearchLedger)turn.State.GetOrAdd(ResearchLedger.Key, _ => new ResearchLedger());
            if (ledger.BeginCall() > MaxCallsPerTurn)
                return ToolResult.Fail($"That is {MaxCallsPerTurn} searches for this answer, the most allowed. Answer with what you have read ({ledger.Working} working source(s)) and say plainly what you could not confirm.");
            var query = QuerySanitizer.Clean(args["query"]?.GetValue<string>() ?? "");
            if (query.Length < 3) return ToolResult.Fail("Give a query of a few words (personal details are removed from queries, so a query of only those is empty).");

            var result = await researcher.ResearchAsync(query, ct).ConfigureAwait(false);
            foreach (var (page, _) in result.Sources) ledger.Add(page.Url.AbsoluteUri, page.Title);
            if (result.Sources.Count == 0)
                return ToolResult.Fail($"Researched \"{query}\": no working source ({result.Problem ?? "none of the pages loaded with readable text"}{(result.Dead.Count > 0 ? "; dead: " + string.Join(", ", result.Dead) : "")}). Try a different, more specific query.");

            var sb = new StringBuilder();
            sb.Append($"Researched \"{query}\": {result.Sources.Count} working source(s)");
            if (result.Dead.Count > 0) sb.Append($"; not counted: {string.Join(", ", result.Dead)}");
            sb.Append($". This answer has now read {ledger.Working} working source(s)");
            sb.AppendLine(ledger.Working < ResearchPolicy.MinSources ? $"; read at least {ResearchPolicy.MinSources} (search again with other words)." : ".");
            var n = 0;
            foreach (var (page, excerpt) in result.Sources)
                sb.AppendLine().AppendLine($"[{++n}] {(page.Title.Length > 0 ? page.Title : page.Url.Host)} — {page.Url.AbsoluteUri}").AppendLine(excerpt);
            return ToolResult.Ok(sb.ToString());
        }
    }
}
