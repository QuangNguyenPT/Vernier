using System.Windows;
using System.Windows.Media;
using Vernier.App;
using Xunit;

namespace Vernier.Tests;

/// <summary>The tutor's marks are smooth hand-drawn curves that still go where they must: round the target, not across it.</summary>
public class MarkShapesTests
{
    [Theory]
    [InlineData(100, 100)] // squat: an ellipse
    [InlineData(420, 24)]  // a line of text: a squarer loop that hugs it
    [InlineData(30, 200)]  // a column
    public void A_loop_goes_round_every_corner_of_its_target_without_ballooning(double w, double h) => WindowsRealApiTests.OnSta(() =>
    {
        var box = new Rect(500, 300, w, h);
        var loop = MarkShapes.Loop(box, 1234);
        // Every corner of the target is inside the loop (closing the drawn path as a region).
        var drawn = PathGeometry.CreateFromGeometry(loop).Figures[0];
        drawn.IsFilled = true; // WPF flattens filled figures only
        var outline = drawn.GetFlattenedPathFigure(0.25, ToleranceType.Absolute);
        var region = new PathGeometry([new PathFigure(outline.StartPoint, outline.Segments, true) { IsFilled = true }]);
        foreach (var corner in new[] { box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight })
            Assert.True(region.FillContains(corner), $"corner {corner} of {box} is outside the loop {loop.Bounds}");
        // ...and the loop stays close: a ring round a line of text must not swallow the lines above and below.
        Assert.True(loop.Bounds.Height < h * 1.45 + 16, $"loop {loop.Bounds} is too tall for a {w}×{h} target");
        Assert.True(loop.Bounds.Width < w * 1.45 + 16, $"loop {loop.Bounds} is too wide for a {w}×{h} target");
        return Task.CompletedTask;
    });

    [Fact]
    public void Every_kind_of_mark_renders_over_light_and_dark_content() => WindowsRealApiTests.OnSta(() =>
    {
        // A sample sheet: half white page, half dark video, each mark kind drawn on both (VERNIER_SHOT_DIR saves it for review).
        const int w = 900, h = 520;
        Vernier.Core.Annotations.Annotation Mark(Vernier.Core.Annotations.AnnotationKind kind, double x, double y, double mw, double mh, string? label = null) =>
            new() { Kind = kind, Points = [new(x, y), new(x + mw, y + mh)], Source = "tutor", Color = Vernier.Core.Annotations.AnnotationColors.Focus, StrokeWidth = 3, Label = label, Confidence = 0.9 };
        var marks = new List<Vernier.Core.Annotations.Annotation>();
        foreach (var x0 in new[] { 40.0, 490.0 })
        {
            marks.Add(Mark(Vernier.Core.Annotations.AnnotationKind.Circle, x0 + 30, 50, 150, 26, "this term"));
            marks.Add(Mark(Vernier.Core.Annotations.AnnotationKind.Highlight, x0 + 20, 140, 220, 22));
            marks.Add(Mark(Vernier.Core.Annotations.AnnotationKind.Underline, x0 + 20, 210, 200, 20));
            marks.Add(Mark(Vernier.Core.Annotations.AnnotationKind.Bracket, x0 + 20, 270, 160, 90, "given"));
            marks.Add(Mark(Vernier.Core.Annotations.AnnotationKind.Arrow, x0 + 250, 460, -40, -90, "start here"));
            marks.Add(Mark(Vernier.Core.Annotations.AnnotationKind.Point, x0 + 300, 300, 30, 30));
        }
        var sheet = new DrawingVisual();
        using (var dc = sheet.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w / 2, h));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 18, 22)), null, new Rect(w / 2, 0, w / 2, h));
            var text = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            foreach (var x0 in new[] { 40.0, 490.0 })
                foreach (var y in new[] { 55.0, 142.0, 212.0, 280.0, 310.0, 340.0 })
                    dc.DrawRectangle(text, null, new Rect(x0 + 30, y + 6, 150, 10));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(sheet);
        foreach (var m in marks) bitmap.Render(MarkPainter.Build(m, p => new Point(p.X, p.Y), new Size(w, h), 1, reveal: false));
        bitmap.Freeze();
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);
        var blue = 0;
        for (var i = 0; i < pixels.Length; i += 4) if (pixels[i] > 150 && pixels[i] > pixels[i + 2] + 60) blue++;
        Assert.True(blue > 4000, $"only {blue} blue pixels: the marks were not drawn");
        if (Environment.GetEnvironmentVariable("VERNIER_SHOT_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(dir, "tutor-marks.png"));
            encoder.Save(file);
        }
        return Task.CompletedTask;
    });

    [Fact]
    public void Shapes_are_the_same_for_the_same_mark_and_differ_between_marks() => WindowsRealApiTests.OnSta(() =>
    {
        var box = new Rect(0, 0, 200, 60);
        Assert.Equal(MarkShapes.Loop(box, 7).ToString(), MarkShapes.Loop(box, 7).ToString());
        Assert.NotEqual(MarkShapes.Loop(box, 7).ToString(), MarkShapes.Loop(box, 8).ToString());
        return Task.CompletedTask;
    });

    [Fact]
    public void Swipe_underline_brace_and_arrow_stay_with_their_target() => WindowsRealApiTests.OnSta(() =>
    {
        var box = new Rect(100, 100, 240, 20);
        var swipe = MarkShapes.Swipe(box, 3, out var thickness);
        Assert.Equal(26, thickness);
        Assert.InRange(swipe.Bounds.Left - thickness / 2, box.Left - 4, box.Left + 2); // the round ends reach just past the box edges
        Assert.InRange(swipe.Bounds.Right + thickness / 2, box.Right - 2, box.Right + 4);

        var under = MarkShapes.Underline(box, 3);
        Assert.InRange(under.Bounds.Top, box.Bottom, box.Bottom + 6);
        Assert.InRange(under.Bounds.Width, box.Width, box.Width + 6);

        var brace = MarkShapes.Brace(box);
        Assert.InRange(brace.Bounds.Height, box.Height - 0.5, box.Height + 0.5);
        Assert.True(brace.Bounds.Right <= box.Left + 10);

        var arrow = MarkShapes.Arrow(new Point(0, 0), new Point(300, 0), 2);
        Assert.True(arrow.Bounds.Height > 10, "the shaft bows a little instead of being a ruler line");
        Assert.InRange(MarkShapes.Length(arrow), 300, 360);
        Assert.InRange(MarkShapes.Length(MarkShapes.Smooth([new Point(0, 0), new Point(100, 0)])), 99.9, 100.1);
        return Task.CompletedTask;
    });
}
