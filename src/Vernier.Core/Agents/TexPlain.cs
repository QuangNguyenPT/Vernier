using System.Text;
using System.Text.RegularExpressions;

namespace Vernier.Core.Agents;

/// <summary>
/// TeX written where only plain text is drawn (a label, the words of a note line, a formula that did not parse) becomes the
/// symbols it means: \Delta → Δ, \frac{a}{b} → a/b, v^{2} → v², \vec{F} → F⃗. Nothing is dropped: what cannot be turned into
/// a symbol loses only its backslash and braces, so the learner never sees raw markup.
/// </summary>
public static partial class TexPlain
{
    private static readonly Dictionary<string, string> Symbols = new()
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε", ["varepsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η", ["theta"] = "θ",
        ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ", ["pi"] = "π", ["rho"] = "ρ", ["sigma"] = "σ", ["tau"] = "τ", ["phi"] = "φ", ["varphi"] = "φ",
        ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω", ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Pi"] = "Π", ["Sigma"] = "Σ",
        ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω", ["times"] = "×", ["cdot"] = "·", ["div"] = "÷", ["pm"] = "±", ["mp"] = "∓", ["approx"] = "≈", ["neq"] = "≠", ["ne"] = "≠",
        ["leq"] = "≤", ["le"] = "≤", ["geq"] = "≥", ["ge"] = "≥", ["infty"] = "∞", ["to"] = "→", ["rightarrow"] = "→", ["Rightarrow"] = "⇒", ["leftarrow"] = "←",
        ["therefore"] = "∴", ["propto"] = "∝", ["partial"] = "∂", ["nabla"] = "∇", ["int"] = "∫", ["sum"] = "Σ", ["prod"] = "Π", ["in"] = "∈", ["perp"] = "⊥",
        ["parallel"] = "∥", ["angle"] = "∠", ["circ"] = "°", ["degree"] = "°", ["ldots"] = "…", ["dots"] = "…", ["cdots"] = "⋯", ["hbar"] = "ħ", ["ell"] = "ℓ", ["sqrt"] = "√", ["frac"] = "", ["vec"] = "", ["mathrm"] = "", ["text"] = "", ["mathbf"] = "",
    };

    private const string SupDigits = "⁰¹²³⁴⁵⁶⁷⁸⁹", SubDigits = "₀₁₂₃₄₅₆₇₈₉";

    [GeneratedRegex(@"\\[dt]?frac\{([^{}]*)\}\{([^{}]*)\}")] private static partial Regex Frac();
    [GeneratedRegex(@"\\sqrt\{([^{}]*)\}")] private static partial Regex Sqrt();
    [GeneratedRegex(@"\\(?:text|mathrm|mathbf|mathit|operatorname|textbf|mathbb|boldsymbol)\{([^{}]*)\}")] private static partial Regex Wrapper();
    [GeneratedRegex(@"\\(?:vec|overrightarrow)\{([^{}]*)\}")] private static partial Regex Vec();
    [GeneratedRegex(@"\\(?:hat|bar|overline)\{([^{}]*)\}")] private static partial Regex Mark();
    [GeneratedRegex(@"\^\{?([0-9+\-]{1,4})\}?")] private static partial Regex Sup();
    [GeneratedRegex(@"_\{?([0-9]{1,3})\}?")] private static partial Regex Sub();
    [GeneratedRegex(@"\\[a-zA-Z]+")] private static partial Regex Command();
    [GeneratedRegex(@"[ \t]{2,}")] private static partial Regex Spaces();

    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text) || (!text.Contains('\\') && !text.Contains('^') && !text.Contains('_') && !text.Contains('{'))) return text;
        var s = text.Replace(@"^\circ", "°").Replace(@"^{\circ}", "°").Replace(@"\(", "").Replace(@"\)", "").Replace(@"\[", "").Replace(@"\]", "").Replace(@"\left", "").Replace(@"\right", "")
                    .Replace(@"\,", " ").Replace(@"\;", " ").Replace(@"\!", "").Replace(@"\quad", "  ").Replace(@"\qquad", "  ").Replace(@"\ ", " ").Replace(@"\%", "%").Replace(@"\$", "$");
        for (var pass = 0; pass < 4; pass++)
        {
            s = Frac().Replace(s, m => $"{Wrap(m.Groups[1].Value)}/{Wrap(m.Groups[2].Value)}");
            s = Sqrt().Replace(s, m => "√" + (m.Groups[1].Value.Length > 1 ? $"({m.Groups[1].Value})" : m.Groups[1].Value));
            s = Wrapper().Replace(s, "$1");
            s = Vec().Replace(s, m => m.Groups[1].Value + "\u20D7");
            s = Mark().Replace(s, "$1");
        }
        s = Sup().Replace(s, m => Script(m.Groups[1].Value, SupDigits, "⁺", "⁻"));
        s = Sub().Replace(s, m => Script(m.Groups[1].Value, SubDigits, "₊", "₋"));
        s = Command().Replace(s, m => Symbols.TryGetValue(m.Value[1..], out var sym) ? sym : m.Value[1..]);
        s = s.Replace("{", "").Replace("}", "").Replace("~", " ");
        return Spaces().Replace(s, " ").Trim();
    }

    private static string Wrap(string part) => part.Length > 1 && part.Any(c => c is '+' or '-' or ' ' or '×') ? $"({part})" : part;

    private static string Script(string digits, string table, string plus, string minus)
    {
        var sb = new StringBuilder();
        foreach (var c in digits) sb.Append(c == '+' ? plus : c == '-' ? minus : table[c - '0']);
        return sb.ToString();
    }
}
