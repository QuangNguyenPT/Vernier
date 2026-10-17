using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Vernier.Core.Agents;
using WpfMath.Controls;

namespace Vernier.App.Views;

/// <summary>
/// A tutor answer, typeset: reading text in Sitka, mathematics laid out by a TeX engine (stacked
/// fractions, roots, real superscripts), lists and code. Read-only and selectable. Bind
/// <see cref="Source"/> to the answer text; it re-renders as the answer streams in.
/// </summary>
public sealed class RichAnswer : RichTextBox
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(RichAnswer), new PropertyMetadata("", (d, _) => ((RichAnswer)d).Render()));

    public string Source { get => (string)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public RichAnswer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        FocusVisualStyle = null;
        SetResourceReference(FontFamilyProperty, "ReadingFont");
        SetResourceReference(ForegroundProperty, "Ink");
        FontSize = 15.5;
        AutomationProperties.SetName(this, "Tutor answer");
        Document = new FlowDocument { PagePadding = new Thickness(0) };
    }

    private void Render()
    {
        var doc = new FlowDocument { PagePadding = new Thickness(0), LineHeight = FontSize * 1.5, TextAlignment = TextAlignment.Left };
        doc.SetResourceReference(FlowDocument.FontFamilyProperty, "ReadingFont");
        doc.FontSize = FontSize;
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "Ink");
        List? list = null;
        foreach (var block in AnswerText.Parse(Source ?? ""))
        {
            if (block.Kind is not (BlockKind.Bullet or BlockKind.Numbered)) list = null;
            switch (block.Kind)
            {
                case BlockKind.DisplayMath:
                    var formula = Formula(block.Spans[0].Text, FontSize * 1.25);
                    doc.Blocks.Add(formula is null
                        ? new Paragraph(new Run(block.Spans[0].Text))
                        : new BlockUIContainer(formula) { Margin = new Thickness(0, 6, 0, 10) });
                    break;
                case BlockKind.Code:
                    var code = new Paragraph(new Run(block.Spans[0].Text)) { Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(10, 8, 10, 8) };
                    code.SetResourceReference(Paragraph.FontFamilyProperty, "MonoFont");
                    code.SetResourceReference(Paragraph.BackgroundProperty, "SurfaceAlt");
                    code.FontSize = FontSize * 0.86;
                    doc.Blocks.Add(code);
                    break;
                case BlockKind.Bullet or BlockKind.Numbered:
                    var marker = block.Kind == BlockKind.Bullet ? TextMarkerStyle.Disc : TextMarkerStyle.Decimal;
                    if (list is null || list.MarkerStyle != marker)
                    {
                        list = new List { MarkerStyle = marker, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(22, 0, 0, 0) };
                        list.SetResourceReference(TextElement.FontFamilyProperty, "ReadingFont"); // a List does not pick up the document's face by itself
                        if (block.Kind == BlockKind.Numbered) list.StartIndex = Math.Max(1, block.Number);
                        doc.Blocks.Add(list);
                    }
                    list.ListItems.Add(new ListItem(Inlines(block, new Paragraph { Margin = new Thickness(0, 0, 0, 3) })));
                    break;
                case BlockKind.Heading:
                    var heading = Inlines(block, new Paragraph { Margin = new Thickness(0, 6, 0, 6), FontWeight = FontWeights.SemiBold });
                    heading.SetResourceReference(Paragraph.FontFamilyProperty, "HeadFont");
                    heading.FontSize = FontSize * 1.12;
                    doc.Blocks.Add(heading);
                    break;
                default:
                    doc.Blocks.Add(Inlines(block, new Paragraph { Margin = new Thickness(0, 0, 0, 10) }));
                    break;
            }
        }
        Document = doc;
    }

    private Paragraph Inlines(AnswerBlock block, Paragraph p)
    {
        foreach (var span in block.Spans)
            switch (span.Kind)
            {
                case SpanKind.Bold: p.Inlines.Add(new Bold(new Run(span.Text))); break;
                case SpanKind.Italic: p.Inlines.Add(new Italic(new Run(span.Text))); break;
                case SpanKind.Code:
                    var code = new Run(span.Text) { FontSize = FontSize * 0.88 };
                    code.SetResourceReference(TextElement.FontFamilyProperty, "MonoFont");
                    code.SetResourceReference(TextElement.BackgroundProperty, "SurfaceAlt");
                    p.Inlines.Add(code);
                    break;
                case SpanKind.Superscript or SpanKind.Subscript:
                    // A real raised (or lowered) figure, smaller, as a typesetter sets it; not the "^" the model typed.
                    // TextTop hangs an exponent from the cap height; Superscript would float it a line-gap above.
                    p.Inlines.Add(new Run(span.Text)
                    {
                        FontSize = FontSize * 0.7,
                        BaselineAlignment = span.Kind == SpanKind.Superscript ? BaselineAlignment.TextTop : BaselineAlignment.Subscript,
                    });
                    break;
                case SpanKind.Math:
                    if (Formula(span.Text, FontSize * 1.08) is { } f) p.Inlines.Add(new InlineUIContainer(f) { BaselineAlignment = BaselineAlignment.Center });
                    else p.Inlines.Add(new Run(span.Text));
                    break;
                default: p.Inlines.Add(new Run(span.Text)); break;
            }
        return p;
    }

    /// <summary>Typeset TeX, or null when it does not parse (the caller then shows the source text).</summary>
    private FormulaControl? Formula(string tex, double size)
    {
        try
        {
            // The engine has \frac but not the text/display-style variants models like to write.
            var source = tex.Replace(@"\tfrac", @"\frac", StringComparison.Ordinal).Replace(@"\dfrac", @"\frac", StringComparison.Ordinal);
            var control = new FormulaControl { Formula = source, Scale = size, SystemTextFontName = "Sitka Text", Focusable = false };
            if (control.HasError) return null;
            control.SetResourceReference(ForegroundProperty, "Ink");
            AutomationProperties.SetName(control, AnswerText.Spoken(tex));
            control.ToolTip = tex; // the source, for copying into notes
            return control;
        }
        catch (Exception e) when (e is XamlMath.Exceptions.TexException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
