using System.Globalization;
using System.Text;
using Vernier.Core.Security;

namespace Vernier.Core.Agents;

/// <summary>One stretch of time spent in one window.</summary>
public sealed record ActivitySpan(string App, string Title, DateTimeOffset Start, DateTimeOffset End)
{
    public TimeSpan Length => End - Start;
}

/// <summary>What is playing, as Windows' media session reports it. Position is extrapolated while playing.</summary>
public sealed record MediaState(string App, string Title, string? Artist, TimeSpan? Position, TimeSpan? Duration, bool Playing, DateTimeOffset ReadAt)
{
    public static string Clock(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : t.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// The learner's recent windows, kept in memory only, for "what was I doing". Private browsing,
/// password managers and Vernier itself are never recorded; Pause clears it.
/// </summary>
public sealed class ActivityTimeline(int maxSpans = 60, TimeSpan? keepFor = null)
{
    private static readonly string[] PrivateApps = ["1password", "bitwarden", "keepass", "keepassxc", "lastpass", "dashlane", "enpass", "credentialuibroker", "consent", "lockapp", "logonui"];
    public const string PrivateTitle = "(private window)";
    private static readonly string[] PrivateTitles = ["inprivate", "incognito", "private browsing", "private window"];
    private readonly LinkedList<ActivitySpan> _spans = new();
    private readonly object _gate = new();
    private readonly TimeSpan _keep = keepFor ?? TimeSpan.FromHours(2);

    /// <summary>Null when the window must not be recorded at all; otherwise the title to keep (masked for private windows).</summary>
    public static string? Recordable(string app, string title)
    {
        var a = app.ToLowerInvariant();
        if (a.Length == 0 || a == "vernier" || PrivateApps.Any(p => a.StartsWith(p, StringComparison.Ordinal))) return null;
        var t = title.ToLowerInvariant();
        return PrivateTitles.Any(t.Contains) ? PrivateTitle : title;
    }

    /// <summary>Called whenever the window in front or its title is sampled.</summary>
    public void Observe(string app, string title, DateTimeOffset now)
    {
        if (Recordable(app, title) is not { } kept) return;
        lock (_gate)
        {
            if (_spans.Last?.Value is { } last && last.App == app && last.Title == kept)
                _spans.Last.Value = last with { End = now };
            else
            {
                if (_spans.Last?.Value is { } previous) _spans.Last.Value = previous with { End = now };
                _spans.AddLast(new ActivitySpan(app, kept, now, now));
            }
            while (_spans.Count > maxSpans || (_spans.First is { } f && now - f.Value.End > _keep)) _spans.RemoveFirst();
        }
    }

    public IReadOnlyList<ActivitySpan> Recent { get { lock (_gate) return [.. _spans]; } }

    public void Clear() { lock (_gate) _spans.Clear(); }
}

/// <summary>
/// Everything Vernier knows about what the learner is doing right now, each fact with where it
/// came from and how old it is. Rendered for the model as untrusted context, never as instructions.
/// </summary>
public sealed record WorkContext(
    DateTimeOffset At,
    ActivitySpan? Now,
    IReadOnlyList<ActivitySpan> Earlier,
    MediaState? Media,
    string? PageAddress,
    string? Selection)
{
    public bool IsEmpty => Now is null && Media is null && PageAddress is null && Selection is null && Earlier.Count == 0;

    public string Render()
    {
        var sb = new StringBuilder();
        static string Age(TimeSpan t) => t.TotalSeconds < 90 ? $"{Math.Max(0, t.TotalSeconds):0} s" : t.TotalMinutes < 90 ? $"{t.TotalMinutes:0} min" : $"{t.TotalHours:0.#} h";
        if (Now is { } now) sb.AppendLine($"- In front: {now.App} — \"{now.Title}\" (for {Age(now.Length)}; window title)");
        if (PageAddress is { Length: > 0 } url) sb.AppendLine($"- Page address: {url} (browser address bar, read just now)");
        if (Media is { } m)
        {
            var where = m.Position is { } p ? $" at {MediaState.Clock(p)}" + (m.Duration is { } d && d > TimeSpan.Zero ? $" of {MediaState.Clock(d)}" : "") : "";
            sb.AppendLine($"- Media: \"{m.Title}\"{(string.IsNullOrWhiteSpace(m.Artist) ? "" : " by " + m.Artist)} in {m.App}{where}, {(m.Playing ? "playing" : "paused")} (Windows media session, {Age(At - m.ReadAt)} ago)");
        }
        if (Selection is { Length: > 0 } sel) sb.AppendLine($"- Selected text: \"{(sel.Length > 600 ? sel[..600] + "…" : sel)}\" (selection in the window in front)");
        if (Earlier.Count > 0)
            sb.AppendLine("- Earlier: " + string.Join("; ", Earlier.TakeLast(8).Reverse().Select(e => $"{e.App} — \"{(e.Title.Length > 70 ? e.Title[..70] + "…" : e.Title)}\" ({Age(e.Length)}, {Age(At - e.End)} ago)")));
        return sb.Length == 0 ? "" : Untrusted.Wrap("activity", sb.ToString().TrimEnd(), 4000);
    }
}
