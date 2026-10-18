using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Xunit;

namespace Vernier.Tests;

public class FreeSpaceTests
{
    // A worksheet: black text-like detail on white in the given rectangles, plain white elsewhere.
    private static (byte[] Bgra, int W, int H) Page(int w, int h, params PxRect[] detail)
    {
        var bgra = new byte[w * h * 4];
        Array.Fill(bgra, (byte)255);
        foreach (var d in detail)
            for (var y = (int)d.Y; y < Math.Min(h, (int)d.Bottom); y++)
                for (var x = (int)d.X; x < Math.Min(w, (int)d.Right); x++)
                {
                    var ink = (x / 3 + y / 3) % 2 == 0 ? (byte)20 : (byte)240; // fine detail, like text
                    var i = (y * w + x) * 4;
                    bgra[i] = bgra[i + 1] = bgra[i + 2] = ink;
                }
        return (bgra, w, h);
    }

    [Fact]
    public void Plain_cells_are_free_and_detail_is_busy()
    {
        var (bgra, w, h) = Page(960, 600, new PxRect(0, 0, 480, 600));
        var space = FreeSpace.Analyse(bgra, w, h, new PxRect(0, 0, 960, 600), cellPx: 24);
        Assert.True(space.IsBusy(2, 5));
        Assert.False(space.IsBusy(30, 5));
        Assert.True(space.IsBusy(-1, 0)); // off the screen is never free
    }

    [Fact]
    public void A_note_goes_beside_the_problem_on_the_empty_side_not_over_it()
    {
        // The problem fills the left half and the top; the right is empty (the learner writes there).
        var (bgra, w, h) = Page(1920, 1080, new PxRect(0, 0, 900, 1080), new PxRect(900, 0, 1020, 120));
        var space = FreeSpace.Analyse(bgra, w, h, new PxRect(0, 0, 1920, 1080));
        var problem = new PxRect(100, 300, 500, 300);
        var placed = space.Place((560, 420), near: problem);
        Assert.False(placed.Covers);
        Assert.True(placed.Rect.X >= 900, $"the card was put at {placed.Rect}");
        Assert.True(placed.Rect.Y >= 100, "and clear of the header");
        Assert.Equal("to the right of it", placed.Where);
        Assert.True(new PxRect(0, 0, 1920, 1080).Contains(new PxPoint(placed.Rect.Right, placed.Rect.Bottom)));
    }

    [Theory]
    [InlineData(Side.Below, "below it")]
    [InlineData(Side.Left, "to the left of it")]
    public void The_side_asked_for_is_used_when_it_has_room(Side side, string where)
    {
        var (bgra, w, h) = Page(1600, 1000, new PxRect(700, 400, 200, 200));
        var space = FreeSpace.Analyse(bgra, w, h, new PxRect(0, 0, 1600, 1000));
        var placed = space.Place((300, 200), near: new PxRect(700, 400, 200, 200), side: side);
        Assert.False(placed.Covers);
        Assert.Equal(where, placed.Where);
    }

    [Fact]
    public void Known_things_are_kept_clear_and_a_full_screen_is_covered_as_little_as_possible()
    {
        var (bgra, w, h) = Page(1000, 600);
        var space = FreeSpace.Analyse(bgra, w, h, new PxRect(0, 0, 1000, 600));
        space.Occupy(new PxRect(0, 0, 1000, 300)); // the ask bar and what it sits over
        var below = space.Place((400, 200));
        Assert.False(below.Covers);
        Assert.True(below.Rect.Y >= 300);

        var (full, fw, fh) = Page(1000, 600, new PxRect(0, 0, 1000, 600));
        var crowded = FreeSpace.Analyse(full, fw, fh, new PxRect(0, 0, 1000, 600));
        var placed = crowded.Place((400, 200));
        Assert.True(placed.Covers); // nowhere is empty: it says so
        Assert.InRange(placed.Rect.X, 0, 600);
    }

    [Fact]
    public void A_card_bigger_than_the_screen_is_cut_down_to_fit_it_not_placed_off_it()
    {
        var space = new FreeSpace(new PxRect(0, 0, 400, 300), 24);
        var placed = space.Place((2000, 2000));
        Assert.True(placed.Rect.Width <= 400 && placed.Rect.Height <= 300);
    }

    [Fact]
    public void A_notes_height_estimate_grows_with_what_is_written()
    {
        var short1 = new NoteContent("Step 1", [new NoteLine(NoteLineKind.Step, "Resolve along the slope")]);
        var long1 = new NoteContent("Step 1", [new NoteLine(NoteLineKind.Step, new string('x', 400), @"F = ma"), new NoteLine(NoteLineKind.Result, "a = 2.8", @"a = 2.8\,\mathrm{m/s^2}")]);
        Assert.True(long1.EstimateHeight(420) > short1.EstimateHeight(420) * 2);
    }
}
