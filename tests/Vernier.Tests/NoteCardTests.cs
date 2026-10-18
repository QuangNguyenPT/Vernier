using System.IO;
using System.Windows.Media.Imaging;
using Vernier.App;
using Vernier.Core.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>The note card: dark template, handwriting, typeset formulas. Rendered to a bitmap, so no window is opened.</summary>
public class NoteCardTests(ITestOutputHelper output)
{
    private static NoteContent Solution(bool handwriting = true) => new("Step 1: Newton's second law along the slope",
    [
        new NoteLine(NoteLineKind.Step, "Forces along the slope: the pull up, the weight component down.", @"6 - mg\sin\theta = ma"),
        new NoteLine(NoteLineKind.Step, "The slope rises 1 m over 3 m, so", @"\sin\theta = \frac{1}{3}"),
        new NoteLine(NoteLineKind.Step, "", @"a = \frac{6 - 1\times 9.8\times \frac{1}{3}}{1} = 2.7\,\mathrm{m/s^{2}}"),
        new NoteLine(NoteLineKind.Result, "Speed at the top", @"v = \sqrt{2as} = \sqrt{2\times 2.7\times 3} \approx 4\,\mathrm{m/s}"),
        new NoteLine(NoteLineKind.Check, "Matches option B."),
        new NoteLine(NoteLineKind.Note, "u = 0 because the mass starts at rest."),
    ], handwriting);

    [Fact]
    public void A_card_is_dark_whatever_is_behind_it_and_holds_the_written_lines() => WindowsRealApiTests.OnSta(() =>
    {
        var card = NoteRenderer.Render(Solution(), 440, 1.25);
        Assert.Equal(440, card.SizeDip.Width);
        Assert.InRange(card.SizeDip.Height, 200, 700);
        Assert.Equal((int)Math.Ceiling(440 * 1.25), card.Image.PixelWidth);

        var pixels = new byte[card.Image.PixelWidth * card.Image.PixelHeight * 4];
        card.Image.CopyPixels(pixels, card.Image.PixelWidth * 4, 0);
        // Premultiplied BGRA. The template body is dark and nearly opaque; the writing is bright.
        int At(int x, int y) => (y * card.Image.PixelWidth + x) * 4;
        var body = At(card.Image.PixelWidth - 12, card.Image.PixelHeight / 2);
        Assert.InRange(pixels[body + 3], 220, 255);
        Assert.True(pixels[body] < 60 && pixels[body + 1] < 60 && pixels[body + 2] < 60, "the card body must be dark");
        var bright = 0;
        for (var i = 0; i < pixels.Length; i += 4) if (pixels[i] > 170 && pixels[i + 1] > 170 && pixels[i + 2] > 170) bright++;
        Assert.True(bright > 800, $"only {bright} bright pixels: the lines were not written");
        output.WriteLine($"card {card.SizeDip.Width:0}×{card.SizeDip.Height:0} dip, {bright} bright pixels");

        if (Environment.GetEnvironmentVariable("VERNIER_SHOT_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var (name, content) in new[] { ("note-card-handwriting.png", Solution()), ("note-card-clean.png", Solution(false)) })
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(NoteRenderer.Render(content, 440, 1.5).Image));
                using var file = File.Create(Path.Combine(dir, name));
                encoder.Save(file);
            }
        }
        return Task.CompletedTask;
    });

    [Fact]
    public void Tex_the_engine_cannot_read_is_shown_as_written_and_a_narrower_card_is_taller() => WindowsRealApiTests.OnSta(() =>
    {
        var broken = new NoteContent(null, [new NoteLine(NoteLineKind.Step, "x", @"\notacommand{")]);
        Assert.True(NoteRenderer.Render(broken, 300, 1).SizeDip.Height > 20); // rendered, not thrown away
        var wide = NoteRenderer.Render(Solution(), 560, 1).SizeDip.Height;
        var narrow = NoteRenderer.Render(Solution(), 340, 1).SizeDip.Height;
        Assert.True(narrow > wide, $"{narrow} should exceed {wide}");
        return Task.CompletedTask;
    });
}
