using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vernier.Core.Agents;
using Vernier.Core.Diagrams;

namespace Vernier.App;

/// <summary>
/// Draws a diagram (a 3D scene, the complex plane, a hypercube) once to a bitmap on the same dark card as a note, so it
/// is placed on free space and costs one image per frame. Colour rule: blue is the tutor's subject, green what was worked
/// out, red a warning, grey everything that only helps you see; axes are neutral with their letters in the usual x red,
/// y green, z blue. Nothing here is a hard box: edges are round-capped, planes are tinted, arrowheads are open.
/// </summary>
public static class DiagramRenderer
{
    private static readonly Dictionary<Guid, (NoteRenderer.Card Card, double Scale)> Cache = [];

    public static void Remember(Guid id, NoteRenderer.Card card, double scale)
    {
        lock (Cache)
        {
            if (Cache.Count >= 16) Cache.Remove(Cache.Keys.First());
            Cache[id] = (card, scale);
        }
    }

    public static NoteRenderer.Card CardFor(Guid id, DiagramSpec spec, double widthDip, double scale)
    {
        lock (Cache)
            if (Cache.TryGetValue(id, out var c) && Math.Abs(c.Scale - scale) < 0.01) return c.Card;
        var card = Render(spec, widthDip, scale);
        Remember(id, card, scale);
        return card;
    }

    public static NoteRenderer.Card Render(DiagramSpec spec, double widthDip, double dpiScale)
    {
        var stack = new StackPanel { Margin = new Thickness(18, 12, 14, 12) };
        if (spec.Title is { Length: > 0 } title)
            stack.Children.Add(new TextBlock { Text = TexPlain.Clean(title), FontFamily = new FontFamily("Sitka Subheading, Sitka Heading, Cambria, Cambria Math"), FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Frozen(Palette.Blue), Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new Scene(spec, widthDip - 34, dpiScale));
        foreach (var line in spec.Caption)
            stack.Children.Add(new TextBlock { Text = TexPlain.Clean(line), FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI, Cambria Math, Segoe UI Symbol"), FontSize = 14, Foreground = Frozen(Palette.Green), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        return NoteRenderer.Wrap(NoteRenderer.WithRule(stack), widthDip, dpiScale);
    }

    private static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private static class Palette
    {
        public static readonly Color Blue = Color.FromRgb(0x6C, 0xC4, 0xFF), Green = Color.FromRgb(0x6F, 0xD9, 0x8A), Red = Color.FromRgb(0xFF, 0x8A, 0x8A),
            Soft = Color.FromRgb(0x8E, 0xA0, 0xAE), Ink = Color.FromRgb(0xF4, 0xF7, 0xFA), Axis = Color.FromRgb(0x5E, 0x72, 0x82);
        public static Color Tone(string? tone) => tone switch { "result" => Green, "warn" => Red, "soft" => Soft, _ => Blue };
    }

    /// <summary>The drawing area. Fitted to its content so a diagram of small numbers is as large as one of big numbers.</summary>
    private sealed class Scene : FrameworkElement
    {
        private readonly DiagramSpec _spec;
        private readonly double _w, _h, _dpi;
        private static readonly Typeface Face = new(new FontFamily("Segoe UI Variable Text, Segoe UI, Cambria Math, Segoe UI Symbol"), FontStyles.Italic, FontWeights.SemiBold, FontStretches.Normal);

        public Scene(DiagramSpec spec, double width, double dpi)
        {
            (_spec, _w, _dpi) = (spec, width, dpi);
            _h = Math.Round(width * (spec.Kind == DiagramKind.Complex ? 0.8 : 0.72));
            Width = _w;
            Height = _h;
        }


        protected override void OnRender(DrawingContext dc)
        {
            switch (_spec.Kind)
            {
                case DiagramKind.Space3D: DrawSpace(dc); break;
                case DiagramKind.Complex: DrawComplex(dc); break;
                case DiagramKind.Forces: DrawForces(dc); break;
                default: DrawHypercube(dc); break;
            }
        }

        private Pen PenOf(Color c, double w, bool dashed = false)
        {
            var pen = new Pen(Frozen(c), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            if (dashed) pen.DashStyle = new DashStyle([3, 3], 0);
            pen.Freeze();
            return pen;
        }

        private void Text(DrawingContext dc, string? text, Point at, Color c, double size = 14, bool centre = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var t = new FormattedText(TexPlain.Clean(text), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, size, Frozen(c), _dpi);
            var origin = centre ? new Point(at.X - t.Width / 2, at.Y - t.Height / 2) : new Point(at.X + 3, at.Y - t.Height - 1);
            dc.DrawRoundedRectangle(Frozen(Color.FromArgb(205, 11, 18, 24)), null, new Rect(origin.X - 3, origin.Y - 1, t.Width + 6, t.Height + 2), 4, 4);
            dc.DrawText(t, origin);
        }

        private void Arrow(DrawingContext dc, Point from, Point to, Color c, double w = 2.2)
        {
            var dir = to - from;
            if (dir.Length < 1) return;
            dir.Normalize();
            var n = new Vector(-dir.Y, dir.X);
            dc.DrawLine(PenOf(Color.FromArgb(120, 0, 0, 0), w + 2.5), from, to - dir * 3);
            dc.DrawLine(PenOf(c, w), from, to - dir * 3);
            var head = new StreamGeometry();
            using (var g = head.Open())
            {
                g.BeginFigure(to - dir * 11 + n * 4.6, false, false);
                g.PolyLineTo([to, to - dir * 11 - n * 4.6], true, true);
            }
            head.Freeze();
            dc.DrawGeometry(null, PenOf(c, w), head);
        }

        // ---- 3D: orthographic, z up, a faint floor grid for depth ----
        private void DrawSpace(DrawingContext dc)
        {
            var items = _spec.Items;
            double extent = 1;
            foreach (var it in items)
                foreach (var v in new[] { it.A, it.B ?? [] })
                    foreach (var k in v) extent = Math.Max(extent, Math.Abs(k));
            extent = Math.Ceiling(extent * 1.15);
            // Fit: project the cube [-extent, extent]^3's corners and scale to the area.
            var fit = new List<V3> { new(extent, 0, 0), new(-extent, 0, 0), new(0, extent, 0), new(0, -extent, 0), new(0, 0, extent), new(0, 0, -extent) };
            foreach (var it in items)
            {
                var a = V3.From(it.A);
                var b = it.B is { Length: >= 2 } ? V3.From(it.B) : a;
                switch (it.Type)
                {
                    case "line": { var r = it.Range is { Length: 2 } rg ? rg : [-1, 1]; fit.Add(a + b * r[0]); fit.Add(a + b * r[1]); break; }
                    case "plane": { var h = it.Range is { Length: > 0 } rg ? rg[0] : extent * 0.8; fit.Add(a + new V3(h, h, 0)); fit.Add(a - new V3(h, h, 0)); break; }
                    default: fit.Add(a); fit.Add(b); break;
                }
            }
            var corners = fit.Select(v => Projection.Project(v)).ToList();
            double minX = corners.Min(c => c.X), maxX = corners.Max(c => c.X), minY = corners.Min(c => c.Y), maxY = corners.Max(c => c.Y);
            var scale = Math.Min((_w - 56) / Math.Max(1e-6, maxX - minX), (_h - 40) / Math.Max(1e-6, maxY - minY));
            Point P(V3 v) { var p = Projection.Project(v); return new Point(_w / 2 + (p.X - (minX + maxX) / 2) * scale, _h / 2 - (p.Y - (minY + maxY) / 2) * scale); }

            // Floor grid (z = 0)
            var grid = PenOf(Color.FromArgb(34, 255, 255, 255), 1);
            for (var k = -extent; k <= extent + 1e-9; k += extent / 4)
            {
                dc.DrawLine(grid, P(new V3(k, -extent, 0)), P(new V3(k, extent, 0)));
                dc.DrawLine(grid, P(new V3(-extent, k, 0)), P(new V3(extent, k, 0)));
            }
            // Axes with their letters
            foreach (var (dir, letter, c) in new[] { (new V3(1, 0, 0), "x", Palette.Red), (new V3(0, 1, 0), "y", Palette.Green), (new V3(0, 0, 1), "z", Palette.Blue) })
            {
                dc.DrawLine(PenOf(Palette.Axis, 1.4), P(dir * -extent), P(dir * extent));
                Arrow(dc, P(dir * (extent * 0.96)), P(dir * extent), Palette.Axis, 1.4);
                Text(dc, letter, P(dir * extent), c, 14);
            }
            // Planes first (behind), then lines and segments, then vectors and points on top.
            foreach (var it in items.Where(i => i.Type == "plane"))
            {
                var n = V3.From(it.B);
                var half = it.Range is { Length: > 0 } r ? r[0] : extent * 0.8;
                var u = Math.Abs(n.Z) < 0.9 * n.Length ? V3.Cross(n, new V3(0, 0, 1)) : V3.Cross(n, new V3(1, 0, 0));
                u = u * (half / u.Length);
                var v = V3.Cross(n, u);
                v = v * (half / v.Length);
                var c = V3.From(it.A);
                var quad = new StreamGeometry();
                using (var g = quad.Open())
                {
                    g.BeginFigure(P(c - u - v), true, true);
                    g.PolyLineTo([P(c + u - v), P(c + u + v), P(c - u + v)], true, true);
                }
                quad.Freeze();
                var tone = Palette.Tone(it.Tone);
                dc.DrawGeometry(Frozen(Color.FromArgb(46, tone.R, tone.G, tone.B)), PenOf(Color.FromArgb(150, tone.R, tone.G, tone.B), 1.2), quad);
                Text(dc, it.Label, P(c + u + v), tone, 13);
            }
            foreach (var it in items.Where(i => i.Type is "line" or "segment"))
            {
                var tone = Palette.Tone(it.Tone);
                V3 a, b;
                if (it.Type == "segment") (a, b) = (V3.From(it.A), V3.From(it.B));
                else
                {
                    var d = V3.From(it.B);
                    var range = it.Range is { Length: 2 } rr ? rr : [-1, 1];
                    a = V3.From(it.A) + d * range[0];
                    b = V3.From(it.A) + d * range[1];
                }
                dc.DrawLine(PenOf(Color.FromArgb(110, 0, 0, 0), 4.2), P(a), P(b));
                dc.DrawLine(PenOf(tone, 2.2, it.Type == "segment" && it.Tone == "soft"), P(a), P(b));
                Text(dc, it.Label, P(b), tone, 14);
            }
            foreach (var it in items.Where(i => i.Type == "vector"))
            {
                var from = V3.From(it.A);
                var to = it.B is { Length: >= 2 } ? V3.From(it.B) : default;
                var tone = Palette.Tone(it.Tone);
                // Dashed drop lines to the floor: where the arrow is in space.
                dc.DrawLine(PenOf(Color.FromArgb(70, tone.R, tone.G, tone.B), 1, true), P(to), P(new V3(to.X, to.Y, 0)));
                Arrow(dc, P(from), P(to), tone);
                Text(dc, it.Label, new Point((P(from).X + P(to).X) / 2 + 4, (P(from).Y + P(to).Y) / 2), tone, 14);
            }
            foreach (var it in items.Where(i => i.Type == "point"))
            {
                var at = P(V3.From(it.A));
                var tone = Palette.Tone(it.Tone);
                dc.DrawEllipse(Frozen(Color.FromArgb(120, 0, 0, 0)), null, at, 5.2, 5.2);
                dc.DrawEllipse(Frozen(tone), null, at, 3.6, 3.6);
                Text(dc, it.Label, new Point(at.X + 4, at.Y - 2), tone, 14);
            }
        }

        // ---- the Argand plane ----
        private void DrawComplex(DrawingContext dc)
        {
            double extent = 1;
            foreach (var it in _spec.Items)
                foreach (var k in it.A) extent = Math.Max(extent, Math.Abs(k) + (it.Type == "circle" ? it.B![0] : 0));
            extent = Math.Ceiling(extent * 1.2);
            double cx = _w / 2, cy = _h / 2, s = Math.Min(_w / 2 - 22, _h / 2 - 16) / extent;
            Point P(double re, double im) => new(cx + re * s, cy - im * s);
            var grid = PenOf(Color.FromArgb(34, 255, 255, 255), 1);
            for (var k = -(int)extent; k <= (int)extent; k++)
            {
                dc.DrawLine(grid, P(k, -extent), P(k, extent));
                dc.DrawLine(grid, P(-extent, k), P(extent, k));
            }
            dc.DrawLine(PenOf(Palette.Axis, 1.4), P(-extent, 0), P(extent, 0));
            dc.DrawLine(PenOf(Palette.Axis, 1.4), P(0, -extent), P(0, extent));
            Arrow(dc, P(extent * 0.95, 0), P(extent, 0), Palette.Axis, 1.4);
            Arrow(dc, P(0, extent * 0.95), P(0, extent), Palette.Axis, 1.4);
            Text(dc, "Re", P(extent, 0) + new Vector(-16, 14), Palette.Soft, 13);
            Text(dc, "Im", P(0, extent) + new Vector(8, 16), Palette.Soft, 13);
            foreach (var it in _spec.Items.Where(i => i.Type == "circle"))
            {
                var tone = Palette.Tone(it.Tone ?? "soft");
                dc.DrawEllipse(null, PenOf(tone, 1.6, true), P(it.A[0], it.A[1]), it.B![0] * s, it.B[0] * s);
                Text(dc, it.Label, P(it.A[0] - it.B[0] * 0.6 + it.B[0] * 0.0, it.A[1] - it.B[0] * 0.8), tone, 13);
            }
            foreach (var it in _spec.Items.Where(i => i.Type == "number"))
            {
                var tone = Palette.Tone(it.Tone);
                var at = P(it.A[0], it.A[1]);
                dc.DrawLine(PenOf(Color.FromArgb(80, tone.R, tone.G, tone.B), 1, true), at, P(it.A[0], 0));
                dc.DrawLine(PenOf(Color.FromArgb(80, tone.R, tone.G, tone.B), 1, true), at, P(0, it.A[1]));
                Arrow(dc, P(0, 0), at, tone);
                Text(dc, it.Label, at + new Vector(6, 2), tone, 14);
            }
        }

        // ---- a free-body diagram: the body on its slope, every force from its centre, lengths to scale ----
        private void DrawForces(DrawingContext dc)
        {
            var forces = _spec.Items.Where(i => i.Type == "force").ToList();
            var largest = Math.Max(1e-9, forces.Max(f => f.A[0]));
            double cx = _w / 2, cy = _h / 2 + 4, reach = Math.Min(_w, _h) * 0.40;
            var slope = _spec.Angle * Math.PI / 180;
            var along = new Vector(Math.Cos(slope), -Math.Sin(slope));   // up the slope, on screen (y down)
            var down = new Vector(Math.Sin(slope), Math.Cos(slope));     // into the surface
            var c = new Point(cx, cy);
            const double bw = 54, bh = 34;
            // The surface under the body, and the incline's angle at its foot.
            var foot = c + down * (bh / 2);
            var a = foot - along * (_w * 0.46);
            var b = foot + along * (_w * 0.46);
            dc.DrawLine(PenOf(Palette.Axis, 2), a, b);
            if (Math.Abs(_spec.Angle) > 0.5)
            {
                var low = slope > 0 ? a : b;
                dc.DrawLine(PenOf(Color.FromArgb(120, Palette.Axis.R, Palette.Axis.G, Palette.Axis.B), 1.4, true), low, new Point(low.X + (slope > 0 ? 1 : -1) * _w * 0.6, low.Y));
                Text(dc, $"{DiagramBuilder.Number(Math.Round(_spec.Angle, 1))}°", new Point(low.X + (slope > 0 ? 34 : -64), low.Y - 2), Palette.Soft, 13);
            }
            // The body, turned to sit on the slope.
            dc.PushTransform(new RotateTransform(-_spec.Angle, cx, cy));
            dc.DrawRoundedRectangle(Frozen(Color.FromArgb(70, Palette.Soft.R, Palette.Soft.G, Palette.Soft.B)), PenOf(Palette.Soft, 1.6), new Rect(cx - bw / 2, cy - bh / 2, bw, bh), 6, 6);
            dc.Pop();
            foreach (var f in forces)
            {
                var deg = f.A[1] * Math.PI / 180;
                var length = Math.Max(48, reach * f.A[0] / largest); // always longer than the body, so a small force is still seen
                var tip = c + new Vector(Math.Cos(deg), -Math.Sin(deg)) * length;
                var tone = Palette.Tone(f.Tone);
                Arrow(dc, c, tip, tone, f.Tone == "result" ? 2.8 : 2.2);
                var dir = new Vector(Math.Cos(deg), -Math.Sin(deg));
                // The result often runs along another force: its label goes to the other side of its arrow, not on top of the other label.
                var side = f.Tone == "result" ? new Vector(-dir.Y, dir.X) * 30 : dir * 10;
                Text(dc, f.Label, tip + side + new Vector(-12, 8), tone, 14);
            }
            dc.DrawEllipse(Frozen(Palette.Ink), null, c, 2.6, 2.6);
        }

        // ---- a cube or a tesseract ----
        private void DrawHypercube(DrawingContext dc)
        {
            var (corners, edges) = Projection.Hypercube(_spec.Dim, _spec.Angle);
            var pts = corners.Select(c => Projection.Project(c, -30, 18)).ToList();
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
            var scale = Math.Min((_w - 50) / (maxX - minX), (_h - 30) / (maxY - minY));
            Point P(int i) => new(_w / 2 + (pts[i].X - (minX + maxX) / 2) * scale, _h / 2 - (pts[i].Y - (minY + maxY) / 2) * scale);
            foreach (var (a, b) in edges)
            {
                // The edge that runs along the fourth axis is the one that connects the inner cube to the outer one.
                var along4 = _spec.Dim >= 4 && (a ^ b) == 8;
                var c = along4 ? Palette.Green : Palette.Blue;
                dc.DrawLine(PenOf(Color.FromArgb(110, 0, 0, 0), 3.6), P(a), P(b));
                dc.DrawLine(PenOf(Color.FromArgb(along4 ? (byte)200 : (byte)235, c.R, c.G, c.B), along4 ? 1.4 : 2), P(a), P(b));
            }
            foreach (var i in Enumerable.Range(0, corners.Length)) dc.DrawEllipse(Frozen(Palette.Ink), null, P(i), 2.4, 2.4);
        }
    }
}
