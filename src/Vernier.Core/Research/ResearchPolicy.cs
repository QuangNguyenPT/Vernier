using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Vernier.Core.Agents;

namespace Vernier.Core.Research;

/// <summary>Whether this turn must be grounded in web sources before the tutor explains, and why (shown to the model).</summary>
public sealed record ResearchDemand(bool Required, string Reason)
{
    public static readonly ResearchDemand None = new(false, "");
}

/// <summary>
/// Decides in code, not by the model's judgement, when the tutor has to research: a web page is in front (its claims need
/// checking), the learner is in a creative or technical tool whose controls the model may misremember (Blender, CAD, an
/// editor), the app is one Vernier does not know, or the question asks how a tool, menu or setting works or for current facts.
/// Reading a worksheet, a PDF or a slide needs no research of its own.
/// </summary>
public static partial class ResearchPolicy
{
    /// <summary>Working sources (pages that loaded and had readable text) a required research step must reach.</summary>
    public const int MinSources = 3;

    // Windows' own shell and plain viewers: nothing about them needs looking up.
    private static readonly HashSet<string> Plain = new(["explorer", "notepad", "vernier", "applicationframehost", "searchhost", "shellexperiencehost", "photos", "mspdfreader"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ToolCategories =["3D", "CAD", "Imaging", "Vector", "Video", "Motion", "Lab data", "Numerics", "Programming"];

    [GeneratedRegex(@"\b(how (do|does|can|to|should)|what does|what is the (tool|button|option|setting|shortcut)|where (is|do i find)|shortcut|hotkey|menu|toolbar|panel|modifier|setting|option|button|plugin|add-?on|feature|latest|current|newest|version|release|news|today)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HowOrCurrent();

    public static ResearchDemand Decide(AppProfile? app, string? processName, string question)
    {
        if (app is { Category: "Browser" }) return new(true, "a web page is on the screen: check what it says against other sources before explaining it");
        if (app is { } a && ToolCategories.Contains(a.Category)) return new(true, $"the learner is in {a.Name}: look up how the tools and controls on the screen work in the current version instead of relying on memory");
        if (app is null && !string.IsNullOrWhiteSpace(processName) && !Plain.Contains(processName)) return new(true, $"the app on the screen ({processName}) is not one Vernier knows: find out how it works before explaining it");
        if (HowOrCurrent().IsMatch(question)) return new(true, "the question asks how something works or about current facts");
        return ResearchDemand.None;
    }
}

/// <summary>What this turn's research found, kept in the turn's tool state: the gate counts it, the tool adds to it.</summary>
public sealed class ResearchLedger
{
    public const string Key = "research";
    private readonly ConcurrentDictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);
    private int _calls;

    /// <summary>Distinct pages that loaded and had readable text, across every research call this turn.</summary>
    public int Working => _sources.Count;
    public int Calls => _calls;
    public IReadOnlyCollection<string> Urls => [.. _sources.Keys];

    public int BeginCall() => Interlocked.Increment(ref _calls);
    public void Add(string url, string title) => _sources.TryAdd(url, title);
}

/// <summary>The backstop: a required research step that did not reach enough working sources gets the answer re-asked once.</summary>
public static class ResearchGate
{
    /// <returns>The instruction for the second attempt, or null when the answer may stand.</returns>
    public static string? Check(ResearchDemand demand, ResearchLedger? ledger, bool toolOffered)
    {
        if (!demand.Required || !toolOffered) return null;
        var working = ledger?.Working ?? 0;
        if (working >= ResearchPolicy.MinSources) return null;
        return $"Before this answer can be shown it has to be grounded: research was required because {demand.Reason}, and {(working == 0 ? "no sources were read" : $"only {working} working source(s) were read")}. " +
               $"Call web_research now (more specific queries if the first found little) until at least {ResearchPolicy.MinSources} working sources are read, then write the whole answer again. " +
               "Explain in your own words for this learner and what is on their screen; use the sources to get the facts right, do not copy or summarise them one by one. End with a short Sources line.";
    }
}

/// <summary>Takes what should not leave the computer out of a search query: email addresses, phone and card-like numbers, long tokens.</summary>
public static partial class QuerySanitizer
{
    public const int MaxLength = 140;

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")] private static partial Regex Email();
    [GeneratedRegex(@"(\+?\d[\d\s().-]{7,}\d)")] private static partial Regex LongNumber();
    [GeneratedRegex(@"\b[A-Za-z0-9_\-]{28,}\b")] private static partial Regex Token();
    [GeneratedRegex(@"https?://\S+")] private static partial Regex Url();
    [GeneratedRegex(@"\s+")] private static partial Regex Space();

    public static string Clean(string query)
    {
        var q = Url().Replace(query, m => Uri.TryCreate(m.Value, UriKind.Absolute, out var u) ? u.Host : "");
        q = Email().Replace(q, "");
        q = LongNumber().Replace(q, "");
        q = Token().Replace(q, "");
        q = Space().Replace(q, " ").Trim();
        return q.Length <= MaxLength ? q : q[..MaxLength].TrimEnd();
    }
}
