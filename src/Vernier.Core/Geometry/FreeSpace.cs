namespace Vernier.Core.Geometry;

public enum Side { Auto, Right, Below, Left, Above }

/// <summary>
/// Where there is room on a screen. The captured pixels are divided into cells; a cell is busy when it holds detail
/// (text, lines, diagrams: a wide spread of brightness) and free when it is plain. Known things (located text, existing
/// marks, the ask bar) can be marked busy too. <see cref="Place"/> then finds where a card of a given size fits, near
/// what it is about, without covering the work.
/// </summary>
public sealed class FreeSpace
{
    public int Cols { get; }
    public int Rows { get; }
    public int CellPx { get; }
    public PxRect Area { get; }
    private readonly bool[] _busy;

    public FreeSpace(PxRect area, int cellPx)
    {
        Area = area;
        CellPx = Math.Max(8, cellPx);
        Cols = Math.Max(1, (int)Math.Ceiling(area.Width / CellPx));
        Rows = Math.Max(1, (int)Math.Ceiling(area.Height / CellPx));
        _busy = new bool[Cols * Rows];
    }

    public bool IsBusy(int col, int row) => col < 0 || row < 0 || col >= Cols || row >= Rows || _busy[row * Cols + col];

    public int BusyCount => _busy.Count(b => b);

    /// <summary>Marks cells busy from BGRA pixels covering <paramref name="area"/> (the pixels may be at a smaller scale than the screen).</summary>
    public static FreeSpace Analyse(byte[] bgra, int width, int height, PxRect area, int cellPx = 24, int contrast = 36)
    {
        var space = new FreeSpace(area, cellPx);
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4) return space;
        double sx = width / area.Width, sy = height / area.Height;
        for (var row = 0; row < space.Rows; row++)
            for (var col = 0; col < space.Cols; col++)
            {
                int x0 = (int)(col * space.CellPx * sx), x1 = Math.Min(width, (int)((col + 1) * space.CellPx * sx));
                int y0 = (int)(row * space.CellPx * sy), y1 = Math.Min(height, (int)((row + 1) * space.CellPx * sy));
                int lo = 255, hi = 0;
                var step = Math.Max(1, (int)(Math.Min(x1 - x0, y1 - y0) / 8));
                for (var y = y0; y < y1; y += step)
                    for (var x = x0; x < x1; x += step)
                    {
                        var i = (y * width + x) * 4;
                        var luma = (bgra[i + 2] * 77 + bgra[i + 1] * 150 + bgra[i] * 29) >> 8;
                        if (luma < lo) lo = luma;
                        if (luma > hi) hi = luma;
                    }
                if (hi - lo > contrast) space._busy[row * space.Cols + col] = true;
            }
        return space;
    }

    /// <summary>Marks the cells a rectangle touches busy, with <paramref name="padPx"/> of room around it.</summary>
    public void Occupy(PxRect rect, double padPx = 0)
    {
        if (rect.IsEmpty) return;
        int c0 = (int)Math.Floor((rect.X - padPx - Area.X) / CellPx), c1 = (int)Math.Floor((rect.Right + padPx - Area.X) / CellPx);
        int r0 = (int)Math.Floor((rect.Y - padPx - Area.Y) / CellPx), r1 = (int)Math.Floor((rect.Bottom + padPx - Area.Y) / CellPx);
        for (var r = Math.Max(0, r0); r <= Math.Min(Rows - 1, r1); r++)
            for (var c = Math.Max(0, c0); c <= Math.Min(Cols - 1, c1); c++) _busy[r * Cols + c] = true;
    }

    /// <summary>Busy cells spread one cell outwards, so a card never touches the work it avoids.</summary>
    public FreeSpace Dilated()
    {
        var copy = new FreeSpace(Area, CellPx);
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                if (_busy[r * Cols + c])
                    for (var dr = -1; dr <= 1; dr++)
                        for (var dc = -1; dc <= 1; dc++)
                        {
                            int rr = r + dr, cc = c + dc;
                            if (rr >= 0 && cc >= 0 && rr < Rows && cc < Cols) copy._busy[rr * Cols + cc] = true;
                        }
        return copy;
    }

    public sealed record Placement(PxRect Rect, double BusyFraction, string Where)
    {
        /// <summary>True when the card had to cover some of the work: nowhere was empty enough.</summary>
        public bool Covers => BusyFraction > 0;
    }

    /// <summary>
    /// The best place for a card <paramref name="size"/> pixels big: wholly on free cells when possible, as near the
    /// <paramref name="near"/> region as it can be, preferring the given side of it (the right is where people write).
    /// Falls back to the least busy spot, never off the screen. Cells are searched with a summed-area table.
    /// </summary>
    public Placement Place((double Width, double Height) size, PxRect? near = null, Side side = Side.Auto, int marginCells = 1)
    {
        var grid = Dilated();
        int w = Math.Min(Cols, (int)Math.Ceiling(size.Width / CellPx)), h = Math.Min(Rows, (int)Math.Ceiling(size.Height / CellPx));
        // sums[r, c]: busy cells above and left of (r, c).
        var sums = new int[(Rows + 1) * (Cols + 1)];
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                sums[(r + 1) * (Cols + 1) + c + 1] = sums[r * (Cols + 1) + c + 1] + sums[(r + 1) * (Cols + 1) + c] - sums[r * (Cols + 1) + c] + (grid._busy[r * Cols + c] ? 1 : 0);
        int Busy(int c0, int r0) =>
            sums[(r0 + h) * (Cols + 1) + c0 + w] - sums[r0 * (Cols + 1) + c0 + w] - sums[(r0 + h) * (Cols + 1) + c0] + sums[r0 * (Cols + 1) + c0];

        var anchor = near is { } n ? new PxPoint((n.X + n.Width / 2 - Area.X) / CellPx, (n.Y + n.Height / 2 - Area.Y) / CellPx) : new PxPoint(Cols / 2.0, Rows / 2.0);
        double? aLeft = near is { } nl ? (nl.X - Area.X) / CellPx : null, aRight = near is { } nr ? (nr.Right - Area.X) / CellPx : null;
        double? aTop = near is { } nt ? (nt.Y - Area.Y) / CellPx : null, aBottom = near is { } nb ? (nb.Bottom - Area.Y) / CellPx : null;

        (int C, int R, int Busy, double Cost)? best = null;
        var m = Math.Min(marginCells, Math.Max(0, (Cols - w) / 2));
        for (var r0 = Math.Min(m, Rows - h); r0 <= Rows - h - Math.Min(m, Rows - h); r0++)
            for (var c0 = Math.Min(m, Cols - w); c0 <= Cols - w - Math.Min(m, Cols - w); c0++)
            {
                var busy = Busy(c0, r0);
                double cx = c0 + w / 2.0, cy = r0 + h / 2.0;
                var cost = Math.Sqrt((cx - anchor.X) * (cx - anchor.X) + (cy - anchor.Y) * (cy - anchor.Y));
                if (side != Side.Auto && near is not null)
                {
                    var onSide = side switch
                    {
                        Side.Right => c0 >= aRight - 0.5,
                        Side.Left => c0 + w <= aLeft + 0.5,
                        Side.Below => r0 >= aBottom - 0.5,
                        _ => r0 + h <= aTop + 0.5,
                    };
                    if (!onSide) cost += 1000; // another side is allowed, but only when this one has no room
                }
                else if (near is not null && aRight is { } ar && c0 >= ar - 0.5) cost -= 0.5; // writing goes beside the work, on its right, when it fits
                // A window that covers work costs far more than any distance.
                var total = cost + busy * 10000.0;
                if (best is null || total < best.Value.Cost) best = (c0, r0, busy, total);
            }
        var pick = best ?? (0, 0, h * w, 0);
        var rect = new PxRect(Area.X + pick.Item1 * CellPx, Area.Y + pick.Item2 * CellPx, Math.Min(size.Width, Area.Width), Math.Min(size.Height, Area.Height));
        var fraction = w * h == 0 ? 0 : (double)pick.Item3 / (w * h);
        var where = near is null ? "on free space" : rect.X >= (near.Value.Right - CellPx) ? "to the right of it" : rect.Right <= near.Value.X + CellPx ? "to the left of it"
            : rect.Y >= near.Value.Bottom - CellPx ? "below it" : rect.Bottom <= near.Value.Y + CellPx ? "above it" : "beside it";
        return new Placement(rect, fraction, where);
    }
}
