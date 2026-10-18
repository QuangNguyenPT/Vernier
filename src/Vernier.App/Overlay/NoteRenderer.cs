using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
    private static readonly Color Paper = Color.FromArgb(0xF2, 0x0B, 0x12, 0x18), PaperTop = Color.FromArgb(0xF2, 0x13, 0x1E, 0x27), Edge = Color.FromRgb(0x45, 0x59, 0x6A);
    private static readonly Color Ink = Color.FromRgb(0xF4, 0xF7, 0xFA), Soft = Color.FromRgb(0xC9, 0xD4, 0xDD), Blue = Color.FromRgb(0x6C, 0xC4, 0xFF),
        Green = Color.FromRgb(0x6F, 0xD9, 0x8A), Red = Color.FromRgb(0xFF, 0x8A, 0x8A);

    private static readonly FontFamily Hand = new("Segoe Print, Ink Free, Segoe Script, Segoe UI, Cambria Math, Segoe UI Symbol"); // sturdy and even: legible at a glance over any page
    private static readonly FontFamily Clean = new("Sitka Text, Cambria, Georgia, Cambria Math, Segoe UI Symbol");
    private static readonly FontFamily Heading = new("Sitka Subheading, Sitka Heading, Cambria, Georgia");

    /// <param name="Image">The card with its soft shadow round it: <see cref="ShadowMargin"/> wider than the card on every side.</param>
    /// <param name="SizeDip">The card itself, without the shadow: what is placed on free space.</param>
    public sealed record Card(BitmapSource Image, Size SizeDip);

    /// <summary>How far the card's shadow reaches past its edge (DIPs).</summary>
    public const double ShadowMargin = 14;

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
            column.Children.Add(new TextBlock { Text = TexPlain.Clean(title).ToUpperInvariant(), FontFamily = Heading, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brush(Blue), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
        foreach (var line in note.Lines) column.Children.Add(Line(line, note.Handwriting));

        return Wrap(WithRule(column), widthDip, dpiScale);
    }

    /// <summary>The blue rule down the left edge that marks a card as the tutor's.</summary>
    public static UIElement WithRule(UIElement content)
    {
        var rule = new Border { Width = 3, Background = Brush(Blue), CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 12, 0, 12), HorizontalAlignment = HorizontalAlignment.Left };
        var grid = new Grid();
        grid.Children.Add(content);
        grid.Children.Add(rule);
        return grid;
    }

    /// <summary>The card itself: dark paper lit from above, a lit rim, a soft shadow, rendered once to a bitmap. Shared by note cards and diagrams.</summary>
    public static Card Wrap(UIElement grid, double widthDip, double dpiScale)
    {
        // Depth from light above: the paper is a shade lighter at the top, its rim is lit along the top and fades down the
        // sides, and it sits on a soft shadow, so it reads as a card lying over the page rather than a box cut out of it.
        var paper = new LinearGradientBrush(PaperTop, Paper, 90);
        paper.Freeze();
        var rim = new LinearGradientBrush(Edge, Color.FromArgb(0x40, Edge.R, Edge.G, Edge.B), 90);
        rim.Freeze();
        var card = new Border
        {
            Child = grid, Background = paper, BorderBrush = rim, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Width = widthDip, UseLayoutRounding = true,
        };
        // The shadow is cast by a plain shape behind the card, not by the card: an effect on the card would rasterise its writing through the effect pass and soften it.
        var shadow = new Border
        {
            Background = Brushes.Black, CornerRadius = new CornerRadius(12),
            Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Direction = 270, Opacity = 0.55, Color = Colors.Black, RenderingBias = RenderingBias.Quality },
        };
        var host = new Grid { Margin = new Thickness(ShadowMargin), Children = { shadow, card } };
        host.Measure(new Size(widthDip + 2 * ShadowMargin, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();

        var outer = host.DesiredSize; // the margin is included: the card plus room for its shadow
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(outer.Width * dpiScale)), Math.Max(1, (int)Math.Ceiling(outer.Height * dpiScale)), 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
        bitmap.Render(host);
        bitmap.Freeze();
        return new Card(bitmap, card.RenderSize);
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
                Text = prefix + TexPlain.Clean(line.Text), TextWrapping = TextWrapping.Wrap, Foreground = Brush(colour),
                FontFamily = handwriting ? Hand : Clean, FontSize = handwriting ? 17.5 : 16.5, LineHeight = handwriting ? 28 : 24,
                FontStyle = line.Kind == NoteLineKind.Note ? FontStyles.Italic : FontStyles.Normal,
            });
        }
        if (line.Math is { Length: > 0 } tex && Formula(tex, colour, line.Kind == NoteLineKind.Result ? 26 : 22) is { } formula)
        {
            // A formula wider than the card is scaled down to fit, never cut off.
            var fitted = new Viewbox
            {
                Child = formula, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(line.Kind == NoteLineKind.Result ? 8 : 4, line.Text.Length > 0 ? 3 : 0, 0, 0),
            };
            if (line.Kind == NoteLineKind.Result)
                return new Border { Child = new StackPanel { Children = { panel, fitted }, Margin = new Thickness(8, 5, 10, 6) }, BorderBrush = Brush(Blue), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(9), Margin = new Thickness(0, 4, 0, 9), HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(fitted);
        }
        else if (line.Math is { Length: > 0 } source)
        {
            // TeX the engine cannot read is shown as written, never dropped.
            panel.Children.Add(new TextBlock { Text = TexPlain.Clean(source), Foreground = Brush(colour), FontFamily = Clean, FontSize = 16, TextWrapping = TextWrapping.Wrap });
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
