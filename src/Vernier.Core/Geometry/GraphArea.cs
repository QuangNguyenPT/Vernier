namespace Vernier.Core.Geometry;

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
