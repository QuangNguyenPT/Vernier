using System.Text;
using System.Text.RegularExpressions;

namespace Vernier.Core.Agents;

public enum SpanKind { Text, Bold, Italic, Code, Math, Superscript, Subscript }
public sealed record AnswerSpan(SpanKind Kind, string Text);

public enum BlockKind { Paragraph, Heading, Bullet, Numbered, DisplayMath, Code }
public sealed record AnswerBlock(BlockKind Kind, IReadOnlyList<AnswerSpan> Spans, int Number = 0);

/// <summary>
/// Splits a tutor's answer into blocks and spans a renderer can typeset: paragraphs, headings,
/// lists, code, and mathematics written as TeX between \( \) or \[ \] (or $ … $ when it is
/// clearly maths). Caret superscripts in plain text (x^2, 10^{-3}) become superscripts. Anything
/// unterminated while streaming stays plain text, so the answer never disappears or flickers.
/// </summary>
public static partial class AnswerText
{
    [GeneratedRegex(@"^\s*(?:[-*•])\s+(.*)$")] private static partial Regex Bullet();
    [GeneratedRegex(@"^\s*(\d{1,3})[.)]\s+(.*)$")] private static partial Regex Numbered();
    [GeneratedRegex(@"^\s*#{1,4}\s+(.*)$")] private static partial Regex Heading();
    // Superscript in plain text: a letter, digit or closing bracket, then ^ and a short exponent (a number, or one or two
    // letters); a sentence's full stop after "x^2." is not part of it.
    [GeneratedRegex(@"(?<=[\p{L}\p{N})\]])\^(\{[^{}]{1,24}\}|[+-]?(?:\p{N}{1,4}(?:\.\p{N}{1,3})?|\p{L}{1,2}))")] private static partial Regex Caret();
    [GeneratedRegex(@"(?<=\p{L})_\{([^{}]{1,24})\}")] private static partial Regex BracedSub();

    public static IReadOnlyList<AnswerBlock> Parse(string text)
    {
        var blocks = new List<AnswerBlock>();
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        var paragraph = new StringBuilder();
        void Flush()
        {
            if (paragraph.Length == 0) return;
            blocks.Add(new AnswerBlock(BlockKind.Paragraph, Spans(paragraph.ToString().Trim())));
            paragraph.Clear();
        }
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                var code = new StringBuilder();
                for (i++; i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal); i++) code.AppendLine(lines[i]);
                blocks.Add(new AnswerBlock(BlockKind.Code, [new AnswerSpan(SpanKind.Code, code.ToString().TrimEnd())]));
                continue;
            }
            if (trimmed.Length == 0) { Flush(); continue; }
            if (DisplayMath(trimmed, lines, ref i) is { } tex) { Flush(); blocks.Add(new AnswerBlock(BlockKind.DisplayMath, [new AnswerSpan(SpanKind.Math, tex)])); continue; }
            if (Heading().Match(line) is { Success: true } h) { Flush(); blocks.Add(new AnswerBlock(BlockKind.Heading, Spans(h.Groups[1].Value))); continue; }
            if (Bullet().Match(line) is { Success: true } b) { Flush(); blocks.Add(new AnswerBlock(BlockKind.Bullet, Spans(b.Groups[1].Value))); continue; }
            if (Numbered().Match(line) is { Success: true } n) { Flush(); blocks.Add(new AnswerBlock(BlockKind.Numbered, Spans(n.Groups[2].Value), int.Parse(n.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))); continue; }
            if (paragraph.Length > 0) paragraph.Append(' ');
            paragraph.Append(trimmed);
        }
        Flush();
        return blocks;
    }

    // \[ … \] or $$ … $$ on its own, possibly over several lines. Unclosed: not display maths (yet).
    private static string? DisplayMath(string first, string[] lines, ref int i)
    {
        string open, close;
        if (first.StartsWith(@"\[", StringComparison.Ordinal)) (open, close) = (@"\[", @"\]");
        else if (first.StartsWith("$$", StringComparison.Ordinal)) (open, close) = ("$$", "$$");
        else return null;
        var body = new StringBuilder(first[open.Length..]);
        var j = i;
        while (!body.ToString().TrimEnd().EndsWith(close, StringComparison.Ordinal))
        {
            if (++j >= lines.Length) return null;
            body.Append('\n').Append(lines[j]);
        }
        var s = body.ToString().TrimEnd();
        var tex = s[..^close.Length].Trim();
        if (tex.Length == 0) return null;
        i = j;
        return tex;
    }

    /// <summary>Inline spans of one block.</summary>
    public static IReadOnlyList<AnswerSpan> Spans(string text)
    {
        var spans = new List<AnswerSpan>();
        var plain = new StringBuilder();
        void Plain()
        {
            if (plain.Length == 0) return;
            // TeX the model wrote outside \( \) is shown as the symbols it means, never as markup.
            var t = plain.ToString();
            spans.AddRange(Scripts(t.Contains('\\') ? TexPlain.Clean(t) : t));
            plain.Clear();
        }
        for (var i = 0; i < text.Length;)
        {
            if (Enclosed(text, i, @"\(", @"\)") is { } m1) { Plain(); spans.Add(new(SpanKind.Math, m1.Inner)); i = m1.Next; continue; }
            // A formula still streaming (its closing has not arrived): show it as symbols until it can be typeset.
            if (string.CompareOrdinal(text, i, @"\(", 0, 2) == 0) { Plain(); spans.Add(new(SpanKind.Text, TexPlain.Clean(text[(i + 2)..]))); break; }
            if (text[i] == '$' && Enclosed(text, i, "$", "$") is { } m2 && LooksLikeMath(m2.Inner)) { Plain(); spans.Add(new(SpanKind.Math, m2.Inner)); i = m2.Next; continue; }
            if (text[i] == '`' && Enclosed(text, i, "`", "`") is { } c) { Plain(); spans.Add(new(SpanKind.Code, c.Inner)); i = c.Next; continue; }
            if (text[i] == '*' && Enclosed(text, i, "**", "**") is { } b) { Plain(); spans.Add(new(SpanKind.Bold, b.Inner)); i = b.Next; continue; }
            if (text[i] == '*' && Enclosed(text, i, "*", "*") is { Inner.Length: > 0 } it && !char.IsWhiteSpace(it.Inner[0])) { Plain(); spans.Add(new(SpanKind.Italic, it.Inner)); i = it.Next; continue; }
            plain.Append(text[i++]);
        }
        Plain();
        return spans;
    }

    private static (string Inner, int Next)? Enclosed(string text, int at, string open, string close)
    {
        if (string.CompareOrdinal(text, at, open, 0, open.Length) != 0) return null;
        var end = text.IndexOf(close, at + open.Length, StringComparison.Ordinal);
        if (end < 0) return null; // still streaming, or a lone symbol: stays plain text
        return (text[(at + open.Length)..end], end + close.Length);
    }

    // "$5 and $10" is money, "$x^2$" is maths.
    private static bool LooksLikeMath(string inner) =>
        inner.Length > 0 && !char.IsWhiteSpace(inner[0]) && !char.IsWhiteSpace(inner[^1]) && inner.IndexOfAny(['\\', '^', '_', '=', '{']) >= 0;

    // Plain text with ^ superscripts and _{} subscripts lifted out.
    private static IEnumerable<AnswerSpan> Scripts(string text)
    {
        var at = 0;
        foreach (Match m in Regex.Matches(text, Caret().ToString() + "|" + BracedSub().ToString()))
        {
            if (m.Index > at) yield return new(SpanKind.Text, text[at..m.Index]);
            var sup = m.Value.StartsWith('^');
            var inner = sup ? m.Groups[1].Value : m.Groups[2].Value;
            yield return new(sup ? SpanKind.Superscript : SpanKind.Subscript, inner.Trim('{', '}'));
            at = m.Index + m.Length;
        }
        if (at < text.Length) yield return new(SpanKind.Text, text[at..]);
    }

    /// <summary>What a formula sounds like, for screen readers and speech: "x squared plus 1 over 2".</summary>
    public static string Spoken(string tex)
    {
        var s = tex;
        s = Regex.Replace(s, @"\\[td]?frac\s*\{([^{}]*)\}\s*\{([^{}]*)\}", "$1 over $2");
        s = Regex.Replace(s, @"\\sqrt\s*\{([^{}]*)\}", "square root of $1");
        s = Regex.Replace(s, @"\^\{?2\}?", " squared");
        s = Regex.Replace(s, @"\^\{?3\}?", " cubed");
        s = Regex.Replace(s, @"\^\{([^{}]*)\}|\^(\S)", m => " to the power " + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value));
        s = Regex.Replace(s, @"_\{([^{}]*)\}|_(\S)", m => " sub " + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value));
        foreach (var (k, v) in new[] { ("\\int", "integral of"), ("\\sum", "sum of"), ("\\cdot", "times"), ("\\times", "times"), ("\\pm", "plus or minus"), ("\\le", "less than or equal to"), ("\\ge", "greater than or equal to"), ("\\neq", "not equal to"), ("\\approx", "approximately"), ("\\infty", "infinity"), ("\\to", "tends to"), ("\\partial", "partial") })
            s = s.Replace(k, " " + v + " ", StringComparison.Ordinal);
        s = Regex.Replace(s, @"\\([a-zA-Z]+)", "$1"); // \alpha → alpha
        s = s.Replace("=", " equals ").Replace("+", " plus ").Replace("{", "").Replace("}", "");
        s = Regex.Replace(s, @"(?<=\s|^)-(?=\s|\w)", " minus ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>The answer as it should be read aloud: formulas spoken, markup removed.</summary>
    public static string ForSpeech(string text) =>
        string.Join("\n", Parse(text).Select(b => Regex.Replace(string.Concat(b.Spans.Select(s => s.Kind switch
        {
            SpanKind.Math => " " + Spoken(s.Text) + " ",
            SpanKind.Superscript => s.Text == "2" ? " squared" : s.Text == "3" ? " cubed" : " to the power " + s.Text,
            SpanKind.Subscript => " sub " + s.Text,
            _ => s.Text,
        })), " {2,}", " ").Trim()));
}
