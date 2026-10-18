using System.Text.RegularExpressions;
using Vernier.Core.Agents;

namespace Vernier.Core.Diagrams;

public enum DiagramKind { Space3D, Complex, Hypercube }

/// <summary>
/// One thing in a diagram. Space3D: point (a), vector (a = from, b = to), line (a = point, b = direction, range = t from..to),
/// plane (a = point, b = normal, range = [half size]), segment (a, b). Complex: number (a = [re, im]), circle (a = centre, b = [radius]).
/// </summary>
public sealed record DiagramItem(string Type, double[] A, double[]? B = null, string? Label = null, string? Tone = null, double[]? Range = null);

/// <param name="Caption">What was worked out, one short line each (numbers computed by code).</param>
/// <param name="Dim">Hypercube: 3 (cube) or 4 (tesseract).</param>
public sealed record DiagramSpec(DiagramKind Kind, string? Title, IReadOnlyList<DiagramItem> Items, IReadOnlyList<string> Caption, int Dim = 3, double Angle = 30);

/// <summary>Everything in a diagram that can be drawn is checked here once, so the renderer never meets a malformed item.</summary>
public static class DiagramBuilder
{
    public const int MaxItems = 16, MaxCaption = 4;
    private static readonly HashSet<string> Space = ["point", "vector", "line", "plane", "segment"], Argand = ["number", "circle"];
    public static readonly string[] Tones = ["main", "result", "warn", "soft"];

    /// <summary>Why this item cannot be drawn, or null.</summary>
    public static string? Problem(DiagramKind kind, DiagramItem item)
    {
        var allowed = kind == DiagramKind.Space3D ? Space : kind == DiagramKind.Complex ? Argand : [];
        if (!allowed.Contains(item.Type)) return $"\"{item.Type}\" cannot be drawn in this diagram (use {string.Join(", ", allowed)})";
        if (item.A is not { Length: >= 2 } || item.A.Any(v => !double.IsFinite(v))) return "a needs numbers (x, y, z)";
        if (item.Type is "vector" or "line" or "plane" or "segment" or "circle" && (item.B is not { Length: >= 1 } || item.B.Any(v => !double.IsFinite(v)))) return "b needs numbers";
        if (item.Type is "line" or "plane" && item.B is { } d && d.Length >= 2 && d.All(v => Math.Abs(v) < 1e-12)) return "the direction/normal is the zero vector";
        if (item.Type == "circle" && item.B![0] <= 0) return "the radius must be positive";
        return null;
    }

    /// <summary>Text for drawing: TeX turned into symbols, length capped.</summary>
    public static string? Text(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var clean = TexPlain.Clean(s.Trim());
        return clean.Length > 28 ? clean[..28] : clean;
    }

    public static string Number(double v) => Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture) : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
