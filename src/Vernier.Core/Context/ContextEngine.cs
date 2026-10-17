using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Vernier.Core.Agents;
using Vernier.Core.Security;

namespace Vernier.Core.Context;

/// <summary>
/// Turns raw window titles and addresses into stable keys. "(515) Lecture 4 - YouTube - Brave" and
/// "Lecture 4 - YouTube - Brave" are the same video; "● notes.md - Visual Studio Code" and
/// "notes.md - Visual Studio Code" are the same file.
/// </summary>
public static partial class TitleNormaliser
{
    [GeneratedRegex(@"^\(\d+\+?\)\s*")] private static partial Regex Counter();               // "(515) " unread / notification counts
    [GeneratedRegex(@"^\d{1,3}%\s+")] private static partial Regex Progress();                 // "45% " downloads, renders
    [GeneratedRegex(@"^[●•*]\s*|\s*[●•*]$|\s+\*$|\*(?=\s+[-–—]\s)")] private static partial Regex Unsaved();    // unsaved markers
    [GeneratedRegex(@"\s+[-–—|]\s+(Google Chrome|Brave|Microsoft​? Edge|Microsoft Edge|Mozilla Firefox|Firefox|Opera|Vivaldi|Arc)(\s*\(.*\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex BrowserSuffix();
    [GeneratedRegex(@"\s+-\s+Profile\s+\d+$|\s+-\s+Personal$|\s+-\s+Work$", RegexOptions.IgnoreCase)] private static partial Regex ProfileSuffix();

    public static string Clean(string title)
    {
        var t = (title ?? "").Trim();
        for (var i = 0; i < 3; i++)
        {
            var before = t;
            t = Counter().Replace(t, "");
            t = Progress().Replace(t, "");
            t = Unsaved().Replace(t, "").Trim();
            t = ProfileSuffix().Replace(t, "");
            t = BrowserSuffix().Replace(t, "");
            if (t == before) break;
        }
        return t.Trim();
    }

    /// <summary>The key an episode is grouped by: the cleaned page address when there is one, else the app and the cleaned title.</summary>
    public static string Key(string app, string title, string? url)
    {
        if (UrlSanitiser.Clean(url) is { } u) return u;
        var a = app.ToLowerInvariant();
        // Terminals retitle themselves with every command and folder: one episode per terminal app.
        if (a is "windowsterminal" or "cmd" or "powershell" or "pwsh" or "conhost" or "wezterm-gui" or "alacritty") return a;
        return a + "|" + Clean(title).ToLowerInvariant();
    }
}

/// <summary>
/// Addresses are kept without what could identify a person or carry a secret: no user info, no
/// fragment, and no query parameters except the few that name the content (a YouTube video id).
/// </summary>
public static class UrlSanitiser
{
    private static readonly Dictionary<string, string[]> Keep = new(StringComparer.OrdinalIgnoreCase)
    {
        ["youtube.com"] = ["v", "list"], ["www.youtube.com"] = ["v", "list"], ["m.youtube.com"] = ["v", "list"],
        ["vimeo.com"] = [], ["github.com"] = [], ["en.wikipedia.org"] = [],
    };

    public static string? Clean(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var text = url.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text; // address bars often hide the scheme
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
        var host = uri.Host.ToLowerInvariant();
        if (host == "youtu.be") return $"https://www.youtube.com/watch?v={uri.AbsolutePath.Trim('/')}";
        var keep = Keep.GetValueOrDefault(host, []);
        var kept = new List<string>();
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = pair.Split('=')[0];
            if (keep.Contains(name, StringComparer.Ordinal)) kept.Add(pair);
        }
        var path = uri.AbsolutePath.TrimEnd('/');
        return $"{uri.Scheme}://{host}{path}{(kept.Count > 0 ? "?" + string.Join("&", kept) : "")}";
    }
}

/// <summary>A stretch of work on one thing (a video, a page, a file), possibly with gaps.</summary>
public sealed class Episode
{
    public long Id { get; set; }
    public required string App { get; init; }
    public required string Key { get; init; }
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; set; }
    /// <summary>Seconds actually spent: in front and not idle, or playing.</summary>
    public double ActiveSeconds { get; set; }
    public string? MediaTitle { get; set; }
    public TimeSpan? MediaPosition { get; set; }
    public TimeSpan? MediaDuration { get; set; }
    /// <summary>Where the learner jumped back from and to, most recent last.</summary>
    public List<(TimeSpan From, TimeSpan To)> Rewinds { get; } = [];
    public int Questions { get; set; }
    public string? LastQuestion { get; set; }
    public string? Summary { get; set; }
    // Read from the screen: kept in memory only, never written to disk.
    public HashSet<string> Entities { get; } = new(StringComparer.Ordinal);
    public List<string> RecentLines { get; } = [];
    public bool Dirty { get; set; } = true;

    public TimeSpan Length => End - Start;

    /// <summary>A private browsing window: its title is masked, and nothing else about it (address, screen text) is kept.</summary>
    public bool IsPrivate => Title == ActivityTimeline.PrivateTitle;

    /// <summary>A copy that the engine will not change afterwards, so a selection can be rendered on any thread.</summary>
    public Episode Copy()
    {
        var e = new Episode
        {
            Id = Id, App = App, Key = Key, Title = Title, Url = Url, Start = Start, End = End, ActiveSeconds = ActiveSeconds,
            MediaTitle = MediaTitle, MediaPosition = MediaPosition, MediaDuration = MediaDuration, Questions = Questions,
            LastQuestion = LastQuestion, Summary = Summary, Dirty = false,
        };
        e.Rewinds.AddRange(Rewinds);
        e.Entities.UnionWith(Entities);
        e.RecentLines.AddRange(RecentLines);
        return e;
    }
}

/// <summary>Where episodes are kept between runs. Screen text is never part of it. A test seam; the app uses <see cref="LearningEpisodeStore"/>.</summary>
public interface IEpisodeStore
{
    IReadOnlyList<Episode> LoadSince(DateTimeOffset since);
    void Save(Episode episode);
    void DeleteBefore(DateTimeOffset before);
    void DeleteSince(DateTimeOffset since);
}

public sealed class LearningEpisodeStore(Learning.LearningStore store) : IEpisodeStore
{
    public IReadOnlyList<Episode> LoadSince(DateTimeOffset since) => store.LoadEpisodesSince(since);
    public void Save(Episode episode) => store.SaveEpisode(episode);
    public void DeleteBefore(DateTimeOffset before) => store.DeleteEpisodes(before: before);
    public void DeleteSince(DateTimeOffset since) => store.DeleteEpisodes(since: since);
}

public sealed record ContextOptions
{
    /// <summary>A window must stay in front this long to count, so alt-tabbing past it does not make an episode.</summary>
    public TimeSpan MinimumDwell { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Returning to the same thing within this gap continues its episode.</summary>
    public TimeSpan Rejoin { get; init; } = TimeSpan.FromMinutes(20);
    /// <summary>No input for this long (and nothing playing) stops time counting.</summary>
    public TimeSpan Idle { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(14);
    public int MaxEpisodesInMemory { get; init; } = 400;
    public int MaxEntitiesPerEpisode { get; init; } = 40;
    public int MaxLinesPerEpisode { get; init; } = 30;
}

/// <summary>
/// The learner's work over time, built by code from what Vernier observes: which windows, pages and
/// videos, for how long, what was rewound, what was on screen, what was asked. A model is never
/// needed to build it; an optional one may add a summary. Thread-safe.
/// </summary>
public sealed partial class ContextEngine
{
    private readonly object _gate = new();
    private readonly ContextOptions _options;
    private readonly IEpisodeStore? _store;
    private readonly List<Episode> _episodes = [];
    // Everything touched since this run began, with what each episode already held, so the session's own time is not mixed with earlier days.
    private sealed record SessionItem(double Seconds0, int Rewinds0, int Questions0, DateTimeOffset FirstSeen);
    private readonly Dictionary<Episode, SessionItem> _session = new(ReferenceEqualityComparer.Instance);
    public DateTimeOffset SessionStart { get; }
    private (string App, string Title, string? Url, string Key, DateTimeOffset Since)? _pending;
    private Episode? _current;
    private Episode? _media;
    private DateTimeOffset _lastTick;

    public ContextEngine(ContextOptions? options = null, IEpisodeStore? store = null, DateTimeOffset? now = null)
    {
        _options = options ?? new ContextOptions();
        _store = store;
        var t = now ?? DateTimeOffset.UtcNow;
        _lastTick = t;
        SessionStart = t;
        if (_store is null) return;
        _store.DeleteBefore(t - _options.Retention);
        _episodes.AddRange(_store.LoadSince(t - _options.Retention).OrderBy(e => e.Start));
        foreach (var e in _episodes) e.Dirty = false;
    }

    public Episode? Current { get { lock (_gate) return _current; } }
    public IReadOnlyList<Episode> Episodes { get { lock (_gate) return [.. _episodes]; } }

    /// <summary>The window in front, sampled or from a focus event. Private windows were filtered by the caller.</summary>
    public void Focus(string app, string title, string? url, DateTimeOffset now)
    {
        if (ActivityTimeline.Recordable(app, title) is not { } kept) { Leave(); return; }
        if (kept == ActivityTimeline.PrivateTitle) url = null; // a private window's address is as private as its title
        var key = TitleNormaliser.Key(app, kept, url);
        lock (_gate)
        {
            if (_current is { } c && c.Key == key)
            {
                c.Title = TitleNormaliser.Clean(kept);
                c.Url = UrlSanitiser.Clean(url) ?? c.Url;
                _pending = null;
                return;
            }
            if (_pending is not { } p || p.Key != key) { _pending = (app, kept, url, key, now); return; }
            if (now - p.Since < _options.MinimumDwell) return;
            var ended = _current;
            _current = Join(p.App, p.Title, p.Url, p.Key, p.Since);
            _pending = null;
            if (ended is not null && ended != _current) Ended = ended;
        }
        if (Ended is { } done) { Ended = null; EpisodeEnded?.Invoke(done); }
    }

    /// <summary>The window in front must not be recorded (an excluded app): nothing is current until a recordable one is.</summary>
    public void Leave() { lock (_gate) { _pending = null; _current = null; } }

    private Episode? Ended { get; set; }

    /// <summary>The learner moved on from an episode (raised outside the lock). An optional summariser listens.</summary>
    public event Action<Episode>? EpisodeEnded;

    /// <summary>A short line describing what the episode was about, from an optional local model.</summary>
    public void Summarise(Episode episode, string summary)
    {
        var line = summary.ReplaceLineEndings(" ").Trim();
        if (line.Length == 0) return;
        lock (_gate)
        {
            episode.Summary = line.Length > 200 ? line[..200] + "…" : line;
            episode.Dirty = true;
        }
    }

    // Continue a recent episode about the same thing, or start a new one.
    private Episode Join(string app, string title, string? url, string key, DateTimeOffset since)
    {
        var recent = _episodes.LastOrDefault(e => e.Key == key && since - e.End <= _options.Rejoin);
        if (recent is not null) { recent.End = since; recent.Dirty = true; return Touch(recent, since); }
        var e = new Episode { App = app, Key = key, Title = TitleNormaliser.Clean(title), Url = UrlSanitiser.Clean(url), Start = since, End = since };
        _episodes.Add(e);
        // The oldest go first, but not what this run is still counting in its session view.
        while (_episodes.Count > _options.MaxEpisodesInMemory && _episodes.FindIndex(x => !_session.ContainsKey(x)) is var old and >= 0) _episodes.RemoveAt(old);
        return Touch(e, since);
    }

    private Episode Touch(Episode e, DateTimeOffset now)
    {
        if (!_session.ContainsKey(e)) _session[e] = new SessionItem(e.ActiveSeconds, e.Rewinds.Count, e.Questions, now);
        return e;
    }

    /// <summary>
    /// The whole live session in a few lines: what the learner has spent this run on, longest first, with rewinds and
    /// questions. Built from the session's own time, so an episode continued from yesterday does not claim yesterday's minutes.
    /// </summary>
    public string? SessionLine(DateTimeOffset now, int maxItems = 6)
    {
        lock (_gate)
        {
            var items = _session.Select(kv => (E: kv.Key, S: kv.Value, Secs: kv.Key.ActiveSeconds - kv.Value.Seconds0,
                    Rew: kv.Key.Rewinds.Count - kv.Value.Rewinds0, Q: kv.Key.Questions - kv.Value.Questions0))
                .Where(x => x.Secs >= 20 || x.Q > 0 || x.Rew > 0).OrderByDescending(x => x.Secs).ThenBy(x => x.S.FirstSeen).ToList();
            if (items.Count == 0) return null;
            string Mins(double s) => s < 90 ? $"{s:0} s" : $"{s / 60:0} min";
            var parts = items.Take(maxItems).Select(x =>
            {
                var title = x.E.MediaTitle is { } m ? "video \"" + m + "\"" : '"' + x.E.Title + '"';
                if (title.Length > 56) title = title[..55] + "…\"";
                var extra = new List<string>();
                if (x.Rew > 0) extra.Add($"rewound {x.Rew}×");
                if (x.Q > 0) extra.Add($"asked {x.Q}×");
                return $"{x.E.App} {title} {Mins(x.Secs)}" + (extra.Count > 0 ? " (" + string.Join(", ", extra) + ")" : "");
            });
            var more = items.Count - maxItems;
            var total = items.Sum(x => x.Secs);
            return string.Create(CultureInfo.InvariantCulture, $"since {SessionStart.ToLocalTime():HH:mm} ({Math.Max(0, (now - SessionStart).TotalMinutes):0} min ago; {Mins(total)} of work): ")
                + string.Join("; ", parts) + (more > 0 ? $"; +{more} smaller" : "");
        }
    }

    /// <summary>Called about once a second: accrues time, closes idle stretches.</summary>
    public void Tick(DateTimeOffset now, TimeSpan sinceInput, bool mediaPlaying)
    {
        lock (_gate)
        {
            var dt = Math.Clamp((now - _lastTick).TotalSeconds, 0, 5); // a sleep or a clock change does not count as work
            _lastTick = now;
            var active = sinceInput < _options.Idle;
            if (_current is { } c && active) { c.ActiveSeconds += dt; c.End = now; c.Dirty = true; }
            if (_media is { } m && mediaPlaying && m != _current) { m.ActiveSeconds += dt; m.End = now; m.Dirty = true; }
        }
    }

    /// <summary>What Windows says is playing. Attaches to the episode about the same item and notices rewinds.</summary>
    public void Media(MediaState? state, DateTimeOffset now)
    {
        if (state is null) return;
        lock (_gate)
        {
            var target = _current is { } c && (c.MediaTitle == state.Title || c.Title.Contains(state.Title, StringComparison.OrdinalIgnoreCase)) ? c
                : _episodes.LastOrDefault(e => e.MediaTitle == state.Title && now - e.End <= _options.Rejoin)
                ?? Join(state.App, state.Title, null, "media|" + state.Title.ToLowerInvariant(), now);
            Touch(target, now);
            if (target.MediaPosition is { } before && state.Position is { } after && target.MediaTitle == state.Title && after < before - TimeSpan.FromSeconds(4))
                target.Rewinds.Add((before, after));
            target.MediaTitle = state.Title;
            target.MediaPosition = state.Position ?? target.MediaPosition;
            target.MediaDuration = state.Duration ?? target.MediaDuration;
            target.End = now;
            target.Dirty = true;
            _media = state.Playing ? target : null;
        }
    }

    [GeneratedRegex(@"(?<![\w])[A-Za-z][\w']{2,}(?:\s+[A-Z][\w']+){0,3}")] private static partial Regex Phrase();
    [GeneratedRegex(@"[=≈≤≥∫∑√∂^]|\\frac|\bd[xyt]\b")] private static partial Regex MathSign();

    /// <summary>Text read from the window in front (local OCR). Lines with maths and headings become entities; nothing is stored on disk.</summary>
    public void ScreenText(IReadOnlyList<string> lines, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_current is not { } c || c.IsPrivate) return;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length < 3 || line.Length > 200 || Untrusted.LooksLikeInjection(line)) continue;
                if (MathSign().IsMatch(line) && c.Entities.Count < _options.MaxEntitiesPerEpisode) c.Entities.Add(line);
                if (!c.RecentLines.Contains(line)) c.RecentLines.Add(line);
            }
            while (c.RecentLines.Count > _options.MaxLinesPerEpisode) c.RecentLines.RemoveAt(0);
        }
    }

    public void Asked(string question, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_current is not { } c) return;
            c.Questions++;
            c.LastQuestion = question.Length > 120 ? question[..120] + "…" : question;
            c.Dirty = true;
        }
    }

    /// <summary>Writes changed episodes to the store. Call every minute and at exit.</summary>
    public int Flush()
    {
        if (_store is null) return 0;
        List<Episode> dirty;
        lock (_gate) dirty = [.. _episodes.Where(e => e.Dirty && e.ActiveSeconds >= 5)];
        foreach (var e in dirty) { _store.Save(e); lock (_gate) e.Dirty = false; }
        return dirty.Count;
    }

    /// <summary>"Forget the last hour" / "forget today" / pause clearing: removes episodes from memory and the store.</summary>
    public void ForgetSince(DateTimeOffset since)
    {
        lock (_gate)
        {
            _episodes.RemoveAll(e => e.End >= since);
            foreach (var e in _session.Keys.Where(e => e.End >= since).ToList()) _session.Remove(e);
            if (_current?.End >= since) _current = null;
            if (_media?.End >= since) _media = null;
            _pending = null;
        }
        _store?.DeleteSince(since);
    }

    private static readonly string[] Stop = ["the", "and", "what", "this", "that", "with", "from", "how", "why", "does", "about", "explain", "mean", "means", "can", "you", "for", "are", "was", "is", "it", "my", "me", "of", "to", "in", "on", "a", "an"];

    private static HashSet<string> Terms(string text) =>
        [.. Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(t => t.Length > 2 && !Stop.Contains(t))];

    /// <summary>
    /// The context for one question: the current episode, what is playing, and earlier episodes
    /// ranked by how recent they are, how much they share with the question, and what the question
    /// refers to ("the video", "earlier", "yesterday").
    /// </summary>
    public ContextSelection Select(string question, DateTimeOffset now, int max = 5)
    {
        lock (_gate)
        {
            var q = Terms(question);
            var lower = question.ToLowerInvariant();
            bool wantsMedia = lower.Contains("video") || lower.Contains("lecture") || lower.Contains("watch"),
                 wantsPast = lower.Contains("earlier") || lower.Contains("before") || lower.Contains("yesterday") || lower.Contains("last time");
            double Score(Episode e)
            {
                var age = Math.Max(0, (now - e.End).TotalHours);
                var recency = Math.Exp(-age / (wantsPast ? 24 : 3));
                var words = Terms(e.Title + " " + string.Join(" ", e.Entities) + " " + (e.MediaTitle ?? ""));
                var overlap = q.Count == 0 ? 0 : (double)q.Count(words.Contains) / q.Count;
                var media = wantsMedia && e.MediaTitle is not null ? 0.6 : 0;
                var weight = Math.Min(1, e.ActiveSeconds / 300); // a few minutes of real use outranks a glance
                return recency * (0.4 + 0.6 * weight) + 2 * overlap + media;
            }
            var earlier = _episodes.Where(e => e != _current && e.ActiveSeconds >= 10)
                .Select(e => (e, s: Score(e))).Where(x => x.s > 0.15).OrderByDescending(x => x.s).Take(max).Select(x => x.e).ToList();
            // Copies: the engine keeps changing its episodes (screen text, ticks) on other threads while the selection is rendered.
            var current = _current?.Copy();
            var playing = _media is null ? null : _media == _current ? current : _media.Copy();
            return new ContextSelection(now, current, playing, [.. earlier.Select(e => e.Copy())], SessionLine(now));
        }
    }
}

/// <summary>What one question gets. The learner sees it in the ask bar and can drop any part before sending.</summary>
public sealed record ContextSelection(DateTimeOffset At, Episode? Current, Episode? Playing, IReadOnlyList<Episode> Earlier, string? Session = null)
{
    public bool IsEmpty => Current is null && Playing is null && Earlier.Count == 0 && Session is null;

    private static string Age(TimeSpan t) => t.TotalSeconds < 90 ? $"{Math.Max(0, t.TotalSeconds):0} s" : t.TotalMinutes < 90 ? $"{t.TotalMinutes:0} min" : t.TotalHours < 36 ? $"{t.TotalHours:0.#} h" : $"{t.TotalDays:0} d";
    private static string Clock(TimeSpan t) => MediaState.Clock(t);

    private string Describe(Episode e, bool now)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{e.App} — \"{e.Title}\"");
        if (e.Url is { } u) sb.Append(" <").Append(u).Append('>');
        sb.Append(now
            ? string.Create(CultureInfo.InvariantCulture, $" (in front, {Age(TimeSpan.FromSeconds(e.ActiveSeconds))} of use)")
            : string.Create(CultureInfo.InvariantCulture, $" ({Age(TimeSpan.FromSeconds(e.ActiveSeconds))} of use, {Age(At - e.End)} ago)"));
        if (e.MediaTitle is { } m)
        {
            sb.Append(CultureInfo.InvariantCulture, $"; video \"{m}\"");
            if (e.MediaPosition is { } p) sb.Append(" at ").Append(Clock(p)).Append(e.MediaDuration is { } d && d > TimeSpan.Zero ? " of " + Clock(d) : "");
            if (e.Rewinds.Count > 0) sb.Append(CultureInfo.InvariantCulture, $"; rewound {e.Rewinds.Count}× (last {Clock(e.Rewinds[^1].From)} → {Clock(e.Rewinds[^1].To)})");
        }
        if (e.Questions > 0) sb.Append(CultureInfo.InvariantCulture, $"; asked {e.Questions}× (last: \"{e.LastQuestion}\")");
        if (e.Summary is { } s) sb.Append("; summary: ").Append(s);
        if (now && e.Entities.Count > 0) sb.Append("; on screen: ").Append(string.Join(" | ", e.Entities.Take(8)));
        return sb.ToString();
    }

    /// <summary>For the model: fenced as untrusted, each line saying where it came from and how old it is.</summary>
    public string Render()
    {
        var sb = new StringBuilder();
        if (Current is { } c) sb.AppendLine("- Now: " + Describe(c, true));
        if (Playing is { } p && p != Current) sb.AppendLine("- Playing: " + Describe(p, false));
        foreach (var e in Earlier) sb.AppendLine("- Earlier: " + Describe(e, false));
        if (Session is not null) sb.AppendLine("- Session: " + Session);
        return sb.Length == 0 ? "" : Untrusted.Wrap("context", sb.ToString().TrimEnd(), 6000);
    }

    /// <summary>A one-line summary for the ask bar: what will go with the question.</summary>
    public string Chip()
    {
        var parts = new List<string>();
        if (Current is { } c) parts.Add(c.Title.Length > 40 ? c.Title[..40] + "…" : c.Title);
        if ((Playing ?? Current) is { MediaPosition: { } pos }) parts.Add("video " + Clock(pos));
        if (Earlier.Count > 0) parts.Add($"+{Earlier.Count} earlier");
        if (Session is not null) parts.Add("whole session");
        return string.Join(" · ", parts);
    }

    public ContextSelection WithoutEarlier() => this with { Earlier = [], Session = null };

    /// <summary>Without the text read from the screen: for when screen capture is off, paused or in Share-Safe, so what was read before does not go out.</summary>
    public ContextSelection WithoutScreenText()
    {
        if (Current is not { } c || (c.Entities.Count == 0 && c.RecentLines.Count == 0)) return this;
        var bare = c.Copy();
        bare.Entities.Clear();
        bare.RecentLines.Clear();
        return this with { Current = bare, Playing = ReferenceEquals(Playing, c) ? bare : Playing };
    }
}
