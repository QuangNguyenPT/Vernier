namespace Vernier.Core.Geometry;

/// <summary>An 8-bit grey image, row-major with no padding.</summary>
public sealed record GrayImage(byte[] Pixels, int Width, int Height)
{
    public byte At(int x, int y) => Pixels[y * Width + x];

    /// <summary>A copy of the rectangle (clamped to the image).</summary>
    public GrayImage Crop(int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Width); y = Math.Clamp(y, 0, Height);
        w = Math.Clamp(w, 0, Width - x); h = Math.Clamp(h, 0, Height - y);
        var px = new byte[w * h];
        for (var j = 0; j < h; j++) Array.Copy(Pixels, (y + j) * Width + x, px, j * w, w);
        return new GrayImage(px, w, h);
    }

    /// <summary>Grey from BGRA, halved with a 2x2 box (the scale marks are matched at).</summary>
    public static GrayImage HalfFromBgra(byte[] bgra, int width, int height, int stride)
    {
        int w = width / 2, h = height / 2;
        var px = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var sum = 0;
                for (var dy = 0; dy < 2; dy++)
                    for (var dx = 0; dx < 2; dx++)
                    {
                        var i = (2 * y + dy) * stride + (2 * x + dx) * 4;
                        sum += (bgra[i] * 29 + bgra[i + 1] * 150 + bgra[i + 2] * 77) >> 8;
                    }
                px[y * w + x] = (byte)((sum + 2) / 4);
            }
        return new GrayImage(px, w, h);
    }
}

/// <param name="X">Best top-left position of the patch in the image.</param>
/// <param name="Score">Zero-mean normalised cross-correlation there, -1..1.</param>
/// <param name="Second">Best coarse score away from the best peak; close to <paramref name="Score"/> means ambiguous (repeated content).</param>
/// <param name="Flat">The patch has too little texture to be recognised at all.</param>
public readonly record struct PatchMatch(int X, int Y, double Score, double Second, bool Flat)
{
    /// <summary>Found, and clearly better than anywhere else: safe to move a mark there.</summary>
    public bool IsConfident => !Flat && Score >= MinScore && Score - Second >= MinMargin;

    public const double MinScore = 0.8, MinMargin = 0.12;
}

/// <summary>Finds a patch in an image. Implemented natively (vn_match) and in managed code (<see cref="PatchMatcher"/>).</summary>
public interface IPatchMatcher
{
    /// <param name="left">Candidate top-left positions are [left, right) x [top, bottom), clamped so the patch fits.</param>
    /// <returns>Null when the search is impossible (patch bigger than the image, empty range).</returns>
    PatchMatch? Match(GrayImage image, GrayImage patch, int left, int top, int right, int bottom);
}

/// <summary>
/// The managed matcher: the same algorithm as native/Vernier.Native/match.cpp, operation for operation, so both give the
/// same answer (a test compares them). Coarse full search at half scale, refinement at full scale round the best coarse spot.
/// </summary>
public sealed class PatchMatcher : IPatchMatcher
{
    public static readonly PatchMatcher Instance = new();

    public PatchMatch? Match(GrayImage image, GrayImage patch, int left, int top, int right, int bottom)
    {
        int iw = image.Width, ih = image.Height, pw = patch.Width, ph = patch.Height;
        if (pw < 8 || ph < 8 || pw > iw || ph > ih) return null;
        var fine = Stats(patch.Pixels, pw, ph);
        if (fine.Var < 36.0 * pw * ph) return new PatchMatch(0, 0, 0, 0, Flat: true);

        int x0 = Math.Max(0, left), y0 = Math.Max(0, top), x1 = Math.Min(iw - pw + 1, right), y1 = Math.Min(ih - ph + 1, bottom);
        if (x0 >= x1 || y0 >= y1) return null;

        var ci = Half(image);
        var cp = Half(patch);
        var coarse = Stats(cp.Pixels, cp.Width, cp.Height);
        int cx0 = x0 / 2, cy0 = y0 / 2, cx1 = Math.Min(ci.Width - cp.Width + 1, (x1 + 1) / 2), cy1 = Math.Min(ci.Height - cp.Height + 1, (y1 + 1) / 2);
        if (cx0 >= cx1 || cy0 >= cy1 || coarse.Var <= 0) return null;
        var cw = cx1 - cx0;
        var scores = new double[cw * (cy1 - cy0)];
        int bx = cx0, by = cy0;
        var best = -2.0;
        for (var y = cy0; y < cy1; y++)
            for (var x = cx0; x < cx1; x++)
            {
                var s = Score(ci, cp, x, y, coarse);
                scores[(y - cy0) * cw + (x - cx0)] = s;
                if (s > best) { best = s; bx = x; by = y; }
            }
        var r = Math.Max(2, Math.Min(cp.Width, cp.Height) / 2);
        var second = -1.0;
        for (var y = cy0; y < cy1; y++)
            for (var x = cx0; x < cx1; x++)
                if (Math.Abs(x - bx) > r || Math.Abs(y - by) > r) second = Math.Max(second, scores[(y - cy0) * cw + (x - cx0)]);

        int fx = 2 * bx, fy = 2 * by;
        var fbest = -2.0;
        for (var y = Math.Max(y0, 2 * by - 3); y <= Math.Min(y1 - 1, 2 * by + 3); y++)
            for (var x = Math.Max(x0, 2 * bx - 3); x <= Math.Min(x1 - 1, 2 * bx + 3); x++)
            {
                var s = Score(image, patch, x, y, fine);
                if (s > fbest) { fbest = s; fx = x; fy = y; }
            }
        // The second-best peak was scored at the coarse scale; put it on the fine scale so the two can be compared.
        return new PatchMatch(fx, fy, fbest, best > 0 ? second * fbest / best : second, false);
    }

    private readonly record struct PatchStats(double Sum, double Var);

    private static PatchStats Stats(byte[] p, int pw, int ph)
    {
        long s = 0, ss = 0;
        for (var i = 0; i < pw * ph; i++) { s += p[i]; ss += (long)p[i] * p[i]; }
        var n = (double)pw * ph;
        return new PatchStats(s, ss - (double)s * s / n);
    }

    private static double Score(GrayImage img, GrayImage p, int x, int y, PatchStats patch)
    {
        long s = 0, ss = 0, sp = 0;
        int iw = img.Width, pw = p.Width, ph = p.Height;
        var ipx = img.Pixels;
        var ppx = p.Pixels;
        for (var j = 0; j < ph; j++)
        {
            int row = (y + j) * iw + x, prow = j * pw, rs = 0, rss = 0, rsp = 0;
            for (var i = 0; i < pw; i++)
            {
                int v = ipx[row + i];
                rs += v;
                rss += v * v;
                rsp += v * ppx[prow + i];
            }
            s += rs; ss += rss; sp += rsp;
        }
        var n = (double)pw * ph;
        var varI = ss - (double)s * s / n;
        if (varI < 4.0 * n) return 0.0;
        return (sp - (double)s * patch.Sum / n) / Math.Sqrt(varI * patch.Var);
    }

    private static GrayImage Half(GrayImage g)
    {
        int w = g.Width / 2, h = g.Height / 2;
        var px = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                int r0 = 2 * y * g.Width + 2 * x, r1 = r0 + g.Width;
                px[y * w + x] = (byte)((g.Pixels[r0] + g.Pixels[r0 + 1] + g.Pixels[r1] + g.Pixels[r1 + 1] + 2) / 4);
            }
        return new GrayImage(px, w, h);
    }
}
