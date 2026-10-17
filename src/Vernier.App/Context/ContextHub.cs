using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Automation;
using System.Windows.Threading;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Context;
using Vernier.Core.Geometry;
using Windows.Graphics.Imaging;
using Windows.Media.Control;
using Windows.Media.Ocr;

namespace Vernier.App.Context;

/// <summary>
/// Feeds the <see cref="ContextEngine"/> from what Windows reports, all the time the learner allows:
/// focus and title changes (out-of-context WinEvent hooks, nothing injected), idle time, the media
/// session, the browser address, and the text of the window in front once a change on it has
/// settled (local OCR, rate-limited, memory only). Nothing here sends anything anywhere.
/// </summary>
public sealed class ContextHub : IDisposable
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private readonly Func<bool> _allowed, _mayRead;
    private readonly Win32.WinEventProc _hookProc; // kept alive: the OS calls it after this method returns
    private readonly nint _focusHook, _nameHook;
    private GlobalSystemMediaTransportControlsSessionManager? _media;
    private MediaState? _lastMedia;
    private int _ticks;
    private nint _front;
    private string? _address;
    private nint _addressFor;
    private DateTimeOffset _addressAt;
    private readonly ScreenReader _reader;

    public ContextEngine Engine { get; }

    /// <summary>The media position jumped backwards while the same item was playing (the learner rewound).</summary>
    public event Action<MediaState, TimeSpan>? Rewound;
    /// <summary>Screen reading started or stopped (drives the "keeping context" indicator).</summary>
    public event Action<bool>? ReadingChanged;

    /// <param name="allowed">Context at all (metadata). Checked on every event.</param>
    /// <param name="mayRead">Reading screen text too (needs screen capture allowed, not Share-Safe, not paused).</param>
    public ContextHub(ContextEngine engine, Func<bool> allowed, Func<bool> mayRead)
    {
        Engine = engine;
        _allowed = allowed;
        _mayRead = mayRead;
        _reader = new ScreenReader(this);
        _hookProc = (_, ev, hwnd, idObject, idChild, _, _) =>
        {
            // The callback only notes what happened; the work is done on the next background pass.
            if (idObject != 0 || idChild != 0) return;
            if (ev == Win32.EVENT_SYSTEM_FOREGROUND || hwnd == _front) Sample();
        };
        const uint flags = Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS;
        _focusHook = Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, 0, _hookProc, 0, 0, flags);
        _nameHook = Win32.SetWinEventHook(Win32.EVENT_OBJECT_NAMECHANGE, Win32.EVENT_OBJECT_NAMECHANGE, 0, _hookProc, 0, 0, flags);
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
    }

    public static string? ProcessName(nint hwnd)
    {
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return null; }
    }

    private bool Excluded(string app) =>
        AppServices.Settings.ContextExcludedApps.Any(x => string.Equals(x, app, StringComparison.OrdinalIgnoreCase));

    // Foreground window and its title into the engine. Cheap: no UI Automation here.
    private void Sample()
    {
        if (!_allowed()) return;
        var front = Win32.GetAncestor(Win32.GetForegroundWindow(), Win32.GA_ROOT);
        if (front == 0 || ProcessName(front) is not { } app || app == "Vernier") return;
        // An excluded app in front: stop reading (the reader would otherwise OCR it over the previous window's area) and end the episode.
        if (Excluded(app)) { _front = 0; Engine.Leave(); return; }
        if (front != _front) { _front = front; _ = RefreshAddressAsync(front); }
        Engine.Focus(app, Win32.SafeTitle(front), _addressFor == front ? _address : null, DateTimeOffset.UtcNow);
    }

    private static readonly string[] Browsers = ["chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc"];

    private async Task RefreshAddressAsync(nint hwnd)
    {
        if (DateTimeOffset.UtcNow - _addressAt < TimeSpan.FromSeconds(2) && _addressFor == hwnd) return;
        if (!Browsers.Contains(ProcessName(hwnd)?.ToLowerInvariant() ?? "")) { _address = null; _addressFor = hwnd; return; }
        _addressAt = DateTimeOffset.UtcNow;
        var work = Task.Run(() => ReadAddress(hwnd));
        try { _address = await work.WaitAsync(TimeSpan.FromSeconds(1.5)); }
        catch (TimeoutException) { _address = null; } // a slow accessibility tree is not waited for
        _addressFor = hwnd;
    }

    private async Task TickAsync()
    {
        if (!_allowed()) { _reader.Stop(); return; }
        Sample();
        if (++_ticks % 3 == 0 && _front != 0) _ = RefreshAddressAsync(_front); // a page changed inside the same window
        MediaState? media = null;
        if (_ticks % 2 == 0)
        {
            try { media = await ReadMediaAsync(); }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException) { }
            if (media is not null)
            {
                Engine.Media(media, DateTimeOffset.UtcNow);
                if (_lastMedia is { Position: { } before } last && last.Title == media.Title && media.Position is { } after && after < before - TimeSpan.FromSeconds(4))
                    Rewound?.Invoke(media, before - after);
                _lastMedia = media;
            }
        }
        Engine.Tick(DateTimeOffset.UtcNow, Win32.SinceLastInput(), _lastMedia?.Playing == true);
        if (_ticks % 60 == 0)
        {
            try { Engine.Flush(); }
            catch (Exception e) when (e is Microsoft.Data.Sqlite.SqliteException or IOException) { AppServices.Log.Warn("Saving context failed: " + e.Message); }
        }
        if (_mayRead()) _reader.Start(); else _reader.Stop();
    }

    /// <summary>What Windows' media session says is playing now, or null. Browsers, VLC, Spotify and others publish it.</summary>
    public async Task<MediaState?> ReadMediaAsync()
    {
        _media ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = _media.GetCurrentSession();
        if (session is null) return null;
        var props = await session.TryGetMediaPropertiesAsync();
        if (props is null || string.IsNullOrWhiteSpace(props.Title)) return null;
        var timeline = session.GetTimelineProperties();
        var playing = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        TimeSpan? position = null, duration = null;
        if (timeline is not null && timeline.EndTime > timeline.StartTime)
        {
            duration = timeline.EndTime - timeline.StartTime;
            var p = timeline.Position - timeline.StartTime;
            if (playing && timeline.LastUpdatedTime > DateTimeOffset.MinValue) p += DateTimeOffset.Now - timeline.LastUpdatedTime;
            position = p < TimeSpan.Zero ? TimeSpan.Zero : p > duration ? duration : p;
        }
        var id = session.SourceAppUserModelId ?? "media app";
        var app = id.Contains("chrome", StringComparison.OrdinalIgnoreCase) ? "Chrome" : id.Contains("msedge", StringComparison.OrdinalIgnoreCase) ? "Edge"
            : id.Contains("brave", StringComparison.OrdinalIgnoreCase) ? "Brave" : id.Contains("firefox", StringComparison.OrdinalIgnoreCase) ? "Firefox" : id;
        return new MediaState(app, props.Title, props.Artist, position, duration, playing, DateTimeOffset.UtcNow);
    }

    /// <summary>Pauses, plays or seeks what is playing. Reversible; used by the tutor during a question.</summary>
    public async Task<bool> ControlMediaAsync(string action, TimeSpan? to)
    {
        _media ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = _media.GetCurrentSession();
        if (session is null) return false;
        return action switch
        {
            "pause" => await session.TryPauseAsync(),
            "play" => await session.TryPlayAsync(),
            "seek" when to is { } t => await session.TryChangePlaybackPositionAsync((session.GetTimelineProperties().StartTime + t).Ticks),
            _ => false,
        };
    }

    /// <summary>Live facts for one question: what is playing, the page address and selected text, read now.</summary>
    public async Task<WorkContext> SnapshotAsync(nint front, bool readContent)
    {
        MediaState? media = null;
        try { media = await ReadMediaAsync(); }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException) { }
        string? address = null, selection = null;
        if (readContent && front != 0)
        {
            var work = Task.Run(() => (ReadAddress(front), ReadSelection()));
            try { (address, selection) = await work.WaitAsync(TimeSpan.FromSeconds(1.5)); }
            catch (TimeoutException) { }
        }
        return new WorkContext(DateTimeOffset.UtcNow, null, [], media, UrlSanitiser.Clean(address), selection);
    }

    private static readonly string[] AddressBarNames = ["Address and search bar", "Search or enter address", "Search with Google or enter address", "Address bar"];

    private static string? ReadAddress(nint hwnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, condition))
            {
                var name = e.Current.Name ?? "";
                if (!AddressBarNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase)) && e.Current.AutomationId != "urlbar-input") continue;
                if (e.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) && ((ValuePattern)vp).Current.Value is { Length: > 0 } v) return v;
            }
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException) { }
        return null;
    }

    private static string? ReadSelection()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is null || focused.Current.IsPassword || !focused.TryGetCurrentPattern(TextPattern.Pattern, out var tp)) return null;
            var text = string.Join(" ", ((TextPattern)tp).GetSelection().Select(r => r.GetText(2000))).Trim();
            return text.Length > 0 ? text : null;
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException) { return null; }
    }

    public void Dispose()
    {
        _timer.Stop();
        _reader.Stop();
        if (_focusHook != 0) Win32.UnhookWinEvent(_focusHook);
        if (_nameHook != 0) Win32.UnhookWinEvent(_nameHook);
        try { Engine.Flush(); }
        catch (Exception e) when (e is Microsoft.Data.Sqlite.SqliteException or IOException or ObjectDisposedException) { }
    }

    /// <summary>
    /// Reads the text of the window in front after it changes and then holds still for a moment
    /// (a page scrolled, a slide advanced, a paused frame). Change is detected on a 32×20 grayscale
    /// thumbnail, from desktop duplication when the native module is present, else GDI. OCR runs at
    /// most every 4 s on a background thread; the text goes to the engine (memory only).
    /// </summary>
    private sealed class ScreenReader(ContextHub hub)
    {
        private CancellationTokenSource? _run;
        public bool Running => _run is not null;

        public void Start()
        {
            if (_run is not null) return;
            _run = new CancellationTokenSource();
            var ct = _run.Token;
            _ = Task.Run(() => LoopAsync(ct), ct);
            hub.ReadingChanged?.Invoke(true);
        }

        public void Stop()
        {
            if (_run is null) return;
            _run.Cancel();
            _run = null;
            hub.ReadingChanged?.Invoke(false);
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            byte[]? last = null;
            var changedAt = DateTime.MinValue;
            var readAt = DateTime.MinValue;
            var pending = false;
            DuplicationSession? dup = null;
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(500, ct);
                    var front = hub._front;
                    if (front == 0 || Win32.WindowBounds(front) is not { } bounds || bounds.Width < 64 || bounds.Height < 64) continue;
                    var monitor = Win32.Monitors().FirstOrDefault(m => m.BoundsPhysical.Contains(bounds.Center))?.BoundsPhysical;
                    if (monitor is not { } mon) continue;
                    var area = bounds.Intersect(mon);
                    if (dup is null || dup.Monitor != mon) { dup?.Dispose(); dup = DuplicationSession.TryOpen(mon); }
                    byte[]? thumb;
                    try
                    {
                        if (dup is not null) { while (dup.Next(0) is not null) { } }
                        thumb = dup is { HasFrame: true } ? dup.Thumb(32, 20, area) : ScreenCaptureService.Thumbnail(area, 32);
                    }
                    catch (InvalidOperationException) { continue; } // locked, secure desktop: try later
                    if (thumb is null) continue;
                    if (last is not null && last.Length == thumb.Length && ScreenDiff.Fraction(last, thumb) > 0.02) { pending = true; changedAt = DateTime.UtcNow; }
                    if (last is null) pending = true;
                    last = thumb;
                    if (!pending || DateTime.UtcNow - changedAt < TimeSpan.FromSeconds(1.2) || DateTime.UtcNow - readAt < TimeSpan.FromSeconds(4) || engine is null) continue;
                    pending = false;
                    readAt = DateTime.UtcNow;
                    var lines = await ReadAsync(engine, dup, area, ct);
                    if (lines.Count > 0) hub.Engine.ScreenText(lines, DateTimeOffset.UtcNow);
                }
            }
            catch (OperationCanceledException) { }
            finally { dup?.Dispose(); }
        }

        private static async Task<IReadOnlyList<string>> ReadAsync(OcrEngine engine, DuplicationSession? dup, PxRect area, CancellationToken ct)
        {
            // The window's pixels at full resolution, downscaled only if OCR's size limit requires it.
            byte[]? bgra = dup is { HasFrame: true } ? dup.Copy(area) : null;
            int w = (int)area.Width, h = (int)area.Height;
            if (bgra is null)
            {
                RawImage raw;
                try { raw = ScreenCaptureService.Raw(ScreenCaptureService.Grab(area)); }
                catch (InvalidOperationException) { return []; }
                (bgra, w, h) = (raw.Bgra, raw.Width, raw.Height);
            }
            if (w > OcrEngine.MaxImageDimension || h > OcrEngine.MaxImageDimension) return [];
            using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(bgra.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
            var result = await engine.RecognizeAsync(bitmap).AsTask(ct);
            return [.. result.Lines.Select(l => l.Text).Where(t => t.Length > 2).Take(80)];
        }
    }
}
