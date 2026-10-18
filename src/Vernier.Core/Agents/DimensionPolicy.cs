using System.Text.RegularExpressions;

namespace Vernier.Core.Agents;

public sealed record VisualDemand(bool Required, string Reason)
{
    public static readonly VisualDemand None = new(false, "");
}

/// <summary>
/// Problems about space, vectors, complex numbers, higher dimensions, and fields are understood by seeing them: decided by
/// code from the question and what is on the screen, so the tutor is made to draw it (overlay_diagram) every time, not when it
/// happens to think of it. Checked after the answer by <see cref="DimensionGate"/>.
/// </summary>
public static partial class DimensionPolicy
{
    [GeneratedRegex(@"\b(vectors?|cross product|dot product|scalar product|skew lines?|normal vector|direction vector|position vector|unit vector|eigenvectors?|projections? of|3\s?-?d|three[- ]dimensional|2\s?-?d|4\s?-?d|four[- ]dimensional|fourth dimension|n[- ]dimensional|higher dimensions?|hypercube|tesseract|quaternions?|complex numbers?|imaginary|argand|de moivre|modulus and argument|polar form|torque|angular momentum|magnetic field|electric field|curl|divergence|gradient of|spherical|cylindrical|tangent plane|plane through|equation of (?:a |the )?(?:plane|line)|lines? (?:intersect|are parallel)|shortest distance|dimensions? of)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Terms();

    [GeneratedRegex(@"(?<![\p{L}])(?:\bi\s?\^\s?2|\bz\s?=\s?[-\d.]+\s?[+-]\s?[-\d.]*\s?i\b|[-\d.]+\s?[+-]\s?[-\d.]*\bi\b|\\vec|\\mathbf\{[a-z]\}|\\times)", RegexOptions.IgnoreCase)]
    private static partial Regex Notation();

    [GeneratedRegex(@"\binclined plane\b|\bplane (?:mirror|wave)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotAPlaneProblem();

    public static VisualDemand Decide(string question, string? screenText)
    {
        var text = NotAPlaneProblem().Replace(question + "\n" + (screenText is { Length: > 4000 } ? screenText[..4000] : screenText), " ");
        if (Terms().Match(question) is { Success: true } q) return new(true, $"the question is about \"{q.Value.ToLowerInvariant()}\": it is understood by seeing it");
        if (Terms().Match(text) is { Success: true } t) return new(true, $"the problem on the screen involves \"{t.Value.ToLowerInvariant()}\": it is understood by seeing it");
        if (Notation().IsMatch(text)) return new(true, "the problem uses vectors or complex numbers: it is understood by seeing it");
        return VisualDemand.None;
    }
}

/// <summary>The backstop: a required visual that was not drawn gets the answer asked for again, once.</summary>
public static class DimensionGate
{
    public static string? Check(VisualDemand demand, bool diagramDrawn, bool toolOffered)
    {
        if (!demand.Required || !toolOffered || diagramDrawn) return null;
        return $"Before this answer can be shown it has to be drawn: a diagram was required because {demand.Reason}, and none was drawn. " +
               "Call overlay_diagram now (space3d for vectors, lines, planes and 3D physics, with the numbers worked out for you by measure; complex for the Argand plane; hypercube for 4D), " +
               "then write the whole answer again around what the learner can see in it. For a dimension above four, draw the 3D slice or the tesseract and explain the rest by analogy.";
    }
}
