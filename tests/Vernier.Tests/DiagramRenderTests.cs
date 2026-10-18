using System.IO;
using System.Windows.Media.Imaging;
using Vernier.App;
using Vernier.Core.Diagrams;
using Xunit;

namespace Vernier.Tests;

/// <summary>The diagram cards render (no window is opened); VERNIER_SHOT_DIR saves them for review.</summary>
public class DiagramRenderTests
{
    private static DiagramSpec Skew()
    {
        var (d, a, b, _) = Geometry3D.LineLine(new(1, 0, 2), new(2, -1, 3), new(0, 0, 0), new(1, 1, 1));
        return new DiagramSpec(DiagramKind.Space3D, "Shortest distance between skew lines",
        [
            new("line", [1, 0, 2], [2, -1, 3], "L₁", "main", [-0.8, 0.8]),
            new("line", [0, 0, 0], [1, 1, 1], "L₂", "main", [-1, 2.2]),
            new("segment", [a.X, a.Y, a.Z], [b.X, b.Y, b.Z], $"d = {DiagramBuilder.Number(Math.Round(d, 3))}", "result"),
            new("vector", [0, 0, 0], [0, 0, 0], null, "soft"),
        ], [$"Shortest distance d = {DiagramBuilder.Number(Math.Round(d, 3))}"]);
    }

    private static readonly DiagramSpec Forces = new(DiagramKind.Space3D, "Torque τ = r × F",
    [
        new("vector", [0, 0, 0], [3, 0, 0], "r", "main"), new("vector", [3, 0, 0], [3, 2, 0], "F", "main"), new("vector", [0, 0, 0], [0, 0, 6], "τ = r×F", "result"),
        new("plane", [0, 0, 0], [0, 0, 1], null, "soft", [3.4]),
    ], ["τ = (0, 0, 6), pointing along z by the right-hand rule"]);

    private static readonly DiagramSpec Argand = new(DiagramKind.Complex, "z = 3 + 4i: modulus and argument",
    [
        new("number", [3, 4], null, "z = 3+4i", "main"), new("number", [-4, 3], null, "iz", "soft"), new("circle", [0, 0], [5], "|z| = 5", "soft"),
    ], ["|z| = 5, arg z ≈ 53.13°. Multiplying by i turns z a quarter-turn."]);

    private static DiagramSpec Ramp()
    {
        // The box on the 30° ramp of the worksheet: weight 14.7 N, normal reaction, tension 26 N up the slope, friction 2.1 N down it.
        var forces = new List<DiagramItem> { new("force", [14.7, 270], null, "W = 14.7 N", "main"), new("force", [14.7 * Math.Cos(Math.PI / 6), 120], null, "N", "main"), new("force", [26, 30], null, "T = 26 N", "main"), new("force", [2.1, 210], null, "F = 2.1 N", "warn") };
        var r = Geometry3D.Resultant(forces.Select(f => (f.A[0], f.A[1])), 30);
        forces.Add(new("force", [r.Size, r.Direction], null, $"ΣF = {DiagramBuilder.Number(Math.Round(r.Size, 2))} N", "result"));
        return new DiagramSpec(DiagramKind.Forces, "Forces on the box", forces, [$"Along the slope (up): {DiagramBuilder.Number(Math.Round(r.Along, 2))} N"], 3, 30);
    }

    private static readonly DiagramSpec Tesseract = new(DiagramKind.Hypercube, "A tesseract (4D cube)", [], ["16 corners, 32 edges; green edges run along the 4th axis."], 4, 30);

    [Fact]
    public void Every_kind_of_diagram_renders_a_dark_card_with_drawing_on_it() => WindowsRealApiTests.OnSta(() =>
    {
        var dir = Environment.GetEnvironmentVariable("VERNIER_SHOT_DIR");
        foreach (var (name, spec) in new[] { ("skew", Skew()), ("torque", Forces), ("argand", Argand), ("tesseract", Tesseract), ("forces", Ramp()) })
        {
            var card = DiagramRenderer.Render(spec, 440, 1.25);
            Assert.InRange(card.SizeDip.Height, 200, 640);
            var px = new byte[card.Image.PixelWidth * card.Image.PixelHeight * 4];
            card.Image.CopyPixels(px, card.Image.PixelWidth * 4, 0);
            var coloured = 0;
            for (var i = 0; i < px.Length; i += 4) if (Math.Abs(px[i] - px[i + 2]) > 60 && px[i + 3] > 200) coloured++;
            Assert.True(coloured > 300, $"{name}: only {coloured} coloured pixels, so nothing was drawn");
            if (dir is { Length: > 0 })
            {
                Directory.CreateDirectory(dir);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(card.Image));
                using var file = File.Create(Path.Combine(dir, $"diagram-{name}.png"));
                encoder.Save(file);
            }
        }
        return Task.CompletedTask;
    });
}
