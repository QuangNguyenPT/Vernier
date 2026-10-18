namespace Vernier.Core.Geometry;

/// <summary>
/// Where the drawn lines of a graph are in a screenshot: dark or strongly coloured pixels (the curve, the axes), not the
/// pale grid. Used to put the model's reading of a graph onto the real curve instead of trusting it to the pixel.
/// </summary>
public sealed class InkMap(byte[] bgra, int width, int height)
{
    public int Width => width;
    public int Height => height;

    public bool IsInk(int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
        var i = (y * width + x) * 4;
        int b = bgra[i], g = bgra[i + 1], r = bgra[i + 2];
        var luma = (r * 77 + g * 150 + b * 29) >> 8;
        var chroma = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        return luma < 105 || (chroma > 110 && luma < 210); // black or blue/red pen, not the grey grid
    }

    /// <summary>The ink in column <paramref name="x"/> (a line may be a few pixels thick, so neighbours count) nearest to <paramref name="y"/>, as the middle of its run; null when there is none within the radius.</summary>
    public double? NearestInkY(int x, double y, int radius)
    {
        for (var d = 0; d <= radius; d++)
            foreach (var sign in d == 0 ? new[] { 1 } : new[] { 1, -1 })
            {
                var yy = (int)Math.Round(y) + sign * d;
                if (!IsInkNear(x, yy)) continue;
                int top = yy, bottom = yy;
                while (IsInkNear(x, top - 1) && yy - top < 12) top--;
                while (IsInkNear(x, bottom + 1) && bottom - yy < 12) bottom++;
                return (top + bottom) / 2.0;
            }
        return null;
    }

    /// <summary>
    /// Where the curve is in column <paramref name="x"/> when nobody said: the topmost line between <paramref name="top"/> and
    /// <paramref name="bottom"/> that is not the axis at <paramref name="axisY"/> (lines thicker than a few pixels, such as text, are skipped).
    /// </summary>
    public double? FirstCurveY(int x, double top, double bottom, double axisY)
    {
        for (var y = (int)Math.Max(0, top); y <= (int)Math.Min(height - 1, bottom); y++)
        {
            if (!IsInkNear(x, y)) continue;
            var start = y;
            while (y + 1 < height && IsInkNear(x, y + 1)) y++;
            var mid = (start + y) / 2.0;
            if (Math.Abs(mid - axisY) <= 4 || y - start > 10) continue;
            return mid;
        }
        return null;
    }

    private bool IsInkNear(int x, int y) => IsInk(x, y) || IsInk(x - 1, y) || IsInk(x + 1, y);

    /// <summary>Follows the curve across columns from <paramref name="x0"/> to <paramref name="x1"/> starting near <paramref name="yStart"/>; the points where it was found, and the column where it was lost (or null).</summary>
    public (List<PxPoint> Points, int? LostAt) Trace(int x0, int x1, double yStart, int step = 2, int follow = 14, int maxGap = 10)
    {
        var points = new List<PxPoint>();
        var y = yStart;
        var gap = 0;
        var dir = x1 >= x0 ? 1 : -1;
        for (var x = x0; dir > 0 ? x <= x1 : x >= x1; x += dir * step)
        {
            // Steep stretches move several pixels per column: look further the longer the curve was just seen to be moving.
            var found = NearestInkY(x, y, follow + gap * step);
            if (found is null) { if (++gap > maxGap) return (points, x); continue; }
            gap = 0;
            y = found.Value;
            points.Add(new PxPoint(x, y));
        }
        return (points, null);
    }
}
