# Graph Report - Hopefully-this-goes-somewhere  (2026-10-17)

## Corpus Check
- 123 files · ~138,193 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 2, .rc 1, .vcxproj 1)

## Summary
- 3537 nodes · 8375 edges · 162 communities (147 shown, 15 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 867 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- LearningStore
- PipelineStage
- UserControl
- UserControl
- ProviderSettings
- TutorStructured
- PxRect
- Win32
- Fact
- TrayIcon
- FilesPage
- UserControl
- ReviewPage
- GraphifySource
- SettingsPage
- Vernier.Core.Agents
- UserControl
- Security and privacy document
- GeminiProvider
- ChatRequest
- Runtime pipeline (TutorPipeline.RunAsync)
- TempDir
- SessionMemory
- AppSettings
- TutorSession
- .Initialize
- AnnotationStore
- AnthropicProvider
- Fact
- .The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible
- UserControl
- CaptureGeometry
- .Parse
- .Monitors
- .Assess
- Hopefully-this-goes-somewhere.cpp
- ResourceDictionary
- Native
- .ExecuteAsync
- ScopedFiles
- MainWindow
- Window
- ScreenCaptureService
- AskBar
- Design system document
- Verification report
- ToolTurn
- Vernier README (tutor that sits over your screen)
- ProposedAnnotation
- Vernier.Tests.csproj
- AppServices
- ExtractedText
- ComputerAction
- Fact
- .Sync
- Threat model
- ToolRegistry
- Verifier
- ITextExtractor
- DiagnosticsPage
- CapturedFrame
- FakeUser32
- VerificationReport
- Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)
- AnnotationOverlayManager
- ConnectorException
- ITool
- TextBox
- Architecture document
- .Claude_code_cli_answers_a_connection_test
- IWindowCapturePolicyService
- InlineData
- IEpisodeStore
- ClaudeCodeProvider
- LibraryPage
- TutorMode
- .Redact
- SettingsTests
- .OnProviderFieldChanged
- ScreenIndex
- WindowCapturePolicyService
- Parser
- CodexCliProvider
- ProviderErrorKind
- .Unsupported_sensitive_and_broken_files_are_refused_with_a_reason
- .Cancel
- CredentialManagerSecretStore
- TranscriptItem
- HotkeyManager
- TutorPanel
- Episode
- PolicyStatus
- .Sync
- ScreenAsk
- Fact
- ContextEngine
- .OnStartup
- .OnPrivacy
- SelectionChangedEventArgs
- McpServer
- .DHash_distance_is_zero_for_identical_and_large_for_inverted
- .OnSessionSelected
- .OnTab
- .ExecuteAsync
- Examples
- ContextHub
- .BuildRequest
- .OnRate
- DeleteSession
- .List
- .Gist
- TitleAndAddressTests
- MarkPainter
- MediaState
- Search
- Hopefully-this-goes-somewhere
- Dark.xaml
- Light.xaml
- SessionsView
- GlassSurface
- Annotation
- .Read
- MemorySecrets
- glow.cpp
- Duplication
- common.h
- .StatusAsync
- SapiSpeechInput
- duplication.cpp
- KnowledgeStoreTests
- .ReviewAndRunAsync
- AnnotationSurfaceWindow
- Depth
- SapiSpeechOutput
- .ExecuteAsync
- TaskKind
- Glow
- Edge
- .Use
- .OnAnswer
- imaging.cpp
- TitleNormaliser
- .Ask
- .WithSelfExcludedAsync
- FakeExtractor
- ContextOptions
- .Overlay_latency_and_cost
- .OnPolicyChanged
- Rect
- Implementation plan, risks, acceptance
- RouteLog
- WndProc
- .OnSelect
- FormulasView
- .OnSpeechStop
- Aurora.xaml
- Glacier.xaml
- Nebula.xaml
- Tools.cs

## God Nodes (most connected - your core abstractions)
1. `PxRect` - 81 edges
2. `TutorSession` - 77 edges
3. `LearningStore` - 76 edges
4. `UserControl` - 70 edges
5. `ToolTurn` - 67 edges
6. `Win32` - 61 edges
7. `AppServices` - 55 edges
8. `AppSettings` - 52 edges
9. `PxPoint` - 49 edges
10. `AskBar` - 48 edges

## Surprising Connections (you probably didn't know these)
- `3. Architecture decisions (ADR log)` --references--> `Window`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Capture.cs
- `Decisions` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `Milestones` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `Defects corrected in revision 2 (refused capture aborted question; ask bar read itself)` --references--> `AskBar`  [EXTRACTED]
  docs/VERIFICATION_REPORT.md → src/Vernier.App/Views/AskBar.cs
- `Verified: UI Automation and Windows OCR both read text; Observer prefers accessibility` --references--> `Observer`  [EXTRACTED]
  docs/VERIFICATION_REPORT.md → src/Vernier.Core/Agents/Observer.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Observer, Mastery Tutor and Verifier cooperate in the tutoring pipeline** — docs_architecture_observer_agent, docs_architecture_mastery_tutor_agent, docs_architecture_verifier_agent, docs_architecture_runtime_pipeline, docs_architecture_vernier_structured_block [EXTRACTED 1.00]
- **Glass, capture light, ask bar and tray form the always-there shell under the yellow/blue rule** — docs_architecture_glass, docs_architecture_capture_light, docs_architecture_ask_bar, docs_architecture_tray, docs_design_system_the_one_rule [INFERRED 0.85]
- **Consent gates, untrusted fencing, action policy and path guard implement the off-by-default security posture** — docs_security_and_privacy_consent_gates, docs_security_and_privacy_threat_prompt_injection, docs_security_and_privacy_threat_harmful_input_actions, docs_security_and_privacy_threat_path_traversal, claude_security_rules [INFERRED 0.85]

## Communities (162 total, 15 thin omitted)

### Community 0 - "LearningStore"
Cohesion: 0.06
Nodes (35): Cards, ExportCard, ExportConcept, ExportDocument, InvalidDataException, microsoft_data_sqlite, SqliteConnection, SqliteDataReader (+27 more)

### Community 1 - "PipelineStage"
Cohesion: 0.13
Nodes (15): Label, Typeface, StageScale, Busy, Current, PipelineStage, Confirm, Correct (+7 more)

### Community 2 - "UserControl"
Cohesion: 0.12
Nodes (23): Kind, Reason, Back, BackHost, CardMeta, CardView, Front, Grades (+15 more)

### Community 3 - "UserControl"
Cohesion: 0.12
Nodes (21): Available, Excluded, Expected, Missing, Observed, Path, Environment, Integrations (+13 more)

### Community 4 - "ProviderSettings"
Cohesion: 0.08
Nodes (32): ChatMessage, ImageInput, HttpClient, ProviderFactory, OpenAiCompatibleProvider, Capabilities, DisplayName, Id (+24 more)

### Community 5 - "TutorStructured"
Cohesion: 0.07
Nodes (29): List, Claim, Expr, UnitsLhs, UnitsRhs, Value, FormulaNote, Derivation (+21 more)

### Community 6 - "PxRect"
Cohesion: 0.08
Nodes (21): CaptureTarget, None, Region, Screen, Window, RECT, IReadOnlyList, DpiMath (+13 more)

### Community 7 - "Win32"
Cohesion: 0.06
Nodes (17): RECT, KEYBDINPUT, LASTINPUTINFO, MonitorEnumProc, MOUSEINPUT, Color, CaptureExclusionSelfTest, DllImport (+9 more)

### Community 8 - "Fact"
Cohesion: 0.12
Nodes (26): Action, PipelineCallbacks, Stage, Text, Tool, ProviderException, IsTransient, Kind (+18 more)

### Community 9 - "TrayIcon"
Cohesion: 0.06
Nodes (32): At, Dismissals, NOTIFYICONDATA, Action, DateTimeOffset, TimeSpan, Proactive, DllImport (+24 more)

### Community 10 - "FilesPage"
Cohesion: 0.06
Nodes (42): Degree, Label, Links, Task, AskAboutNote, DisconnectVault, EditInstruction, Explain (+34 more)

### Community 11 - "UserControl"
Cohesion: 0.09
Nodes (26): Checks, IsUser, Meta, Notices, ActionsBar, ActionsText, AskKey, Bar (+18 more)

### Community 12 - "ReviewPage"
Cohesion: 0.22
Nodes (7): ResolveButton, Reveal, KeyEventArgs, RoutedEventArgs, ReviewPage, Button, StudyItem

### Community 13 - "GraphifySource"
Cohesion: 0.16
Nodes (13): Output, JsonElement, Ok, Task, GraphEdge, GraphifySource, Name, GraphNode (+5 more)

### Community 14 - "SettingsPage"
Cohesion: 0.12
Nodes (10): DateTimeOffset, Func, RoutedEventArgs, Task, AccountRow, ActionName, UseName, SettingsPage (+2 more)

### Community 15 - "Vernier.Core.Agents"
Cohesion: 0.06
Nodes (68): anthropic, anthropic_exceptions, anthropic_models_messages, Vernier.Core.Context, Vernier.App.Views, Vernier.Core.Tools, Vernier.App.Context, Vernier.Tests (+60 more)

### Community 16 - "UserControl"
Cohesion: 0.08
Nodes (32): ActionLabel, ActionName, CanAct, Id, SignedIn, State, UseName, Accounts (+24 more)

### Community 17 - "Security and privacy document"
Cohesion: 0.15
Nodes (16): Security rules (non-negotiable), Window capture policy (IWindowCapturePolicyService over INativeWindowApi), learning.db SQLite schema (user_version 2, FTS5 messages_fts), API keys: environment variable, else Windows Credential Manager, Window capture exclusion (SetWindowDisplayAffinity WDA_EXCLUDEFROMCAPTURE on own windows), Consent gates enforced in code (AllowScreenCapture, AllowMicrophone, AllowSendNotesToRemote, AllowComputerControl), Local storage (%LOCALAPPDATA%\Vernier: learning.db, settings.json, logs, tmp), No telemetry, analytics or crash reports (+8 more)

### Community 18 - "GeminiProvider"
Cohesion: 0.14
Nodes (18): CancellationToken, HashSet, HttpClient, HttpMethod, HttpRequestMessage, HttpResponseMessage, IAsyncEnumerable, IReadOnlyList (+10 more)

### Community 19 - "ChatRequest"
Cohesion: 0.08
Nodes (32): ChatRunner (timeout, bounded retry, vision gate), Stage: Teach (IChatProvider.StreamAsync via ChatRunner), Threat: runaway cost or loops, Message, Raw, CancellationToken, CaptureGeometry, Task (+24 more)

### Community 20 - "Runtime pipeline (TutorPipeline.RunAsync)"
Cohesion: 0.11
Nodes (23): Step 4: Check your own work (second route, units, signs, limiting cases), Do-not rules (no solution dumps, no vague praise, no invented file contents, no unopened citations), Hint escalation ladder (point, leading question, principle, show the step), Step 5: Finish with a mastery check (transfer question, mental model, what is still open), One right answer is not mastery, Mastery Tutor skill (/mastery-tutor), Misconception matters more than the slip, Subject notes (mathematics, physics/engineering, programming, sciences) (+15 more)

### Community 21 - "TempDir"
Cohesion: 0.11
Nodes (14): Dir, ScopedFiles / PathGuard (confined file access, Propose then Apply), Threat: path traversal / unintended file writes, IReadOnlyCollection, CancellationToken, IEnumerable, PathGuard, Root (+6 more)

### Community 22 - "SessionMemory"
Cohesion: 0.21
Nodes (9): DateTimeOffset, IReadOnlyList, List, SessionMemory, Count, TurnNote, Fact, SessionMemoryTests (+1 more)

### Community 23 - "AppSettings"
Cohesion: 0.05
Nodes (37): ModelRoute, List, AppSettings, AcknowledgedObserveCost, ActiveProvider, ActiveProviderId, AllowActivityContext, AllowComputerControl (+29 more)

### Community 24 - "TutorSession"
Cohesion: 0.06
Nodes (30): Grant, ObservableCollection, ProviderKey, CancellationTokenSource, IDisposable, IEnumerable, IReadOnlyList, List (+22 more)

### Community 25 - ".Initialize"
Cohesion: 0.33
Nodes (4): Exception, IReadOnlyList, LinkedList, DiagnosticLog

### Community 26 - "AnnotationStore"
Cohesion: 0.17
Nodes (10): Func, IReadOnlyList, List, AnnotationStore, CanRedo, CanUndo, Items, Visible (+2 more)

### Community 27 - "AnthropicProvider"
Cohesion: 0.06
Nodes (29): IAnthropicClient, MessageParam, CancellationToken, Dictionary, Exception, IReadOnlyList, JsonElement, JsonObject (+21 more)

### Community 28 - "Fact"
Cohesion: 0.16
Nodes (7): JsonSerializerOptions, Regex, TutorOutput, Fact, GeometryTests, TutorOutputTests, VerifierTests

### Community 29 - ".The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible"
Cohesion: 0.07
Nodes (30): BaseUrl, ButtonBase, ComboBoxItem, Control, DependencyObject, EnumProc, DllImport, HashSet (+22 more)

### Community 30 - "UserControl"
Cohesion: 0.10
Nodes (24): Concept, Derivation, Description, Expression, Mastery.Level, Mastery.Reason, Meaning, Problem (+16 more)

### Community 31 - "CaptureGeometry"
Cohesion: 0.12
Nodes (18): Assisted input / computer use (ActionPolicy.Review, preview, approval, ActionExecutor, SendInputDriver), Manual: assisted input on a harmless target (Notepad), Threat: model proposes harmful input actions, ProposedAction, Amount, Keys, Text, Type (+10 more)

### Community 32 - ".Parse"
Cohesion: 0.05
Nodes (40): InlineUIContainer, Inner, Next, RichTextBox, Run, DependencyProperty, FormulaControl, Paragraph (+32 more)

### Community 33 - ".Monitors"
Cohesion: 0.11
Nodes (17): POINT, MONITORINFO, IReadOnlyList, MONITORINFO, POINT, Dispatcher, DllImport, Fact (+9 more)

### Community 34 - ".Assess"
Cohesion: 0.12
Nodes (17): ConceptState, DateTimeOffset, IReadOnlyList, CardSchedule, Evidence, EvidenceKinds, MasteryAssessment, MasteryLevel (+9 more)

### Community 35 - "Hopefully-this-goes-somewhere.cpp"
Cohesion: 0.15
Nodes (16): ATOM, BOOL, HINSTANCE, About(), HWND, LPARAM, LRESULT, UINT (+8 more)

### Community 36 - "ResourceDictionary"
Cohesion: 0.12
Nodes (19): IsDropDownOpen, B, Box, C, PART_ContentHost, PART_Track, ResourceDictionary, S (+11 more)

### Community 37 - "Native"
Cohesion: 0.06
Nodes (29): 4b. Revision 5 plan (owner request of 2026-10-16), Decisions, Milestones, Reflection: what the owner asked for, across both sessions, FrameInfo, IDisposable, LibraryImport, Png (+21 more)

### Community 38 - ".ExecuteAsync"
Cohesion: 0.17
Nodes (10): INPUT, INPUTUNION, CancellationToken, SendInputDriver, INPUT, KEYBDINPUT, MOUSEINPUT, InvalidOperationException (+2 more)

### Community 39 - "ScopedFiles"
Cohesion: 0.10
Nodes (20): JournalEntry, Role, ConcurrentDictionary, DateTimeOffset, IEnumerable, List, Text, FileChange (+12 more)

### Community 40 - "MainWindow"
Cohesion: 0.13
Nodes (11): ReviewNav, Dictionary, FrameworkElement, RadioButton, RoutedEventArgs, MainWindow, AskHotkey, Bar (+3 more)

### Community 41 - "Window"
Cohesion: 0.14
Nodes (17): GoalText, Lamp, NoticeBar, NoticeText, ObserveText, PageHost, PauseButton, PolicyText (+9 more)

### Community 42 - "ScreenCaptureService"
Cohesion: 0.17
Nodes (8): BitmapSource, Int32Rect, R, ScreenCaptureService, DrawingContext, Point, Height, Width

### Community 43 - "AskBar"
Cohesion: 0.11
Nodes (15): Border, DockPanel, FontFamily, Button, KeyEventArgs, List, Rectangle, TextBox (+7 more)

### Community 44 - "Design system document"
Cohesion: 0.14
Nodes (18): Product shape (tray-resident, Ask and Pen only, yellow/blue rule, capture light on while looking), Accounts via vendor CLIs (CliAccounts), Design system document, Direction A: Instrument (v1, workspace-first), Direction B: Glass and two inks (chosen), Keyboard chords, On-screen ink colours (fixed, drawn over a 3 px dark halo), Why it was redesigned (v1 warm paper, serif, orange accent) (+10 more)

### Community 45 - "Verification report"
Cohesion: 0.10
Nodes (22): Verification workflow (Evaluate, Correct, Confirm), Acceptance checklist (definition of done), Manual: Ask, Pen and the capture light, Manual: capture exclusion with your conferencing tool, Manual verification checklist, Items marked not verified by the author (multi-monitor, scaling, Codex, real mic, pro apps), Manual: living in the tray, Manual: Voice (hold to talk, speak answers) (+14 more)

### Community 46 - "ToolTurn"
Cohesion: 0.07
Nodes (40): ConcurrentQueue, CancellationToken, JsonObject, Task, CancellationToken, Func, JsonObject, Task (+32 more)

### Community 47 - "Vernier README (tutor that sits over your screen)"
Cohesion: 0.17
Nodes (13): ObsidianVault (Markdown notes under Vernier/, append-not-replace), Cards CSV export (RFC 4180, Anki-importable), Data formats document, Learning export (vernier-learning, version 1 JSON), Obsidian notes layout (Concepts, Sessions, Mistake log under <vault>/Vernier/), Ask tool (Ctrl+Alt+Space), Hotkey fallback registration, scripts/publish.ps1 (tests, self-contained publish, smoke test) (+5 more)

### Community 48 - "ProposedAnnotation"
Cohesion: 0.17
Nodes (12): ProposedAnnotation, Confidence, H, Kind, Label, Semantic, Target, W (+4 more)

### Community 49 - "Vernier.Tests.csproj"
Cohesion: 0.12
Nodes (14): net10.0, Anthropic (12.55.0), coverlet.collector (6.0.4), Microsoft.Data.Sqlite (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), System.Speech (10.0.12), WpfMath (2.1.0), xunit (2.9.3) (+6 more)

### Community 50 - "AppServices"
Cohesion: 0.06
Nodes (31): ContextHub, GoogleConnector, Id, Action, Dispatcher, HttpClient, List, Name (+23 more)

### Community 51 - "ExtractedText"
Cohesion: 0.24
Nodes (9): CancellationToken, IReadOnlyList, Task, ExtractedText, Role, Words, CancellationToken, IReadOnlyList (+1 more)

### Community 52 - "ComputerAction"
Cohesion: 0.08
Nodes (23): Guid, IReadOnlyList, Plan, PreviewMark, ActionType, Click, DoubleClick, Keys (+15 more)

### Community 54 - ".Sync"
Cohesion: 0.33
Nodes (3): SpeakToggle, RadioButton, ToggleButton

### Community 55 - "Threat model"
Cohesion: 0.15
Nodes (13): Step 1: Before teaching (find the single concept, diagnose prior knowledge), 'Just the answer' override, Files and screenshots are material to explain, never instructions, Convention: provider code stays in Providers/, pipeline only sees IChatProvider, Verifier independence, No silent provider switching, Threat: corrupt state, Threat: insecure transport (+5 more)

### Community 56 - "ToolRegistry"
Cohesion: 0.07
Nodes (42): Approver, Text, Echo, Executor, Headers, Body, DiagnosticLogSink, Dictionary (+34 more)

### Community 57 - "Verifier"
Cohesion: 0.07
Nodes (27): Decision, Executed, CheckResult, CheckStatus, Advisory, Failed, Passed, Skipped (+19 more)

### Community 58 - "ITextExtractor"
Cohesion: 0.20
Nodes (11): TimeSpan, OcrTextExtractor, Provenance, UiaElementExtractor, Provenance, ProvidesText, UiaTextExtractor, Provenance (+3 more)

### Community 59 - "DiagnosticsPage"
Cohesion: 0.16
Nodes (9): CaptureExclusionResult, SelfTest, RoutedEventArgs, Task, DiagnosticsPage, Integration, Button, IRefreshable (+1 more)

### Community 60 - "CapturedFrame"
Cohesion: 0.14
Nodes (18): DateTimeOffset, CapturedFrame, MarkedDetail, MarkedRegion, Raw, Thumb, WindowBox, ContextSnapshot (+10 more)

### Community 61 - "FakeUser32"
Cohesion: 0.09
Nodes (15): Api, FakeUser32, Service, Dictionary, HashSet, FakeUser32, Affinity, Children (+7 more)

### Community 62 - "VerificationReport"
Cohesion: 0.16
Nodes (12): CancellationToken, IEnumerable, IReadOnlyList, Task, TimeSpan, TokenUsage, TutorTurn, VerificationReport (+4 more)

### Community 63 - "Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)"
Cohesion: 0.22
Nodes (9): Convention: Core never references WPF or P/Invoke, graphify-out/ project knowledge graph, Convention: no abstraction without a second implementation or a test seam, Rule: always launch tests/screenshots with --data <temp dir>, Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF), Vernier.App project (WPF, Platform/Overlay/Views), Vernier.Core project (net10.0, no UI), Vernier.Tests (xUnit: logic, fixture, real-Windows desktop, opt-in live) (+1 more)

### Community 64 - "AnnotationOverlayManager"
Cohesion: 0.06
Nodes (29): DateTime, Dock, Motion: the capture wave is the one expressive motion, Color, DispatcherTimer, EventArgs, Func, IEnumerable (+21 more)

### Community 65 - "ConnectorException"
Cohesion: 0.05
Nodes (48): HttpMessageHandler, Pkce, Redirect, CancellationToken, HttpClient, HttpListener, HttpListenerContext, Listener (+40 more)

### Community 66 - "ITool"
Cohesion: 0.07
Nodes (46): 3. Architecture decisions (ADR log), IEnumerable, HttpClient, IEnumerable, ContextTools, GetContext, Definition, MediaControl (+38 more)

### Community 67 - "TextBox"
Cohesion: 0.21
Nodes (11): AssessText, EdgeText, ExcludedText, GoalsText, GoogleClientIdBox, IntervalsText, LevelText, PaceText (+3 more)

### Community 68 - "Architecture document"
Cohesion: 0.19
Nodes (16): AnnotationOverlayManager (owns glass and strips, collects ink, InkSettled after 1.3 s), AnnotationStore (immutable list, undo/redo, Rebase, DropOutside), Architecture document, Ask bar (the only interactive overlay), Capture light (EdgeStripWindow, four edge strips per monitor), Glass (AnnotationSurfaceWindow, one transparent click-through window per monitor), Pen capture (ink painted into the image, MarkedRegion, MarkedDetail crop), Stack decision: C# on .NET 10 with WPF, no C++ layer (+8 more)

### Community 69 - ".Claude_code_cli_answers_a_connection_test"
Cohesion: 0.22
Nodes (7): CancellationToken, Fact, JsonObject, Task, LiveClaudeCodeTests, Enabled, RealGraphifyTests

### Community 70 - "IWindowCapturePolicyService"
Cohesion: 0.13
Nodes (10): Window, CapturePolicyMode, RequestExclusion, ShareSafe, Standard, IReadOnlyList, IWindowCapturePolicyService, Mode (+2 more)

### Community 71 - "InlineData"
Cohesion: 0.19
Nodes (5): Dictionary, Units, InlineData, Theory, ExpressionTests

### Community 72 - "IEpisodeStore"
Cohesion: 0.20
Nodes (4): IReadOnlyList, LearningStore, IEpisodeStore, LearningEpisodeStore

### Community 73 - "ClaudeCodeProvider"
Cohesion: 0.06
Nodes (37): ExitCode, IReadOnlyDictionary, Process, ContextSelection, IReadOnlyList, TutorRequest, ToolSession, Definitions (+29 more)

### Community 74 - "LibraryPage"
Cohesion: 0.27
Nodes (5): SessionRow, RoutedEventArgs, LibraryPage, SessionRow, TextChangedEventArgs

### Community 75 - "TutorMode"
Cohesion: 0.29
Nodes (6): TutorMode, Annotate, Ask, Assist, Review, Tutor

### Community 76 - ".Redact"
Cohesion: 0.23
Nodes (4): GeneratedRegex, Regex, SecretRedactor, Untrusted

### Community 77 - "SettingsTests"
Cohesion: 0.20
Nodes (5): JsonSerializerOptions, SettingsStore, LoadWarning, Path, SettingsTests

### Community 78 - ".OnProviderFieldChanged"
Cohesion: 0.20
Nodes (7): EndpointText, GoogleConnectButton, ModelText, RemoveSample, TestButton, VisionCheck, Button

### Community 79 - "ScreenIndex"
Cohesion: 0.11
Nodes (19): Item, Score, DateTimeOffset, Dictionary, IReadOnlyList, List, ResolvedTarget, ScreenIndex (+11 more)

### Community 80 - "WindowCapturePolicyService"
Cohesion: 0.16
Nodes (11): Dictionary, DisplayAffinity, INativeWindowApi, CurrentProcessId, OsBuild, WindowCapturePolicyService, Mode, States (+3 more)

### Community 81 - "Parser"
Cohesion: 0.33
Nodes (5): Expr, Parser, AtEnd, Peek, Position

### Community 82 - "CodexCliProvider"
Cohesion: 0.12
Nodes (15): AnthropicProvider (official SDK, Messages streaming), ClaudeCodeProvider (claude -p stream-json subprocess, Claude Code sign-in), CodexCliProvider (codex exec subprocess, ChatGPT sign-in), GeminiProvider (streamGenerateContent SSE), IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities), OpenAiCompatibleProvider (OpenAI, Moonshot/Kimi, Ollama, LM Studio, llama.cpp), Reference project: claude-overlay, Self-exclusion during Vernier's own captures (+7 more)

### Community 83 - "ProviderErrorKind"
Cohesion: 0.20
Nodes (10): ProviderErrorKind, Auth, BadRequest, Network, NotConfigured, RateLimited, Refused, Server (+2 more)

### Community 84 - ".Unsupported_sensitive_and_broken_files_are_refused_with_a_reason"
Cohesion: 0.33
Nodes (4): Exception, AttachmentException, AttachmentReaderTests, ZipArchive

### Community 85 - ".Cancel"
Cohesion: 0.29
Nodes (3): Input, Send, TextBox

### Community 86 - "CredentialManagerSecretStore"
Cohesion: 0.42
Nodes (3): CREDENTIAL, CREDENTIAL, CredentialManagerSecretStore

### Community 87 - "TranscriptItem"
Cohesion: 0.22
Nodes (8): INotifyPropertyChanged, TranscriptItem, Checks, IsUser, Meta, Notices, Role, Text

### Community 88 - "HotkeyManager"
Cohesion: 0.20
Nodes (7): Action, Dictionary, HwndSource, List, HotkeyManager, Unavailable, EventArgs

### Community 89 - "TutorPanel"
Cohesion: 0.22
Nodes (7): Talk, KeyEventArgs, MouseButtonEventArgs, TranscriptItem, TutorSession, TutorPanel, Session

### Community 90 - "Episode"
Cohesion: 0.06
Nodes (30): From, SemaphoreSlim, Task, EpisodeSummariser, To, Episode, ActiveSeconds, App (+22 more)

### Community 91 - "PolicyStatus"
Cohesion: 0.33
Nodes (6): PolicyStatus, Applied, Failed, NotRequested, Requested, Unsupported

### Community 93 - "ScreenAsk"
Cohesion: 0.38
Nodes (5): CancellationToken, Task, ScreenAsk, HasInk, ToolObservation

### Community 94 - "Fact"
Cohesion: 0.29
Nodes (7): DateTimeOffset, Fact, Fixed, Provenance, NoText, Provenance, ScreenContextTests

### Community 95 - "ContextEngine"
Cohesion: 0.11
Nodes (18): App, Key, SessionItem, Since, DateTimeOffset, Dictionary, HashSet, List (+10 more)

### Community 96 - ".OnStartup"
Cohesion: 0.13
Nodes (8): ExitEventArgs, Application, Task, App, Task, FirstRunWindow, StartupEventArgs, Window

### Community 97 - ".OnPrivacy"
Cohesion: 0.29
Nodes (9): AllowActivity, AllowCapture, AllowControl, AllowMic, AllowNotes, AutoRouteBox, Proactive, ReadScreen (+1 more)

### Community 98 - "SelectionChangedEventArgs"
Cohesion: 0.28
Nodes (6): CheckerBox, ContextModelBox, ModelBox, ProviderBox, SelectionChangedEventArgs, ComboBox

### Community 99 - "McpServer"
Cohesion: 0.10
Nodes (19): HttpListenerResponse, IAsyncDisposable, JsonArray, Action, CancellationTokenSource, ConcurrentDictionary, DiagnosticLogSink, HttpListener (+11 more)

### Community 100 - ".DHash_distance_is_zero_for_identical_and_large_for_inverted"
Cohesion: 0.40
Nodes (3): ReadOnlySpan, PerceptualHash, ArgumentException

### Community 101 - ".OnSessionSelected"
Cohesion: 0.33
Nodes (5): Concepts, Mistakes, Sessions, SelectionChangedEventArgs, ListBox

### Community 102 - ".OnTab"
Cohesion: 0.53
Nodes (5): TabConcepts, TabFormulas, TabMistakes, TabSessions, RadioButton

### Community 103 - ".ExecuteAsync"
Cohesion: 0.33
Nodes (6): Plan, CancellationToken, JsonObject, Task, ComputerAct, Definition

### Community 104 - "Examples"
Cohesion: 0.67
Nodes (3): Examples, Formulas, ItemsControl

### Community 105 - "ContextHub"
Cohesion: 0.10
Nodes (16): GlobalSystemMediaTransportControlsSessionManager, OcrEngine, ScreenReader, CancellationToken, CancellationTokenSource, DateTimeOffset, DispatcherTimer, Func (+8 more)

### Community 106 - ".BuildRequest"
Cohesion: 0.16
Nodes (13): Attachment, ContextSelection, IReadOnlyList, ToolSession, LearningSummary, MasteryTutor, TimeSpan, TutorPipeline (+5 more)

### Community 107 - ".OnRate"
Cohesion: 0.50
Nodes (3): RoutedPropertyChangedEventArgs, RateSlider, Slider

### Community 108 - "DeleteSession"
Cohesion: 0.50
Nodes (4): DeleteSession, Toggle, ToVault, Button

### Community 109 - ".List"
Cohesion: 0.15
Nodes (10): NoteHit, CancellationToken, GeneratedRegex, HashSet, IReadOnlyList, Regex, LineDiff, NoteHit (+2 more)

### Community 111 - "TitleAndAddressTests"
Cohesion: 0.47
Nodes (3): InlineData, Theory, TitleAndAddressTests

### Community 112 - "MarkPainter"
Cohesion: 0.15
Nodes (16): Alpha, Brush, Colour, Dashed, DrawingVisual, Halo, Pen, Point (+8 more)

### Community 113 - "MediaState"
Cohesion: 0.20
Nodes (12): DateTimeOffset, IReadOnlyList, LinkedList, TimeSpan, ActivitySpan, Length, ActivityTimeline, Recent (+4 more)

### Community 114 - "Search"
Cohesion: 0.67
Nodes (3): Search, SessionText, TextBox

### Community 120 - "GlassSurface"
Cohesion: 0.13
Nodes (13): Mark, MouseEventArgs, Action, Guid, List, MouseButtonEventArgs, GlassSurface, MarkCount (+5 more)

### Community 121 - "Annotation"
Cohesion: 0.11
Nodes (17): DateTimeOffset, TimeSpan, Annotation, Color, Confidence, CreatedAt, Id, Kind (+9 more)

### Community 122 - ".Read"
Cohesion: 0.31
Nodes (5): Data, IReadOnlyList, Name, AttachedDocument, AttachmentReader

### Community 123 - "MemorySecrets"
Cohesion: 0.16
Nodes (11): Serving, Dictionary, MemorySecrets, Bodies, Fact, HttpListener, List, Listener (+3 more)

### Community 124 - "glow.cpp"
Cohesion: 0.18
Nodes (16): cmath, d2d1_1, dcomp, dwmapi, VN_API, vn_rect, Dither(), thread (+8 more)

### Community 125 - "Duplication"
Cohesion: 0.12
Nodes (17): ID3D11Device, ID3D11DeviceContext, ID3D11Texture2D, IDXGIOutputDuplication, ComPtr, Duplication, context, device (+9 more)

### Community 126 - "common.h"
Cohesion: 0.13
Nodes (12): algorithm, atomic, client, malloc, memory, mutex, sdkddkver, stdint (+4 more)

### Community 127 - ".StatusAsync"
Cohesion: 0.24
Nodes (9): Args, File, CancellationToken, Func, IReadOnlyList, Task, CliAccount, CliAccounts (+1 more)

### Community 128 - "SapiSpeechInput"
Cohesion: 0.17
Nodes (8): SpeechRecognitionEngine, StringBuilder, Task, ISpeechInput, IsListening, SapiSpeechInput, IsListening, TaskCompletionSource

### Community 130 - "duplication.cpp"
Cohesion: 0.21
Nodes (14): d3d11, dxgi1_2, CoveredFraction(), HRESULT, vector, VN_API, vn_rect, EnsureStaging() (+6 more)

### Community 131 - "KnowledgeStoreTests"
Cohesion: 0.21
Nodes (6): JournalEntry, Fact, InlineData, IOException, Theory, KnowledgeStoreTests

### Community 132 - ".ReviewAndRunAsync"
Cohesion: 0.25
Nodes (3): Task, Window, RoutedEventArgs

### Community 133 - "AnnotationSurfaceWindow"
Cohesion: 0.19
Nodes (7): DpiScale, EventArgs, IReadOnlyList, AnnotationSurfaceWindow, Handle, MarkCount, Monitor

### Community 134 - "Depth"
Cohesion: 0.24
Nodes (8): MARGINS, Result, Dictionary, DllImport, Window, Depth, MARGINS, Result

### Community 135 - "SapiSpeechOutput"
Cohesion: 0.20
Nodes (5): SpeechSynthesizer, ISpeechOutput, IsSpeaking, SapiSpeechOutput, IsSpeaking

### Community 136 - ".ExecuteAsync"
Cohesion: 0.20
Nodes (6): CancellationToken, Func, JsonObject, Task, ScreenTools, IdSequence

### Community 137 - "TaskKind"
Cohesion: 0.11
Nodes (17): Provider, GeneratedRegex, IReadOnlyList, Label, Regex, ModelRoute, Effort, Model (+9 more)

### Community 138 - "Glow"
Cohesion: 0.14
Nodes (13): HANDLE, Glow, argb, frames, hwnd, mode, monitor, quit (+5 more)

### Community 139 - "Edge"
Cohesion: 0.14
Nodes (14): ID2D1Bitmap1, IDCompositionSurface, IDCompositionVisual2, ComPtr, vector, Edge, bitmap, h (+6 more)

### Community 140 - ".Use"
Cohesion: 0.41
Nodes (4): DateTimeOffset, Fact, TimeSpan, ContextEngineTests

### Community 142 - "imaging.cpp"
Cohesion: 0.22
Nodes (9): combaseapi, ComScope, hr, HRESULT, VN_API, vn_free(), vn_png_encode(), vn_version() (+1 more)

### Community 143 - "TitleNormaliser"
Cohesion: 0.35
Nodes (4): 6. Review log (Agent B), GeneratedRegex, Regex, TitleNormaliser

### Community 144 - ".Ask"
Cohesion: 0.24
Nodes (8): Cancel, Dialog, Button, CancellationToken, Ok, Task, Window, ConfirmDialog

### Community 145 - ".WithSelfExcludedAsync"
Cohesion: 0.29
Nodes (4): Func, Func, Task, StaThread

### Community 146 - "FakeExtractor"
Cohesion: 0.16
Nodes (11): Processes, Profile, AppProfile, AppProfiles, InlineData, Theory, FakeExtractor, Calls (+3 more)

### Community 147 - "ContextOptions"
Cohesion: 0.14
Nodes (11): TimeSpan, ContextOptions, Idle, MaxEntitiesPerEpisode, MaxEpisodesInMemory, MaxLinesPerEpisode, MinimumDwell, Rejoin (+3 more)

### Community 148 - ".Overlay_latency_and_cost"
Cohesion: 0.13
Nodes (12): 1. Evidence gathered (reconnaissance), 2. Diagnosis of v0.3.0 (measured or evidenced), 4. Milestones (order revised after review R1), 5. Measurements, Agent harness plan (revision 4), AssistedInput, Guid, Fact (+4 more)

### Community 149 - ".OnPolicyChanged"
Cohesion: 0.50
Nodes (3): PolicyBox, SelectionChangedEventArgs, ComboBox

### Community 150 - "Rect"
Cohesion: 0.29
Nodes (6): FrameworkElement, Rect, DependencyProperty, DrawingContext, LevelScale, Level

### Community 151 - "Implementation plan, risks, acceptance"
Cohesion: 0.14
Nodes (15): Original Win32 C++ template files (unrelated leftovers), Convention: all screen geometry is physical pixels on the virtual desktop, Persistence (LearningStore SQLite, WAL, migrations, backups, FTS5), Screen geometry (CaptureGeometry, MonitorInfo, DpiMath), Stage: Present (annotations mapped image to physical pixels), Direction C: Companion cursor (Clicky model), Reference project: clicky-windows, Deliberately not taken (always-on mic, bypassPermissions, telemetry, unprompted model cascades) (+7 more)

### Community 152 - "RouteLog"
Cohesion: 0.38
Nodes (4): DateTimeOffset, JsonSerializerOptions, RouteLog, RouteRecord

### Community 153 - "WndProc"
Cohesion: 0.33
Nodes (6): HWND, LPARAM, LRESULT, UINT, WPARAM, WndProc()

### Community 154 - ".OnSelect"
Cohesion: 0.50
Nodes (3): Queue, SelectionChangedEventArgs, ListBox

### Community 160 - ".OnSpeechStop"
Cohesion: 0.40
Nodes (3): SpeechPause, SpeechStop, Button

### Community 166 - "Tools.cs"
Cohesion: 0.10
Nodes (18): CancellationToken, Task, TimeSpan, UiApprover, JsonNode, IToolApprover, Json, SchemaCheck (+10 more)

## Knowledge Gaps
- **666 isolated node(s):** `Hopefully-this-goes-somewhere`, `device`, `context`, `dup`, `latest` (+661 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1197 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **15 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TutorSession` connect `TutorSession` to `LearningStore`, `PipelineStage`, `.ReviewAndRunAsync`, `ProviderSettings`, `Vernier.Core.Agents`, `Security and privacy document`, `ContextOptions`, `Runtime pipeline (TutorPipeline.RunAsync)`, `ChatRequest`, `SessionMemory`, `AppSettings`, `.Initialize`, `AnnotationStore`, `ScreenCaptureService`, `Design system document`, `ToolTurn`, `AppServices`, `Verifier`, `CapturedFrame`, `VerificationReport`, `Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)`, `IWindowCapturePolicyService`, `TutorMode`, `.Cancel`, `TranscriptItem`, `.Sync`, `ScreenAsk`, `.BuildRequest`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **Why does `AppServices` connect `AppServices` to `SapiSpeechInput`, `LearningStore`, `ProviderSettings`, `SapiSpeechOutput`, `TrayIcon`, `FilesPage`, `Vernier.Core.Agents`, `TitleNormaliser`, `.WithSelfExcludedAsync`, `AppSettings`, `TutorSession`, `.Initialize`, `AnnotationStore`, `RouteLog`, `.Monitors`, `ToolRegistry`, `AnnotationOverlayManager`, `ConnectorException`, `IWindowCapturePolicyService`, `SettingsTests`, `.OnStartup`, `McpServer`?**
  _High betweenness centrality (0.088) - this node is a cross-community bridge._
- **Why does `ProviderSettings` connect `ProviderSettings` to `.Claude_code_cli_answers_a_connection_test`, `Fact`, `ClaudeCodeProvider`, `TaskKind`, `MemorySecrets`, `SettingsPage`, `Vernier.Core.Agents`, `CodexCliProvider`, `GeminiProvider`, `AppSettings`, `TutorSession`, `ToolRegistry`, `AnthropicProvider`, `.Sync`?**
  _High betweenness centrality (0.056) - this node is a cross-community bridge._
- **Are the 24 inferred relationships involving `PxRect` (e.g. with `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible()` and `.Frame()`) actually correct?**
  _`PxRect` has 24 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `LearningStore` (e.g. with `.Episodes_survive_a_restart_without_screen_text_and_old_ones_and_forgotten_ones_are_deleted()` and `.A_real_tutoring_turn_runs_through_the_whole_pipeline()`) actually correct?**
  _`LearningStore` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 5 inferred relationships involving `ToolTurn` (e.g. with `.SmokeCheckAsync()` and `.Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied()`) actually correct?**
  _`ToolTurn` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Hopefully-this-goes-somewhere`, `device`, `context` to the rest of the system?**
  _666 weakly-connected nodes found - possible documentation gaps or missing edges._