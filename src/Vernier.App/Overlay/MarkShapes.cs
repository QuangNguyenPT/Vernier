using System.Windows;
using System.Windows.Media;

namespace Vernier.App;

/// <summary>
/// The shapes of the tutor's marks, drawn the way a hand draws them on paper: smooth curves with a little life in them,
/// never a hard rectangle. Every shape is a frozen geometry and is deterministic for its seed (the mark's id), so a mark
/// rebuilt at another scale is the same mark.
/// </summary>
internal static class MarkShapes
{
    /// <summary>A smooth curve through the points (uniform Catmull-Rom as cubic Béziers): no corners where the samples join.</summary>
    public static StreamGeometry Smooth(IReadOnlyList<Point> p, bool closed = false)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(p[0], false, closed);
            if (p.Count == 2) ctx.LineTo(p[1], true, true);
            else if (p.Count > 2)
            {
                var n = p.Count;
                Point At(int k) => closed ? p[(k % n + n) % n] : p[Math.Clamp(k, 0, n - 1)];
                var controls = new List<Point>((closed ? n : n - 1) * 3);
                for (var i = 0; i < (closed ? n : n - 1); i++)
                {
                    Point p0 = At(i - 1), p1 = At(i), p2 = At(i + 1), p3 = At(i + 2);
                    controls.Add(p1 + (p2 - p0) / 6);
                    controls.Add(p2 - (p3 - p1) / 6);
                    controls.Add(p2);
                }
                ctx.PolyBezierTo(controls, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    /// <summary>
    /// A loop drawn round the box like a marker circling a word: it passes outside every corner, starts upper left and
    /// overshoots its start a little instead of closing exactly. Squat targets get an ellipse; long ones a flatter, squarer
    /// superellipse, so a loop round a line of text hugs it instead of ballooning.
    /// </summary>
    public static Geometry Loop(Rect box, int seed)
    {
        var rng = new Random(seed);
        var aspect = Math.Max(box.Width, box.Height) / Math.Max(1, Math.Min(box.Width, box.Height));
        var n = Math.Clamp(2 + (aspect - 1) * 0.3, 2, 2.8); // never as square as a box
        var k = Math.Pow(2, 1 / n); // |x|^n + |y|^n = 1 through the box corners
        double cx = box.X + box.Width / 2, cy = box.Y + box.Height / 2, rx = box.Width / 2 * k + 4, ry = box.Height / 2 * k + 4;
        double phaseA = rng.NextDouble() * Math.Tau, phaseB = rng.NextDouble() * Math.Tau, start = -Math.PI * (0.62 + rng.NextDouble() * 0.12);
        const int steps = 44;
        const double overshoot = 0.34;
        var points = new Point[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var f = (double)i / steps;
            var t = start + (Math.Tau + overshoot) * f;
            var r = (0.975 + 0.06 * f) * (1 + 0.018 * Math.Sin(2 * t + phaseA) + 0.011 * Math.Sin(3 * t + phaseB));
            double c = Math.Cos(t), s = Math.Sin(t);
            points[i] = new Point(cx + rx * r * Math.Sign(c) * Math.Pow(Math.Abs(c), 2 / n), cy + ry * r * Math.Sign(s) * Math.Pow(Math.Abs(s), 2 / n));
        }
        return Smooth(points);
    }

    /// <summary>The centre line of a highlighter swipe across the box; drawn with a wide translucent round-capped pen of <paramref name="thickness"/>.</summary>
    public static Geometry Swipe(Rect box, int seed, out double thickness)
    {
        var rng = new Random(seed);
        thickness = box.Height + 6;
        var inset = Math.Min(thickness / 2 - 3, box.Width / 2 - 1); // the round caps reach past the ends
        double y = box.Y + box.Height / 2, x0 = box.Left + inset, x1 = box.Right - inset;
        if (x1 - x0 < 2) (x0, x1) = (x0 - 1, x0 + 1);
        double Lift() => (rng.NextDouble() - 0.5) * Math.Min(2.4, box.Height * 0.12);
        return Smooth([new Point(x0, y + Lift()), new Point(x0 + (x1 - x0) / 3, y + Lift()), new Point(x0 + (x1 - x0) * 2 / 3, y + Lift()), new Point(x1, y + Lift())]);
    }

    /// <summary>An underline that sags a touch and lifts at its end, the way a quick pen stroke does.</summary>
    public static Geometry Underline(Rect box, int seed)
    {
        var rng = new Random(seed);
        double y = box.Bottom + 3, w = box.Width, sag = Math.Min(2.2, w * 0.02) * (0.7 + rng.NextDouble() * 0.6);
        return Smooth([new Point(box.Left - 2, y), new Point(box.Left + w * 0.35, y + sag), new Point(box.Left + w * 0.7, y + sag * 0.8), new Point(box.Right + 2, y - sag * 0.6)]);
    }

    /// <summary>A curly brace down the left side of the box, its point facing away from what it groups.</summary>
    public static Geometry Brace(Rect box)
    {
        double x = box.Left, top = box.Top, bottom = box.Bottom, mid = (top + bottom) / 2;
        var d = Math.Clamp(box.Height / 6, 3, 9);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(x + d, top), false, false);
            ctx.QuadraticBezierTo(new Point(x, top), new Point(x, top + d), true, true);
            ctx.LineTo(new Point(x, mid - d), true, true);
            ctx.QuadraticBezierTo(new Point(x, mid), new Point(x - d, mid), true, true);
            ctx.QuadraticBezierTo(new Point(x, mid), new Point(x, mid + d), true, true);
            ctx.LineTo(new Point(x, bottom - d), true, true);
            ctx.QuadraticBezierTo(new Point(x, bottom), new Point(x + d, bottom), true, true);
        }
        g.Freeze();
        return g;
    }

    /// <summary>An arrow with a gently bowed shaft and an open head aligned to where the shaft arrives.</summary>
    public static Geometry Arrow(Point from, Point to, int seed)
    {
        var shaft = to - from;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(from, false, false);
            if (shaft.Length < 1) { ctx.LineTo(to, true, true); }
            else
            {
                var normal = new Vector(-shaft.Y, shaft.X) / shaft.Length;
                var bend = Math.Min(36, shaft.Length * 0.12) * (seed % 2 == 0 ? 1 : -1);
                Point c1 = from + shaft / 3 + normal * bend, c2 = from + shaft * 2 / 3 + normal * bend * 0.75;
                ctx.BezierTo(c1, c2, to, true, true);
                var arrive = to - c2;
                arrive.Normalize();
                var side = new Vector(-arrive.Y, arrive.X);
                const double length = 13, spread = 0.48; // tan of the head's half-angle
                ctx.BeginFigure(to - arrive * length + side * length * spread, false, false);
                ctx.PolyLineTo([to, to - arrive * length - side * length * spread], true, true);
            }
        }
        g.Freeze();
        return g;
    }

    /// <summary>The length of a geometry's outline in DIPs (for the draw-on reveal).</summary>
    public static double Length(Geometry g)
    {
        var total = 0.0;
        // WPF flattens only filled figures and a mark's strokes are not filled, so each figure is marked filled first
        // (filling changes nothing about the outline's length).
        foreach (var source in PathGeometry.CreateFromGeometry(g).Figures)
        {
            source.IsFilled = true;
            var figure = source.GetFlattenedPathFigure(0.5, ToleranceType.Absolute);
            var last = figure.StartPoint;
            foreach (var segment in figure.Segments)
                foreach (var p in segment is PolyLineSegment poly ? poly.Points : segment is LineSegment line ? [line.Point] : (IEnumerable<Point>)[])
                {
                    total += (p - last).Length;
                    last = p;
                }
        }
        return total;
    }
}
