using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Vernier.Core.Tools;

namespace Vernier.Core.Agents;

/// <param name="Tool">The drawing tool that answers this need: overlay_diagram, graph_area or graph_sketch.</param>
/// <param name="Kind">For overlay_diagram: space3d, complex, hypercube or forces.</param>
/// <param name="Scene">For forces: slope, fluid (a body in water) or stacked (two blocks).</param>
/// <param name="Also">A second drawing the problem needs (a graph question about forces: the graph, then the forces), planned but not gated.</param>
public sealed record VisualDemand(bool Required, string Reason, string Tool = "overlay_diagram", string Kind = "space3d", string Scene = "slope", VisualDemand? Also = null)
{
    public static readonly VisualDemand None = new(false, "");
}

/// <summary>
/// Problems that are understood by seeing them are drawn every time, decided by code from the question and the screen
/// text, not left to the model: an area or slope on a graph (graph_area), forces on a body (a free-body diagram), vectors,
/// lines and planes (3D), complex numbers (the Argand plane), four dimensions and more (a tesseract and analogy).
/// Checked after the answer by <see cref="DrawingGate"/>.
/// </summary>
public static partial class DimensionPolicy
{
    [GeneratedRegex(@"\b(area under|displacement from (?:the|this) graph|distance travelled from|work done from|from the (?:v|velocity)[-–\s]?t(?:ime)? graph|(?:velocity|speed|force|current)[-–\s](?:time|distance) graph|gradient of the graph|slope of the graph|estimate the acceleration|graph (?:below |above )?shows|use the graph|from the graph)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GraphQuestion();

    [GeneratedRegex(@"\b(sketch (?:how|a graph|the graph|a line|the line|a curve|the curve|your graph)|on the (?:graph|grid|axes),? (?:sketch|draw)|draw (?:your|a|the) graph|plot (?:how|a graph of))\b", RegexOptions.IgnoreCase)]
    private static partial Regex SketchQuestion();

    [GeneratedRegex(@"\b(water|liquid|fluid|oil|upthrust|buoyan\w*|viscosity|viscous|terminal (?:velocity|speed)|stokes)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FluidScene();

    [GeneratedRegex(@"\b(placed on (?:top of )?another|on top of (?:another|the other)|stacked|between the blocks|top block|bottom block)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StackedScene();

    [GeneratedRegex(@"\b(complex numbers?|imaginary|argand|de moivre|modulus and argument|polar form|roots of unity)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Complex();

    [GeneratedRegex(@"\b(4\s?-?d|four[- ]dimensional|fourth dimension|n[- ]dimensional|higher dimensions?|hypercube|tesseract|5\s?-?d|five[- ]dimensional)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Higher();

    [GeneratedRegex(@"\b(vectors?|cross product|dot product|scalar product|skew lines?|normal vector|direction vector|position vector|unit vector|eigenvectors?|3\s?-?d|three[- ]dimensional|torque|angular momentum|magnetic field|electric field|curl|divergence|gradient of|spherical|cylindrical|tangent plane|plane through|equation of (?:a |the )?(?:plane|line)|lines? (?:intersect|are parallel)|shortest distance)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Space();

    [GeneratedRegex(@"\b(forces?|tension|friction(?:al)?|normal (?:force|reaction|contact)|reaction force|weight|free[- ]body|inclined|incline|ramp|pulley|equilibrium|resultant|net force|drag|upthrust|buoyan\w*|terminal (?:velocity|speed)|thrust|lift force|centripetal|circular motion|newton'?s (?:first|second|third) law|f\s?=\s?ma)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Forces();

    [GeneratedRegex(@"(?:\bi\s?\^\s?2|\bz\s?=\s?[-\d.]+\s?[+-]\s?[-\d.]*\s?i\b|\\vec|\\mathbf\{[a-z]\}|\\times)", RegexOptions.IgnoreCase)]
    private static partial Regex Notation();

    /// <param name="screenIsStudyMaterial">False for code editors and terminals: their text is code, not a problem, and must not force a drawing.</param>
    private static bool ClearlyAProblem(string screen)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in new[] { SketchQuestion(), GraphQuestion(), Higher(), Complex(), Space(), Forces() })
            foreach (System.Text.RegularExpressions.Match m in r.Matches(screen)) terms.Add(m.Value.ToLowerInvariant());
        return terms.Count >= 2 || GraphQuestion().IsMatch(screen) || SketchQuestion().IsMatch(screen);
    }

    public static VisualDemand Decide(string question, string? screenText, bool screenIsStudyMaterial = true)
    {
        var screen = screenIsStudyMaterial ? (screenText is { Length: > 4000 } ? screenText[..4000] : screenText ?? "") : "";
        var both = question + "\n" + screen;
        // The question decides first. The screen alone decides only when it clearly shows such a problem: two different terms of it, not one stray word.
        foreach (var text in new[] { question, both })
        {
            if (text != question && !ClearlyAProblem(screen)) break;
            // A worksheet about forces with a graph needs both: the graph (sketched or shaded) is the answer, the forces are why.
            var forces = Forces().Match(text) is { Success: true } fm ? new VisualDemand(true, $"it is about \"{fm.Value.ToLowerInvariant()}\": draw the forces on the body", Kind: "forces", Scene: SceneOf(text)) : null;
            if (SketchQuestion().Match(text) is { Success: true } k)
                return new(true, $"the question asks for a graph to be drawn (\"{k.Value.ToLowerInvariant()}\"): sketch it on the learner's axes", "graph_sketch", Also: forces);
            if (GraphQuestion().Match(text) is { Success: true } g)
                return new(true, $"the answer is read off a graph (\"{g.Value.ToLowerInvariant()}\"): shade it on the real curve", "graph_area", Also: forces);
            if (Higher().Match(text) is { Success: true } h) return new(true, $"it is about \"{h.Value.ToLowerInvariant()}\": draw the tesseract or a 3D slice and explain by analogy", Kind: "hypercube");
            if (Complex().Match(text) is { Success: true } c) return new(true, $"it is about \"{c.Value.ToLowerInvariant()}\": draw it on the Argand plane", Kind: "complex");
            if (Space().Match(text) is { Success: true } s) return new(true, $"it is about \"{s.Value.ToLowerInvariant()}\": draw it in 3D", Kind: "space3d");
            if (forces is not null) return forces;
        }
        if (Notation().IsMatch(question + "\n" + screen) && (Notation().IsMatch(question) || ClearlyAProblem(screen))) return new(true, "the problem uses vectors or complex numbers: draw it", Kind: "space3d");
        return VisualDemand.None;
    }

    private static string SceneOf(string text) => StackedScene().IsMatch(text) ? "stacked" : FluidScene().IsMatch(text) ? "fluid" : "slope";
}

/// <summary>The backstop after an answer: a required drawing that was not made, or a drawing that failed and was never tried again, is asked for once more.</summary>
public static class DrawingGate
{
    public static readonly string[] DrawingTools = ["overlay_mark", "overlay_marks", "overlay_write", "overlay_diagram", "graph_area", "graph_sketch"];

    public static string? Check(VisualDemand demand, IReadOnlyList<ToolEvent> events, IReadOnlySet<string> offered)
    {
        // 1. A drawing call failed and no later call of the same tool succeeded: do not let the answer give up.
        var lastFailure = events.LastOrDefault(e => e.IsError && DrawingTools.Contains(e.Tool));
        if (lastFailure is not null && !events.SkipWhile(e => e != lastFailure).Skip(1).Any(e => e.Tool == lastFailure.Tool && !e.IsError))
            return $"Your last {lastFailure.Tool} call failed: {Short(lastFailure.Outcome)} Do not give up on drawing and do not tell the learner the graph or screen is wrong because of a tool error. " +
                   $"Fix what the message says (for a graph: read two labelled ticks again, or use trace with only x_from and x_to; for a mark: use an id from the screen list) and call {lastFailure.Tool} again, then write the whole answer again.";
        // 2. A drawing was required and none of that kind was made.
        if (!demand.Required || !offered.Contains(demand.Tool)) return null;
        if (events.Any(e => e.Tool == demand.Tool && !e.IsError)) return null;
        return $"Before this answer can be shown it has to be drawn: {demand.Reason}, and nothing was drawn. Call {demand.Tool} now" +
               (demand.Tool == "overlay_diagram" ? $" with kind {demand.Kind}" : "") + " (see the plan in the question for the exact call), then write the whole answer again around what the learner can see.";
    }

    private static string Short(string s) => s.Length > 300 ? s[..300] + "…" : s;
}

/// <summary>
/// The plan for one answer, written by code from what the policies decided, as numbered tool calls with their arguments
/// half filled in. Small models follow a concrete numbered plan far better than a list of rules; large ones lose nothing.
/// </summary>
public static class TurnPlan
{
    public static string? Build(VisualDemand visual, Research.ResearchDemand research, string? appName, string question, bool canResearch, bool canDraw)
    {
        var steps = new List<string>();
        if (research.Required && canResearch)
            steps.Add($"web_research {Json(new JsonObject { ["query"] = $"{(appName is { Length: > 0 } ? appName + " " : "")}{Topic(question)}", ["why"] = "check how it works before explaining" })} (read at least 3 working sources; search again with other words if fewer)");
        if (visual.Required && canDraw)
            foreach (var need in new[] { visual, visual.Also }.OfType<VisualDemand>())
                steps.Add(Step(need));
        if (steps.Count == 0) return null;
        steps.Add("overlay_marks / overlay_write as needed (marks on the things you talk about; a note card for the working)");
        steps.Add("Answer in words, using only the numbers Vernier's tools returned. If a tool call fails, read its message, fix the arguments and call it again; never stop because of a tool error.");
        return "Plan for this answer, worked out by Vernier; do the steps in order (the JSON is the call to make, with this problem's own values):\n" +
               string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"));
    }

    private static string Step(VisualDemand visual) =>
            visual.Tool switch
            {
                "graph_area" => $"graph_area {Json(new JsonObject { ["trace"] = new JsonObject { ["x_from"] = 0, ["x_to"] = 10 }, ["area_unit"] = "m" })} (put in the real x range, e.g. to where the motion stops; Vernier finds the axes from the tick labels on the screen; add x_ticks and y_ticks only if it asks for them; for straight-line pieces you may give shapes instead)",
                "graph_sketch" => $"graph_sketch {Json(new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(13.7, 9.81)), ["x_ticks"] = new JsonArray(new JsonObject { ["px"] = 800, ["value"] = 13.7 }), ["label"] = "Ff = (5/7)F" })} (work the relationship out first; points in data units from the start to the end of the range asked for; when an axis tick is a symbol such as Fmax, give its px from the screenshot and the value you calculated; Vernier finds the axes and snaps the tick)",
                _ => visual.Kind switch
                {
                    "forces" when visual.Scene == "fluid" => $"overlay_diagram {Json(new JsonObject { ["kind"] = "forces", ["scene"] = "fluid", ["bodies"] = new JsonArray("sphere"), ["items"] = new JsonArray(new JsonObject { ["type"] = "force", ["a"] = new JsonArray(1.4e-5, 270), ["label"] = "W" }, new JsonObject { ["type"] = "force", ["a"] = new JsonArray(4.6e-6, 90), ["label"] = "drag D" }, new JsonObject { ["type"] = "force", ["a"] = new JsonArray(9.4e-6, 90), ["label"] = "upthrust U" }), ["title"] = "Forces on the sphere" })} (the body in the fluid; each force a = [size, direction in degrees from +x, anticlockwise: 270 is straight down]; Vernier adds the net force)",
                    "forces" when visual.Scene == "stacked" => $"overlay_diagram {Json(new JsonObject { ["kind"] = "forces", ["scene"] = "stacked", ["bodies"] = new JsonArray("5.0 kg", "2.0 kg"), ["items"] = new JsonArray(new JsonObject { ["type"] = "force", ["a"] = new JsonArray(13.7, 0), ["label"] = "F", ["on"] = 1 }, new JsonObject { ["type"] = "force", ["a"] = new JsonArray(9.81, 180), ["label"] = "friction", ["on"] = 1 }, new JsonObject { ["type"] = "force", ["a"] = new JsonArray(9.81, 0), ["label"] = "friction", ["on"] = 0 }), ["title"] = "Forces on the blocks" })} (bodies bottom first; on: 0 = bottom block, 1 = top block; a friction pair is one force on each block, opposite ways; Vernier adds the net force on each block)",
                    "forces" => $"overlay_diagram {Json(new JsonObject { ["kind"] = "forces", ["angle"] = 30, ["items"] = new JsonArray(new JsonObject { ["type"] = "force", ["a"] = new JsonArray(14.7, 270), ["label"] = "W = 14.7 N" }, new JsonObject { ["type"] = "force", ["a"] = new JsonArray(12.7, 120), ["label"] = "N" }), ["title"] = "Forces on the box" })} (angle = slope of the incline in degrees, 0 on level ground; each force a = [size, direction in degrees from +x, anticlockwise: 270 is straight down]; Vernier adds the net force and the components along and across the slope)",
                    "complex" => $"overlay_diagram {Json(new JsonObject { ["kind"] = "complex", ["items"] = new JsonArray(new JsonObject { ["type"] = "number", ["a"] = new JsonArray(3, 4), ["label"] = "z" }), ["title"] = "z on the Argand plane" })}",
                    "hypercube" => $"overlay_diagram {Json(new JsonObject { ["kind"] = "hypercube", ["dim"] = 4, ["title"] = "A tesseract" })} (then explain the extra dimension by analogy)",
                    _ => $"overlay_diagram {Json(new JsonObject { ["kind"] = "space3d", ["measure"] = "skew_lines", ["items"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(1, 0, 2), ["b"] = new JsonArray(2, -1, 3), ["label"] = "L1" }, new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0, 0), ["b"] = new JsonArray(1, 1, 1), ["label"] = "L2" }), ["title"] = "The two lines" })} (put in the problem's own points and directions; measure: skew_lines, point_plane, angle, cross_product or none; Vernier computes the numbers)",
                },
            };

    private static string Json(JsonObject o) => o.ToJsonString();

    // A short search topic from the question: its words without filler, at most eight.
    private static string Topic(string question)
    {
        string[] filler = ["what", "does", "this", "the", "how", "do", "i", "is", "a", "an", "of", "to", "me", "can", "you", "please", "explain", "why", "here", "my", "it"];
        var words = question.ToLowerInvariant().Split([' ', ',', '.', '?', '!', ':', ';'], StringSplitOptions.RemoveEmptyEntries).Where(w => !filler.Contains(w)).Take(8);
        return string.Join(" ", words);
    }
}
