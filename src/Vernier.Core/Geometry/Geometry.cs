namespace Vernier.Core.Geometry;

public readonly record struct PxPoint(double X, double Y);

public readonly record struct PxRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public PxPoint Center => new(X + Width / 2, Y + Height / 2);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(PxPoint p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public bool Contains(PxRect r) => r.X >= X && r.Y >= Y && r.Right <= Right && r.Bottom <= Bottom;

    public PxRect Intersect(PxRect o)
    {
        var x = Math.Max(X, o.X);
        var y = Math.Max(Y, o.Y);
        var r = Math.Min(Right, o.Right);
        var b = Math.Min(Bottom, o.Bottom);
        return r <= x || b <= y ? default : new PxRect(x, y, r - x, b - y);
    }
}

/// <summary>
/// Where a captured image came from. <see cref="SourcePhysical"/> is the captured area in
/// virtual-desktop physical pixels; the image itself may be downscaled to
/// <see cref="ImageWidth"/> x <see cref="ImageHeight"/> before it is sent to a model.
/// All model-supplied coordinates are in image space and must pass through this mapping.
/// </summary>
public sealed record CaptureGeometry(PxRect SourcePhysical, int ImageWidth, int ImageHeight)
{
    public double ScaleX => SourcePhysical.Width / ImageWidth;
    public double ScaleY => SourcePhysical.Height / ImageHeight;

    public PxRect ImageBounds => new(0, 0, ImageWidth, ImageHeight);

    public PxPoint ImageToPhysical(PxPoint p) =>
        new(SourcePhysical.X + p.X * ScaleX, SourcePhysical.Y + p.Y * ScaleY);

    public PxRect ImageToPhysical(PxRect r) =>
        new(SourcePhysical.X + r.X * ScaleX, SourcePhysical.Y + r.Y * ScaleY, r.Width * ScaleX, r.Height * ScaleY);

    public PxPoint PhysicalToImage(PxPoint p) =>
        new((p.X - SourcePhysical.X) / ScaleX, (p.Y - SourcePhysical.Y) / ScaleY);

    /// <summary>
    /// Lower bound on positional error from downscaling alone: half an image pixel, in physical
    /// pixels. Model localisation error comes on top of this and is not knowable here.
    /// </summary>
    public double QuantizationUncertaintyPx => Math.Max(ScaleX, ScaleY) / 2;
}

/// <summary>A monitor in physical pixels with its DPI scale (1.0 = 96 DPI).</summary>
public sealed record MonitorInfo(PxRect BoundsPhysical, double DpiScale, bool IsPrimary);

public static class DpiMath
{
    /// <summary>Physical virtual-desktop point to device-independent units local to a monitor-sized window.</summary>
    public static PxPoint PhysicalToWindowDip(PxPoint physical, MonitorInfo monitor) =>
        new((physical.X - monitor.BoundsPhysical.X) / monitor.DpiScale,
            (physical.Y - monitor.BoundsPhysical.Y) / monitor.DpiScale);

    public static PxPoint WindowDipToPhysical(PxPoint dip, MonitorInfo monitor) =>
        new(monitor.BoundsPhysical.X + dip.X * monitor.DpiScale,
            monitor.BoundsPhysical.Y + dip.Y * monitor.DpiScale);

    /// <summary>Monitor containing the point, else the nearest one (after a monitor is unplugged).</summary>
    public static MonitorInfo? MonitorFor(PxPoint physical, IReadOnlyList<MonitorInfo> monitors)
    {
        MonitorInfo? best = null;
        var bestDist = double.MaxValue;
        foreach (var m in monitors)
        {
            if (m.BoundsPhysical.Contains(physical)) return m;
            var c = m.BoundsPhysical.Center;
            var d = (c.X - physical.X) * (c.X - physical.X) + (c.Y - physical.Y) * (c.Y - physical.Y);
            if (d < bestDist) { bestDist = d; best = m; }
        }
        return best;
    }

    /// <summary>Fit a downscaled image size inside maxEdge while preserving aspect ratio. Never upscales.</summary>
    public static (int Width, int Height) FitWithin(int width, int height, int maxEdge)
    {
        var longest = Math.Max(width, height);
        if (longest <= maxEdge) return (width, height);
        var k = (double)maxEdge / longest;
        return (Math.Max(1, (int)Math.Round(width * k)), Math.Max(1, (int)Math.Round(height * k)));
    }
}

public static class PerceptualHash
{
    /// <summary>
    /// Difference hash over a 9x8 grayscale thumbnail (72 bytes, row-major). Similar frames give
    /// hashes with a small Hamming distance, which is how unchanged screens are skipped.
    /// </summary>
    public static ulong DHash(ReadOnlySpan<byte> gray9x8)
    {
        if (gray9x8.Length != 72) throw new ArgumentException("Expected 9x8 grayscale thumbnail.", nameof(gray9x8));
        ulong hash = 0;
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
                if (gray9x8[y * 9 + x] > gray9x8[y * 9 + x + 1])
                    hash |= 1UL << (y * 8 + x);
        return hash;
    }

    public static int Distance(ulong a, ulong b) => System.Numerics.BitOperations.PopCount(a ^ b);
}

public static class PolylineMath
{
    /// <summary>
    /// Ramer–Douglas–Peucker: drops points that lie within <paramref name="tolerance"/> pixels of
    /// the line through their neighbours. A pen stroke of thousands of points keeps its shape
    /// with a few hundred.
    /// </summary>
    public static IReadOnlyList<PxPoint> Simplify(IReadOnlyList<PxPoint> points, double tolerance)
    {
        if (points.Count < 3) return points;
        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            double worst = 0;
            var index = -1;
            for (var i = first + 1; i < last; i++)
            {
                var d = Distance(points[i], points[first], points[last]);
                if (d > worst) { worst = d; index = i; }
            }
            if (index < 0 || worst <= tolerance) continue;
            keep[index] = true;
            stack.Push((first, index));
            stack.Push((index, last));
        }
        return [.. points.Where((_, i) => keep[i])];
    }

    private static double Distance(PxPoint p, PxPoint a, PxPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var length = dx * dx + dy * dy;
        if (length == 0) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length, 0, 1);
        double x = a.X + t * dx - p.X, y = a.Y + t * dy - p.Y;
        return Math.Sqrt(x * x + y * y);
    }
}

public static class ScreenDiff
{
    /// <summary>
    /// Fraction of cells that differ noticeably between two equal-size grayscale thumbnails.
    /// Used to tell "the thing a mark points at has moved or gone" from a blinking caret.
    /// </summary>
    public static double Fraction(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int tolerance = 24)
    {
        if (a.Length != b.Length || a.Length == 0) return 1;
        var changed = 0;
        for (var i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > tolerance) changed++;
        return (double)changed / a.Length;
    }
}

public static class OverlayPlacement
{
    /// <summary>
    /// Where to put a floating panel so it sits next to its target without covering it: right,
    /// left, below, above, in that order; if nothing fits, the monitor corner farthest from the
    /// target's centre. The result is always fully on the monitor.
    /// </summary>
    public static PxPoint Beside(PxRect target, double width, double height, PxRect monitor, double gap = 12)
    {
        width = Math.Min(width, monitor.Width);
        height = Math.Min(height, monitor.Height);
        var y = Math.Clamp(target.Y, monitor.Y, monitor.Bottom - height);
        var x = Math.Clamp(target.X, monitor.X, monitor.Right - width);
        if (target.Right + gap + width <= monitor.Right) return new PxPoint(target.Right + gap, y);
        if (target.X - gap - width >= monitor.X) return new PxPoint(target.X - gap - width, y);
        if (target.Bottom + gap + height <= monitor.Bottom) return new PxPoint(x, target.Bottom + gap);
        if (target.Y - gap - height >= monitor.Y) return new PxPoint(x, target.Y - gap - height);
        return new PxPoint(
            target.Center.X < monitor.Center.X ? monitor.Right - width : monitor.X,
            target.Center.Y < monitor.Center.Y ? monitor.Bottom - height : monitor.Y);
    }

    /// <summary>Pulls a window back onto the nearest monitor after a display change.</summary>
    public static PxPoint Clamp(PxRect window, PxRect monitor) => new(
        Math.Clamp(window.X, monitor.X, Math.Max(monitor.X, monitor.Right - window.Width)),
        Math.Clamp(window.Y, monitor.Y, Math.Max(monitor.Y, monitor.Bottom - window.Height)));
}
