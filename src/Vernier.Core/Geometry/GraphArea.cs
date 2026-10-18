namespace Vernier.Core.Geometry;

/// <summary>
/// Reads a graph's axes from the tick labels the screen reader found, so the tutor does not have to: a row of numbers
/// evenly spread and increasing to the right is the x axis, a column of numbers increasing upwards is the y axis. Each is
/// fitted with a straight line through every label (not just two), and refused when the labels do not lie on one.
/// </summary>
public static class AxisFinder
{
    public const int MinLabels = 3;

    public static AxisCalibration? Find(IEnumerable<(string Text, PxRect Box)> labels)
    {
        var numbers = labels.Select(l => (Value: Parse(l.Text), l.Box)).Where(l => l.Value is not null).Select(l => (V: l.Value!.Value, B: l.Box)).ToList();
        if (numbers.Count < 2 * MinLabels - 1) return null;
        var x = Best(numbers, along: n => n.B.Center.X, across: n => n.B.Center.Y, increasingWithPixels: true);
        var y = Best(numbers, along: n => n.B.Center.Y, across: n => n.B.Right, increasingWithPixels: false);
        if (x is null || y is null) return null;
        var axes = new AxisCalibration(x.Value.A, x.Value.B, y.Value.A, y.Value.B);
        return axes.Problem() is null ? axes : null;
    }

    private static double? Parse(string text)
    {
        var t = text.Trim().Replace('−', '-').Replace('–', '-').Replace(',', '.');
        return t.Length is > 0 and <= 8 && double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // The largest group of labels lined up across the axis whose values fit px = a·value + b, as two ticks at its ends.
    private static (AxisTick A, AxisTick B)? Best(List<(double V, PxRect B)> numbers, Func<(double V, PxRect B), double> along, Func<(double V, PxRect B), double> across, bool increasingWithPixels)
    {
        (AxisTick, AxisTick)? best = null;
        var bestCount = 0;
        foreach (var seed in numbers)
        {
            var tolerance = Math.Max(6, seed.B.Height * 0.6);
            var group = numbers.Where(n => Math.Abs(across(n) - across(seed)) <= tolerance).OrderBy(along).ToList();
            group = [.. group.GroupBy(n => Math.Round(along(n))).Select(g => g.First())];
            if (group.Count < MinLabels || group.Count <= bestCount) continue;
            // Values must move one way as the pixels do: right for x, up (smaller y) for y.
            var monotonic = group.Zip(group.Skip(1)).All(p => increasingWithPixels ? p.Second.V > p.First.V : p.Second.V < p.First.V);
            if (!monotonic) continue;
            // Least-squares px = a·value + b; every label must sit on it (within 2 % of the span).
            double n = group.Count, sv = group.Sum(g => g.V), sp = group.Sum(along), svv = group.Sum(g => g.V * g.V), svp = group.Sum(g => g.V * along(g));
            var den = n * svv - sv * sv;
            if (Math.Abs(den) < 1e-12) continue;
            var a = (n * svp - sv * sp) / den;
            var b = (sp - a * sv) / n;
            var span = Math.Abs(along(group[^1]) - along(group[0]));
            if (span < AxisCalibration.MinTickSpacingPx || group.Any(g => Math.Abs(a * g.V + b - along(g)) > Math.Max(3, span * 0.02))) continue;
            double v0 = group[0].V, v1 = group[^1].V;
            best = (new AxisTick(a * v0 + b, v0), new AxisTick(a * v1 + b, v1));
            bestCount = group.Count;
        }
        return best;
    }
}

/// <summary>A tick read off a graph's axis: where it is in the screenshot (pixels along that axis) and the value printed there.</summary>
public readonly record struct AxisTick(double Px, double Value);

/// <summary>
/// A linear graph read from two ticks per axis, mapping data (x, y) to screenshot pixels. The reading is the model's, so it
/// is checked for what code can check (ticks apart, values different) and the area it gives is reported as approximate.
/// </summary>
public sealed record AxisCalibration(AxisTick X0, AxisTick X1, AxisTick Y0, AxisTick Y1)
{
    /// <summary>Ticks closer than this in the screenshot are too close to read a scale from.</summary>
    public const double MinTickSpacingPx = 20;

    /// <summary>Why this reading cannot be used, or null.</summary>
    public string? Problem()
    {
        foreach (var (name, a, b) in new[] { ("x", X0, X1), ("y", Y0, Y1) })
        {
            if (!double.IsFinite(a.Px) || !double.IsFinite(b.Px) || !double.IsFinite(a.Value) || !double.IsFinite(b.Value)) return $"the {name} ticks must be numbers";
            if (Math.Abs(a.Px - b.Px) < MinTickSpacingPx) return $"the two {name} ticks are only {Math.Abs(a.Px - b.Px):0} px apart; pick ticks at least {MinTickSpacingPx:0} px apart";
            if (a.Value == b.Value) return $"the two {name} ticks have the same value";
        }
        return null;
    }

    public PxPoint ToImage(double x, double y) =>
        new(X0.Px + (x - X0.Value) * (X1.Px - X0.Px) / (X1.Value - X0.Value), Y0.Px + (y - Y0.Value) * (Y1.Px - Y0.Px) / (Y1.Value - Y0.Value));

    /// <summary>The data values at a screenshot position (the inverse of <see cref="ToImage"/>).</summary>
    public (double X, double Y) ToData(double px, double py) =>
        (X0.Value + (px - X0.Px) * (X1.Value - X0.Value) / (X1.Px - X0.Px), Y0.Value + (py - Y0.Px) * (Y1.Value - Y0.Value) / (Y1.Px - Y0.Px));

    /// <summary>The area of a polygon given in data units (absolute shoelace), in x-unit × y-unit.</summary>
    public static double Area(IReadOnlyList<(double X, double Y)> v)
    {
        var twice = 0.0;
        for (var i = 0; i < v.Count; i++)
        {
            var (x1, y1) = v[i];
            var (x2, y2) = v[(i + 1) % v.Count];
            twice += x1 * y2 - x2 * y1;
        }
        return Math.Abs(twice) / 2;
    }

    /// <summary>Whether two non-adjacent edges cross: the shoelace area of a self-crossing outline is not its area.</summary>
    public static bool SelfIntersects(IReadOnlyList<(double X, double Y)> v)
    {
        var n = v.Count;
        for (var i = 0; i < n; i++)
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue; // these two share a vertex
                if (Cross(v[i], v[(i + 1) % n], v[j], v[(j + 1) % n])) return true;
            }
        return false;

        static double Side((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        static bool Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d) =>
            Side(a, b, c) * Side(a, b, d) < 0 && Side(c, d, a) * Side(c, d, b) < 0;
    }
}
