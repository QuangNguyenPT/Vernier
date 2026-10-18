using Vernier.Core.Geometry;

namespace Vernier.Core.Annotations;

/// <summary>What was under one tutor mark when it was drawn: a grey patch at half scale, and where it was (physical pixels).</summary>
/// <param name="Patch">Null when there was nothing to recognise (a blank area): such a mark can only follow its window.</param>
public sealed record MarkAnchor(Guid MarkId, PxRect PatchArea, GrayImage? Patch)
{
    /// <summary>Patches are cut at most this big (half-scale pixels), from the middle of the target: enough to be distinctive, cheap to search.</summary>
    public const int MaxPatchWidth = 160, MaxPatchHeight = 96, MinPatch = 24;

    /// <summary>Cuts the anchor for a mark whose target is <paramref name="target"/> out of <paramref name="view"/>, a half-scale grey image of <paramref name="viewArea"/>.</summary>
    public static MarkAnchor From(Guid id, PxRect target, GrayImage view, PxRect viewArea)
    {
        // Half-scale target with a little margin, at least MinPatch on each side, at most the maximum, centred.
        double cx = (target.X + target.Width / 2 - viewArea.X) / 2, cy = (target.Y + target.Height / 2 - viewArea.Y) / 2;
        var w = (int)Math.Clamp(target.Width / 2 + 8, MinPatch, MaxPatchWidth);
        var h = (int)Math.Clamp(target.Height / 2 + 8, MinPatch, MaxPatchHeight);
        var x = (int)Math.Round(cx - w / 2.0);
        var y = (int)Math.Round(cy - h / 2.0);
        if (x < 0 || y < 0 || x + w > view.Width || y + h > view.Height)
            return new MarkAnchor(id, target, null); // not fully in view: it can only follow its window
        return new MarkAnchor(id, new PxRect(viewArea.X + 2 * x, viewArea.Y + 2 * y, 2 * w, 2 * h), view.Crop(x, y, w, h));
    }
}

/// <param name="Moves">Marks found again elsewhere, with the shift to apply (physical pixels).</param>
/// <param name="Lost">Marks whose content is gone or cannot be told apart from similar content: remove these only.</param>
/// <param name="ClearAll">So much is gone that the screen is a different one: remove every tutor mark.</param>
public sealed record RefitResult(IReadOnlyDictionary<Guid, PxPoint> Moves, IReadOnlySet<Guid> Lost, bool ClearAll);

/// <summary>
/// Decides what happens to the tutor's marks after the screen under them changed, by looking for each mark's own content
/// again: a mark whose content scrolled or reflowed moves with it, a mark whose content is gone is removed alone, and only
/// when most of them are gone is everything cleared. A mark is moved only on a confident, unambiguous match; a mark that
/// points at the wrong thing is worse than no mark, so anything doubtful is removed instead.
/// </summary>
public static class MarkRefit
{
    /// <summary>Share of checkable marks that must be lost before the whole screen counts as changed.</summary>
    public const double ClearAllShare = 0.7;
    /// <summary>How far content is looked for: most of the view's height (scrolling), a little of its width (reflow).</summary>
    public const double SearchHeightShare = 0.5, SearchWidthShare = 0.15;

    /// <param name="view">Half-scale grey image of <paramref name="viewArea"/> as it is now (Vernier's own windows excluded).</param>
    public static RefitResult Decide(IReadOnlyList<MarkAnchor> anchors, GrayImage view, PxRect viewArea, IPatchMatcher matcher)
    {
        var moves = new Dictionary<Guid, PxPoint>();
        var lost = new HashSet<Guid>();
        var checkable = 0;
        foreach (var a in anchors)
        {
            if (a.Patch is not { } patch) continue; // nothing to recognise: it follows its window only
            checkable++;
            int ex = (int)Math.Round((a.PatchArea.X - viewArea.X) / 2), ey = (int)Math.Round((a.PatchArea.Y - viewArea.Y) / 2);
            int rx = (int)(view.Width * SearchWidthShare), ry = (int)(view.Height * SearchHeightShare);
            var found = matcher.Match(view, patch, ex - rx, ey - ry, ex + rx + 1, ey + ry + 1);
            if (found is { Flat: true })
            {
                // Blank: nothing to recognise, so it stays where it is while that spot is still the same blank; anything else there means it is gone.
                if (!StillBlank(view, patch, ex, ey)) lost.Add(a.MarkId);
                continue;
            }
            if (found is not { IsConfident: true } m) { lost.Add(a.MarkId); continue; }
            double dx = 2 * (m.X - ex), dy = 2 * (m.Y - ey);
            if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2) moves[a.MarkId] = new PxPoint(dx, dy);
        }
        var clearAll = checkable > 0 && lost.Count > ClearAllShare * checkable;
        return new RefitResult(clearAll ? new Dictionary<Guid, PxPoint>() : moves, lost, clearAll);
    }

    private static bool StillBlank(GrayImage view, GrayImage patch, int x, int y)
    {
        if (x < 0 || y < 0 || x + patch.Width > view.Width || y + patch.Height > view.Height) return false;
        static (double Mean, double Sd) Stats(Func<int, int, byte> at, int w, int h)
        {
            double s = 0, ss = 0;
            for (var j = 0; j < h; j++)
                for (var i = 0; i < w; i++) { var v = at(i, j); s += v; ss += v * v; }
            var n = (double)w * h;
            var mean = s / n;
            return (mean, Math.Sqrt(Math.Max(0, ss / n - mean * mean)));
        }
        var then = Stats(patch.At, patch.Width, patch.Height);
        var now = Stats((i, j) => view.At(x + i, y + j), patch.Width, patch.Height);
        return now.Sd < 6 && Math.Abs(now.Mean - then.Mean) < 12;
    }
}
