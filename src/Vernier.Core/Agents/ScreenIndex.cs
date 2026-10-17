using System.Globalization;
using System.Text;
using Vernier.Core.Geometry;

namespace Vernier.Core.Agents;

public enum ScreenItemKind { Line, Word, Element, Phrase }

/// <param name="Bounds">Physical pixels on the virtual desktop.</param>
/// <param name="Source">"ocr" or "accessibility": how Vernier knows where it is.</param>
public sealed record ScreenItem(string Id, string Text, ScreenItemKind Kind, PxRect Bounds, string Source, string? Role = null);

/// <summary>Where a mark goes and how Vernier found it, so the learner and the model can judge it.</summary>
public sealed record ResolvedTarget(PxRect Bounds, string Method, double Confidence, ScreenItem? Item);

/// <summary>Ids for one turn. Numbering continues across captures, so an id from an earlier look can never name something on a newer one.</summary>
public sealed class IdSequence
{
    private int _next;
    public string Next(char prefix) => prefix.ToString() + Interlocked.Increment(ref _next).ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Everything located on one capture, each with an id the model can name instead of guessing
/// pixels. Ids belong to this observation: a newer capture makes them invalid, so a mark is
/// never placed on a screen that has since changed.
/// </summary>
public sealed class ScreenIndex
{
    private readonly List<ScreenItem> _items;
    private readonly Dictionary<string, ScreenItem> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly IdSequence _ids;
    // Tool calls can arrive in parallel; matching adds phrase items. One lock guards the list and the map.
    private readonly object _gate = new();

    /// <param name="ids">The turn's id sequence; a new one per index when omitted (tests, one-off use).</param>
    public ScreenIndex(string observationId, CaptureGeometry geometry, IReadOnlyList<ExtractedText> layout, DateTimeOffset at, long window = 0, IdSequence? ids = null)
    {
        ObservationId = observationId;
        Geometry = geometry;
        At = at;
        Window = window;
        _ids = ids ?? new IdSequence();
        _items = [];
        foreach (var t in layout)
        {
            if (t.BoundsImage is not { IsEmpty: false } box || string.IsNullOrWhiteSpace(t.Text)) continue;
            var physical = geometry.ImageToPhysical(box);
            if (t.Provenance == "ocr")
            {
                var line = Add(new ScreenItem(_ids.Next('t'), t.Text.Trim(), ScreenItemKind.Line, physical, "ocr"));
                foreach (var w in t.Words ?? [])
                    if (w.BoundsImage is { IsEmpty: false } wb && !string.IsNullOrWhiteSpace(w.Text))
                        Add(new ScreenItem(_ids.Next('w'), w.Text.Trim(), ScreenItemKind.Word, geometry.ImageToPhysical(wb), "ocr", line.Id));
            }
            else Add(new ScreenItem(_ids.Next('e'), t.Text.Trim(), ScreenItemKind.Element, physical, "accessibility", t.Role));
        }
    }

    public string ObservationId { get; }
    public CaptureGeometry Geometry { get; }
    public DateTimeOffset At { get; }
    public long Window { get; }
    public IReadOnlyList<ScreenItem> Items { get { lock (_gate) return [.. _items]; } }

    private ScreenItem Add(ScreenItem item)
    {
        lock (_gate)
        {
            _items.Add(item);
            _byId[item.Id] = item;
        }
        return item;
    }

    public ScreenItem? Get(string id) { lock (_gate) return _byId.GetValueOrDefault(id.Trim()); }

    /// <summary>
    /// Places matching the text, best first. Exact element or line text, then a run of words in
    /// one line, then a line or element containing it, tolerating OCR's usual symbol confusions.
    /// A run of words becomes a new "phrase" item so the model can refer to it by id.
    /// </summary>
    public IReadOnlyList<(ScreenItem Item, double Score)> Find(string query, int max = 8)
    {
        var q = Normalise(query);
        if (q.Length == 0) return [];
        var qTokens = Tokens(query);
        var found = new List<(ScreenItem, double)>();
        foreach (var item in Items.Where(i => i.Kind is ScreenItemKind.Line or ScreenItemKind.Element)) // a copy: matching adds phrase items
        {
            var n = Normalise(item.Text);
            if (n == q) found.Add((item, item.Kind == ScreenItemKind.Element ? 1.0 : 0.98));
            else if (item.Kind == ScreenItemKind.Line && WordRun(item, qTokens) is { } run) found.Add((run, 0.95));
            else if (n.Contains(q, StringComparison.Ordinal) && q.Length >= 2) found.Add((item, 0.8 * Math.Min(1, (double)q.Length / Math.Max(1, n.Length) + 0.5)));
            else if (q.Length >= 5 && Near(n, q)) found.Add((item, 0.6));
        }
        return [.. found.OrderByDescending(f => f.Item2).ThenBy(f => f.Item1.Bounds.Width * f.Item1.Bounds.Height).Take(max)];
    }

    // The smallest run of words in a line that spells the query: "x^2" inside "f(x) = x^2 + 1" marks just that.
    private ScreenItem? WordRun(ScreenItem line, IReadOnlyList<string> qTokens)
    {
        if (qTokens.Count == 0) return null;
        var words = Items.Where(w => w.Kind == ScreenItemKind.Word && w.Role == line.Id).ToList();
        var joinedQuery = string.Concat(qTokens);
        for (var start = 0; start < words.Count; start++)
        {
            var acc = new StringBuilder();
            for (var end = start; end < words.Count && acc.Length < joinedQuery.Length + 4; end++)
            {
                acc.Append(Normalise(words[end].Text));
                if (acc.ToString() != joinedQuery) continue;
                var run = words.Skip(start).Take(end - start + 1).ToList();
                if (run.Count == 1 && end == start) return words[start];
                double l = run.Min(w => w.Bounds.X), t = run.Min(w => w.Bounds.Y), r = run.Max(w => w.Bounds.Right), b = run.Max(w => w.Bounds.Bottom);
                return Add(new ScreenItem(_ids.Next('p'), string.Join(" ", run.Select(w => w.Text)), ScreenItemKind.Phrase, new PxRect(l, t, r - l, b - t), "ocr", line.Id));
            }
        }
        return null;
    }

    /// <summary>
    /// Turns a model's target into a box. An id from this observation wins; then text (refused
    /// when it matches more than one place unless <paramref name="occurrence"/> picks one); then a
    /// pixel box in the screenshot, which is the model's estimate and is reported as such.
    /// </summary>
    public ResolvedTarget Resolve(string? id, string? text, int? occurrence, PxRect? imageBox)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (Get(id) is { } item) return new ResolvedTarget(item.Bounds, item.Source == "ocr" ? "text found by OCR" : "accessibility element", item.Source == "ocr" ? 0.9 : 0.97, item);
            throw new Tools.ToolException($"'{id}' is not an id on the current screen (observation {ObservationId}). Ids from an earlier look are no longer valid; use find_on_screen or look first.");
        }
        if (!string.IsNullOrWhiteSpace(text))
        {
            var matches = Find(text);
            if (matches.Count == 0)
                throw new Tools.ToolException($"No text matching \"{Trim(text)}\" was found on the current screen. Try a shorter or different part of it, use find_on_screen, or give a box in screenshot pixels.");
            var best = matches.Where(m => m.Score >= matches[0].Score - 0.001).ToList();
            ScreenItem chosen;
            if (occurrence is { } k)
            {
                if (k < 1 || k > matches.Count) throw new Tools.ToolException($"occurrence {k} is out of range: \"{Trim(text)}\" matched {matches.Count} place(s).");
                chosen = matches[k - 1].Item;
            }
            else if (best.Count > 1 && !SamePlace(best))
                throw new Tools.ToolException($"\"{Trim(text)}\" matches {best.Count} places. Pick one by id: " +
                    string.Join("; ", best.Take(6).Select(m => $"{m.Item.Id} \"{Trim(m.Item.Text)}\" at {Box(m.Item.Bounds)}")));
            else chosen = best[0].Item;
            var score = matches.First(m => m.Item == chosen).Score;
            return new ResolvedTarget(chosen.Bounds, chosen.Source == "ocr" ? "text found by OCR" : "accessibility element", Math.Min(0.95, score), chosen);
        }
        if (imageBox is { } b)
        {
            var clipped = b.Intersect(Geometry.ImageBounds);
            if (clipped.IsEmpty) throw new Tools.ToolException($"The box {Box(b)} is outside the screenshot ({Geometry.ImageWidth}x{Geometry.ImageHeight}).");
            if (clipped.Width * clipped.Height > 0.6 * Geometry.ImageWidth * Geometry.ImageHeight)
                throw new Tools.ToolException("That box covers most of the screen, which points at nothing in particular. Mark the specific thing.");
            return new ResolvedTarget(Geometry.ImageToPhysical(clipped), "estimated from the screenshot", 0.45, null);
        }
        throw new Tools.ToolException("Give a target: an id from the screen list, the text to mark, or a box in screenshot pixels.");
    }

    /// <summary>A compact list for the prompt: lines and controls with ids and screenshot-pixel boxes, the most relevant first.</summary>
    public string Describe(int maxItems = 120, PxRect? focusPhysical = null)
    {
        var listed = Items.Where(i => i.Kind is ScreenItemKind.Line or ScreenItemKind.Element).ToList();
        if (focusPhysical is { } f)
        {
            var c = f.Center;
            listed = [.. listed.OrderBy(i => i.Bounds.Intersect(f).IsEmpty ? 1 : 0).ThenBy(i => Distance(i.Bounds.Center, c))];
        }
        else listed = [.. listed.OrderBy(i => Math.Round(i.Bounds.Y / 12)).ThenBy(i => i.Bounds.X)];
        var sb = new StringBuilder();
        foreach (var i in listed.Take(maxItems))
            sb.Append(i.Id).Append(' ').Append(i.Kind == ScreenItemKind.Element ? $"[{i.Role ?? "control"}] " : "")
              .Append('"').Append(Trim(i.Text, 90)).Append("\" ").Append(Box(Image(i.Bounds))).Append('\n');
        if (listed.Count > maxItems) sb.Append($"(+{listed.Count - maxItems} more; use find_on_screen)\n");
        return sb.ToString();
    }

    public PxRect Image(PxRect physical)
    {
        var tl = Geometry.PhysicalToImage(new PxPoint(physical.X, physical.Y));
        return new PxRect(tl.X, tl.Y, physical.Width / Geometry.ScaleX, physical.Height / Geometry.ScaleY);
    }

    public static string Box(PxRect r) => string.Create(CultureInfo.InvariantCulture, $"({r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0})");

    private static bool SamePlace(List<(ScreenItem Item, double Score)> best) =>
        best.All(m => !m.Item.Bounds.Intersect(best[0].Item.Bounds).IsEmpty); // an element and the OCR line of the same label

    private static double Distance(PxPoint a, PxPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static string Trim(string s, int max = 60) => s.Length <= max ? s : s[..max] + "…";

    /// <summary>Case, width, spacing and the symbols OCR and fonts disagree on (minus signs, ×, quotes, superscripts) are folded together.</summary>
    public static string Normalise(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            var c = ch switch
            {
                '−' or '–' or '—' or '‐' => '-',
                '×' or '·' or '∙' => '*',
                '’' or '‘' or '`' => '\'',
                '“' or '”' => '"',
                _ => ch,
            };
            if (!char.IsWhiteSpace(c)) sb.Append(c);
        }
        return sb.ToString().Replace("^", "");
    }

    private static List<string> Tokens(string s) =>
        [.. s.Split((char[])[' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(Normalise).Where(t => t.Length > 0)];

    // One edit per five characters: OCR reads "rn" for "m", "0" for "o".
    private static bool Near(string text, string query)
    {
        var allowed = Math.Max(1, query.Length / 5);
        for (var i = 0; i + query.Length - allowed <= text.Length; i++)
        {
            var window = text.Substring(i, Math.Min(query.Length, text.Length - i));
            if (Levenshtein(window, query) <= allowed) return true;
        }
        return false;
    }

    private static int Levenshtein(string a, string b)
    {
        var d = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) d[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            var prev = d[0];
            d[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var keep = d[j];
                d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), prev + (a[i - 1] == b[j - 1] ? 0 : 1));
                prev = keep;
            }
        }
        return d[b.Length];
    }
}
