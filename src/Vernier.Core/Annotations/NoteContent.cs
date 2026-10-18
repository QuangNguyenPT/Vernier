namespace Vernier.Core.Annotations;

public enum NoteLineKind { Step, Result, Note, Check, Error }

/// <summary>One line of a note: words, a formula (TeX), or both. A formula is typeset; words are written in the note's hand.</summary>
public sealed record NoteLine(NoteLineKind Kind, string Text, string? Math = null);

/// <summary>
/// What the tutor writes on the screen in a note card: a worked solution beside the problem, an explanation, a checklist.
/// The card is dark whatever the theme, because it floats over arbitrary content.
/// </summary>
public sealed record NoteContent(string? Title, IReadOnlyList<NoteLine> Lines, bool Handwriting = true)
{
    public const int MaxLines = 14, MaxTextChars = 220, MaxMathChars = 160;

    /// <summary>A guess at the card's height in DIPs before it is drawn, to look for room (the renderer measures the real one).</summary>
    public double EstimateHeight(double widthDip)
    {
        var charsPerLine = Math.Max(12, widthDip / (Handwriting ? 9.0 : 7.6));
        var height = 28.0 + (Title is { Length: > 0 } ? 30 : 0);
        foreach (var line in Lines)
        {
            var text = line.Text.Length == 0 ? 0 : Math.Ceiling(line.Text.Length / charsPerLine) * (Handwriting ? 28 : 22);
            var math = line.Math is { Length: > 0 } ? 40 : 0;
            height += text + math + 8 + (line.Kind == NoteLineKind.Result ? 10 : 0);
        }
        return height;
    }
}
