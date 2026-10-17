using Vernier.App.Platform;
using Vernier.Core.Context;
using Vernier.Core.Settings;

namespace Vernier.App.Context;

/// <summary>
/// Local, code-only suggestions: when the learner seems stuck (for now, rewinding the same part of
/// a video), a tray notification offers help. Clicking it opens the ask bar with a question filled
/// in; nothing is sent until they press Enter. Rare by policy, quieter each time it is waved away,
/// and silent in Pause, Share-Safe, while an answer is coming, or with the setting off.
/// </summary>
public sealed class Proactive
{
    private readonly SuggestionPolicy _policy = new();
    private readonly RewindWatch _rewinds = new();
    private readonly TrayIcon _tray;
    private readonly Action<string> _ask;
    private Suggestion? _showing;
    private DateTimeOffset _shownAt;
    // Windows does not always report a notification that was closed or timed out; one this old counts as waved away.
    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(10);

    public Proactive(ContextHub hub, TrayIcon tray, Action<string> ask)
    {
        _tray = tray;
        _ask = ask;
        hub.Rewound += (media, _) => AppServices.OnUi(() =>
        {
            if (media.Position is { } at && _rewinds.Rewound(media.Title, at, DateTimeOffset.UtcNow) is { } s) Offer(s);
        });
        tray.NotificationClicked = () =>
        {
            if (_showing is not { } s) return;
            _showing = null;
            if (DateTimeOffset.UtcNow - _shownAt > Stale) { _policy.Dismissed(s.Kind, DateTimeOffset.UtcNow); return; } // an old toast from the action centre
            _policy.Accepted(s.Kind);
            _ask(s.Question);
        };
        tray.NotificationDismissed = () =>
        {
            if (_showing is not { } s) return;
            _showing = null;
            _policy.Dismissed(s.Kind, DateTimeOffset.UtcNow);
        };
    }

    private void Offer(Suggestion s)
    {
        var session = AppServices.Session;
        if (!AppServices.Settings.ProactiveSuggestions || session.IsPaused || session.IsBusy || AppServices.Policy.Mode == CapturePolicyMode.ShareSafe) return;
        var now = DateTimeOffset.UtcNow;
        if (_showing is { } unanswered) { _policy.Dismissed(unanswered.Kind, now); _showing = null; } // no click before the next offer: waved away
        if (!_policy.TryOffer(s.Kind, now)) return;
        _showing = s;
        _shownAt = now;
        _tray.Notify(s.Title, s.Text);
        AppServices.Log.Info($"Suggestion offered ({s.Kind}).");
    }
}
