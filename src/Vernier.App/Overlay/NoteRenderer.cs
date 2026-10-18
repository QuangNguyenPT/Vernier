using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using WpfMath.Controls;

namespace Vernier.App;

/// <summary>
/// Draws a note card to a bitmap once: a dark template (dark whatever the theme, because it floats over arbitrary
/// content) with the tutor's blue rule, a title, and lines written in a handwriting face with formulas typeset
/// beside them. The glass then draws the bitmap like any other mark, so a card costs one image per frame, not a layout.
/// </summary>
public static class NoteRenderer
{
    private static readonly Color Paper = Color.FromArgb(0xF0, 0x0C, 0x14, 0x1B), Edge = Color.FromRgb(0x45, 0x59, 0x6A);
    private static readonly Color Ink = Color.FromRgb(0xE4, 0xEC, 0xF2), Soft = Color.FromRgb(0xB2, 0xC0, 0xCB), Blue = Color.FromRgb(0x6C, 0xC4, 0xFF),
        Green = Color.FromRgb(0x6F, 0xD9, 0x8A), Red = Color.FromRgb(0xFF, 0x8A, 0x8A);

    private static readonly FontFamily Hand = new("Ink Free, Segoe Print, Segoe Script, Segoe UI");
    private static readonly FontFamily Clean = new("Sitka Text, Cambria, Georgia");
    private static readonly FontFamily Heading = new("Sitka Subheading, Sitka Heading, Cambria, Georgia");

    public sealed record Card(BitmapSource Image, Size SizeDip);

    // The card rendered for the tool (to learn its size and find room) is reused when the glass draws it.
    private static readonly Dictionary<Guid, (Card Card, double Scale)> Cache = [];

    public static void Remember(Guid id, Card card, double scale)
    {
        lock (Cache)
        {
            if (Cache.Count >= 32) Cache.Remove(Cache.Keys.First());
            Cache[id] = (card, scale);
        }
    }

    /// <summary>The card for a note mark at this scale: the remembered one, or drawn again (the monitor's scale changed).</summary>
    public static Card CardFor(Guid id, NoteContent note, double widthDip, double scale)
    {
        lock (Cache)
            if (Cache.TryGetValue(id, out var c) && Math.Abs(c.Scale - scale) < 0.01) return c.Card;
        var card = Render(note, widthDip, scale);
        Remember(id, card, scale);
        return card;
    }

    private static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>Lays the card out at <paramref name="widthDip"/> and renders it at <paramref name="dpiScale"/> (physical pixels per DIP). UI thread.</summary>
    public static Card Render(NoteContent note, double widthDip, double dpiScale)
    {
        var column = new StackPanel { Margin = new Thickness(18, 12, 16, 14) };
        if (note.Title is { Length: > 0 } title)
            column.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontFamily = Heading, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brush(Blue), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
        foreach (var line in note.Lines) column.Children.Add(Line(line, note.Handwriting));

        var rule = new Border { Width = 3, Background = Brush(Blue), CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 12, 0, 12), HorizontalAlignment = HorizontalAlignment.Left };
        var grid = new Grid();
        grid.Children.Add(column);
        grid.Children.Add(rule);
        var card = new Border { Child = grid, Background = Brush(Paper), BorderBrush = Brush(Edge), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Width = widthDip, UseLayoutRounding = true };
        card.Measure(new Size(widthDip, double.PositiveInfinity));
        card.Arrange(new Rect(card.DesiredSize));
        card.UpdateLayout();

        var size = card.DesiredSize;
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(size.Width * dpiScale)), Math.Max(1, (int)Math.Ceiling(size.Height * dpiScale)), 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
        bitmap.Render(card);
        bitmap.Freeze();
        return new Card(bitmap, size);
    }

    private static UIElement Line(NoteLine line, bool handwriting)
    {
        var colour = line.Kind switch { NoteLineKind.Note => Soft, NoteLineKind.Error => Red, NoteLineKind.Result => Blue, NoteLineKind.Check => Green, _ => Ink };
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 7) };
        if (line.Text.Length > 0)
        {
            var prefix = line.Kind == NoteLineKind.Check ? "✓ " : line.Kind == NoteLineKind.Error ? "✗ " : "";
            panel.Children.Add(new TextBlock
            {
                Text = prefix + line.Text, TextWrapping = TextWrapping.Wrap, Foreground = Brush(colour),
                FontFamily = handwriting ? Hand : Clean, FontSize = handwriting ? 19 : 15.5, LineHeight = handwriting ? 25 : 22,
                FontStyle = line.Kind == NoteLineKind.Note ? FontStyles.Italic : FontStyles.Normal,
            });
        }
        if (line.Math is { Length: > 0 } tex && Formula(tex, colour, line.Kind == NoteLineKind.Result ? 24 : 20) is { } formula)
        {
            formula.Margin = new Thickness(line.Kind == NoteLineKind.Result ? 8 : 4, line.Text.Length > 0 ? 3 : 0, 0, 0);
            formula.HorizontalAlignment = HorizontalAlignment.Left;
            if (line.Kind == NoteLineKind.Result)
                return new Border { Child = new StackPanel { Children = { panel, formula }, Margin = new Thickness(8, 5, 10, 6) }, BorderBrush = Brush(Blue), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 4, 0, 9), HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(formula);
        }
        else if (line.Math is { Length: > 0 } source)
        {
            // TeX the engine cannot read is shown as written, never dropped.
            panel.Children.Add(new TextBlock { Text = source, Foreground = Brush(colour), FontFamily = Clean, FontSize = 16, TextWrapping = TextWrapping.Wrap });
        }
        return panel;
    }

    private static FormulaControl? Formula(string tex, Color colour, double size)
    {
        try
        {
            var source = tex.Replace(@"\tfrac", @"\frac", StringComparison.Ordinal).Replace(@"\dfrac", @"\frac", StringComparison.Ordinal);
            var control = new FormulaControl { Formula = source, Scale = size, SystemTextFontName = "Sitka Text", Foreground = Brush(colour) };
            return control.HasError ? null : control;
        }
        catch (Exception e) when (e is XamlMath.Exceptions.TexException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
