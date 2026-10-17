using Vernier.Core.Agents;

namespace Vernier.Core.Context;

/// <summary>An offer of help worked out by code from what the learner is doing. It becomes a question only if they accept it.</summary>
public sealed record Suggestion(string Kind, string Title, string Text, string Question);

/// <summary>
/// Keeps suggestions rare: at most two an hour, fifteen minutes apart, and each kind the learner
/// waves away waits twice as long as before to come back (from twenty minutes up to a day).
/// Accepting one resets its kind.
/// </summary>
public sealed class SuggestionPolicy
{
    public const int PerHour = 2;
    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan BaseCooldown = TimeSpan.FromMinutes(20), MaxCooldown = TimeSpan.FromDays(1);

    private readonly List<DateTimeOffset> _shown = [];
    private readonly Dictionary<string, (int Dismissals, DateTimeOffset Until)> _kinds = [];

    /// <summary>True (and counted) when a suggestion of this kind may be shown now.</summary>
    public bool TryOffer(string kind, DateTimeOffset now)
    {
        _shown.RemoveAll(t => now - t >= TimeSpan.FromHours(1));
        if (_shown.Count >= PerHour || (_shown.Count > 0 && now - _shown[^1] < MinGap)) return false;
        if (_kinds.TryGetValue(kind, out var k) && now < k.Until) return false;
        _shown.Add(now);
        return true;
    }

    public void Dismissed(string kind, DateTimeOffset now)
    {
        var dismissals = (_kinds.TryGetValue(kind, out var k) ? k.Dismissals : 0) + 1;
        var wait = TimeSpan.FromTicks(Math.Min(MaxCooldown.Ticks, BaseCooldown.Ticks * (1L << Math.Min(dismissals - 1, 20))));
        _kinds[kind] = (dismissals, now + wait);
    }

    public void Accepted(string kind) => _kinds.Remove(kind);
}

/// <summary>Going back to the same part of a video twice within five minutes reads as "this part is hard".</summary>
public sealed class RewindWatch
{
    public static readonly TimeSpan Span = TimeSpan.FromMinutes(5), SamePart = TimeSpan.FromSeconds(45);
    private readonly List<(string Title, TimeSpan To, DateTimeOffset At)> _recent = [];

    public Suggestion? Rewound(string title, TimeSpan to, DateTimeOffset now)
    {
        _recent.RemoveAll(r => now - r.At > Span);
        _recent.Add((title, to, now));
        var same = _recent.Where(r => r.Title == title && (r.To - to).Duration() <= SamePart).ToList();
        if (same.Count < 2) return null;
        _recent.RemoveAll(r => r.Title == title); // one offer per stretch of rewinding
        var at = MediaState.Clock(TimeSpan.FromSeconds(Math.Floor(same.Min(r => r.To.TotalSeconds))));
        var name = title.Length > 80 ? title[..80] + "…" : title;
        return new Suggestion("rewind", "Stuck on this part?",
            $"You went back to {at} in “{name}” {same.Count} times. Click to ask about it; nothing is sent until you press Enter.",
            $"I keep going back to {at} in the video \"{name}\". Can you explain what happens in this part?");
    }
}
