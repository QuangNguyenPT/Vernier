using System.Text.Json;
using System.Text.RegularExpressions;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Providers;
using Vernier.Core.Security;
using Vernier.Core.Verification;

namespace Vernier.Core.Agents;

/// <summary>
/// Agent 3. Checks what the tutor produced. Arithmetic, units and annotation geometry are
/// recomputed by code, not by asking a model again. A second model is optional and is only
/// described as independent when it really is a different provider or model.
/// </summary>
public sealed partial class Verifier
{
    public const int MaxClaims = 24;

    [GeneratedRegex(@"\b(definitely|certainly|always|never|guaranteed|without (a )?doubt|obviously|it is proven)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CertaintyWords();

    [GeneratedRegex(@"https?://[^\s)\]>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex Urls();

    public VerificationReport Check(TutorOutput output, CaptureGeometry? geometry)
    {
        var checks = new List<CheckResult>();
        if (output.ParseProblem is not null)
            checks.Add(new CheckResult("format", CheckStatus.Advisory, output.ParseProblem));

        foreach (var claim in (output.Data.Claims ?? []).Take(MaxClaims))
        {
            if (!string.IsNullOrWhiteSpace(claim.Expr) && !string.IsNullOrWhiteSpace(claim.Value))
                checks.Add(CheckArithmetic(claim.Expr, claim.Value));
            if (!string.IsNullOrWhiteSpace(claim.UnitsLhs) && !string.IsNullOrWhiteSpace(claim.UnitsRhs))
                checks.Add(CheckUnits(claim.UnitsLhs, claim.UnitsRhs));
        }

        foreach (var a in output.Data.Annotations ?? [])
            checks.Add(CheckAnnotation(a, geometry));

        if (CertaintyWords().IsMatch(output.Text) && !checks.Any(c => c.Status == CheckStatus.Passed))
            checks.Add(new CheckResult("certainty", CheckStatus.Advisory,
                "The answer uses absolute wording but nothing in it could be checked mechanically. Treat it as the tutor's reasoning, not as verified fact."));

        var urls = Urls().Matches(output.Text).Select(m => m.Value).Distinct().Take(5).ToList();
        if (urls.Count > 0)
            checks.Add(new CheckResult("references", CheckStatus.Advisory,
                $"{urls.Count} link(s) were written from the model's memory and were not retrieved or checked."));

        return new VerificationReport(checks);
    }

    public static CheckResult CheckArithmetic(string expr, string claimed)
    {
        if (!Expr.TryEvaluate(expr, out var computed, out var error))
            return new CheckResult("arithmetic", CheckStatus.Skipped, $"Could not recompute \"{expr}\": {error}.");
        if (!Expr.MatchesClaim(computed, claimed, out _))
            return new CheckResult("arithmetic", CheckStatus.Failed,
                $"{expr} recomputes to {computed.ToString("G10", System.Globalization.CultureInfo.InvariantCulture)}, but the answer states {claimed}.");
        return new CheckResult("arithmetic", CheckStatus.Passed, $"{expr} = {claimed} recomputed and confirmed.");
    }

    public static CheckResult CheckUnits(string lhs, string rhs)
    {
        var same = Units.SameDimension(lhs, rhs, out var detail);
        return same switch
        {
            true => new CheckResult("units", CheckStatus.Passed, "Dimensions agree: " + detail + "."),
            false => new CheckResult("units", CheckStatus.Failed, "Dimensions disagree: " + detail + "."),
            null => new CheckResult("units", CheckStatus.Skipped, "Units not checked: " + detail + "."),
        };
    }

    public static CheckResult CheckAnnotation(ProposedAnnotation a, CaptureGeometry? geometry)
    {
        var name = string.IsNullOrWhiteSpace(a.Target) ? a.Kind : $"{a.Kind} on \"{a.Target}\"";
        if (geometry is null)
            return new CheckResult("annotation", CheckStatus.Failed, $"{name}: no capture exists to place it on.");
        if (!Enum.TryParse<AnnotationKind>(a.Kind, ignoreCase: true, out var kind))
            return new CheckResult("annotation", CheckStatus.Failed, $"Unknown annotation kind \"{a.Kind}\".");
        if (new[] { a.X, a.Y, a.W, a.H, a.X2 ?? 0, a.Y2 ?? 0 }.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
            return new CheckResult("annotation", CheckStatus.Failed, $"{name}: coordinates are not numbers.");

        var bounds = geometry.ImageBounds;
        var from = new PxPoint(a.X, a.Y);
        if (!bounds.Contains(from))
            return new CheckResult("annotation", CheckStatus.Failed, $"{name}: ({a.X:0}, {a.Y:0}) is outside the {geometry.ImageWidth}×{geometry.ImageHeight} capture.");

        if (kind is AnnotationKind.Arrow)
        {
            if (a.X2 is null || a.Y2 is null || !bounds.Contains(new PxPoint(a.X2.Value, a.Y2.Value)))
                return new CheckResult("annotation", CheckStatus.Failed, $"{name}: the arrow needs an end point inside the capture.");
        }
        else if (kind is not AnnotationKind.Label)
        {
            if (a.W <= 1 || a.H <= 1)
                return new CheckResult("annotation", CheckStatus.Failed, $"{name}: the region has no size.");
            if (!bounds.Contains(new PxRect(a.X, a.Y, a.W, a.H)))
                return new CheckResult("annotation", CheckStatus.Failed, $"{name}: the region extends outside the capture.");
            if (a.W * a.H > 0.6 * bounds.Width * bounds.Height)
                return new CheckResult("annotation", CheckStatus.Failed, $"{name}: the region covers most of the capture, so it points at nothing in particular.");
        }
        if (string.IsNullOrWhiteSpace(a.Target))
            return new CheckResult("annotation", CheckStatus.Failed, $"{a.Kind}: no stated target, so its placement cannot be judged.");
        return new CheckResult("annotation", CheckStatus.Passed,
            $"{name}: inside the capture. Placement is the model's estimate (±{Math.Max(8, geometry.QuantizationUncertaintyPx):0}px or more), not a measured position.");
    }

    /// <summary>Maps verified proposals from image space to physical screen pixels.</summary>
    public static IReadOnlyList<Annotation> ToAnnotations(TutorOutput output, CaptureGeometry geometry, TimeSpan? lifetime = null)
    {
        var result = new List<Annotation>();
        foreach (var a in output.Data.Annotations ?? [])
        {
            if (CheckAnnotation(a, geometry).Status != CheckStatus.Passed) continue;
            var kind = Enum.Parse<AnnotationKind>(a.Kind, ignoreCase: true);
            PxPoint[] imagePoints = kind switch
            {
                AnnotationKind.Arrow => [new(a.X, a.Y), new(a.X2!.Value, a.Y2!.Value)],
                AnnotationKind.Label => [new(a.X, a.Y)],
                _ => [new(a.X, a.Y), new(a.X + a.W, a.Y + a.H)],
            };
            PxRect? target = kind is AnnotationKind.Arrow or AnnotationKind.Label
                ? null
                : geometry.ImageToPhysical(new PxRect(a.X, a.Y, a.W, a.H));
            result.Add(new Annotation
            {
                Kind = kind,
                Points = imagePoints.Select(geometry.ImageToPhysical).ToArray(),
                Color = AnnotationColors.ForMeaning(a.Semantic),
                Label = a.Label,
                Meaning = a.Semantic ?? "focus",
                TargetDescription = a.Target,
                TargetRegion = target,
                Confidence = Math.Clamp(a.Confidence, 0, 1),
                UncertaintyPx = geometry.QuantizationUncertaintyPx,
                Source = "tutor",
                Lifetime = lifetime,
            });
        }
        return result;
    }

    /// <summary>
    /// Optional second opinion. The checker is shown the problem and the answer and must solve
    /// the problem itself before comparing. Its reply is untrusted and parsed defensively.
    /// </summary>
    public static async Task<CheckResult> SecondOpinionAsync(
        IChatProvider checker, IChatProvider tutorProvider, string question, string contextText, string answer, TimeSpan timeout, CancellationToken ct)
    {
        var independent = checker.Id != tutorProvider.Id || checker.Model != tutorProvider.Model;
        var method = independent ? "second-model" : "same-model";
        const string system =
            "You are a meticulous checker of tutoring answers. First solve the problem yourself, then compare with the given answer. " +
            "Judge only correctness of facts, mathematics, units and logic, not style. " + Untrusted.SystemRule +
            " Reply with one JSON object only: {\"verdict\":\"agree\"|\"disagree\"|\"unsure\",\"issue\":\"<one sentence, empty if agree>\"}";
        var prompt =
            "Problem:\n" + Untrusted.Wrap("learner-question", question, 4000) + "\n\n" +
            (string.IsNullOrWhiteSpace(contextText) ? "" : "Context:\n" + Untrusted.Wrap("screen", contextText, 6000) + "\n\n") +
            "Answer to check:\n" + Untrusted.Wrap("tutor-answer", answer, 8000);
        try
        {
            var reply = await ChatRunner.CompleteAsync(checker, new ChatRequest(system, [ChatMessage.User(prompt)], 1500), timeout, ct).ConfigureAwait(false);
            var json = ExtractJsonObject(reply.Text);
            if (json is null) return new CheckResult("second-opinion", CheckStatus.Skipped, "The checker's reply could not be read.", method);
            using var doc = JsonDocument.Parse(json);
            var verdict = doc.RootElement.TryGetProperty("verdict", out var v) ? v.GetString() : null;
            var issue = doc.RootElement.TryGetProperty("issue", out var i) ? i.GetString() ?? "" : "";
            var who = independent ? $"{checker.DisplayName} (independent of the tutor model)" : "The same model, re-reading its own answer (not independent)";
            return verdict switch
            {
                "agree" => new CheckResult("second-opinion", independent ? CheckStatus.Passed : CheckStatus.Advisory, $"{who} agrees.", method),
                "disagree" => new CheckResult("second-opinion", CheckStatus.Failed, $"{who} disagrees: {issue}", method),
                _ => new CheckResult("second-opinion", CheckStatus.Advisory, $"{who} was unsure: {issue}", method),
            };
        }
        catch (ProviderException e)
        {
            return new CheckResult("second-opinion", CheckStatus.Skipped, $"The checker was unavailable ({e.Kind}).", method);
        }
        catch (JsonException)
        {
            return new CheckResult("second-opinion", CheckStatus.Skipped, "The checker's reply was not valid JSON.", method);
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }
}

/// <summary>Bounds what a model-proposed input plan may do before a person is even asked to approve it.</summary>
public static class ActionPolicy
{
    public const int MaxSteps = 12;
    public const int MaxTextLength = 400;

    private static readonly string[] BlockedChords =
        ["win+r", "win+l", "win+x", "win+d", "alt+f4", "ctrl+alt+del", "ctrl+alt+delete", "ctrl+shift+esc", "win+e", "alt+tab", "win"];

    public sealed record Decision(ComputerAction Action, bool Allowed, string Reason);

    public static IReadOnlyList<Decision> Review(IReadOnlyList<ProposedAction>? proposed, CaptureGeometry? geometry, PxRect? targetWindow)
    {
        var result = new List<Decision>();
        if (proposed is null) return result;
        foreach (var p in proposed.Take(MaxSteps + 1))
        {
            if (result.Count == MaxSteps)
            {
                result.Add(new Decision(new ComputerAction(ActionType.Wait, null, null, null, 0, "plan truncated"), false, $"Plans are limited to {MaxSteps} steps."));
                break;
            }
            if (!Enum.TryParse<ActionType>(p.Type?.Replace("_", "").Replace("-", ""), ignoreCase: true, out var type))
            {
                result.Add(new Decision(new ComputerAction(ActionType.Wait, null, null, null, 0, p.Why ?? ""), false, $"Unknown action \"{p.Type}\"."));
                continue;
            }

            PxPoint? point = null;
            string? refusal = null;
            if (type is ActionType.Click or ActionType.DoubleClick or ActionType.RightClick or ActionType.Move)
            {
                if (geometry is null || p.X is null || p.Y is null) refusal = "No capture to locate this point on.";
                else if (!geometry.ImageBounds.Contains(new PxPoint(p.X.Value, p.Y.Value))) refusal = "The point is outside the captured area.";
                else
                {
                    point = geometry.ImageToPhysical(new PxPoint(p.X.Value, p.Y.Value));
                    if (targetWindow is { } w && !w.Contains(point.Value)) refusal = "The point is outside the target window.";
                }
            }
            else if (type is ActionType.Type)
            {
                if (string.IsNullOrEmpty(p.Text)) refusal = "Nothing to type.";
                else if (p.Text.Length > MaxTextLength) refusal = $"Text longer than {MaxTextLength} characters is not typed automatically.";
                else if (p.Text.Any(c => char.IsControl(c) && c != '\t')) refusal = "Text contains control characters (Enter must be a separate, visible key step).";
            }
            else if (type is ActionType.Keys)
            {
                var chord = Normalize(p.Keys);
                if (chord.Length == 0) refusal = "No keys given.";
                else if (IsBlocked(chord)) refusal = $"{p.Keys} affects the system or other applications and is never sent.";
            }

            var amount = type switch
            {
                ActionType.Scroll => Math.Clamp(p.Amount ?? 1, -20, 20),
                ActionType.Wait => Math.Clamp(p.Amount ?? 300, 0, 3000),
                _ => 0,
            };
            var action = new ComputerAction(type, point, p.Text, p.Keys, amount, p.Why ?? "");
            result.Add(new Decision(action, refusal is null, refusal ?? "Allowed, pending your approval."));
        }
        return result;
    }

    // As a set of keys, so shift+ctrl+esc is the same chord as ctrl+shift+esc; anything with the Windows key is a system chord.
    private static bool IsBlocked(string chord)
    {
        var keys = chord.Replace("delete", "del").Split('+').ToHashSet();
        return keys.Contains("win") || BlockedChords.Any(b => b.Replace("delete", "del").Split('+').ToHashSet().SetEquals(keys));
    }

    public static string Normalize(string? keys) =>
        string.Join("+", (keys ?? "").ToLowerInvariant().Replace("windows", "win").Replace("control", "ctrl").Replace("escape", "esc")
            .Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

/// <summary>Runs an approved plan one step at a time. Stops at the first refusal, failure or cancellation.</summary>
public sealed class ActionExecutor(IInputDriver driver)
{
    public async Task<(int Executed, string? StoppedBecause)> RunAsync(
        IReadOnlyList<ActionPolicy.Decision> plan, Func<ComputerAction, Task<bool>> approveStep, CancellationToken ct)
    {
        var done = 0;
        foreach (var step in plan)
        {
            if (ct.IsCancellationRequested) return (done, "Stopped by you.");
            if (!step.Allowed) return (done, "Blocked: " + step.Reason);
            if (!await approveStep(step.Action).ConfigureAwait(true)) return (done, "You declined a step.");
            try
            {
                await driver.ExecuteAsync(step.Action, ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException) { return (done, "Stopped by you."); }
            catch (InvalidOperationException e) { return (done, e.Message); }
            done++;
        }
        return (done, null);
    }
}
