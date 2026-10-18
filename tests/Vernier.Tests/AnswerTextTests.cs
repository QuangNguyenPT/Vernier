using Vernier.Core.Agents;
using Xunit;

namespace Vernier.Tests;

public class AnswerTextTests
{
    private static IReadOnlyList<AnswerSpan> Spans(string text) => AnswerText.Parse(text).Single().Spans;

    [Fact]
    public void TeX_written_outside_math_delimiters_shows_as_symbols_never_as_markup()
    {
        var text = string.Concat(Spans(@"So \Delta v = 3 and the angle is \theta, with \frac{1}{2}mv^2 left.").Select(s => s.Text));
        Assert.DoesNotContain('\\', text);
        Assert.Contains("Δ", text);
        Assert.Contains("θ", text);
    }

    [Fact]
    public void A_formula_still_streaming_never_shows_its_raw_opening()
    {
        // Mid-stream the closing \) has not arrived yet: the learner must not see "\(" and backslash commands flash up.
        var spans = Spans(@"The speed is \( v = \sqrt{2as");
        Assert.DoesNotContain(spans, s => s.Text.Contains('\\'));
        Assert.Contains(spans, s => s.Text.Contains("√"));
    }

    [Fact]
    public void Inline_tex_becomes_math()
    {
        var spans = Spans(@"The slope is \( \frac{dy}{dx} \) here.");
        Assert.Equal([SpanKind.Text, SpanKind.Math, SpanKind.Text], spans.Select(s => s.Kind));
        Assert.Equal(@" \frac{dy}{dx} ", spans[1].Text);
    }

    [Fact]
    public void Display_math_spans_lines()
    {
        var blocks = AnswerText.Parse("So:\n\\[\n\\int_0^1 x\\,dx\n= \\tfrac{1}{2}\n\\]\nDone.");
        Assert.Equal([BlockKind.Paragraph, BlockKind.DisplayMath, BlockKind.Paragraph], blocks.Select(b => b.Kind));
        Assert.Contains(@"\tfrac{1}{2}", blocks[1].Spans[0].Text);
    }

    [Theory]
    [InlineData("x^2 + 1", "2")]
    [InlineData("10^{-3} m", "-3")]
    [InlineData("(a+b)^n", "n")]
    [InlineData("so it is x^2.", "2")]
    [InlineData("about 1.5^0.5 here", "0.5")]
    public void Caret_becomes_superscript(string text, string exponent)
    {
        var sup = Assert.Single(Spans(text), s => s.Kind == SpanKind.Superscript);
        Assert.Equal(exponent, sup.Text);
    }

    [Fact]
    public void Braced_subscript_becomes_subscript() =>
        Assert.Equal("0", Assert.Single(Spans("v_{0} is the start speed"), s => s.Kind == SpanKind.Subscript).Text);

    [Theory]
    [InlineData("It costs $5 and $10.")]          // money, not maths
    [InlineData("a lone ^ caret and snake_case")]  // no superscript, no subscript
    public void Not_maths_stays_text(string text)
    {
        var spans = Spans(text);
        Assert.DoesNotContain(spans, s => s.Kind is SpanKind.Math or SpanKind.Subscript);
        Assert.Equal(text, string.Concat(spans.Select(s => s.Text))); // nothing lost
    }

    [Fact]
    public void Dollar_math_when_it_is_clearly_maths() =>
        Assert.Contains(Spans("Here $x^2=4$ holds."), s => s.Kind == SpanKind.Math && s.Text == "x^2=4");

    [Fact]
    public void Lists_headings_and_code()
    {
        var blocks = AnswerText.Parse("## Steps\n1. Expand\n2. Collect\n- note\n```\nx = 1\n```");
        Assert.Equal([BlockKind.Heading, BlockKind.Numbered, BlockKind.Numbered, BlockKind.Bullet, BlockKind.Code], blocks.Select(b => b.Kind));
        Assert.Equal(2, blocks[2].Number);
        Assert.Equal("x = 1", blocks[4].Spans[0].Text);
    }

    [Fact]
    public void Bold_and_italic()
    {
        var spans = Spans("a **key** idea, *not* this");
        Assert.Contains(spans, s => s.Kind == SpanKind.Bold && s.Text == "key");
        Assert.Contains(spans, s => s.Kind == SpanKind.Italic && s.Text == "not");
    }

    [Theory]
    [InlineData(@"\frac{a}{b}", "a over b")]
    [InlineData(@"x^{2} + 1", "x squared plus 1")]
    [InlineData(@"\sqrt{x}", "square root of x")]
    [InlineData(@"v_{0}", "v sub 0")]
    public void Formulas_are_spoken_in_words(string tex, string spoken) => Assert.Equal(spoken, AnswerText.Spoken(tex));

    [Fact]
    public void Speech_has_no_markup() =>
        Assert.Equal("The area is a over b and x squared.", AnswerText.ForSpeech(@"The area is \(\frac{a}{b}\) and x^2."));
}

[Collection("desktop")]
public class RichAnswerTests
{
    [Fact]
    public void Formulas_are_typeset_and_named_for_screen_readers() => WindowsRealApiTests.OnSta(() =>
    {
        var answer = new Vernier.App.Views.RichAnswer { Source = @"Take \( \tfrac{a}{b} \) and \( \badcommand{ \)." };
        var inlines = answer.Document.Blocks.OfType<System.Windows.Documents.Paragraph>().Single().Inlines.ToList();
        var formula = Assert.Single(inlines.OfType<System.Windows.Documents.InlineUIContainer>()).Child;
        Assert.IsType<WpfMath.Controls.FormulaControl>(formula);
        Assert.Equal("a over b", System.Windows.Automation.AutomationProperties.GetName(formula));
        // Malformed TeX is shown as its source, never dropped.
        Assert.Contains(inlines.OfType<System.Windows.Documents.Run>(), r => r.Text.Contains("badcommand", StringComparison.Ordinal));
        return Task.CompletedTask;
    });
}
