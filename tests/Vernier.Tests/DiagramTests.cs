using Vernier.Core.Agents;
using Vernier.Core.Diagrams;
using Vernier.Core.Geometry;
using Xunit;

namespace Vernier.Tests;

/// <summary>Symbols instead of markup, the 3D numbers worked out in code, the real curve found from pixels, and a diagram forced for dimension problems.</summary>
public class DiagramTests
{
    [Theory]
    [InlineData(@"\Delta v = 3\,\mathrm{m/s}", "Δ v = 3 m/s")]
    [InlineData(@"\frac{1}{2}mv^{2}", "1/2mv²")]
    [InlineData(@"F = ma \approx 6 \times 10^{3}", "F = ma ≈ 6 × 10³")]
    [InlineData(@"\vec{AB} \cdot \vec{u}", "AB⃗ · u⃗")]
    [InlineData(@"\sqrt{2as} \to v", "√(2as) → v")]
    [InlineData(@"\text{Speed at the top}: \theta = 30^\circ", "Speed at the top: θ = 30°")]
    [InlineData("plain words stay", "plain words stay")]
    public void Stray_TeX_becomes_the_symbols_it_means_and_never_raw_markup(string tex, string expected)
    {
        Assert.Equal(expected, TexPlain.Clean(tex));
        Assert.DoesNotContain('\\', TexPlain.Clean(tex));
    }

    [Fact]
    public void The_distance_between_skew_lines_the_foot_points_and_the_other_numbers_are_exact()
    {
        // The x axis and the line through (0,0,1) along y: the common perpendicular is the z segment of length 1 at the origin.
        var (d, a, b, skew) = Geometry3D.LineLine(new(0, 0, 0), new(1, 0, 0), new(0, 0, 1), new(0, 1, 0));
        Assert.True(skew);
        Assert.Equal(1, d, 9);
        Assert.Equal(new V3(0, 0, 0), a);
        Assert.Equal(new V3(0, 0, 1), b);
        // x=1−t, y=1+t, z=3−t and x=2+s, y=1−2s, z=s: n = u×v = (−1, 0, 1), d = |(q−p)·n|/|n| = 4/√2.
        var (d2, _, _, _) = Geometry3D.LineLine(new(1, 1, 3), new(-1, 1, -1), new(2, 1, 0), new(1, -2, 1));
        Assert.Equal(2 * Math.Sqrt(2), d2, 9);
        Assert.False(Geometry3D.LineLine(new(0, 0, 0), new(1, 0, 0), new(0, 2, 0), new(2, 0, 0)).Skew); // parallel
        Assert.Equal(2, Geometry3D.LineLine(new(0, 0, 0), new(1, 0, 0), new(0, 2, 0), new(2, 0, 0)).Distance, 9);

        var (pd, foot) = Geometry3D.PointPlane(new(1, 2, 7), new(0, 0, 3), new(0, 0, 2));
        Assert.Equal(4, pd, 9);
        Assert.Equal(new V3(1, 2, 3), foot);
        Assert.Equal(90, Geometry3D.AngleDegrees(new(1, 0, 0), new(0, 5, 0)), 9);
        Assert.Equal(new V3(0, 0, 1), V3.Cross(new(1, 0, 0), new(0, 1, 0)));
    }

    [Fact]
    public void The_view_keeps_z_up_and_a_tesseract_has_its_16_corners_and_32_edges()
    {
        Assert.True(Projection.Project(new(0, 0, 1)).Y > 0.8);
        Assert.Equal(0, Projection.Project(new(0, 0, 0)).X, 9);
        var (corners, edges) = Projection.Hypercube(4, 30);
        Assert.Equal(16, corners.Length);
        Assert.Equal(32, edges.Length);
        Assert.Equal(12, Projection.Hypercube(3, 0).Edges.Length);
    }

    // A curve drawn into a synthetic screenshot: y = 0.9·sqrt(x) over a 600 x 400 plot, ticks x: 0 → 100 px, 10 → 500 px; y: 0 → 350 px, 3 → 50 px.
    private static (InkMap Ink, AxisCalibration Axes) CurveShot()
    {
        const int w = 600, h = 400;
        var bgra = new byte[w * h * 4];
        Array.Fill(bgra, (byte)255);
        void Dot(int x, int y, byte v) { if ((uint)x < w && (uint)y < h) { var i = (y * w + x) * 4; bgra[i] = bgra[i + 1] = bgra[i + 2] = v; } }
        for (var x = 100; x <= 500; x += 40) for (var y = 40; y < 360; y++) Dot(x, y, 205); // pale grid
        for (var x = 100; x <= 500; x++) { Dot(x, 350, 20); Dot(x, 351, 20); }               // x axis
        for (var x = 100; x <= 500; x++)
        {
            var data = (x - 100) / 40.0;                       // 0..10
            var y = 350 - 0.9 * Math.Sqrt(data) * 100;         // 3 units = 300 px
            for (var t = -1; t <= 1; t++) Dot(x, (int)Math.Round(y) + t, 15); // a 3 px pen line
        }
        return (new InkMap(bgra, w, h), new AxisCalibration(new(100, 0), new(500, 10), new(350, 0), new(50, 3)));
    }

    [Fact]
    public void A_corner_off_the_curve_is_pulled_onto_it_and_the_pale_grid_is_not_mistaken_for_the_line()
    {
        var (ink, axes) = CurveShot();
        var at = axes.ToImage(4, 2.2);                                   // the model's estimate; the curve is at 0.9·√4 = 1.8 there
        var seen = ink.NearestInkY((int)at.X, at.Y, 60);
        Assert.NotNull(seen);
        Assert.Equal(axes.ToImage(4, 1.8).Y, seen!.Value, 1.5);
        Assert.Null(ink.NearestInkY(120, 120, 10));                     // only a grid line near here: no ink
    }

    [Fact]
    public void Tracing_the_drawn_curve_gives_the_real_area_not_an_estimate()
    {
        var (ink, axes) = CurveShot();
        var (points, lost) = ink.Trace(100 + 4, 500, 350 - 0.9 * Math.Sqrt(0.1) * 100);
        Assert.Null(lost);
        var data = PolylineMath.Simplify(points, 1.5).Select(p => axes.ToData(p.X, p.Y)).ToList();
        double area = 0;
        for (var i = 1; i < data.Count; i++) area += (data[i].X - data[i - 1].X) * (data[i].Y + data[i - 1].Y) / 2;
        var exact = 0.9 * 2.0 / 3 * Math.Pow(10, 1.5); // ∫0.9√x dx from 0 to 10 = 18.97
        Assert.InRange(area, exact * 0.97, exact * 1.02);
        // Three triangles/trapezoids guessed from the same curve would be far off: the trace follows it.
    }

    [Theory]
    [InlineData("Find the shortest distance between the skew lines", true)]
    [InlineData("Find the cross product of u and v", true)]
    [InlineData("Write z = 3 + 4i in polar form", true)]
    [InlineData("Explain a tesseract", true)]
    [InlineData("What is the magnetic field around a wire", true)]
    [InlineData("What is the speed of the mass on the inclined plane", false)]
    [InlineData("Calculate the tension in the string", false)]
    public void A_diagram_is_required_by_code_for_space_vector_complex_and_higher_dimension_questions(string question, bool required) =>
        Assert.Equal(required, DimensionPolicy.Decide(question, null).Required);

    [Fact]
    public void The_screen_text_alone_can_demand_a_diagram_and_the_gate_asks_again_until_it_is_drawn()
    {
        var demand = DimensionPolicy.Decide("explain this", "1 Find the shortest distance between the skew lines x = 1 + 2t, y = -t");
        Assert.True(demand.Required);
        Assert.NotNull(DimensionGate.Check(demand, diagramDrawn: false, toolOffered: true));
        Assert.Null(DimensionGate.Check(demand, true, true));
        Assert.Null(DimensionGate.Check(demand, false, toolOffered: false));
        Assert.Null(DimensionGate.Check(VisualDemand.None, false, true));
    }

    [Fact]
    public void Items_that_cannot_be_drawn_are_refused_with_a_reason()
    {
        Assert.Null(DiagramBuilder.Problem(DiagramKind.Space3D, new("vector", [0, 0, 0], [1, 2, 3])));
        Assert.Contains("cannot be drawn", DiagramBuilder.Problem(DiagramKind.Complex, new("plane", [0, 0], [0, 0, 1])));
        Assert.Contains("zero vector", DiagramBuilder.Problem(DiagramKind.Space3D, new("plane", [0, 0, 0], [0, 0, 0])));
        Assert.Contains("numbers", DiagramBuilder.Problem(DiagramKind.Space3D, new("point", [double.NaN, 0, 0])));
    }
}
