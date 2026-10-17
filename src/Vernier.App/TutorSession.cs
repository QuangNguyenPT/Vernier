using System.Collections.ObjectModel;
using Vernier.App.Agent;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Learning;
using Vernier.Core.Providers;
using Vernier.Core.Settings;
using Vernier.Core.Tools;
using Vernier.Core.Windowing;

namespace Vernier.App;

public sealed class TranscriptItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _text = "", _meta = "";
    private IReadOnlyList<CheckResult> _checks = [];
    private IReadOnlyList<string> _notices = [];

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public required string Role { get; init; }
    public bool IsUser => Role == "user";
    public string Text { get => _text; set { _text = value; Raise(nameof(Text)); } }
    public string Meta { get => _meta; set { _meta = value; Raise(nameof(Meta)); } }
    public IReadOnlyList<CheckResult> Checks { get => _checks; set { _checks = value; Raise(nameof(Checks)); } }
    public IReadOnlyList<string> Notices { get => _notices; set { _notices = value; Raise(nameof(Notices)); } }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

/// <summary>A question about the screen: which monitor to look at, and any ink the learner drew on it (physical pixels).</summary>
/// <param name="Foreground">The application the learner was using; its text and name give the tutor context. 0 if unknown.</param>
public sealed record ScreenAsk(PxRect Monitor, IReadOnlyList<IReadOnlyList<PxPoint>> Ink, nint Foreground = 0)
{
    public bool HasInk => Ink.Count > 0;
}

/// <summary>A capture made by a tool during a turn, with what was located on it.</summary>
public sealed record ToolObservation(CapturedFrame Frame, ScreenIndex Screen);

/// <summary>
/// The one place that knows what the tutor is doing right now. The workspace and the ask bar
/// both show this same state. All members are used from the UI thread.
/// </summary>
public sealed class TutorSession
{
    public const string DefaultScreenQuestion = "Look at my screen. Explain what I am working on, or if it is my own work, tell me what to fix or improve.";
    public const string DefaultInkQuestion = "Explain what I marked.";
    public const string DefaultAttachmentQuestion = "Explain what I attached, and how it relates to what is on my screen.";

    private readonly AppSettings _settings;
    private readonly ProviderFactory _providers;
    private readonly TutorPipeline _pipeline;
    private readonly LearningStore _store;
    private readonly ScreenCaptureService _capture;
    private readonly Observer _observer;
    private readonly AnnotationStore _annotations;
    private readonly IWindowCapturePolicyService _policy;
    private readonly DiagnosticLog _log;
    private readonly List<ChatMessage> _history = [];
    private readonly SessionMemory _memory = new();
    private CancellationTokenSource? _turn;
    private long? _sessionId;
    private int _observations;
    // A tool turn granted and a CLI started while the learner is still typing (see Prewarm).
    private (ToolTurn Turn, IDisposable Grant, string ProviderKey)? _prewarmed;

    public TutorSession(AppSettings settings, ProviderFactory providers, TutorPipeline pipeline, LearningStore store,
        ScreenCaptureService capture, Observer observer, AnnotationStore annotations, IWindowCapturePolicyService policy, DiagnosticLog log)
    {
        (_settings, _providers, _pipeline, _store, _capture, _observer, _annotations, _policy, _log) =
            (settings, providers, pipeline, store, capture, observer, annotations, policy, log);
    }

    public ObservableCollection<TranscriptItem> Transcript { get; } = [];
    /// <summary>Every tool call of this run of the app, newest last, for the activity list.</summary>
    public ObservableCollection<ToolEvent> ToolEvents { get; } = [];
    public event Action<ToolEvent>? ToolActivity;
    public TutorMode Mode { get; set; } = TutorMode.Tutor;
    public bool IsBusy { get; private set; }
    public bool IsPaused { get; private set; }
    /// <summary>True from the moment a capture starts until the answer that uses it is finished.</summary>
    public bool IsLooking { get; private set; }
    public PipelineStage? Stage { get; private set; }
    public ContextSnapshot? LastSnapshot { get; private set; }
    public TutorTurn? LastTurn { get; private set; }
    public IReadOnlyList<ActionPolicy.Decision> PendingActions { get; private set; } = [];
    /// <summary>The window that was in front for the last screen question; proposed input steps are confined to it.</summary>
    public nint ActionWindow { get; private set; }
    public string Status { get; private set; } = "Ready.";

    /// <summary>State changed; views re-read the properties above.</summary>
    public event Action? Changed;
    public event Action<TranscriptItem>? AnswerUpdated;
    public event Action<string>? AnswerCompleted;

    /// <summary>Set once the learner has agreed, for this run of the app, to send attached files to a remote provider.</summary>
    public bool AttachmentsToRemoteAllowed { get; set; }

    /// <summary>The overlay removed marks because what they pointed at changed.</summary>
    public void NoteMarksWentStale()
    {
        if (IsBusy) return;
        Status = "The screen changed, so the marks were cleared. Ask again to look at it as it is now.";
        Changed?.Invoke();
    }

    public bool CaptureBlocked => !_settings.AllowScreenCapture || _policy.Mode == CapturePolicyMode.ShareSafe || IsPaused;

    public string CaptureBlockedReason =>
        !_settings.AllowScreenCapture ? "Screen capture is off. Turn it on in Settings › Privacy."
        : _policy.Mode == CapturePolicyMode.ShareSafe ? "Share-Safe mode is on, and it stops Vernier looking at the screen. Switch the selector at the top of the workspace to Standard window or Request capture exclusion, then ask again."
        : IsPaused ? "Paused." : "";

    public void NewConversation()
    {
        Cancel();
        _history.Clear();
        _memory.Clear();
        Transcript.Clear();
        _sessionId = null;
        LastTurn = null;
        PendingActions = [];
        Status = "New conversation.";
        Changed?.Invoke();
    }

    /// <summary>Pause: stops capture, the running answer, the microphone and speech, immediately.</summary>
    public void Pause()
    {
        IsPaused = true;
        Cancel();
        DiscardPrewarm();
        AppServices.SpeechIn?.Cancel();
        AppServices.SpeechOut?.Stop();
        AppServices.Overlays?.ArmPen(false);
        Status = "Paused. Nothing is being captured, recorded or sent.";
        Changed?.Invoke();
    }

    public void Resume()
    {
        IsPaused = false;
        Status = "Ready.";
        Changed?.Invoke();
    }

    public void Cancel() => _turn?.Cancel();

    /// <summary>Called when Share-Safe mode is switched on.</summary>
    public void EnterShareSafe()
    {
        AppServices.Overlays?.ArmPen(false);
        Status = "Share-Safe: capture exclusion requested, no new captures.";
        Changed?.Invoke();
    }

    private async Task<ContextSnapshot?> CaptureAsync(ScreenAsk screen, CancellationToken ct)
    {
        var foreground = screen.Foreground != 0 && Win32.IsWindow(screen.Foreground) ? screen.Foreground : Win32.GetAncestor(Win32.GetForegroundWindow(), Win32.GA_ROOT);
        Win32.GetWindowThreadProcessId(foreground, out var pid);
        if (pid == Environment.ProcessId) foreground = 0; // never read Vernier's own windows as the subject
        ActionWindow = foreground;
        AppServices.Overlays?.Track(foreground);
        // Keep Vernier's own windows out of its own captures, whatever the user's policy mode is.
        CapturedFrame? frame;
        try
        {
            frame = await AppServices.WithSelfExcludedAsync(() => _capture.Capture(new CaptureTarget.Screen(screen.Monitor, foreground), _settings.MaxImageEdge, screen.Ink));
        }
        catch (InvalidOperationException e)
        {
            // A locked session or a secure desktop refuses capture. The question is still worth answering without the screen.
            _log.Warn("Screen capture failed: " + e.Message);
            return null;
        }
        if (frame is null) return null;
        return LastSnapshot = await _observer.ObserveAsync(frame, ct);
    }

    /// <summary>
    /// A capture made by the look tool: the whole monitor again, or one area at full resolution.
    /// Runs from a tool thread; the capture itself happens through the UI thread's dispatcher.
    /// </summary>
    public async Task<ToolObservation> ObserveForToolAsync(ScreenAsk ask, PxRect area, IdSequence ids, CancellationToken ct)
    {
        var frame = await AppServices.Ui.InvokeAsync(async () =>
        {
            if (CaptureBlocked) throw new ToolException(CaptureBlockedReason);
            var foreground = ActionWindow;
            try { return await AppServices.WithSelfExcludedAsync(() => _capture.Capture(new CaptureTarget.Screen(area, foreground), _settings.MaxImageEdge)); }
            catch (InvalidOperationException e) { throw new ToolException("Windows refused the capture: " + e.Message); }
        }).Task.Unwrap().ConfigureAwait(false);
        if (frame is null) throw new ToolException("That area could not be captured.");
        var snapshot = await _observer.ObserveOnceAsync(frame, ct).ConfigureAwait(false);
        return new ToolObservation(frame, new ScreenIndex($"o{Interlocked.Increment(ref _observations)}", frame.Geometry, snapshot.Layout, frame.At, frame.WindowHandle, ids));
    }

    /// <summary>The tools a screen question gets. Each also checks its own setting on every call.</summary>
    private static readonly string[] ToolNames = [.. ScreenTools.Names, .. ContextTools.Names, .. ComputerTools.Names];

    /// <summary>Note tools join only when a vault is connected and notes may go to this provider (local, or allowed in Settings).</summary>
    /// Google Docs: adding is offered whenever it is connected (each addition approved); reading follows the notes rule.
    private string[] ToolNamesFor(IChatProvider provider)
    {
        TurnProviderIsLocal = provider.Capabilities.IsLocal;
        var notesMayGo = provider.Capabilities.IsLocal || _settings.AllowSendNotesToRemote;
        string[] names = !string.IsNullOrWhiteSpace(_settings.VaultPath) && notesMayGo ? [.. ToolNames, .. NoteTools.Names] : ToolNames;
        if (AppServices.Google.IsConnected) names = [.. names, .. GoogleTools.WriteNames, .. notesMayGo ? GoogleTools.ReadNames : []];
        return names;
    }

    private static IReadOnlyList<ToolDefinition> Definitions(IEnumerable<string> names) =>
        [.. AppServices.Tools.Registry.Definitions.Where(d => names.Contains(d.Name))];

    /// <summary>Send earlier episodes with the next question (the learner can drop them from the ask bar).</summary>
    public bool IncludeEarlierContext { get; set; } = true;
    /// <summary>Earlier turns of this conversation matching a query, in full (the <c>session_recall</c> tool).</summary>
    public string Recall(string query) => _memory.Recall(query);

    /// <summary>Whether the provider of the current tool turn runs on this computer (tool gates recheck the notes permission with it).</summary>
    public bool TurnProviderIsLocal { get; private set; }
    /// <summary>The context that went with the last question, for the ask bar to show.</summary>
    public Vernier.Core.Context.ContextSelection? LastContext { get; private set; }

    /// <summary>The model the learner picked in the ask bar; it serves every question until changed. Null: default or routes.</summary>
    public ProviderSettings? ModelOverride { get; set; }
    private TokenUsage? LastUsage { get; set; }

    public static string Describe(ProviderSettings p) =>
        p.DisplayName + (string.IsNullOrWhiteSpace(p.Model) ? "" : " · " + p.Model) + (string.IsNullOrWhiteSpace(p.Effort) ? "" : " · " + p.Effort + " effort");

    private static string? ProcessName(nint hwnd)
    {
        if (hwnd == 0) return null;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return null; }
    }

    private string ProviderKey(ProviderSettings p) => $"{p.Id}|{p.Model}|{p.Effort}|{_settings.AllowComputerControl}";

    /// <summary>
    /// Called when the ask bar opens. With Claude Code, the CLI takes several seconds to start; it is
    /// started now, with this turn's tools granted, so the start-up overlaps the learner typing.
    /// Nothing is captured or sent: the CLI waits until the question is asked, and is discarded if not.
    /// </summary>
    public void Prewarm()
    {
        ProviderSettings chosen;
        try { chosen = ModelRouter.Choose(_settings, TaskKind.Tutor, ModelOverride).Provider; }
        catch (ProviderException) { return; }
        if (IsBusy || CaptureBlocked || AppServices.Mcp is null || chosen.Kind != ProviderKinds.ClaudeCode) return;
        if (_prewarmed is { } warm && warm.Turn.IsActive && warm.ProviderKey == ProviderKey(chosen) && ClaudeCodeProvider.HasWarm) return;
        DiscardPrewarm();
        if (_providers.Create(chosen) is not ClaudeCodeProvider provider) return;
        // The grant expires with the waiting CLI (two minutes), so an unused one cannot linger.
        var names = ToolNamesFor(provider);
        var turn = new ToolTurn(names, new CancellationTokenSource(ClaudeCodeProvider.WarmLifetime).Token);
        var grant = AppServices.Mcp.Open(turn);
        provider.Prewarm(_pipeline.SystemPromptFor(provider, _settings.AllowComputerControl, tools: true), new ToolSession(Definitions(names), turn, AppServices.Tools, AppServices.Mcp));
        _prewarmed = (turn, grant, ProviderKey(chosen));
    }

    /// <summary>The bar closed or the learner paused: the waiting CLI and its grant go.</summary>
    public void DiscardPrewarm()
    {
        if (_prewarmed is { } warm) warm.Grant.Dispose();
        _prewarmed = null;
        ClaudeCodeProvider.DiscardWarm();
    }

    /// <param name="screen">When given, the monitor is captured for this question. Null asks a text-only question.</param>
    /// <param name="attachments">Files the learner attached to this question.</param>
    public async Task AskAsync(string question, string? notesContext = null, ScreenAsk? screen = null, IReadOnlyList<Attachment>? attachments = null)
    {
        attachments ??= [];
        question = question.Trim();
        if (question.Length == 0 && screen is not null)
            question = screen.HasInk ? DefaultInkQuestion : attachments.Count > 0 ? DefaultAttachmentQuestion : DefaultScreenQuestion;
        if (question.Length == 0 || IsBusy) return;
        if (IsPaused) Resume();

        // A pen question is an "explain by pointing" question; anything else uses the mode the learner chose.
        var mode = screen is { HasInk: true } ? TutorMode.Annotate : Mode;
        RouteDecision route;
        try
        {
            var task = ModelRouter.Classify(question, screen?.HasInk == true, attachments.Any(a => a.Image is not null),
                screen is null ? null : AppProfiles.Match(ProcessName(screen.Foreground), null), mode);
            route = ModelRouter.Choose(_settings, task, ModelOverride);
        }
        catch (ProviderException e)
        {
            Status = e.Message;
            Changed?.Invoke();
            return;
        }
        var settingsProvider = route.Provider;

        IsBusy = true;
        _turn = new CancellationTokenSource();
        var ct = _turn.Token;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double? firstWords = null;
        bool ok = false, called = false;
        Transcript.Add(new TranscriptItem
        {
            Role = "user", Text = question,
            Meta = (screen is null ? "you" : screen.HasInk ? "you · marked on screen" : "you · about the screen") + (attachments.Count > 0 ? $" · {attachments.Count} attached" : ""),
        });
        var answer = new TranscriptItem { Role = "assistant", Meta = Describe(settingsProvider) };
        Transcript.Add(answer);
        IDisposable? toolGrant = null;
        Status = "Working…";
        Changed?.Invoke();

        try
        {
            var provider = _providers.Create(settingsProvider);
            // Refusals are decided here, before anything is captured or sent: a tutor asked about a screen it was not given can only say it cannot see it.
            string? refusal =
                screen is not null && CaptureBlocked ? CaptureBlockedReason
                : attachments.Count > 0 && !provider.Capabilities.IsLocal && !_settings.AllowSendNotesToRemote && !AttachmentsToRemoteAllowed
                    ? $"Attached files are not sent to {settingsProvider.DisplayName} until you allow it."
                : null;
            ContextSnapshot? context = null;
            if (refusal is null && screen is not null)
            {
                Stage = PipelineStage.Observe;
                IsLooking = true;
                Changed?.Invoke();
                context = await CaptureAsync(screen, ct);
                if (context is null) refusal = "Windows would not let Vernier capture the screen (a locked session, a sign-in prompt or a protected window does this). Nothing was sent. Try again once your work is in front.";
            }
            if (refusal is not null)
            {
                answer.Notices = [refusal];
                Status = "Not sent.";
                AnswerUpdated?.Invoke(answer);
                return;
            }

            IChatProvider? checker = _settings.Providers.FirstOrDefault(p => p.Id == _settings.VerifierProviderId) is { } v ? _providers.Create(v) : null;
            _sessionId ??= _store.StartSession(question.Length > 60 ? question[..60] + "…" : question, mode.ToString());

            // Tools exist only for a question about the screen, and only for providers that can call them.
            ToolSession? tools = null;
            ScreenIndex? index = null;
            if (context?.Frame is { } captured && provider.Capabilities.Tools)
            {
                ToolTurn toolTurn;
                var names = ToolNamesFor(provider);
                // A warm turn is reused only with the same tools: the vault or the notes permission may have changed since.
                if (_prewarmed is { } warm && warm.Turn.IsActive && warm.ProviderKey == ProviderKey(settingsProvider) && warm.Turn.Allowed.SetEquals(names))
                {
                    toolTurn = warm.Turn;
                    toolGrant = warm.Grant;
                    _prewarmed = null;
                }
                else
                {
                    DiscardPrewarm();
                    toolTurn = new ToolTurn(names, CancellationToken.None);
                    toolGrant = AppServices.Mcp?.Open(toolTurn);
                }
                ct.Register(toolTurn.End); // Esc revokes the tools at once
                index = new ScreenIndex($"o{Interlocked.Increment(ref _observations)}", captured.Geometry, context.Layout, captured.At, captured.WindowHandle, ScreenTools.Ids(toolTurn));
                toolTurn.State[ScreenTools.ScreenKey] = index;
                toolTurn.State[ScreenTools.FrameKey] = captured;
                toolTurn.State[ScreenTools.AskKey] = screen!;
                toolTurn.EventAdded += e => AppServices.OnUi(() => { ToolEvents.Add(e); ToolActivity?.Invoke(e); });
                tools = new ToolSession(Definitions(names), toolTurn, AppServices.Tools, AppServices.Mcp);
            }

            var callbacks = new PipelineCallbacks
            {
                Stage = s => AppServices.OnUi(() => { Stage = s; Changed?.Invoke(); }),
                // Below input and render priority, coalesced by the pipeline: the pen and the edge light stay smooth while text streams.
                Text = t =>
                {
                    firstWords ??= clock.Elapsed.TotalSeconds;
                    AppServices.OnUiBackground(() => { answer.Text = t; AnswerUpdated?.Invoke(answer); });
                },
                Tool = n => AppServices.OnUi(() => { if (!n.Finished && n.Name.Length > 0) { Status = ToolStatus(n.Name); Changed?.Invoke(); } }),
            };
            WorkContext? work = null;
            Vernier.Core.Context.ContextSelection? episodes = null;
            if (_settings.AllowActivityContext && AppServices.Context is { } hub)
            {
                work = await hub.SnapshotAsync(ActionWindow, readContent: context is not null);
                episodes = hub.Engine.Select(question, DateTimeOffset.UtcNow);
                if (!IncludeEarlierContext) episodes = episodes.WithoutEarlier();
                if (CaptureBlocked) episodes = episodes.WithoutScreenText(); // capture off / Share-Safe: text read earlier stays here
                hub.Engine.Asked(question, DateTimeOffset.UtcNow);
                LastContext = episodes;
            }
            var request = new TutorRequest(mode, question, context, [.. _history], _sessionId, notesContext, _settings.AllowComputerControl, attachments, tools, index, work, episodes,
                _memory.Render(MasteryTutor.HistoryTurns));
            called = true;
            var turn = await Task.Run(() => _pipeline.RunAsync(request, provider, checker, callbacks, ct), ct);
            var allowActions = _settings.AllowComputerControl && context is not null && ActionWindow != 0;

            answer.Text = turn.Output.Text;
            answer.Checks = turn.Verification.Checks;
            answer.Notices = [.. answer.Notices, .. turn.Notices];
            answer.Meta = $"{Describe(settingsProvider)} · {route.Why}" + (turn.Usage is { } u ? $" · {u.Input} in / {u.Output} out tokens" : "") + (turn.Corrected ? " · corrected once" : "");
            ok = true;
            LastUsage = turn.Usage;
            _history.Add(ChatMessage.User(MasteryTutor.Remember(question, context, attachments)));
            // What the tutor did with its tools stays in the conversation, so "the second mark" means something next turn.
            var did = tools?.Turn.Events.Where(e => !e.IsError && e.Tool == "overlay_mark").Select(e => e.Outcome).ToList() ?? [];
            // Outcomes quote screen text, so they are fenced like any other screen text.
            _history.Add(ChatMessage.Assistant(turn.Output.Text + (did.Count > 0 ? "\n" + Vernier.Core.Security.Untrusted.Wrap("marks-drawn", string.Join(" | ", did), 1500) : "")));
            LastTurn = turn;
            if (_history.Count > 4 * MasteryTutor.HistoryMessages) _history.RemoveRange(0, _history.Count - 2 * MasteryTutor.HistoryMessages); // only the newest are ever sent; older ones live on in the session memory
            _memory.Add(new TurnNote(DateTimeOffset.UtcNow,
                context?.Frame is { } fr ? $"{(string.IsNullOrWhiteSpace(fr.ProcessName) ? "screen" : fr.ProcessName)} — {fr.WindowTitle}" : "typed, no screen",
                question, turn.Output.Data.Concept, SessionMemory.Gist(turn.Output.Text), turn.Verification.Passed, turn.Verification.Checks.Count(c => c.Status == CheckStatus.Failed),
                did.Count, turn.Output.Data.Misconception, turn.Output.Data.OpenQuestion));

            if (turn.Annotations.Count > 0)
            {
                _annotations.Add([.. turn.Annotations]);
                AppServices.Overlays?.Watch(WatchArea(turn.Annotations, context?.Frame?.Geometry.SourcePhysical));
            }
            PendingActions = allowActions && context?.Frame is { } f
                ? ActionPolicy.Review(turn.Output.Data.Actions, f.Geometry, Win32.WindowBounds(ActionWindow))
                : [];

            Status = turn.Verification.Summary;
            AnswerUpdated?.Invoke(answer);
            AnswerCompleted?.Invoke(turn.Output.Text);
        }
        catch (OperationCanceledException)
        {
            answer.Text = answer.Text.Length > 0 ? answer.Text + "\n\n[Stopped]" : "[Stopped before an answer arrived]";
            Status = "Stopped.";
            AnswerUpdated?.Invoke(answer);
        }
        catch (ProviderException e)
        {
            _log.Error($"Provider {settingsProvider.Id} failed ({e.Kind})", e);
            answer.Text = "";
            answer.Notices = [Explain(e)];
            Status = "The provider could not answer.";
            AnswerUpdated?.Invoke(answer);
        }
        catch (Exception e) when (e is InvalidOperationException or System.IO.IOException or System.Runtime.InteropServices.COMException)
        {
            _log.Error("Turn failed", e);
            answer.Notices = ["Something went wrong before the answer was ready: " + e.Message];
            Status = "Failed.";
            AnswerUpdated?.Invoke(answer);
        }
        finally
        {
            toolGrant?.Dispose(); // the turn's tools are revoked: nothing can be drawn or run after the answer
            if (_prewarmed is not null) DiscardPrewarm(); // started for a question that went elsewhere (another provider, no screen)
            if (called) // refusals never reached a model, so there is nothing to record
                try
                {
                    AppServices.Routes?.Add(new RouteRecord(DateTimeOffset.UtcNow, route.Task.ToString(), settingsProvider.Id, settingsProvider.Model, settingsProvider.Effort, route.Why,
                        firstWords, clock.Elapsed.TotalSeconds, LastUsage?.Input, LastUsage?.Output, ok));
                }
                catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { _log.Warn("Could not record the route: " + e.Message); }
            LastUsage = null;
            IsBusy = false;
            IsLooking = false;
            Stage = null;
            _turn.Dispose();
            _turn = null;
            Changed?.Invoke();
        }
    }

    // The area the new marks sit in, with room around it: what must stay still for them to stay true.
    private static PxRect? WatchArea(IReadOnlyList<Annotation> marks, PxRect? captured)
    {
        var points = marks.SelectMany(m => m.Points).ToList();
        if (points.Count == 0 || captured is not { } screen) return null;
        double l = points.Min(p => p.X), t = points.Min(p => p.Y), r = points.Max(p => p.X), b = points.Max(p => p.Y);
        const double pad = 60;
        return new PxRect(l - pad, t - pad, r - l + 2 * pad, b - t + 2 * pad).Intersect(screen);
    }

    public void ClearPendingActions()
    {
        PendingActions = [];
        Changed?.Invoke();
    }

    private static string ToolStatus(string tool) => tool switch
    {
        "overlay_mark" => "marking the screen",
        "overlay_clear" => "clearing marks",
        "look" => "looking closer",
        "find_on_screen" => "finding it on screen",
        _ => "using " + tool,
    };

    private static string Explain(ProviderException e) => e.Kind switch
    {
        ProviderErrorKind.NotConfigured => e.Message,
        ProviderErrorKind.Auth => "The provider rejected the sign-in. " + e.Message,
        ProviderErrorKind.RateLimited => "The provider is rate limiting requests. Vernier retried a bounded number of times and stopped. " + e.Message,
        ProviderErrorKind.Network => "The provider could not be reached. Check the network connection. " + e.Message,
        ProviderErrorKind.Timeout => e.Message + " Nothing was switched to another provider.",
        _ => e.Message,
    };
}
