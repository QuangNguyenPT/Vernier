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
    public void The_axes_are_read_from_the_tick_labels_so_the_model_needs_only_the_x_range()
    {
        // Tick labels as the screen reader finds them (image pixels), with other text around them.
        (string, PxRect) At(string t, double cx, double cy) => (t, new PxRect(cx - 6, cy - 7, 12, 14));
        var labels = new List<(string, PxRect)> { At("Time / s", 300, 400), At("15", 60, 50), At("Find the displacement", 200, 20) };
        foreach (var (v, i) in new[] { ("0", 0), ("2", 1), ("4", 2), ("6", 3), ("8", 4), ("10", 5) }) labels.Add(At(v, 100 + i * 80, 372));
        foreach (var (v, i) in new[] { ("0", 0), ("5", 1), ("10", 2), ("15", 3) }) labels.Add(At(v, 80, 350 - i * 100));
        var axes = AxisFinder.Find(labels);
        Assert.NotNull(axes);
        Assert.Equal(new PxPoint(260, 250), axes!.ToImage(4, 5));
        Assert.Null(AxisFinder.Find([At("1", 10, 10), At("7", 50, 10), At("3", 90, 10)])); // not in order: not an axis
    }

    [Fact]
    public void The_curve_is_found_without_being_told_where_it_starts_and_the_axis_line_is_skipped()
    {
        var (ink, axes) = CurveShot();
        var x = (int)axes.ToImage(1, 0).X;
        var y = ink.FirstCurveY(x, 40, 360, axes.ToImage(0, 0).Y);
        Assert.NotNull(y);
        Assert.Equal(axes.ToImage(1, 0.9).Y, y!.Value, 1.5); // 0.9·√1, not the axis at 350
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
    [InlineData("Find the shortest distance between the skew lines", "overlay_diagram", "space3d")]
    [InlineData("Find the cross product of u and v", "overlay_diagram", "space3d")]
    [InlineData("Write z = 3 + 4i in polar form", "overlay_diagram", "complex")]
    [InlineData("Explain a tesseract", "overlay_diagram", "hypercube")]
    [InlineData("What is the speed of the mass on the inclined plane?", "overlay_diagram", "forces")]
    [InlineData("Calculate the tension in the string", "overlay_diagram", "forces")]
    [InlineData("Show that the terminal speed is given by this", "overlay_diagram", "forces")]
    [InlineData("Find the displacement from the velocity-time graph", "graph_area", "space3d")]
    [InlineData("Estimate the acceleration of the bottle at its maximum height", "graph_area", "space3d")]
    public void A_drawing_is_required_by_code_and_the_right_tool_and_kind_are_chosen(string question, string tool, string kind)
    {
        var d = DimensionPolicy.Decide(question, null);
        Assert.True(d.Required, question);
        Assert.Equal(tool, d.Tool);
        if (tool == "overlay_diagram") Assert.Equal(kind, d.Kind);
    }

    [Fact]
    public void The_two_worksheets_each_get_their_graph_and_their_forces_planned()
    {
        // Text as the screen reader gives it for the two worksheets (Fixtures/Worksheets).
        const string sphere = "A small solid sphere is released from rest at the surface of a container of water. The graph below shows how the velocity of the sphere v varies with time t. Explain the shape of the graph. The radius of the sphere is 0.61 mm and the viscosity of the water is 1.0 x 10-3 Pa s. Show that the maximum drag force acting on the sphere is around 5 x 10-6 N. Use the graph to estimate the depth of water in the container.";
        const string blocks = "A block of mass 2.0 kg is placed on another block of mass 5.0 kg. The blocks sit on a frictionless surface, but the coefficient of static friction between the blocks is 0.50. Calculate the magnitude of the maximum force Fmax. On the graph, sketch how the force of static friction Ff acting on the bottom block varies with the force F.";
        var a = DimensionPolicy.Decide("help me with this question", sphere);
        Assert.Equal("graph_area", a.Tool);
        Assert.Equal(("forces", "fluid"), (a.Also?.Kind, a.Also?.Scene));
        var b = DimensionPolicy.Decide("how do I do (ii)?", blocks);
        Assert.Equal("graph_sketch", b.Tool);
        Assert.Equal(("forces", "stacked"), (b.Also?.Kind, b.Also?.Scene));
        var plan = TurnPlan.Build(b, Vernier.Core.Research.ResearchDemand.None, null, "how do I do (ii)?", false, true)!;
        Assert.Contains("1. graph_sketch {\"points\"", plan);
        Assert.Contains("2. overlay_diagram {\"kind\":\"forces\",\"scene\":\"stacked\"", plan);
    }

    [Fact]
    public void A_white_worksheet_inside_a_dark_screen_is_page_not_ink_when_its_window_is_given()
    {
        // A dark desktop (luma 30) fills most of the screen; the worksheet window (white, one grey curve pixel) is a small part of it.
        int w = 400, h = 300;
        var px = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++) { px[i * 4] = px[i * 4 + 1] = px[i * 4 + 2] = 30; px[i * 4 + 3] = 255; }
        var page = new PxRect(20, 20, 120, 90);
        for (var y = 20; y < 110; y++) for (var x = 20; x < 140; x++) { var k = (y * w + x) * 4; px[k] = px[k + 1] = px[k + 2] = 255; }
        var curve = (60 * w + 70) * 4; px[curve] = px[curve + 1] = px[curve + 2] = 128;
        Assert.True(new InkMap(px, w, h).IsInk(40, 40));                 // judged against the whole screen, the white page reads as ink
        var ink = new InkMap(px, w, h, page);
        Assert.False(ink.IsInk(40, 40));                                 // judged inside the window: page
        Assert.True(ink.IsInk(70, 60));                                  // and the grey curve is ink
    }

    [Fact]
    public void Plain_questions_need_no_drawing() => Assert.False(DimensionPolicy.Decide("What does this word mean?", "An essay about the history of Rome").Required);

    [Fact]
    public void One_stray_word_or_code_on_the_screen_does_not_force_a_drawing()
    {
        Assert.False(DimensionPolicy.Decide("What is this?", "public List<Vector3> vectors = new();").Required);             // one term
        Assert.False(DimensionPolicy.Decide("What is this?", "var force = vector * 2; // tension", screenIsStudyMaterial: false).Required); // a code editor
        Assert.True(DimensionPolicy.Decide("What is this?", "A box on a ramp: find the tension and the friction force").Required);         // a real problem
    }

    private static Vernier.Core.Tools.ToolEvent Event(string tool, bool error, string outcome = "") => new("t", tool, "{}", error, outcome, TimeSpan.Zero, DateTimeOffset.UtcNow);

    [Fact]
    public void The_gate_asks_again_when_a_required_drawing_is_missing_or_a_failed_drawing_was_never_retried()
    {
        var demand = DimensionPolicy.Decide("explain this", "1 Find the shortest distance between the skew lines x = 1 + 2t, y = -t");
        Assert.True(demand.Required); // the screen text alone demands it
        HashSet<string> offered = ["overlay_diagram", "graph_area", "overlay_marks"];
        Assert.Contains("overlay_diagram", DrawingGate.Check(demand, [], offered));
        Assert.Null(DrawingGate.Check(demand, [Event("overlay_diagram", false)], offered));
        Assert.Null(DrawingGate.Check(demand, [], new HashSet<string>())); // the tool was not offered: nothing to demand

        // A failure that was never retried: the answer is sent back with the error and how to fix it, even when nothing was required.
        var retry = DrawingGate.Check(VisualDemand.None, [Event("graph_area", true, "The axes cannot be read like that: the two x ticks are only 4 px apart.")], offered);
        Assert.Contains("4 px apart", retry);
        Assert.Contains("call graph_area again", retry);
        Assert.Null(DrawingGate.Check(VisualDemand.None, [Event("graph_area", true, "x"), Event("graph_area", false)], offered)); // fixed on the next try
    }

    [Fact]
    public void The_plan_names_each_tool_with_a_call_to_copy_so_a_small_model_can_follow_it()
    {
        var plan = TurnPlan.Build(DimensionPolicy.Decide("What is the tension in the string?", null), new Vernier.Core.Research.ResearchDemand(true, "x"), "Blender", "What does the bevel modifier do?", true, true);
        Assert.NotNull(plan);
        Assert.Contains("1. web_research {\"query\":\"Blender bevel modifier", plan);
        Assert.Contains("2. overlay_diagram {\"kind\":\"forces\"", plan);
        Assert.Contains("never stop because of a tool error", plan);
        Assert.Null(TurnPlan.Build(VisualDemand.None, Vernier.Core.Research.ResearchDemand.None, null, "hello", true, true));
    }

    [Fact]
    public void The_net_force_and_its_components_along_a_slope_are_exact()
    {
        // A 1.5 kg box on a 30° slope: weight 14.7 N down, normal reaction 14.7·cos30 across the slope; friction 2.1 N down the slope.
        var w = 14.7;
        var r = Geometry3D.Resultant([(w, 270), (w * Math.Cos(Math.PI / 6), 120), (2.1, 210)], 30);
        Assert.Equal(0, r.Across, 9);                           // nothing out of the surface
        Assert.Equal(-(w * 0.5 + 2.1), r.Along, 9);             // down the slope: mg sin30 + friction
        Assert.Equal(Math.Abs(r.Along), r.Size, 9);
        Assert.Equal(0, Geometry3D.Resultant([(5, 0), (5, 180)]).Size, 9); // balanced
    }

    [Fact]
    public void Arguments_a_small_model_gets_slightly_wrong_are_repaired_instead_of_refused()
    {
        var schema = System.Text.Json.Nodes.JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"required":["kind","items"],"properties":{
              "kind":{"type":"string","enum":["space3d","forces"]},
              "angle":{"type":"number"},
              "items":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["type","a"],"properties":{
                 "type":{"type":"string"},"a":{"type":"array","items":{"type":"number"}}}}}}}
            """)!.AsObject();
        var args = System.Text.Json.Nodes.JsonNode.Parse("""
            {"Kind":"Forces","angle":"30","items":"[{\"type\":\"force\",\"a\":[\"14.7\",270],\"colour\":\"red\"}]","note":"extra"}
            """)!.AsObject();
        var fixedArgs = Vernier.Core.Tools.SchemaCheck.Repair(schema, args);
        Assert.Null(Vernier.Core.Tools.SchemaCheck.Validate(schema, fixedArgs));
        Assert.Equal("forces", fixedArgs["kind"]!.GetValue<string>());
        Assert.Equal(30, fixedArgs["angle"]!.GetValue<double>());
        Assert.Equal(14.7, fixedArgs["items"]![0]!["a"]![0]!.GetValue<double>());
        var point = Vernier.Core.Tools.SchemaCheck.Repair(schema, System.Text.Json.Nodes.JsonNode.Parse("""{"kind":"space3d","items":[{"type":"point","a":{"x":1,"y":2,"z":3}}]}""")!.AsObject());
        Assert.Equal(3, point["items"]![0]!["a"]!.AsArray().Count);
        Assert.Contains("\"kind\":\"space3d\"", Vernier.Core.Tools.SchemaCheck.Example(schema)); // what a refusal shows as the shape to use
    }

    [Fact]
    public void Items_that_cannot_be_drawn_are_refused_with_a_reason()
    {
        Assert.Null(DiagramBuilder.Problem(DiagramKind.Space3D, new("vector", [0, 0, 0], [1, 2, 3])));
        Assert.Contains("cannot be drawn", DiagramBuilder.Problem(DiagramKind.Complex, new("plane", [0, 0], [0, 0, 1])));
        Assert.Contains("other way", DiagramBuilder.Problem(DiagramKind.Forces, new("force", [-3, 90])));
        Assert.Contains("zero vector", DiagramBuilder.Problem(DiagramKind.Space3D, new("plane", [0, 0, 0], [0, 0, 0])));
        Assert.Contains("numbers", DiagramBuilder.Problem(DiagramKind.Space3D, new("point", [double.NaN, 0, 0])));
    }
}

/// <summary>Regression: tools made ready while the learner typed died two minutes after the bar opened, in the middle of long answers.</summary>
public class WarmTurnTests
{
    [Fact]
    public async Task A_warm_turn_that_a_question_took_keeps_its_tools_after_the_warm_lifetime()
    {
        var expired = 0;
        var (taken, expiry) = Vernier.App.TutorSession.WarmTurn(["overlay_marks"], TimeSpan.FromMilliseconds(60), () => Interlocked.Increment(ref expired));
        expiry.Dispose(); // the question took it
        await Task.Delay(200);
        Assert.True(taken.IsActive, "the answer's tools must not end on the warm timer");
        Assert.Equal(0, expired);

        var (unused, _) = Vernier.App.TutorSession.WarmTurn(["overlay_marks"], TimeSpan.FromMilliseconds(60), () => Interlocked.Increment(ref expired));
        await Task.Delay(300);
        Assert.Equal(1, expired); // an unused one is still thrown away on time
        Assert.True(unused.IsActive); // by the discard, which revokes its grant, not by killing a turn an answer might hold
    }
}
