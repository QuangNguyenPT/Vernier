using System.Text;
using Vernier.Core.Annotations;
using Vernier.Core.Learning;
using Vernier.Core.Providers;
using Vernier.Core.Settings;

namespace Vernier.Core.Agents;

public sealed record TutorRequest(
    TutorMode Mode,
    string Question,
    ContextSnapshot? Context,
    IReadOnlyList<ChatMessage> History,
    long? SessionId,
    string? NotesContext = null,
    bool AllowActions = false,
    IReadOnlyList<Attachment>? Attachments = null,
    ToolSession? Tools = null,
    ScreenIndex? Screen = null,
    WorkContext? Work = null,
    Context.ContextSelection? Episodes = null,
    string? SessionDigest = null,
    Research.ResearchDemand? Research = null,
    VisualDemand? Visual = null);

public sealed class PipelineCallbacks
{
    public Action<PipelineStage>? Stage { get; init; }
    /// <summary>Full visible text so far (structured block hidden). Called at most every 50 ms while streaming, and once at the end.</summary>
    public Action<string>? Text { get; init; }
    /// <summary>The model started or finished a tool call.</summary>
    public Action<ToolCallNote>? Tool { get; init; }
}

/// <summary>
/// Observe → Interpret → Teach → Verify → (Correct → Confirm) → Present → Update mastery.
/// The correction loop runs at most <see cref="MaxCorrections"/> time(s); if the corrected answer
/// still fails, it is presented with the failures visible rather than regenerated again.
/// </summary>
public sealed class TutorPipeline(MasteryTutor tutor, Verifier verifier, LearningStore store, AppSettings settings, DiagnosticLog log)
{
    public const int MaxCorrections = 1;
    public static readonly TimeSpan TeachTimeout = TimeSpan.FromSeconds(180);
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(60);

    public async Task<TutorTurn> RunAsync(
        TutorRequest request, IChatProvider provider, IChatProvider? checker, PipelineCallbacks callbacks, CancellationToken ct)
    {
        var stages = new List<PipelineStage>();
        var notices = new List<string>();
        void Enter(PipelineStage s) { stages.Add(s); callbacks.Stage?.Invoke(s); }

        // Observe happened before this call (capture needs the UI thread); record it only if there is context.
        var context = request.Context;
        if (context is not null) Enter(PipelineStage.Observe);

        Enter(PipelineStage.Interpret);
        var vision = provider.Capabilities.Vision;
        var attachments = request.Attachments ?? [];
        if (context?.Frame is not null && !vision)
            notices.Add($"{provider.DisplayName} is text-only here, so the screenshot was not sent; only the extracted text was.");
        if (!vision && attachments.Any(a => a.Image is not null))
            notices.Add($"{provider.DisplayName} is text-only here, so attached pictures were not sent.");
        var chat = tutor.BuildRequest(request.Mode, request.Question, context, request.History, settings.Learner, LearningFor(provider), vision, request.NotesContext, attachments,
            request.AllowActions, request.Screen, provider.Capabilities.Tools ? request.Tools : null, request.Work, request.Episodes, request.SessionDigest, request.Research, request.Visual);

        Enter(PipelineStage.Teach);
        var (raw, usage) = await StreamAsync(provider, chat, callbacks, ct).ConfigureAwait(false);

        // Research gate: an answer that had to be grounded and was not is asked for again, once, with the reason.
        var demand = request.Research ?? Research.ResearchDemand.None;
        var offered = chat.Tools?.Turn.Allowed.Contains("web_research") == true;
        Research.ResearchLedger? Ledger() => chat.Tools?.Turn.State.TryGetValue(Research.ResearchLedger.Key, out var l) == true ? l as Research.ResearchLedger : null;
        var asks = new List<string>();
        if (Research.ResearchGate.Check(demand, Ledger(), offered) is { } researchAsk) asks.Add(researchAsk);
        var drew = chat.Tools?.Turn.Events.Any(e => e.Tool == "overlay_diagram" && !e.IsError) == true;
        if (DimensionGate.Check(request.Visual ?? VisualDemand.None, drew, chat.Tools?.Turn.Allowed.Contains("overlay_diagram") == true) is { } visualAsk) asks.Add(visualAsk);
        if (asks.Count > 0)
        {
            Enter(PipelineStage.Correct);
            try
            {
                (raw, var moreUsage) = await StreamAsync(provider, tutor.BuildResearchReask(chat, raw, string.Join("\n\n", asks)), callbacks, ct).ConfigureAwait(false);
                usage = Add(usage, moreUsage);
                log.Info($"Research re-ask: {Ledger()?.Working ?? 0} working source(s) after it.");
            }
            catch (ProviderException e) { notices.Add($"The answer was not researched as required and could not be asked again ({e.Kind})."); }
            if (demand.Required && (Ledger()?.Working ?? 0) < Research.ResearchPolicy.MinSources)
                notices.Add($"This explanation is based on {Ledger()?.Working ?? 0} working web source(s), fewer than the {Research.ResearchPolicy.MinSources} required; check details of the software or page yourself.");
        }
        var output = TutorOutput.Parse(raw);

        Enter(PipelineStage.Verify);
        var geometry = vision ? context?.Frame?.Geometry : null;
        var report = await VerifyAsync(output, geometry, request, context, provider, checker, ct).ConfigureAwait(false);

        var corrected = false;
        var correction = report.HasFailures ? System.Diagnostics.Stopwatch.StartNew() : null; // the learner waits this long for a fixed answer
        for (var attempt = 0; report.HasFailures && attempt < MaxCorrections; attempt++)
        {
            Enter(PipelineStage.Correct);
            string fixedRaw;
            try
            {
                (fixedRaw, var moreUsage) = await StreamAsync(provider, tutor.BuildCorrection(chat, raw, report), callbacks, ct).ConfigureAwait(false);
                usage = Add(usage, moreUsage);
            }
            catch (ProviderException e)
            {
                // Keep the first answer with its failures showing; a failed correction must not hide them.
                notices.Add($"The answer could not be corrected automatically ({e.Kind}). The failed checks are shown below.");
                callbacks.Text?.Invoke(output.Text);
                break;
            }
            Enter(PipelineStage.Confirm);
            raw = fixedRaw;
            output = TutorOutput.Parse(raw);
            report = await VerifyAsync(output, geometry, request, context, provider, checker, ct).ConfigureAwait(false);
            corrected = true;
        }
        TimeSpan? correctionTook = correction?.Elapsed;
        if (correctionTook is { } took) log.Info($"Correction pass took {took.TotalMilliseconds:0} ms ({(report.HasFailures ? "still failing" : "passed")}).");
        if (corrected)
            notices.Add(report.HasFailures
                ? "The answer was corrected once and still fails a check. It is shown as-is with the failure marked; it was not regenerated again."
                : "A checker found a problem in the first draft; the answer above is the corrected one and passed re-checking.");

        Enter(PipelineStage.Present);
        callbacks.Text?.Invoke(output.Text);
        IReadOnlyList<Annotation> annotations = geometry is null ? [] : Verifier.ToAnnotations(output, geometry, TimeSpan.FromMinutes(10));
        if (output.Data.Actions is { Count: > 0 })
        {
            if (!request.AllowActions)
                notices.Add("The tutor suggested input actions, but assisted control is off, so they were discarded.");
            // Review happens again at execution time against the live window; this is only the preview.
        }

        Enter(PipelineStage.UpdateMastery);
        try
        {
            UpdateMastery(request, output);
        }
        catch (Exception e) when (e is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException or IOException)
        {
            log.Error("Could not update the learning record", e);
            notices.Add("This turn could not be saved to the learning record.");
        }

        return new TutorTurn(output, report, corrected, annotations, usage, stages, notices, correctionTook);
    }

    private async Task<VerificationReport> VerifyAsync(
        TutorOutput output, Geometry.CaptureGeometry? geometry, TutorRequest request, ContextSnapshot? context,
        IChatProvider provider, IChatProvider? checker, CancellationToken ct)
    {
        var report = verifier.Check(output, geometry);
        // A model call is only spent when code could not confirm anything, and only if the user configured a checker.
        if (checker is null || report.HasFailures || report.Passed > 0 || output.Text.Length < 200) return report;
        var second = await Verifier.SecondOpinionAsync(checker, provider, request.Question, context?.JoinedText ?? "", output.Text, CheckTimeout, ct).ConfigureAwait(false);
        return new VerificationReport([.. report.Checks, second]);
    }

    private static async Task<(string Raw, TokenUsage? Usage)> StreamAsync(IChatProvider provider, ChatRequest chat, PipelineCallbacks callbacks, CancellationToken ct)
    {
        var sb = new StringBuilder();
        TokenUsage? usage = null;
        // The visible text is rebuilt at most every 50 ms: rebuilding and posting it per token starves the UI thread.
        var sinceShown = System.Diagnostics.Stopwatch.StartNew();
        var pending = false;
        await foreach (var chunk in ChatRunner.StreamWithRetryAsync(provider, chat, TeachTimeout, ct).ConfigureAwait(false))
        {
            if (chunk.Usage is not null) usage = chunk.Usage;
            if (chunk.Tool is { } note) callbacks.Tool?.Invoke(note);
            if (chunk.Text is null) continue;
            sb.Append(chunk.Text);
            pending = true;
            if (sinceShown.ElapsedMilliseconds < 50) continue;
            callbacks.Text?.Invoke(TutorOutput.VisibleWhileStreaming(sb.ToString()));
            sinceShown.Restart();
            pending = false;
        }
        if (pending) callbacks.Text?.Invoke(TutorOutput.VisibleWhileStreaming(sb.ToString()));
        if (sb.Length == 0)
            throw new ProviderException(ProviderErrorKind.Unavailable, $"{provider.DisplayName} returned an empty answer.");
        return (sb.ToString(), usage);
    }

    // The learning record is local by default: its digest only reaches a remote provider with the learner's consent.
    private LearningSummary LearningFor(IChatProvider provider) => provider.Capabilities.IsLocal || settings.AllowSendNotesToRemote ? SafeSummary() : LearningSummary.None;

    /// <summary>The system prompt a turn on this provider will use; lets a CLI provider start before the question arrives.</summary>
    public string SystemPromptFor(IChatProvider provider, bool allowActions, bool tools) =>
        tutor.BuildSystem(settings.Learner, LearningFor(provider), allowActions, tools && provider.Capabilities.Tools);

    private LearningSummary SafeSummary()
    {
        try { return LearningSummary.From(store); }
        catch (Microsoft.Data.Sqlite.SqliteException e)
        {
            log.Error("Could not read the learning record", e);
            return LearningSummary.None;
        }
    }

    private void UpdateMastery(TutorRequest request, TutorOutput output)
    {
        if (request.SessionId is { } session)
        {
            store.AddMessage(session, "user", request.Question);
            store.AddMessage(session, "assistant", output.Text);
        }
        var d = output.Data;
        if (string.IsNullOrWhiteSpace(d.Concept)) return;

        var subject = string.IsNullOrWhiteSpace(d.Subject) ? "General" : d.Subject!;
        var conceptId = store.EnsureConcept(subject, d.Concept!);
        foreach (var p in (d.Prerequisites ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Take(5))
            store.AddPrerequisite(conceptId, subject, p);
        // Only gradeable kinds are accepted from the model; "self_report" can never be promoted to evidence here.
        if (d.Evidence is { Kind: not null } ev && EvidenceKinds.Counted.Contains(ev.Kind) && ev.Kind != EvidenceKinds.Recall)
            store.AddEvidence(conceptId, ev.Kind, ev.Correct);
        if (!string.IsNullOrWhiteSpace(d.Misconception)) store.AddMisconception(conceptId, d.Misconception!);
        if (!string.IsNullOrWhiteSpace(d.OpenQuestion)) store.AddOpenQuestion(conceptId, d.OpenQuestion!);
        if (d.Formula is { Expression: not null } f) store.AddFormula(conceptId, f.Expression, f.Meaning, f.Derivation);
        if (d.Check is { Question: not null, Answer: not null } c && c.Question.Trim().Length > 0)
            store.AddCard(conceptId, c.Question, c.Answer, due: DateTimeOffset.UtcNow.AddDays(Math.Max(1, settings.ReviewIntervalsDays.FirstOrDefault(1))));
    }

    private static TokenUsage? Add(TokenUsage? a, TokenUsage? b) =>
        a is null ? b : b is null ? a : new TokenUsage(a.Input + b.Input, a.Output + b.Output);
}
