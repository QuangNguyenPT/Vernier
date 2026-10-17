using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vernier.Core.Agents;

/// <summary>What one finished turn left in the session: enough to remember what was asked, about what, and what came of it.</summary>
public sealed record TurnNote(
    DateTimeOffset At, string Where, string Question, string? Concept, string Gist,
    int ChecksPassed, int ChecksFailed, int Marks, string? Misconception, string? OpenQuestion);

/// <summary>
/// The whole live conversation, kept by code. The tutor is sent the most recent turns word for word
/// (<see cref="MasteryTutor.HistoryTurns"/>); everything before them is rendered here, one line per
/// turn, and once that no longer fits the budget the oldest turns fold into a summary line that keeps
/// what matters most: the concepts covered, what was asked, failed checks, misconceptions and open
/// questions. Nothing is dropped silently and no model is needed to write it.
/// </summary>
public sealed partial class SessionMemory
{
    public const int Budget = 4500;
    /// <summary>A guard against a session left open for days; far beyond what a person asks in one sitting. Past it the very oldest turn goes.</summary>
    public const int MaxTurns = 5000;

    private readonly List<TurnNote> _turns = [];
    private readonly object _gate = new();

    public int Count { get { lock (_gate) return _turns.Count; } }

    public void Add(TurnNote note)
    {
        lock (_gate)
        {
            _turns.Add(note);
            if (_turns.Count > MaxTurns) _turns.RemoveAt(0);
        }
    }

    public void Clear() { lock (_gate) _turns.Clear(); }

    [GeneratedRegex(@"```.*?```", RegexOptions.Singleline)] private static partial Regex Fence();
    [GeneratedRegex(@"\\[\(\)\[\]]|[*`#]")] private static partial Regex Markup();
    [GeneratedRegex(@"(?<=[.?!])\s")] private static partial Regex SentenceEnd();

    /// <summary>The first sentence of an answer, without markup: what the tutor said, in one line.</summary>
    public static string Gist(string answer)
    {
        var text = Markup().Replace(Fence().Replace(answer ?? "", " "), "").Trim();
        text = Regex.Replace(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "", @"\s+", " ").Trim();
        var first = SentenceEnd().Split(text, 2)[0];
        return Cut(first.Length >= 12 ? first : text, 150);
    }

    private static string Cut(string s, int max)
    {
        s = s.ReplaceLineEndings(" ").Trim();
        return s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
    }

    private static string Clock(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Line(int number, TurnNote n)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"#{number} {Clock(n.At)} [{Cut(n.Where, 50)}] asked: \"{Cut(n.Question, 120)}\"");
        sb.Append(" → ");
        if (n.Concept is { Length: > 0 }) sb.Append(Cut(n.Concept, 40)).Append(": ");
        sb.Append(n.Gist);
        var extras = new List<string>();
        if (n.ChecksFailed > 0) extras.Add($"{n.ChecksFailed} check(s) failed");
        else if (n.ChecksPassed > 0) extras.Add($"{n.ChecksPassed} check(s) passed");
        if (n.Marks > 0) extras.Add($"{n.Marks} mark(s) drawn");
        if (n.Misconception is { Length: > 0 }) extras.Add("misconception: " + Cut(n.Misconception, 80));
        if (n.OpenQuestion is { Length: > 0 }) extras.Add("left open: " + Cut(n.OpenQuestion, 80));
        if (extras.Count > 0) sb.Append(" (").Append(string.Join("; ", extras)).Append(')');
        return sb.ToString();
    }

    // The oldest turns, folded: what was covered and what is still unresolved.
    private static string Fold(int firstNumber, IReadOnlyList<TurnNote> turns)
    {
        var concepts = turns.Where(t => !string.IsNullOrWhiteSpace(t.Concept)).GroupBy(t => t.Concept!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Take(6)
            .Select(g => g.Count() > 1 ? $"{Cut(g.Key, 30)} ×{g.Count()}" : Cut(g.Key, 30));
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"#{firstNumber}–#{firstNumber + turns.Count - 1} ({Clock(turns[0].At)}–{Clock(turns[^1].At)}, folded):");
        var covered = string.Join(", ", concepts);
        if (covered.Length > 0) sb.Append(" concepts ").Append(covered).Append(';');
        sb.Append(" asked e.g. ").Append(string.Join(" / ", turns.Where(t => t.Question.Length > 0).Take(4).Select(t => '"' + Cut(t.Question, 60) + '"'))).Append(';');
        var failed = turns.Sum(t => t.ChecksFailed);
        if (failed > 0) sb.Append(CultureInfo.InvariantCulture, $" {failed} check(s) failed;");
        var mis = turns.Select(t => t.Misconception).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
        if (mis.Count > 0) sb.Append(" misconceptions: ").Append(string.Join(" / ", mis.Select(m => Cut(m!, 70)))).Append(';');
        var open = turns.Select(t => t.OpenQuestion).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
        if (open.Count > 0) sb.Append(" left open: ").Append(string.Join(" / ", open.Select(o => Cut(o!, 70)))).Append(';');
        return Cut(sb.ToString().TrimEnd(';'), Budget / 2);
    }

    /// <summary>
    /// Earlier turns that match a query, best first, each in full (not folded): for when the digest has folded the turn
    /// the tutor needs. Word overlap with the question, the answer's first sentence, the concept and the screen it was about.
    /// </summary>
    public string Recall(string query, int max = 6)
    {
        var terms = Regex.Split(query.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length >= 3).Distinct().ToArray();
        TurnNote[] all;
        lock (_gate) all = [.. _turns];
        if (all.Length == 0) return "No earlier turns in this session.";
        var scored = all.Select((n, i) => (n, i, score: terms.Length == 0 ? 0 : terms.Count(t =>
                (n.Question + " " + n.Concept + " " + n.Gist + " " + n.Where + " " + n.Misconception + " " + n.OpenQuestion).Contains(t, StringComparison.OrdinalIgnoreCase))))
            .Where(x => terms.Length == 0 || x.score > 0).OrderByDescending(x => x.score).ThenByDescending(x => x.i).Take(max).OrderBy(x => x.i).ToList();
        return scored.Count == 0 ? "No earlier turn matches that." : string.Join(Environment.NewLine, scored.Select(x => Line(x.i + 1, x.n)));
    }

    /// <summary>
    /// Every turn except the last <paramref name="skipLast"/> (those are sent in full), newest last, within
    /// <see cref="Budget"/> characters. Empty when nothing older than the recent turns exists.
    /// </summary>
    public string Render(int skipLast)
    {
        TurnNote[] older;
        lock (_gate) older = [.. _turns.Take(Math.Max(0, _turns.Count - skipLast))];
        if (older.Length == 0) return "";
        var lines = older.Select((n, i) => Line(i + 1, n)).ToArray();
        var total = lines.Sum(l => l.Length + 1);
        var folded = 0;
        string? summary = null;
        while (total > Budget && folded < older.Length - 1)
        {
            // Fold more of the oldest turns until what remains fits, always leaving the newest line in full.
            folded = Math.Min(older.Length - 1, folded + Math.Max(1, (total - Budget) / 140));
            summary = Fold(1, older.Take(folded).ToArray());
            total = summary.Length + 1 + lines.Skip(folded).Sum(l => l.Length + 1);
        }
        var sb = new StringBuilder();
        if (summary is not null) sb.AppendLine(summary);
        foreach (var l in lines.Skip(folded)) sb.AppendLine(l);
        return sb.ToString().TrimEnd();
    }
}
