using System.Globalization;

namespace Vernier.Core.Verification;

/// <summary>
/// Small arithmetic evaluator used to recompute numeric claims. Supports + - * / ^, unary minus,
/// parentheses, scientific notation, common functions and the constants pi and e.
/// Anything it cannot parse is reported as unparsed, never guessed.
/// </summary>
public static class Expr
{
    private const int MaxLength = 400;
    private const int MaxDepth = 48;

    public static bool TryEvaluate(string? expression, out double value, out string? error)
    {
        value = double.NaN;
        error = null;
        if (string.IsNullOrWhiteSpace(expression)) { error = "empty expression"; return false; }
        if (expression.Length > MaxLength) { error = "expression too long"; return false; }
        var text = expression
            .Replace("×", "*").Replace("·", "*").Replace("÷", "/").Replace("−", "-")
            .Replace("**", "^").Replace("π", "pi").Replace("√", "sqrt");
        try
        {
            var p = new Parser(text);
            value = p.ParseExpression(0);
            p.SkipSpaces();
            if (!p.AtEnd) { error = $"unexpected '{p.Peek}' at position {p.Position}"; return false; }
            if (double.IsNaN(value) || double.IsInfinity(value)) { error = "result is not a finite number"; return false; }
            return true;
        }
        catch (FormatException e)
        {
            error = e.Message;
            return false;
        }
    }

    /// <summary>
    /// True when <paramref name="computed"/> rounds to the claimed figure at the precision the
    /// claim was written with ("3.14" accepts 3.135..3.145; "3.2e5" accepts 3.15e5..3.25e5).
    /// </summary>
    public static bool MatchesClaim(double computed, string claimed, out double claimedValue)
    {
        claimedValue = double.NaN;
        var s = claimed.Trim().Replace("−", "-").Replace(",", "").Replace("×10^", "e").Replace("x10^", "e").Replace("*10^", "e");
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out claimedValue)) return false;

        var mantissa = s;
        var exponent = 0;
        var eAt = s.IndexOfAny(['e', 'E']);
        if (eAt >= 0)
        {
            mantissa = s[..eAt];
            if (!int.TryParse(s[(eAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return false;
        }
        var dot = mantissa.IndexOf('.');
        var decimals = dot < 0 ? 0 : mantissa.Length - dot - 1;
        var tolerance = 0.5 * Math.Pow(10, exponent - decimals);
        // Slack for binary floating point at the rounding boundary.
        return Math.Abs(computed - claimedValue) <= tolerance * 1.000001 + Math.Abs(claimedValue) * 1e-12;
    }

    private sealed class Parser(string text)
    {
        public int Position { get; private set; }
        public bool AtEnd => Position >= text.Length;
        public char Peek => AtEnd ? '\0' : text[Position];

        public void SkipSpaces()
        {
            while (!AtEnd && char.IsWhiteSpace(text[Position])) Position++;
        }

        public double ParseExpression(int depth)
        {
            if (depth > MaxDepth) throw new FormatException("expression nested too deeply");
            var left = ParseTerm(depth);
            while (true)
            {
                SkipSpaces();
                if (Peek == '+') { Position++; left += ParseTerm(depth); }
                else if (Peek == '-') { Position++; left -= ParseTerm(depth); }
                else return left;
            }
        }

        private double ParseTerm(int depth)
        {
            var left = ParseUnary(depth);
            while (true)
            {
                SkipSpaces();
                if (Peek == '*') { Position++; left *= ParseUnary(depth); }
                else if (Peek == '/')
                {
                    Position++;
                    var right = ParseUnary(depth);
                    if (right == 0) throw new FormatException("division by zero");
                    left /= right;
                }
                else return left;
            }
        }

        // Unary minus binds looser than ^ so that -2^2 = -4, as in written mathematics.
        private double ParseUnary(int depth)
        {
            SkipSpaces();
            if (Peek == '-') { Position++; return -ParseUnary(depth + 1); }
            if (Peek == '+') { Position++; return ParseUnary(depth + 1); }
            return ParsePower(depth);
        }

        private double ParsePower(int depth)
        {
            var b = ParsePrimary(depth);
            SkipSpaces();
            if (Peek != '^') return b;
            Position++;
            var exponent = ParseUnary(depth + 1); // right associative
            return Math.Pow(b, exponent);
        }

        private double ParsePrimary(int depth)
        {
            SkipSpaces();
            if (Peek == '(')
            {
                Position++;
                var v = ParseExpression(depth + 1);
                SkipSpaces();
                if (Peek != ')') throw new FormatException("missing ')'");
                Position++;
                return v;
            }
            if (char.IsDigit(Peek) || Peek == '.') return ParseNumber();
            if (char.IsLetter(Peek))
            {
                var start = Position;
                while (!AtEnd && (char.IsLetterOrDigit(text[Position]) || text[Position] == '_')) Position++;
                var name = text[start..Position].ToLowerInvariant();
                SkipSpaces();
                if (Peek != '(')
                    return name switch
                    {
                        "pi" => Math.PI,
                        "e" => Math.E,
                        _ => throw new FormatException($"unknown symbol '{name}' (only numbers can be recomputed)"),
                    };
                Position++;
                var arg = ParseExpression(depth + 1);
                SkipSpaces();
                if (Peek != ')') throw new FormatException("missing ')'");
                Position++;
                return name switch
                {
                    "sqrt" => arg < 0 ? throw new FormatException("square root of a negative number") : Math.Sqrt(arg),
                    "abs" => Math.Abs(arg),
                    "sin" => Math.Sin(arg),
                    "cos" => Math.Cos(arg),
                    "tan" => Math.Tan(arg),
                    "asin" => Math.Asin(arg),
                    "acos" => Math.Acos(arg),
                    "atan" => Math.Atan(arg),
                    "exp" => Math.Exp(arg),
                    "ln" => arg <= 0 ? throw new FormatException("ln of a non-positive number") : Math.Log(arg),
                    "log" or "log10" => arg <= 0 ? throw new FormatException("log of a non-positive number") : Math.Log10(arg),
                    "log2" => arg <= 0 ? throw new FormatException("log of a non-positive number") : Math.Log2(arg),
                    _ => throw new FormatException($"unknown function '{name}'"),
                };
            }
            throw new FormatException(AtEnd ? "unexpected end of expression" : $"unexpected '{Peek}' at position {Position}");
        }

        private double ParseNumber()
        {
            var start = Position;
            while (!AtEnd && (char.IsDigit(text[Position]) || text[Position] == '.')) Position++;
            if (!AtEnd && (text[Position] == 'e' || text[Position] == 'E'))
            {
                var save = Position++;
                if (!AtEnd && (text[Position] == '+' || text[Position] == '-')) Position++;
                if (AtEnd || !char.IsDigit(text[Position])) Position = save; // 'e' was the constant, not an exponent
                else while (!AtEnd && char.IsDigit(text[Position])) Position++;
            }
            return double.TryParse(text[start..Position], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw new FormatException($"bad number '{text[start..Position]}'");
        }
    }
}

/// <summary>SI dimensional analysis: do two unit expressions describe the same physical dimension?</summary>
public static class Units
{
    // Exponents of m, kg, s, A, K, mol, cd.
    private static readonly Dictionary<string, int[]> Known = new(StringComparer.Ordinal)
    {
        ["1"] = [0, 0, 0, 0, 0, 0, 0], ["rad"] = [0, 0, 0, 0, 0, 0, 0], ["%"] = [0, 0, 0, 0, 0, 0, 0],
        ["m"] = [1, 0, 0, 0, 0, 0, 0], ["g"] = [0, 1, 0, 0, 0, 0, 0], ["kg"] = [0, 1, 0, 0, 0, 0, 0],
        ["s"] = [0, 0, 1, 0, 0, 0, 0], ["A"] = [0, 0, 0, 1, 0, 0, 0], ["K"] = [0, 0, 0, 0, 1, 0, 0],
        ["mol"] = [0, 0, 0, 0, 0, 1, 0], ["cd"] = [0, 0, 0, 0, 0, 0, 1],
        ["min"] = [0, 0, 1, 0, 0, 0, 0], ["h"] = [0, 0, 1, 0, 0, 0, 0],
        ["Hz"] = [0, 0, -1, 0, 0, 0, 0],
        ["N"] = [1, 1, -2, 0, 0, 0, 0],
        ["Pa"] = [-1, 1, -2, 0, 0, 0, 0],
        ["J"] = [2, 1, -2, 0, 0, 0, 0], ["eV"] = [2, 1, -2, 0, 0, 0, 0],
        ["W"] = [2, 1, -3, 0, 0, 0, 0],
        ["C"] = [0, 0, 1, 1, 0, 0, 0],
        ["V"] = [2, 1, -3, -1, 0, 0, 0],
        ["Ω"] = [2, 1, -3, -2, 0, 0, 0], ["ohm"] = [2, 1, -3, -2, 0, 0, 0],
        ["F"] = [-2, -1, 4, 2, 0, 0, 0],
        ["T"] = [0, 1, -2, -1, 0, 0, 0],
        ["Wb"] = [2, 1, -2, -1, 0, 0, 0],
        ["H"] = [2, 1, -2, -2, 0, 0, 0],
        ["L"] = [3, 0, 0, 0, 0, 0, 0],
    };

    private static readonly string[] Prefixes = ["k", "m", "c", "µ", "u", "n", "p", "M", "G", "d"];

    public static bool TryDimensions(string? unitExpression, out int[] dims, out string? error)
    {
        dims = new int[7];
        error = null;
        if (string.IsNullOrWhiteSpace(unitExpression)) { error = "empty unit"; return false; }
        var s = unitExpression.Replace("·", "*").Replace("⋅", "*").Replace("×", "*").Replace("²", "^2").Replace("³", "^3").Replace("⁻¹", "^-1").Trim();
        var sign = 1;
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '*' || char.IsWhiteSpace(c)) { i++; continue; }
            // A slash divides by the next unit only (ordinary precedence): "kg*m/s^2" and "J/kg/K" read as written.
            if (c == '/') { sign = -1; i++; continue; }
            if (c == '(' || c == ')') { error = "parentheses in units are not supported"; return false; }

            var start = i;
            while (i < s.Length && (char.IsLetter(s[i]) || s[i] == '%' || s[i] == 'Ω' || s[i] == 'µ' || (i == start && s[i] == '1'))) i++;
            var symbol = s[start..i];
            if (symbol.Length == 0) { error = $"unexpected '{c}' in unit"; return false; }

            var power = 1;
            if (i < s.Length && s[i] == '^')
            {
                i++;
                var pStart = i;
                if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (!int.TryParse(s[pStart..i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out power))
                { error = $"bad exponent after '{symbol}'"; return false; }
            }

            if (!TryLookup(symbol, out var unit)) { error = $"unknown unit '{symbol}'"; return false; }
            for (var k = 0; k < 7; k++) dims[k] += sign * power * unit[k];
            sign = 1;
        }
        return true;
    }

    public static bool? SameDimension(string? a, string? b, out string detail)
    {
        if (!TryDimensions(a, out var da, out var ea)) { detail = $"could not read '{a}': {ea}"; return null; }
        if (!TryDimensions(b, out var db, out var eb)) { detail = $"could not read '{b}': {eb}"; return null; }
        var same = da.AsSpan().SequenceEqual(db);
        detail = same ? $"{a} and {b} have the same dimension" : $"{a} is {Describe(da)} but {b} is {Describe(db)}";
        return same;
    }

    private static bool TryLookup(string symbol, out int[] dims)
    {
        if (Known.TryGetValue(symbol, out dims!)) return true;
        foreach (var p in Prefixes)
            if (symbol.Length > p.Length && symbol.StartsWith(p, StringComparison.Ordinal) && Known.TryGetValue(symbol[p.Length..], out dims!))
                return true;
        dims = [];
        return false;
    }

    private static string Describe(int[] d)
    {
        string[] names = ["m", "kg", "s", "A", "K", "mol", "cd"];
        var parts = Enumerable.Range(0, 7).Where(i => d[i] != 0).Select(i => d[i] == 1 ? names[i] : $"{names[i]}^{d[i]}").ToArray();
        return parts.Length == 0 ? "dimensionless" : string.Join("·", parts);
    }
}
