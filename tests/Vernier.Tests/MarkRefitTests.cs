using Vernier.App.Platform;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Xunit;

namespace Vernier.Tests;

/// <summary>Marks follow their own content when the screen changes, and are removed only when their content is gone.</summary>
public class MarkRefitTests
{
    // A textured "page" (half-scale grey): random blocks, so every area is distinctive, like text and diagrams.
    private static GrayImage Page(int w, int h, int seed, int shiftY = 0)
    {
        var rng = new Random(seed);
        var tall = h + 400;
        var blocks = new byte[(w / 4 + 1) * (tall / 4 + 1)];
        rng.NextBytes(blocks);
        var px = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                px[y * w + x] = blocks[(y + 200 + shiftY) / 4 * (w / 4 + 1) + x / 4];
        return new GrayImage(px, w, h);
    }

    private static readonly PxRect View = new(1000, 500, 1200, 800); // physical; the grey images are half of it: 600 x 400

    [Fact]
    public void A_mark_whose_content_scrolled_out_is_found_again_anywhere_when_it_comes_back()
    {
        var before = Page(600, 400, 3);
        var a = MarkAnchor.From(Guid.NewGuid(), new PxRect(1300, 700, 140, 40), before, View);
        foreach (var matcher in Matchers())
        {
            Assert.Null(MarkRefit.Recover(a, Page(600, 400, 3, shiftY: 180), View, matcher));                    // scrolled out of view: not there
            Assert.Equal(new PxPoint(0, 300), MarkRefit.Recover(a, Page(600, 400, 3, shiftY: -150), View, matcher)); // back, further down than the scroll search reaches
        }
    }

    [Fact]
    public void Marks_with_nothing_to_recognise_follow_the_shift_most_marks_agree_on()
    {
        Assert.Equal(new PxPoint(0, -74), MarkRefit.CommonShift([new(0, -74), new(0, -74), new(2, -72)]));
        Assert.Null(MarkRefit.CommonShift([new(0, -74)]));                    // one mark is not a scroll
        Assert.Null(MarkRefit.CommonShift([new(0, -74), new(0, 120), new(40, 0)])); // they disagree: no common shift
    }

    [Fact]
    public void A_scroll_moves_each_mark_with_its_content()
    {
        var before = Page(600, 400, 1);
        var a = MarkAnchor.From(Guid.NewGuid(), new PxRect(1200, 700, 160, 40), before, View);
        var b = MarkAnchor.From(Guid.NewGuid(), new PxRect(1700, 820, 90, 90), before, View);
        Assert.NotNull(a.Patch);
        var after = Page(600, 400, 1, shiftY: 37); // the page scrolled up 37 half-pixels = 74 physical
        foreach (var matcher in Matchers())
        {
            var result = MarkRefit.Decide([a, b], after, View, matcher);
            Assert.False(result.ClearAll);
            Assert.Empty(result.Lost);
            Assert.Equal(new PxPoint(0, -74), result.Moves[a.MarkId]);
            Assert.Equal(new PxPoint(0, -74), result.Moves[b.MarkId]);
        }
    }

    [Fact]
    public void Only_the_mark_whose_content_is_gone_is_removed_and_most_gone_clears_everything()
    {
        var before = Page(600, 400, 2);
        var targets = new[] { new PxRect(1100, 600, 120, 40), new PxRect(1600, 600, 120, 40), new PxRect(1100, 1000, 120, 40), new PxRect(1600, 1000, 120, 40) };
        var anchors = targets.Select(t => MarkAnchor.From(Guid.NewGuid(), t, before, View)).ToList();

        // The area under the first mark is replaced by something else; the rest is untouched.
        var after = new GrayImage((byte[])before.Pixels.Clone(), 600, 400);
        var other = Page(600, 400, 99);
        for (var y = 40; y < 100; y++)
            for (var x = 20; x < 140; x++) after.Pixels[y * 600 + x] = other.Pixels[y * 600 + x];
        var one = MarkRefit.Decide(anchors, after, View, PatchMatcher.Instance);
        Assert.Equal([anchors[0].MarkId], one.Lost);
        Assert.Empty(one.Moves); // nothing else moved
        Assert.False(one.ClearAll);

        // A different page altogether: everything goes.
        var all = MarkRefit.Decide(anchors, Page(600, 400, 77), View, PatchMatcher.Instance);
        Assert.True(all.ClearAll);
    }

    [Fact]
    public void Repeated_content_is_ambiguous_so_the_mark_is_removed_not_moved_to_a_lookalike()
    {
        // Identical rows (a table): the patch matches several places equally well.
        var row = Page(600, 40, 5);
        var px = new byte[600 * 400];
        for (var y = 0; y < 400; y++) Array.Copy(row.Pixels, (y % 40) * 600, px, y * 600, 600);
        var table = new GrayImage(px, 600, 400);
        var anchor = MarkAnchor.From(Guid.NewGuid(), new PxRect(1300, 700, 120, 30), table, View);
        var scrolled = new GrayImage([.. px.Skip(600 * 13), .. px.Take(600 * 13)], 600, 400);
        var result = MarkRefit.Decide([anchor], scrolled, View, PatchMatcher.Instance);
        Assert.Contains(anchor.MarkId, result.Lost);
        Assert.Empty(result.Moves);
    }

    [Fact]
    public void A_blank_area_cannot_be_recognised_so_its_mark_stays_while_it_is_still_blank_and_goes_when_it_is_not()
    {
        GrayImage Fill(byte v) => new(Enumerable.Repeat(v, 600 * 400).ToArray(), 600, 400);
        var anchor = MarkAnchor.From(Guid.NewGuid(), new PxRect(1300, 700, 120, 30), Fill(240), View);
        var same = MarkRefit.Decide([anchor], Fill(236), View, PatchMatcher.Instance);
        Assert.Empty(same.Lost);
        Assert.Empty(same.Moves);
        Assert.Contains(anchor.MarkId, MarkRefit.Decide([anchor], Fill(10), View, PatchMatcher.Instance).Lost); // the white page went black
        Assert.Contains(anchor.MarkId, MarkRefit.Decide([anchor], Page(600, 400, 3), View, PatchMatcher.Instance).Lost); // text appeared there
    }

    [Fact]
    public void The_native_matcher_gives_exactly_the_managed_answer()
    {
        if (!Native.Available) return; // the managed matcher is then the only one, and it is what the other tests use
        var rng = new Random(11);
        for (var i = 0; i < 12; i++)
        {
            var image = Page(500, 300, 100 + i, shiftY: rng.Next(0, 50));
            var patch = image.Crop(rng.Next(0, 380), rng.Next(0, 220), rng.Next(16, 110), rng.Next(16, 70));
            var (l, t) = (rng.Next(0, 200), rng.Next(0, 100));
            var managed = PatchMatcher.Instance.Match(image, patch, l, t, l + 250, t + 180);
            var native = new NativePatchMatcher().Match(image, patch, l, t, l + 250, t + 180);
            Assert.Equal(managed, native);
        }
    }

    private static IEnumerable<IPatchMatcher> Matchers() => Native.Available ? [PatchMatcher.Instance, new NativePatchMatcher()] : [PatchMatcher.Instance];
}
