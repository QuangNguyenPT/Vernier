using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;

namespace Vernier.Core.Agents;

public enum TutorMode { Ask, Tutor, Annotate, Review, Assist }

// ---------- Observer -> Tutor contract ----------

/// <summary>One consented capture. The image may be downscaled; <see cref="Geometry"/> maps it back.</summary>
public sealed record CapturedFrame(
    byte[] ImageBytes, string MediaType, CaptureGeometry Geometry, ulong Hash,
    string? WindowTitle, string? ProcessName, long WindowHandle, DateTimeOffset At)
{
    /// <summary>Where the learner drew, in image pixels. Their ink is already painted into <see cref="ImageBytes"/>.</summary>
    public PxRect? MarkedRegion { get; init; }
    /// <summary>Full-resolution crop around <see cref="MarkedRegion"/>, so small circled things survive downscaling.</summary>
    public byte[]? MarkedDetail { get; init; }
    /// <summary>
    /// A small grayscale picture of the capture, fine enough that one changed line of text shows.
    /// The 64-bit <see cref="Hash"/> cannot tell a small window changing on a large screen.
    /// </summary>
    public byte[]? Thumb { get; init; }
    /// <summary>The un-inked capture at full resolution, so OCR reads small text the downscaled image loses.</summary>
    public RawImage? Raw { get; init; }
    /// <summary>The window in front, in screenshot pixels, when known.</summary>
    public PxRect? WindowBox { get; init; }
}

public sealed record ExtractedText(string Text, PxRect? BoundsImage, double Confidence, string Provenance)
{
    /// <summary>For an OCR line: its words with their own boxes (image pixels), so one symbol in a line can be marked.</summary>
    public IReadOnlyList<ExtractedText>? Words { get; init; }
    /// <summary>For an accessibility element: its control type ("button", "edit", "tab item" …).</summary>
    public string? Role { get; init; }
}

public interface ITextExtractor
{
    string Provenance { get; }
    /// <summary>False for sources that only locate things (element names and boxes) and are not the screen's text.</summary>
    bool ProvidesText => true;
    Task<IReadOnlyList<ExtractedText>> ExtractAsync(CapturedFrame frame, CancellationToken ct);
}

/// <summary>A capture's pixels at full resolution (BGRA, top-down), for OCR. Never sent to a model.</summary>
public sealed record RawImage(byte[] Bgra, int Width, int Height);

public sealed record ContextSnapshot(
    DateTimeOffset At,
    CapturedFrame? Frame,
    IReadOnlyList<ExtractedText> Texts,
    IReadOnlyList<string> Equations,
    IReadOnlyList<string> CodeLines,
    bool ChangedSinceLast,
    double Confidence,
    IReadOnlyList<string> Ambiguities,
    AppProfile? App,
    string TextProvenance)
{
    public string JoinedText => string.Join("\n", Texts.Select(t => t.Text));
    /// <summary>How this capture differs from the one before it, in plain words. Null for the first capture.</summary>
    public string? SinceLast { get; init; }
    /// <summary>Everything any source located on screen, with boxes (OCR lines with words, accessibility elements). Used to place marks.</summary>
    public IReadOnlyList<ExtractedText> Layout { get; init; } = [];
}

/// <summary>Something the learner attached to a question: extracted text, or one picture (PNG or JPEG).</summary>
public sealed record Attachment(string Name, string? Text, byte[]? Image = null, string MediaType = "image/png");

// ---------- Tutor -> Verifier/Presenter contract ----------

public sealed class Claim
{
    public string? Expr { get; set; }
    public string? Value { get; set; }
    public string? UnitsLhs { get; set; }
    public string? UnitsRhs { get; set; }
}

public sealed class ProposedAnnotation
{
    public string Kind { get; set; } = "highlight";
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double? X2 { get; set; }
    public double? Y2 { get; set; }
    public string? Label { get; set; }
    public string? Target { get; set; }
    public string? Semantic { get; set; }
    public double Confidence { get; set; } = 0.5;
}

public sealed class MasteryCheck
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
}

public sealed class LearnerEvidence
{
    /// <summary>diagnostic | explain | transfer | recall</summary>
    public string? Kind { get; set; }
    public bool Correct { get; set; }
}

public sealed class FormulaNote
{
    public string? Expression { get; set; }
    public string? Meaning { get; set; }
    public string? Derivation { get; set; }
}

public sealed class ProposedAction
{
    public string Type { get; set; } = "";
    public double? X { get; set; }
    public double? Y { get; set; }
    public string? Text { get; set; }
    public string? Keys { get; set; }
    public int? Amount { get; set; }
    public string? Why { get; set; }
}

public sealed class TutorStructured
{
    public string? Subject { get; set; }
    public string? Concept { get; set; }
    public List<string>? Prerequisites { get; set; }
    public List<Claim>? Claims { get; set; }
    public List<ProposedAnnotation>? Annotations { get; set; }
    public MasteryCheck? Check { get; set; }
    public string? Misconception { get; set; }
    public LearnerEvidence? Evidence { get; set; }
    public FormulaNote? Formula { get; set; }
    public List<string>? Uncertain { get; set; }
    public List<ProposedAction>? Actions { get; set; }
    public string? OpenQuestion { get; set; }
}

public sealed record TutorOutput(string Text, TutorStructured Data, bool StructuredBlockPresent, string? ParseProblem)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly Regex Block = new(@"```vernier\s*(?<json>.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OpenBlock = new(@"```vernier.*\z", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Splits a model reply into prose and its optional ```vernier JSON block. Model output is
    /// untrusted: a missing or malformed block degrades to plain prose plus a recorded problem.
    /// </summary>
    public static TutorOutput Parse(string raw)
    {
        raw ??= "";
        var matches = Block.Matches(raw);
        if (matches.Count == 0)
        {
            var truncated = OpenBlock.IsMatch(raw);
            return new TutorOutput(OpenBlock.Replace(raw, "").Trim(), new TutorStructured(), false,
                truncated ? "The structured block was cut off; checks that depend on it were skipped." : null);
        }
        var last = matches[^1];
        var prose = Block.Replace(raw, "").Trim();
        try
        {
            var data = JsonSerializer.Deserialize<TutorStructured>(last.Groups["json"].Value, Json) ?? new TutorStructured();
            return new TutorOutput(prose, data, true, null);
        }
        catch (JsonException e)
        {
            return new TutorOutput(prose, new TutorStructured(), true, "The structured block was not valid JSON (" + e.Message.Split('.')[0] + ").");
        }
    }

    /// <summary>Hides a partially streamed block so the learner never watches JSON scroll past.</summary>
    public static string VisibleWhileStreaming(string partial)
    {
        var at = partial.IndexOf("```vernier", StringComparison.OrdinalIgnoreCase);
        return (at < 0 ? partial : partial[..at]).TrimEnd();
    }
}

// ---------- Verifier contract ----------

public enum CheckStatus { Passed, Failed, Advisory, Skipped }

/// <param name="Method">"deterministic", "second-model" or "same-model" — the last is never called independent.</param>
public sealed record CheckResult(string Kind, CheckStatus Status, string Detail, string Method = "deterministic");

public sealed record VerificationReport(IReadOnlyList<CheckResult> Checks)
{
    public static readonly VerificationReport Empty = new([]);
    public bool HasFailures => Checks.Any(c => c.Status == CheckStatus.Failed);
    public int Passed => Checks.Count(c => c.Status == CheckStatus.Passed);
    public IEnumerable<CheckResult> Failures => Checks.Where(c => c.Status == CheckStatus.Failed);

    public string Summary =>
        Checks.Count == 0 ? "Nothing in this answer could be checked mechanically."
        : HasFailures ? $"{Failures.Count()} check(s) failed, {Passed} passed."
        : Passed > 0 ? $"{Passed} check(s) passed."
        : "No mechanical checks applied; see notes.";
}

// ---------- Pipeline contract ----------

public enum PipelineStage { Observe, Interpret, Teach, Verify, Correct, Confirm, Present, UpdateMastery }

public sealed record TutorTurn(
    TutorOutput Output,
    VerificationReport Verification,
    bool Corrected,
    IReadOnlyList<Annotation> Annotations,
    Providers.TokenUsage? Usage,
    IReadOnlyList<PipelineStage> StagesRun,
    IReadOnlyList<string> Notices,
    TimeSpan? CorrectionTook = null);

// ---------- Assisted computer use ----------

public enum ActionType { Click, DoubleClick, RightClick, Move, Type, Keys, Scroll, Wait }

/// <summary>One proposed input step. Points are virtual-desktop physical pixels.</summary>
public sealed record ComputerAction(ActionType Type, PxPoint? Point, string? Text, string? Keys, int Amount, string Why)
{
    public string Describe() => Type switch
    {
        ActionType.Click => $"Click at ({Point?.X:0}, {Point?.Y:0})",
        ActionType.DoubleClick => $"Double-click at ({Point?.X:0}, {Point?.Y:0})",
        ActionType.RightClick => $"Right-click at ({Point?.X:0}, {Point?.Y:0})",
        ActionType.Move => $"Move pointer to ({Point?.X:0}, {Point?.Y:0})",
        ActionType.Type => $"Type \"{Text}\"",
        ActionType.Keys => $"Press {Keys}",
        ActionType.Scroll => $"Scroll {(Amount < 0 ? "down" : "up")} {Math.Abs(Amount)} notch(es)",
        ActionType.Wait => $"Wait {Amount} ms",
        _ => Type.ToString(),
    };
}

public interface IInputDriver
{
    /// <summary>Performs one step. Implementations must refuse rather than guess when a step is unsafe.</summary>
    Task ExecuteAsync(ComputerAction action, CancellationToken ct);
}

public sealed record AppProfile(string Name, string Category, string Hints);
