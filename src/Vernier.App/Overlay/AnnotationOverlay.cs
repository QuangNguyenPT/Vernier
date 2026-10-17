using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Vernier.App.Platform;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;

namespace Vernier.App;

public enum EdgeSignal { Off, Armed, Looking }

/// <summary>
/// A thin light-blue band along one edge of a monitor. Four of them frame the screen while
/// Vernier is looking at it: a steady frame while the pen is armed, a travelling wave while a
/// capture is being read. Thin separate windows keep the animation off the full-screen glass.
/// </summary>
public sealed class EdgeStripWindow : Window
{
    public const double ThicknessDip = 12;
    private static readonly Color Signal = Color.FromRgb(0x6C, 0xC4, 0xFF);

    private readonly Dock _side;
    private readonly MonitorInfo _monitor;
    private readonly Rectangle _band = new();
    private readonly TranslateTransform _travel = new();

    public EdgeStripWindow(MonitorInfo monitor, Dock side)
    {
        (_monitor, _side) = (monitor, side);
        Title = "Vernier capture light";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Opacity = 0;

        var horizontal = side is Dock.Top or Dock.Bottom;
        // Brightest at the screen edge, gone a few pixels in.
        var inward = side switch
        {
            Dock.Top => new LinearGradientBrush(Signal, Colors.Transparent, new Point(0, 0), new Point(0, 1)),
            Dock.Bottom => new LinearGradientBrush(Signal, Colors.Transparent, new Point(0, 1), new Point(0, 0)),
            Dock.Left => new LinearGradientBrush(Signal, Colors.Transparent, new Point(0, 0), new Point(1, 0)),
            _ => new LinearGradientBrush(Signal, Colors.Transparent, new Point(1, 0), new Point(0, 0)),
        };
        _band.Fill = inward;
        // The wave: a repeating bright/dim pattern along the edge that slides. It is a mask, so the animation costs no layout or redraw code.
        _band.OpacityMask = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute, SpreadMethod = GradientSpreadMethod.Repeat,
            StartPoint = new Point(0, 0), EndPoint = horizontal ? new Point(360, 0) : new Point(0, 360),
            GradientStops = [new(Color.FromArgb(255, 0, 0, 0), 0), new(Color.FromArgb(70, 0, 0, 0), 0.5), new(Color.FromArgb(255, 0, 0, 0), 1)],
            Transform = _travel,
        };
        Content = _band;
        AutomationProperties.SetName(this, "Vernier is looking at the screen");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var ex = (long)Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE) | Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, (nint)ex);
        var b = _monitor.BoundsPhysical;
        var t = (int)Math.Round(ThicknessDip * _monitor.DpiScale);
        var (x, y, w, h) = _side switch
        {
            Dock.Top => ((int)b.X, (int)b.Y, (int)b.Width, t),
            Dock.Bottom => ((int)b.X, (int)b.Bottom - t, (int)b.Width, t),
            Dock.Left => ((int)b.X, (int)b.Y, t, (int)b.Height),
            _ => ((int)b.Right - t, (int)b.Y, t, (int)b.Height),
        };
        Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, x, y, w, h, Win32.SWP_NOACTIVATE);
    }

    public void Show(EdgeSignal signal)
    {
        var property = _side is Dock.Top or Dock.Bottom ? TranslateTransform.XProperty : TranslateTransform.YProperty;
        // Clockwise round the screen: right along the top, down the right side, left along the bottom, up the left side.
        var forward = _side is Dock.Top or Dock.Right;
        var moving = signal == EdgeSignal.Looking && SystemParameters.ClientAreaAnimation; // respects "show animations in Windows"
        _travel.BeginAnimation(property, moving
            ? new DoubleAnimation(forward ? 0 : 360, forward ? 360 : 0, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever }
            : null);
        var target = signal switch { EdgeSignal.Looking => 1.0, EdgeSignal.Armed => 0.55, _ => 0.0 };
        BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(signal == EdgeSignal.Off ? 150 : 220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }
}

/// <summary>Owns the glass and the edge lights on every monitor, rebuilds them when displays change, and collects the learner's pen strokes.</summary>
public sealed class AnnotationOverlayManager : IDisposable
{
    private readonly AnnotationStore _store;
    private readonly List<AnnotationSurfaceWindow> _glass = [];
    private readonly List<EdgeStripWindow> _edges = [];
    // The native light (Vernier.Native): composited by DWM, drawn on its own thread, a real travelling crest.
    // The WPF strips above are the fallback when the native module is not built or cannot start.
    private readonly List<NativeGlow> _lights = [];

    /// <summary>The capture light is the native one (true) or the WPF strips (false).</summary>
    public bool NativeLight => _lights.Count > 0;
    private readonly List<Annotation> _ink = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private nint _trackedWindow;
    private PxRect? _trackedBounds;
    private bool _looking;
    private PxRect? _watch;
    private byte[]? _watchBaseline;
    private int _ticks;
    private bool _sampling;
    // No baseline is taken until marks have finished appearing: a fading-in mark is not the screen changing.
    private DateTime _settledAt;
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(600);

    public bool PenArmed { get; private set; }
    public bool IsRunning => _glass.Count > 0;
    public EdgeSignal Signal { get; private set; }

    /// <summary>Strokes drawn since the pen was armed, oldest first (physical pixels).</summary>
    public IReadOnlyList<IReadOnlyList<PxPoint>> Ink => _ink.Select(a => a.Points).ToList();
    public PxRect? InkBounds => _ink.Count == 0 ? null : Bounds(_ink.SelectMany(a => a.Points));

    public event Action? PenChanged;
    /// <summary>What the tutor's marks pointed at has changed (scrolled, zoomed, video moved on); the marks were removed.</summary>
    public event Action? MarksWentStale;
    /// <summary>Whether the screen may be sampled right now. Sampling is local, never sent, and follows the same consent as capture.</summary>
    public Func<bool> CanSample { get; set; } = () => false;

    /// <summary>Share of the watched area that must look different before marks count as stale.</summary>
    public const double StaleFraction = 0.10;
    private const int WatchThumb = 48;

    public AnnotationOverlayManager(AnnotationStore store)
    {
        _store = store;
        _store.Changed += OnStoreChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Puts the glass up. It stays for the life of the app.</summary>
    public void Start()
    {
        if (IsRunning) return;
        foreach (var monitor in Win32.Monitors())
        {
            var glass = new AnnotationSurfaceWindow(monitor, _store);
            glass.StrokeDrawn += OnStroke;
            AppServices.TrackWindow(glass, $"Glass ({monitor.BoundsPhysical.Width:0}×{monitor.BoundsPhysical.Height:0})");
            glass.Show(); // created once so its handle and capture policy exist; hidden again below while empty
            glass.SetPenArmed(PenArmed);
            _glass.Add(glass);
            if (NativeGlow.TryCreate(monitor.BoundsPhysical, (int)Math.Round(EdgeStripWindow.ThicknessDip * 1.25 * monitor.DpiScale), LightColour) is { } light)
            {
                AppServices.Policy.Register(light.Hwnd, "Capture light");
                _lights.Add(light);
                continue;
            }
            foreach (var side in new[] { Dock.Top, Dock.Right, Dock.Bottom, Dock.Left })
            {
                var edge = new EdgeStripWindow(monitor, side);
                AppServices.TrackWindow(edge, "Capture light");
                edge.Show();
                _edges.Add(edge);
            }
        }
        ShowOnlyWhenNeeded();
        ApplySignal();
        _timer.Start();
    }

    public void ArmPen(bool armed)
    {
        if (PenArmed == armed) return;
        PenArmed = armed;
        if (armed) _ink.Clear();
        foreach (var g in _glass) g.SetPenArmed(armed);
        ShowOnlyWhenNeeded();
        ApplySignal();
        PenChanged?.Invoke();
    }

    /// <summary>Forgets which strokes belong to the current question. The ink itself stays on the glass until cleared.</summary>
    public void ConsumeInk() => _ink.Clear();

    /// <summary>Turns the edge wave on or off: on from the moment a capture starts until its answer is done.</summary>
    private bool _watching;

    /// <summary>Vernier is reading the screen for context (local only): a small steady mark at the top of the screen says so.</summary>
    public void SetWatching(bool watching)
    {
        if (_watching == watching) return;
        _watching = watching;
        ApplySignal();
    }

    public bool Watching => _watching;

    public void SetLooking(bool looking)
    {
        if (_looking == looking) return;
        _looking = looking;
        ApplySignal();
    }

    /// <summary>Marks placed on this window follow it when it moves.</summary>
    public void Track(nint hwnd)
    {
        _trackedWindow = hwnd;
        _trackedBounds = hwnd == 0 ? null : Win32.WindowBounds(hwnd);
    }

    /// <summary>
    /// Watches the area the tutor just marked. Marks are drawn at fixed screen positions; once the
    /// content under them moves they point at the wrong thing, so they are removed instead.
    /// </summary>
    public void Watch(PxRect? area)
    {
        _watch = area is { IsEmpty: false } a ? a.Intersect(Win32.VirtualDesktop()) : null;
        _watchBaseline = null;
        _settledAt = DateTime.UtcNow + SettleTime;
    }

    /// <summary>Watches the area covering every tutor mark on this monitor, with room around it.</summary>
    public void WatchTutorMarks(PxRect monitor)
    {
        var points = _store.Items.Where(a => a.Source == "tutor").SelectMany(a => a.Points).Where(monitor.Contains).ToList();
        if (points.Count == 0) { Watch(null); return; }
        const double pad = 60;
        var b = Bounds(points);
        Watch(new PxRect(b.X - pad, b.Y - pad, b.Width + 2 * pad, b.Height + 2 * pad).Intersect(monitor));
    }

    private async void CheckStale()
    {
        if (_watch is not { } area || _sampling || ++_ticks % 3 != 0) return; // about once every 1.2 s
        if (!_store.Items.Any(a => a.Source == "tutor")) { _watch = null; return; }
        // Not while a capture is being read, and not while one of Vernier's own windows covers the area: both would read as change.
        if (_looking || !CanSample() || OwnWindowOver(area)) { _watchBaseline = null; return; }
        if (DateTime.UtcNow < _settledAt) return;
        _sampling = true;
        byte[] now;
        try { now = await Task.Run(() => ScreenCaptureService.Thumbnail(area, WatchThumb)); } // GDI and scaling stay off the UI thread
        catch (InvalidOperationException) { return; } // locked session: try again later
        catch (Exception e) when (e is ArgumentException or System.ComponentModel.Win32Exception or OutOfMemoryException)
        {
            AppServices.Log.Warn("Checking whether marks are stale failed: " + e.GetType().Name); // an async void timer handler must not throw
            return;
        }
        finally { _sampling = false; }
        if (_watch != area) return; // moved or replaced while sampling
        if (_watchBaseline is null) { _watchBaseline = now; return; }
        if (ScreenDiff.Fraction(_watchBaseline, now) < StaleFraction) return;
        _watch = null;
        _watchBaseline = null;
        // Only the tutor's marks: the learner's own ink is theirs to remove.
        _store.RemoveWhere(a => a.Source == "tutor");
        MarksWentStale?.Invoke();
    }

    private bool OwnWindowOver(PxRect area)
    {
        var passive = _glass.Cast<Window>().Concat(_edges).Select(w => new WindowInteropHelper(w).Handle).Concat(_lights.Select(l => l.Hwnd)).ToHashSet();
        // Only the always-on-top ones (the ask bar, dialogs): they float over the work without being part of it.
        // The workspace is an ordinary window; if it really covers the area, the marked thing is hidden and the marks should go.
        const long topmost = 0x8; // WS_EX_TOPMOST
        return AppServices.Policy.States.Any(s => !passive.Contains(s.Hwnd) && Win32.IsWindowVisible(s.Hwnd)
            && ((long)Win32.GetWindowLongPtr(s.Hwnd, Win32.GWL_EXSTYLE) & topmost) != 0
            && Win32.WindowBounds(s.Hwnd) is { } b && !b.Intersect(area).IsEmpty);
    }

    private void ApplySignal()
    {
        _watchBaseline = null; // the edge light is inside the picture too
        Signal = _looking ? EdgeSignal.Looking : PenArmed ? EdgeSignal.Armed : EdgeSignal.Off;
        foreach (var e in _edges) e.Show(Signal);
        var reduced = !SystemParameters.ClientAreaAnimation; // "show animations in Windows" off: a slow breath instead of travel
        foreach (var l in _lights) l.Set(Signal switch { EdgeSignal.Looking => 2, EdgeSignal.Armed => 1, _ => _watching ? 3 : 0 }, reduced);
    }

    /// <summary>The light's colour: the tutor's signal blue, a little brighter than the marks so it reads at the screen edge.</summary>
    public static uint LightColour { get; set; } = 0xFF6CC4FF;

    private void OnStroke(IReadOnlyList<PxPoint> stroke)
    {
        var mark = new Annotation { Kind = AnnotationKind.Freehand, Points = PolylineMath.Simplify(stroke, 0.75), Color = AnnotationColors.Learner, StrokeWidth = 4, Source = "user", Meaning = "drawn by the learner", Lifetime = TimeSpan.FromMinutes(10) };
        _ink.Add(mark);
        _store.Add(mark);
    }

    private void OnStoreChanged()
    {
        // Undo or Clear may have removed ink the pending question was about.
        _ink.RemoveAll(i => _store.Items.All(a => a.Id != i.Id));
        _watchBaseline = null; // the marks are part of the picture being compared
        _settledAt = DateTime.UtcNow + SettleTime;
        foreach (var g in _glass) g.Sync();
        ShowOnlyWhenNeeded();
    }

    // An empty glass is hidden, so a full-screen layered window is not composited over everything for nothing.
    private void ShowOnlyWhenNeeded()
    {
        foreach (var g in _glass)
        {
            var needed = PenArmed || g.MarkCount > 0;
            if (needed && !g.IsVisible) { g.Show(); g.SetPenArmed(PenArmed); RaiseInteractive(); }
            else if (!needed && g.IsVisible) g.Hide();
        }
    }

    // A glass shown again lands on top of the topmost band; the ask bar must stay above it, or the pen's hit plate would swallow its clicks.
    private static void RaiseInteractive()
    {
        foreach (Window w in Application.Current?.Windows ?? new WindowCollection())
            if (w is Views.AskBar { IsVisible: true } bar)
                Win32.SetWindowPos(new WindowInteropHelper(bar).Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }

    private void CloseWindows()
    {
        foreach (var w in _glass.Cast<Window>().Concat(_edges)) w.Close();
        foreach (var l in _lights)
        {
            AppServices.Policy.Unregister(l.Hwnd);
            l.Dispose();
        }
        _glass.Clear();
        _edges.Clear();
        _lights.Clear();
        _timer.Stop();
    }

    // Resolution, scaling or monitor arrangement changed: rebuild against the new layout and drop marks
    // that now fall off every display rather than drawing them somewhere wrong.
    private void OnDisplayChanged(object? sender, EventArgs e) => AppServices.OnUi(() =>
    {
        if (!IsRunning) return;
        CloseWindows();
        _store.DropOutside(Win32.Monitors());
        Start();
    });

    private void Tick()
    {
        _store.PruneExpired(DateTimeOffset.UtcNow);
        if (_trackedWindow != 0 && _trackedBounds is { } old && Win32.WindowBounds(_trackedWindow) is { } current && current != old)
        {
            _store.Rebase(old, current, Win32.VirtualDesktop());
            _trackedBounds = current;
            // A moved window takes its marks with it; a resized one reflows, which the comparison below will catch.
            if (_watch is { } w) Watch(new PxRect(w.X + current.X - old.X, w.Y + current.Y - old.Y, w.Width, w.Height));
        }
        CheckStale();
    }

    private static PxRect Bounds(IEnumerable<PxPoint> points)
    {
        var list = points.ToList();
        double l = list.Min(p => p.X), t = list.Min(p => p.Y);
        return new PxRect(l, t, list.Max(p => p.X) - l, list.Max(p => p.Y) - t);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        _store.Changed -= OnStoreChanged;
        CloseWindows();
    }
}
