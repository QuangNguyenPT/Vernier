namespace Vernier.Core.Geometry;

/// <summary>
/// Where the drawn lines of a graph are in a screenshot: pixels far from the page's own brightness (the curve, the axes,
/// even when anti-aliased to mid grey) or strongly coloured, not the pale grid. Used to put the model's reading of a graph
/// onto the real curve instead of trusting it to the pixel.
/// </summary>
public sealed class InkMap
{
    /// <summary>How far from the page brightness a pixel must be to count as drawn. Real worksheets: page 255, grid 182–230, an anti-aliased curve 122–133, axes under 140.</summary>
    public const int Contrast = 95;

    private readonly byte[] _bgra;
    private readonly int _background;

    /// <param name="sample">Where the page is (the learner's window, in these pixels): its brightness is read there, so a dark desktop round a white worksheet does not turn the whole page into ink.</param>
    public InkMap(byte[] bgra, int width, int height, PxRect? sample = null)
    {
        _bgra = bgra;
        Width = width;
        Height = height;
        _background = Background(bgra, width, height, sample is { IsEmpty: false } r ? r.Intersect(new PxRect(0, 0, width, height)) : new PxRect(0, 0, width, height));
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>The page's brightness: the commonest luma over a sparse sample of the image.</summary>
    public int BackgroundLuma => _background;

    public bool IsInk(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return false;
        var i = (y * Width + x) * 4;
        int b = _bgra[i], g = _bgra[i + 1], r = _bgra[i + 2];
        var luma = (r * 77 + g * 150 + b * 29) >> 8;
        var chroma = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        return Math.Abs(luma - _background) > Contrast || (chroma > 110 && Math.Abs(luma - _background) > 45); // pen or pencil, not the grid
    }

    private static int Background(byte[] bgra, int width, int height, PxRect area)
    {
        if (area.IsEmpty) area = new PxRect(0, 0, width, height);
        var histogram = new int[256];
        int x0 = (int)area.X, y0 = (int)area.Y, x1 = (int)Math.Min(width, area.Right), y1 = (int)Math.Min(height, area.Bottom);
        var step = Math.Max(1, (int)Math.Sqrt((double)(x1 - x0) * (y1 - y0) / 20000));
        for (var y = y0; y < y1; y += step)
            for (var x = x0; x < x1; x += step)
            {
                var i = (y * width + x) * 4;
                histogram[(bgra[i + 2] * 77 + bgra[i + 1] * 150 + bgra[i] * 29) >> 8]++;
            }
        return Array.IndexOf(histogram, histogram.Max());
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
        for (var y = (int)Math.Max(0, top); y <= (int)Math.Min(Height - 1, bottom); y++)
        {
            if (!IsInkNear(x, y)) continue;
            var start = y;
            while (y + 1 < Height && IsInkNear(x, y + 1)) y++;
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

/// <summary>
/// The drawn axes of a graph found from the pixels alone, for grids whose labels the screen reader cannot read (an empty
/// grid to sketch on, with only "0" and a symbol such as Fmax): the longest horizontal and vertical lines that meet at
/// a corner. Origin is their crossing; the ends are where each line stops (arrow heads included).
/// </summary>
public sealed record GraphFrame(PxPoint Origin, double XEnd, double YEnd)
{
    public const int MinLength = 120;

    public static GraphFrame? Find(InkMap ink)
    {
        var rows = Runs(ink, horizontal: true);
        var cols = Runs(ink, horizontal: false);
        GraphFrame? best = null;
        var bestScore = 0.0;
        foreach (var (y, x0, x1) in rows)
            foreach (var (x, y0, y1) in cols)
            {
                // An L: the vertical line comes down to the horizontal one at its left end.
                if (Math.Abs(x - x0) > 8 || Math.Abs(y1 - y) > 8) continue;
                var score = (x1 - x0) + (y1 - y0);
                if (score <= bestScore) continue;
                bestScore = score;
                best = new GraphFrame(new PxPoint(x, y), x1, y0);
            }
        return best;
    }

    /// <summary>The column of the tick mark (a short stroke across the x axis) nearest <paramref name="px"/>, within 14 px; null when there is none.</summary>
    public double? SnapXTick(InkMap ink, double px) => Snap(px, c => Count(ink, (int)c, (int)Origin.Y, vertical: true));

    /// <summary>
    /// The last tick mark along the x axis before its arrow head, in the axis's last third: on a sketch grid that is usually
    /// the labelled end of the range (Fmax). Null when there is none.
    /// </summary>
    public double? LastXTick(InkMap ink)
    {
        var length = XEnd - Origin.X;
        for (var c = (int)(XEnd - 14); c > Origin.X + length * 0.66; c--)
            if (Count(ink, c, (int)Origin.Y, vertical: true) >= 3) return c;
        return null;
    }

    /// <summary>The row of the tick mark across the y axis nearest <paramref name="py"/>, within 14 px.</summary>
    public double? SnapYTick(InkMap ink, double py) => Snap(py, r => Count(ink, (int)Origin.X, (int)r, vertical: false));

    private static double? Snap(double at, Func<double, int> marks)
    {
        double? best = null;
        for (var d = 0; d <= 14; d++)
            foreach (var c in d == 0 ? new[] { at } : [at - d, at + d])
                if (best is null && marks(c) >= 3) best = c;
        return best;
    }

    // Ink just off the axis line on either side (past its own thickness), in the column (or row) across it.
    private static int Count(InkMap ink, int along, int axis, bool vertical)
    {
        var n = 0;
        for (var d = 4; d <= 10; d++)
        {
            if (vertical ? ink.IsInk(along, axis + d) || ink.IsInk(along, axis - d) : ink.IsInk(axis + d, along) || ink.IsInk(axis - d, along)) n++;
        }
        return n;
    }

    // Per row (or column): the longest run of ink allowing 2 px gaps, kept when long enough and not the screenshot's own edge.
    private static List<(int At, int Start, int End)> Runs(InkMap ink, bool horizontal)
    {
        int lines = horizontal ? ink.Height : ink.Width, length = horizontal ? ink.Width : ink.Height;
        var found = new List<(int, int, int)>();
        for (var l = 0; l < lines; l++)
        {
            int bestStart = 0, bestEnd = -1, start = -1, gap = 0;
            for (var p = 0; p < length; p++)
            {
                if (horizontal ? ink.IsInk(p, l) : ink.IsInk(l, p)) { if (start < 0) start = p; gap = 0; if (p - start > bestEnd - bestStart) (bestStart, bestEnd) = (start, p); }
                else if (start >= 0 && ++gap > 2) start = -1;
            }
            var run = bestEnd - bestStart;
            if (run >= MinLength && run < length * 0.95 && bestStart > 2 && bestEnd < length - 3) found.Add((l, bestStart, bestEnd));
        }
        return found;
    }
}
