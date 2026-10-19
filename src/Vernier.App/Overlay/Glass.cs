using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vernier.App.Platform;
using Vernier.Core.Agents;
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
    private static readonly Typeface LabelFace = new(new FontFamily("Segoe UI Variable Text, Segoe UI, Cambria Math, Segoe UI Symbol"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    public static readonly Brush HitPlate = Frozen(Color.FromArgb(1, 0, 0, 0));
    private static readonly Brush Halo = Frozen(Color.FromArgb(96, 0, 0, 0));
    private static readonly Brush PlateFill = Frozen(Color.FromArgb(236, 12, 20, 27));
    private static readonly Pen PlateEdge = FrozenPen(Color.FromArgb(46, 255, 255, 255), 1);
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
        pen = new Pen(halo ? Halo : BrushOf(hex), halo ? width + 2.5 : width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (dashed && !halo) pen.DashStyle = new DashStyle([4, 3], 0);
        pen.Freeze();
        return Pens[(hex, width, dashed, halo)] = pen;
    }

    private static Pen FrozenPen(Color c, double width)
    {
        var pen = new Pen(Frozen(c), width);
        pen.Freeze();
        return pen;
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>One smooth stroke as one geometry, drawn twice (dark halo, then ink): no beading at the joints.</summary>
    public static void Stroke(DrawingContext dc, IReadOnlyList<Point> points, string colour, double width)
    {
        var g = MarkShapes.Smooth(points);
        dc.DrawGeometry(null, PenOf(colour, width, false, true), g);
        dc.DrawGeometry(null, PenOf(colour, width, false, false), g);
    }

    /// <param name="reveal">A new tutor mark is drawn on along its length (about a fifth of a second, like a quick pen stroke) as it
    /// fades in; a pointer ring also pulses three times. With Windows animations off it only fades. Once drawn it is static.</param>
    public static DrawingVisual Build(Annotation a, Func<PxPoint, Point> local, Size bounds, double pixelsPerDip, bool reveal)
    {
        var visual = new DrawingVisual();
        var pts = a.Points.Select(local).ToList();
        if (pts.Count == 0) return visual;
        var uncertain = a is { Source: "tutor", Confidence: < 0.5 };
        var seed = a.Id.GetHashCode();
        var halo = PenOf(a.Color, a.StrokeWidth, uncertain, true);
        var pen = PenOf(a.Color, a.StrokeWidth, uncertain, false);
        var motion = reveal && SystemParameters.ClientAreaAnimation;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Point? labelAt = null;
        var box = pts.Count >= 2 ? new Rect(pts[0], pts[1]) : new Rect(pts[0], new Size(0, 0));
        var centre = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);

        using var dc = visual.RenderOpen();
        if (reveal) dc.PushOpacity(0, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease }.CreateClock());

        // A dashed (uncertain) mark keeps its dashes, so it is not drawn on.
        void Draw(Geometry g, Pen ink, Pen? under)
        {
            if (motion && !uncertain)
            {
                var length = MarkShapes.Length(g);
                var duration = TimeSpan.FromMilliseconds(Math.Clamp(120 + length * 0.25, 140, 300)); // quick: the mark is there by the time the eye gets there
                ink = DrawOn(ink, length, duration, ease);
                under = under is null ? null : DrawOn(under, length, duration, ease);
            }
            if (under is not null) dc.DrawGeometry(null, under, g);
            dc.DrawGeometry(null, ink, g);
        }

        switch (a.Kind)
        {
            case AnnotationKind.Arrow when pts.Count >= 2:
                Draw(MarkShapes.Arrow(pts[0], pts[1], seed), pen, halo);
                labelAt = pts[0];
                break;
            case AnnotationKind.Highlight when pts.Count >= 2:
                // A highlighter swipe: translucent, round-ended, no outline and no halo (a dark rim would dirty what it lights up).
                var swipe = MarkShapes.Swipe(box, seed, out var thickness);
                Draw(swipe, MarkerOf(a.Color, thickness), null);
                labelAt = box.TopLeft;
                break;
            case AnnotationKind.Underline when pts.Count >= 2:
                Draw(MarkShapes.Underline(box, seed), pen, halo);
                labelAt = box.TopLeft;
                break;
            case AnnotationKind.Circle when pts.Count >= 2:
                var loop = MarkShapes.Loop(box, seed);
                Draw(loop, pen, halo);
                labelAt = new Point(loop.Bounds.X + loop.Bounds.Width * 0.12, loop.Bounds.Y + 2);
                break;
            case AnnotationKind.Bracket when pts.Count >= 2:
                Draw(MarkShapes.Brace(box), pen, halo);
                labelAt = box.TopLeft;
                break;
            case AnnotationKind.Point:
                // A ring round the target plus a dot: "this one". The ring pulses briefly when it appears.
                var at = pts.Count >= 2 ? centre : pts[0];
                var radius = Math.Max(14, Math.Min(box.Width, box.Height) / 2 + 8);
                Draw(new EllipseGeometry(at, radius, radius), pen, halo);
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
            case AnnotationKind.Path when a.Source == "tutor" && pts.Count >= 2:
            {
                // A sketched graph (graph_sketch): exactly where code put it, drawn on from its start.
                var path = new StreamGeometry();
                using (var g = path.Open())
                {
                    g.BeginFigure(pts[0], false, false);
                    g.PolyLineTo(pts.Skip(1).ToList(), true, true);
                }
                path.Freeze();
                Draw(path, pen, halo);
                labelAt = new Point(pts[^1].X + 8, pts[^1].Y);
                break;
            }
            case AnnotationKind.Region when pts.Count >= 3:
            {
                // A shaded area on a graph (graph_area): straight edges, because the shape is what was added up; a light fill, an outline drawn on.
                var region = new StreamGeometry();
                using (var g = region.Open())
                {
                    g.BeginFigure(pts[0], true, true);
                    g.PolyLineTo(pts.Skip(1).ToList(), true, true);
                }
                region.Freeze();
                dc.DrawGeometry(BrushOf(a.Color, 60), null, region);
                Draw(region, pen, halo);
                labelAt = new Point(region.Bounds.X + 6, region.Bounds.Y + region.Bounds.Height * 0.55 + 14);
                break;
            }
            case AnnotationKind.Note or AnnotationKind.Diagram when pts.Count >= 2 && (a.Note is not null || a.Diagram is not null):
            {
                // A written card: drawn once to a bitmap (see NoteRenderer); it wipes in from the top like writing appearing.
                var cardWidth = Math.Abs(pts[1].X - pts[0].X);
                var card = a.Diagram is { } diagram ? DiagramRenderer.CardFor(a.Id, diagram, cardWidth, pixelsPerDip) : NoteRenderer.CardFor(a.Id, a.Note!, cardWidth, pixelsPerDip);
                if (reveal)
                {
                    var wipeStart = new GradientStop(Colors.Black, -0.15);
                    var wipeEdge = new GradientStop(Colors.Transparent, 0.0);
                    var mask = new LinearGradientBrush(new GradientStopCollection { wipeStart, wipeEdge }, new Point(0, 0), new Point(0, 1));
                    var duration = TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 260 : 1);
                    wipeStart.BeginAnimation(GradientStop.OffsetProperty, new DoubleAnimation(-0.15, 1.0, duration) { EasingFunction = ease });
                    wipeEdge.BeginAnimation(GradientStop.OffsetProperty, new DoubleAnimation(0.0, 1.15, duration) { EasingFunction = ease });
                    dc.PushOpacityMask(mask);
                }
                // The bitmap carries the card's soft shadow round it, so it is drawn that much outside the card's rectangle.
                var m = NoteRenderer.ShadowMargin;
                dc.DrawImage(card.Image, new Rect(pts[0].X - m, pts[0].Y - m, card.SizeDip.Width + 2 * m, card.SizeDip.Height + 2 * m));
                if (reveal) dc.Pop();
                break;
            }
            default:
                if (pts.Count > 1) Stroke(dc, pts, a.Color, a.StrokeWidth);
                break;
        }

        if (a.Label is { Length: > 0 } label && labelAt is { } spot)
        {
            var text = new FormattedText(TexPlain.Clean(label), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, 14, System.Windows.Media.Brushes.White, pixelsPerDip) { MaxTextWidth = 260 };
            // A rounded tag with a dot of the mark's colour; kept on the glass even when the mark sits against a screen edge.
            double w = text.Width + 26, h = text.Height + 8;
            var plate = new Rect(Math.Clamp(spot.X, 0, Math.Max(0, bounds.Width - w)), Math.Clamp(spot.Y - h - 4, 0, Math.Max(0, bounds.Height - h)), w, h);
            var corner = Math.Min(h / 2, 10);
            dc.DrawRoundedRectangle(PlateFill, PlateEdge, plate, corner, corner);
            dc.DrawEllipse(BrushOf(a.Color), null, new Point(plate.X + 11, plate.Y + h / 2), 3.2, 3.2);
            dc.DrawText(text, new Point(plate.X + 19, plate.Y + 4));
        }
        return visual;
    }

    // An unfrozen copy of the pen whose dash covers the whole stroke and slides into place: the stroke grows from its start.
    private static Pen DrawOn(Pen basis, double length, TimeSpan duration, IEasingFunction ease)
    {
        var pen = basis.Clone();
        var dash = length / pen.Thickness + 1;
        pen.DashCap = PenLineCap.Round;
        pen.DashStyle = new DashStyle([dash, dash], dash);
        pen.DashStyle.BeginAnimation(DashStyle.OffsetProperty, new DoubleAnimation(dash, 0, duration) { EasingFunction = ease });
        return pen;
    }

    private static Pen MarkerOf(string hex, double thickness)
    {
        var pen = new Pen(BrushOf(hex, 72), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }
}
