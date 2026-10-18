using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vernier.App.Platform;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;

namespace Vernier.App;

/// <summary>
/// The glass: one transparent, click-through, topmost window per monitor, always present.
/// The tutor draws on it (arrows, boxes, circles, underlines, brackets, labels, pointers). The
/// learner has one tool, the pen; while it is armed the glass takes the mouse, otherwise every
/// click passes through.
/// </summary>
/// <remarks>
/// Retained mode: every mark is turned into its own visual once, when it is added, and is not
/// redrawn again unless it changes. Measured on v0.3.0, redrawing every mark on every frame put
/// a new mark up to 1.2 s late once a few minutes of ink were on the glass.
/// </remarks>
public sealed class AnnotationSurfaceWindow : Window
{
    private readonly GlassSurface _surface;
    private readonly AnnotationStore _store;
    public MonitorInfo Monitor { get; }

    /// <summary>The learner finished a stroke (physical pixels).</summary>
    public event Action<IReadOnlyList<PxPoint>>? StrokeDrawn;

    /// <summary>When set, the pen is drawn by the native ink layer (own thread, straight into DWM) and this window only starts and stops it.</summary>
    public NativeInk? Ink { get => _surface.NativeInk; set => _surface.NativeInk = value; }

    public AnnotationSurfaceWindow(MonitorInfo monitor, AnnotationStore store)
    {
        Monitor = monitor;
        _store = store;
        Title = "Vernier glass";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = _surface = new GlassSurface(monitor, stroke => StrokeDrawn?.Invoke(stroke));
        AutomationProperties.SetName(this, "Vernier screen annotations");
    }

    private nint Handle => new WindowInteropHelper(this).Handle;

    /// <summary>How many marks are on this glass right now (for tests and diagnostics).</summary>
    public int MarkCount => _surface.MarkCount;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        SetClickThrough(true);
        Place();
        Sync();
    }

    // Positioned in physical pixels so mixed-DPI layouts cannot shift it.
    private void Place()
    {
        var b = Monitor.BoundsPhysical;
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, (int)b.X, (int)b.Y, (int)b.Width, (int)b.Height, Win32.SWP_NOACTIVATE);
    }

    // Moving onto a monitor with another scale makes WPF resize the window to its own suggestion;
    // put it back over the monitor exactly and rebuild the marks at the new scale.
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Place();
        _surface.Rebuild(_store.Items, _store.Visible);
    }

    public void SetPenArmed(bool armed)
    {
        _surface.PenArmed = armed;
        SetClickThrough(!armed);
        Cursor = armed ? Cursors.Pen : Cursors.Arrow;
    }

    /// <summary>Brings the glass in line with the store: adds new marks, drops removed ones, rebuilds changed ones.</summary>
    public void Sync() => _surface.Sync(_store.Items, _store.Visible);

    /// <summary>Kept for callers that predate retained rendering; same as <see cref="Sync"/>.</summary>
    public void Redraw() => Sync();

    private void SetClickThrough(bool through)
    {
        if (Handle == 0) return;
        var ex = (long)Win32.GetWindowLongPtr(Handle, Win32.GWL_EXSTYLE) | Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        ex = through ? ex | Win32.WS_EX_TRANSPARENT : ex & ~Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLongPtr(Handle, Win32.GWL_EXSTYLE, (nint)ex);
    }
}

/// <summary>Holds one visual per mark, a hit plate for the pen, and the stroke being drawn.</summary>
internal sealed class GlassSurface : FrameworkElement
{
    private readonly MonitorInfo _monitor;
    private readonly Action<IReadOnlyList<PxPoint>> _strokeDrawn;
    private readonly VisualCollection _children;
    private readonly DrawingVisual _hitPlate = new();
    private readonly DrawingVisual _live = new();
    private readonly Dictionary<Guid, (Annotation Mark, DrawingVisual Visual)> _marks = [];
    private List<Point>? _stroke;
    private bool _liveDirty, _penArmed, _nativeStroke;

    public NativeInk? NativeInk { get; set; }

    public GlassSurface(MonitorInfo monitor, Action<IReadOnlyList<PxPoint>> strokeDrawn)
    {
        (_monitor, _strokeDrawn) = (monitor, strokeDrawn);
        _children = new VisualCollection(this) { _hitPlate, _live };
    }

    public int MarkCount => _marks.Count;

    public bool PenArmed
    {
        get => _penArmed;
        set
        {
            _penArmed = value;
            using var dc = _hitPlate.RenderOpen();
            // Alpha 1: invisible, but it makes the whole glass hit-testable while the pen is armed.
            if (value) dc.DrawRectangle(MarkPainter.HitPlate, null, new Rect(0, 0, 1e5, 1e5));
        }
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    private double Scale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    public void Sync(IReadOnlyList<Annotation> items, bool visible)
    {
        var bounds = _monitor.BoundsPhysical;
        var wanted = visible ? items.Where(a => a.Points.Any(bounds.Contains)).ToList() : [];
        var ids = wanted.Select(a => a.Id).ToHashSet();
        foreach (var gone in _marks.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            _children.Remove(_marks[gone].Visual);
            _marks.Remove(gone);
        }
        var scale = Scale;
        var size = new Size(bounds.Width / scale, bounds.Height / scale);
        foreach (var a in wanted)
        {
            var isNew = !_marks.TryGetValue(a.Id, out var existing);
            if (!isNew && ReferenceEquals(existing.Mark, a)) continue; // unchanged: nothing to redraw
            if (!isNew) _children.Remove(existing.Visual);
            var visual = MarkPainter.Build(a, p => new Point((p.X - bounds.X) / scale, (p.Y - bounds.Y) / scale), size, VisualTreeHelper.GetDpi(this).PixelsPerDip,
                reveal: isNew && a.Source == "tutor");
            // Settled marks are rasterised once and composed as a texture, so a glass full of ink costs no more per frame than an empty one.
            if (!visual.ContentBounds.IsEmpty) visual.CacheMode = new BitmapCache { SnapsToDevicePixels = true };
            _children.Insert(_children.Count - 1, visual); // below the stroke being drawn
            _marks[a.Id] = (a, visual);
        }
    }

    public void Rebuild(IReadOnlyList<Annotation> items, bool visible)
    {
        foreach (var (_, visual) in _marks.Values) _children.Remove(visual);
        _marks.Clear();
        Sync(items, visible);
    }

    // ---- the pen: points are collected on input, the stroke is redrawn at most once per frame ----

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!PenArmed) return;
        if (NativeInk is { } ink)
        {
            // The native layer follows the cursor itself; WPF only keeps the mouse so the click does not reach the app below.
            _nativeStroke = true;
            ink.Start(watchButton: e.StylusDevice is null);
            CaptureMouse();
            return;
        }
        _stroke = [e.GetPosition(this)];
        CaptureMouse();
        CompositionTarget.Rendering += OnFrame;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_stroke is null) return;
        var p = e.GetPosition(this);
        if ((p - _stroke[^1]).Length < 1.5 || _stroke.Count >= 4000) return;
        _stroke.Add(p);
        _liveDirty = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => EndStroke();
    protected override void OnLostMouseCapture(MouseEventArgs e) => EndStroke();

    private void EndStroke()
    {
        if (_nativeStroke)
        {
            _nativeStroke = false;
            NativeInk?.Stop();
            if (IsMouseCaptured) ReleaseMouseCapture();
            return;
        }
        if (_stroke is null) return;
        var stroke = _stroke;
        _stroke = null;
        CompositionTarget.Rendering -= OnFrame;
        if (IsMouseCaptured) ReleaseMouseCapture();
        using (_live.RenderOpen()) { } // the finished stroke comes back from the store as a retained mark
        var scale = Scale;
        var origin = _monitor.BoundsPhysical;
        if (stroke.Count >= 3) _strokeDrawn(stroke.Select(p => new PxPoint(origin.X + p.X * scale, origin.Y + p.Y * scale)).ToArray());
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!_liveDirty || _stroke is not { Count: > 1 } stroke) return;
        _liveDirty = false;
        using var dc = _live.RenderOpen();
        MarkPainter.Stroke(dc, stroke, AnnotationColors.Learner, 4);
    }
}

/// <summary>Turns a mark into drawing instructions. Pens, brushes and colours are created once and frozen.</summary>
internal static class MarkPainter
{
    private static readonly Typeface LabelFace = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    public static readonly Brush HitPlate = Frozen(Color.FromArgb(1, 0, 0, 0));
    private static readonly Brush Halo = Frozen(Color.FromArgb(140, 0, 0, 0));
    private static readonly Brush PlateFill = Frozen(Color.FromRgb(12, 22, 32));
    private static readonly Dictionary<string, Color> Colours = [];
    private static readonly Dictionary<(string Colour, double Width, bool Dashed, bool Halo), Pen> Pens = [];
    private static readonly Dictionary<(string Colour, byte Alpha), Brush> Brushes = [];

    private static Color ColourOf(string hex)
    {
        if (Colours.TryGetValue(hex, out var c)) return c;
        try { c = (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { c = (Color)ColorConverter.ConvertFromString(AnnotationColors.Focus); } // a bad colour must not lose the mark
        return Colours[hex] = c;
    }

    private static Brush BrushOf(string hex, byte alpha = 255)
    {
        if (Brushes.TryGetValue((hex, alpha), out var b)) return b;
        var c = ColourOf(hex);
        return Brushes[(hex, alpha)] = Frozen(Color.FromArgb(alpha, c.R, c.G, c.B));
    }

    private static Pen PenOf(string hex, double width, bool dashed, bool halo)
    {
        if (Pens.TryGetValue((hex, width, dashed, halo), out var pen)) return pen;
        pen = new Pen(halo ? Halo : BrushOf(hex), halo ? width + 3 : width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (dashed && !halo) pen.DashStyle = new DashStyle([4, 3], 0);
        pen.Freeze();
        return Pens[(hex, width, dashed, halo)] = pen;
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>One polyline as one geometry, drawn twice (dark halo, then ink): no beading at the joints.</summary>
    public static void Stroke(DrawingContext dc, IReadOnlyList<Point> points, string colour, double width)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
        }
        g.Freeze();
        dc.DrawGeometry(null, PenOf(colour, width, false, true), g);
        dc.DrawGeometry(null, PenOf(colour, width, false, false), g);
    }

    /// <param name="reveal">A new tutor mark fades in and settles from 94 % scale over about 200 ms, so it is noticed;
    /// a pointer ring also pulses three times. With Windows animations off it only fades. Once settled it is static.</param>
    public static DrawingVisual Build(Annotation a, Func<PxPoint, Point> local, Size bounds, double pixelsPerDip, bool reveal)
    {
        var visual = new DrawingVisual();
        var pts = a.Points.Select(local).ToList();
        if (pts.Count == 0) return visual;
        var uncertain = a is { Source: "tutor", Confidence: < 0.5 };
        var halo = PenOf(a.Color, a.StrokeWidth, uncertain, true);
        var pen = PenOf(a.Color, a.StrokeWidth, uncertain, false);
        var motion = reveal && SystemParameters.ClientAreaAnimation;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Point? labelAt = null;
        var box = pts.Count >= 2 ? new Rect(pts[0], pts[1]) : new Rect(pts[0], new Size(0, 0));
        var centre = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);

        using var dc = visual.RenderOpen();
        if (reveal) dc.PushOpacity(0, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease }.CreateClock());
        if (motion)
        {
            var grow = new ScaleTransform(0.94, 0.94, centre.X, centre.Y);
            var settle = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
            grow.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
            grow.BeginAnimation(ScaleTransform.ScaleYProperty, settle);
            dc.PushTransform(grow);
        }
        void Both(Geometry g, Brush? fill = null) { dc.DrawGeometry(null, halo, g); dc.DrawGeometry(fill, pen, g); }
        switch (a.Kind)
        {
            case AnnotationKind.Arrow when pts.Count >= 2:
                var dir = pts[1] - pts[0];
                var arrow = new StreamGeometry();
                using (var g = arrow.Open())
                {
                    g.BeginFigure(pts[0], false, false);
                    g.LineTo(pts[1], true, true);
                    if (dir.Length > 1)
                    {
                        dir.Normalize();
                        var normal = new Vector(-dir.Y, dir.X);
                        g.BeginFigure(pts[1] - dir * 14 + normal * 6, true, true);
                        g.LineTo(pts[1], true, true);
                        g.LineTo(pts[1] - dir * 14 - normal * 6, true, true);
                    }
                }
                arrow.Freeze();
                Both(arrow, BrushOf(a.Color));
                labelAt = pts[0];
                break;
            case AnnotationKind.Highlight when pts.Count >= 2:
                Both(new RectangleGeometry(box), BrushOf(a.Color, 40));
                labelAt = box.TopLeft;
                break;
            case AnnotationKind.Underline when pts.Count >= 2:
                Both(new LineGeometry(new Point(box.Left, box.Bottom + 3), new Point(box.Right, box.Bottom + 3)));
                labelAt = box.TopLeft;
                break;
            case AnnotationKind.Circle when pts.Count >= 2:
                // The box is the thing's bounding box. An ellipse inside it cuts across the corners of what it should
                // enclose, so use the ellipse through the corners (radii x sqrt 2); very wide or tall targets get a rounded box.
                var cr = box;
                cr.Inflate(3, 3);
                var aspect = Math.Max(cr.Width, cr.Height) / Math.Max(1, Math.Min(cr.Width, cr.Height));
                var r = Math.Min(cr.Width, cr.Height) / 2;
                Both(aspect > 3
                    ? new RectangleGeometry(cr, r, r)
                    : new EllipseGeometry(centre, cr.Width / 2 * Math.Sqrt(2), cr.Height / 2 * Math.Sqrt(2)));
                labelAt = aspect > 3 ? cr.TopLeft : new Point(cr.X, cr.Y - cr.Height * 0.21);
                break;
            case AnnotationKind.Bracket when pts.Count >= 2:
                var bracket = new StreamGeometry();
                using (var g = bracket.Open())
                {
                    g.BeginFigure(new Point(box.Left + 8, box.Top), false, false);
                    g.PolyLineTo([box.TopLeft, box.BottomLeft, new Point(box.Left + 8, box.Bottom)], true, true);
                }
                bracket.Freeze();
                Both(bracket);
                labelAt = box.TopLeft;
                break;
            case AnnotationKind.Point:
                // A ring round the target plus a dot: "this one". The ring pulses briefly when it appears.
                var at = pts.Count >= 2 ? centre : pts[0];
                var radius = Math.Max(14, Math.Min(box.Width, box.Height) / 2 + 8);
                Both(new EllipseGeometry(at, radius, radius));
                dc.DrawEllipse(BrushOf(a.Color), null, at, 3.5, 3.5);
                labelAt = new Point(at.X + radius * 0.7, at.Y - radius * 0.7);
                if (motion)
                {
                    var ring = new ScaleTransform(1, 1, at.X, at.Y);
                    var spread = new DoubleAnimation(1, 1.7, TimeSpan.FromMilliseconds(700)) { RepeatBehavior = new RepeatBehavior(3), EasingFunction = ease };
                    ring.BeginAnimation(ScaleTransform.ScaleXProperty, spread);
                    ring.BeginAnimation(ScaleTransform.ScaleYProperty, spread);
                    dc.PushTransform(ring);
                    dc.PushOpacity(0, new DoubleAnimation(0.8, 0, TimeSpan.FromMilliseconds(700)) { RepeatBehavior = new RepeatBehavior(3) }.CreateClock());
                    dc.DrawEllipse(null, PenOf(a.Color, 2, false, false), at, radius, radius);
                    dc.Pop();
                    dc.Pop();
                }
                break;
            case AnnotationKind.Label:
                labelAt = pts[0];
                break;
            case AnnotationKind.Note when a.Note is { } note && pts.Count >= 2:
            {
                // A written card: drawn once to a bitmap (see NoteRenderer); it wipes in from the top like writing appearing.
                var card = NoteRenderer.CardFor(a.Id, note, Math.Abs(pts[1].X - pts[0].X), pixelsPerDip);
                if (reveal)
                {
                    var wipeStart = new GradientStop(Colors.Black, -0.15);
                    var wipeEdge = new GradientStop(Colors.Transparent, 0.0);
                    var mask = new LinearGradientBrush(new GradientStopCollection { wipeStart, wipeEdge }, new Point(0, 0), new Point(0, 1));
                    var duration = TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 420 : 1);
                    wipeStart.BeginAnimation(GradientStop.OffsetProperty, new DoubleAnimation(-0.15, 1.0, duration) { EasingFunction = ease });
                    wipeEdge.BeginAnimation(GradientStop.OffsetProperty, new DoubleAnimation(0.0, 1.15, duration) { EasingFunction = ease });
                    dc.PushOpacityMask(mask);
                }
                dc.DrawImage(card.Image, new Rect(pts[0], card.SizeDip));
                if (reveal) dc.Pop();
                break;
            }
            default:
                if (pts.Count > 1) Stroke(dc, pts, a.Color, a.StrokeWidth);
                break;
        }

        if (a.Label is { Length: > 0 } label && labelAt is { } spot)
        {
            var text = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, 12.5, System.Windows.Media.Brushes.White, pixelsPerDip) { MaxTextWidth = 260 };
            // Kept on the glass even when the mark sits against a screen edge.
            double w = text.Width + 14, h = text.Height + 6;
            var plate = new Rect(Math.Clamp(spot.X, 0, Math.Max(0, bounds.Width - w)), Math.Clamp(spot.Y - h - 4, 0, Math.Max(0, bounds.Height - h)), w, h);
            dc.DrawRectangle(PlateFill, PenOf(a.Color, 1.5, false, false), plate);
            dc.DrawText(text, new Point(plate.X + 7, plate.Y + 3));
        }
        return visual;
    }
}
