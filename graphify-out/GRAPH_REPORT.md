# Graph Report - Hopefully-this-goes-somewhere  (2026-10-19)

## Corpus Check
- 161 files · ~293,343 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 2, .rc 1, .vcxproj 1)

## Summary
- 4407 nodes · 10735 edges · 193 communities (180 shown, 13 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1089 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d9994696`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- LearningStore
- Vernier.Core.Settings
- UserControl
- UserControl
- ProviderSettings
- TutorStructured
- PxPoint
- Win32
- Fact
- TrayIcon
- UserControl
- UserControl
- ReviewPage
- GraphifySource
- SettingsPage
- system_windows
- UserControl
- .Swipe_underline_brace_and_arrow_stay_with_their_target
- ProviderException
- ChatRequest
- Runtime pipeline (TutorPipeline.RunAsync)
- GrayImage
- SessionMemory
- AppSettings
- TutorSession
- .Initialize
- Annotation
- AnthropicProvider
- Fact
- .The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible
- UserControl
- .LiveAsync
- .Parse
- .List
- .Assess
- Hopefully-this-goes-somewhere.cpp
- ResourceDictionary
- Native
- .A_real_mouse_drag_on_the_grip_moves_the_card_and_nothing_else
- ScopedFiles
- MainWindow
- Window
- ScreenCaptureService
- AskBar
- Design system document
- Verification report
- ExtractedText
- Vernier README (tutor that sits over your screen)
- .Read
- Vernier.Tests.csproj
- AppServices
- FilesPage
- .Review
- Fact
- .Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied
- Threat model
- ToolRegistry
- Verifier
- ScreenContextTests
- DiagnosticsPage
- CapturedFrame
- ResearchTests
- PipelineStage
- .SaveSettings
- AnnotationOverlayManager
- ConnectorException
- ITool
- TextBox
- Architecture document
- match.cpp
- WebResearcher.cs
- Parser
- Ink
- ClaudeCodeProvider
- LibraryPage
- Tools.cs
- PathGuard
- Rect
- .OnProviderFieldChanged
- ScreenIndex
- OpenAiCompatibleProvider
- .OnStartup
- .ExecuteAsync
- ProviderErrorKind
- DuplicationSession
- .Cancel
- CredentialManagerSecretStore
- Vernier.Core.Agents
- HotkeyManager
- .Resultant
- Episode
- AnnotationSurfaceWindow
- .RunAsync
- DiagnosticLog
- TutorPanel
- ContextEngine
- .Hypercube
- .OnPrivacy
- .OnProviderChanged
- McpServer
- .Clean
- Concepts
- .OnTab
- .ExecuteAsync
- .Extract
- ContextHub
- .BuildRequest
- .Split
- DeleteSession
- ObsidianVault
- Vernier.Core.Geometry
- .Focus
- .Build
- MediaState
- .OnSearch
- Hopefully-this-goes-somewhere
- Dark.xaml
- Light.xaml
- FormulasView
- GlassSurface
- DiagramSpec
- ink.cpp
- MemorySecrets
- glow.cpp
- Duplication
- common.h
- NativeInk
- SapiSpeechInput
- TranscriptItem
- duplication.cpp
- KnowledgeStoreTests
- NoteRenderer
- Rect
- Depth
- V3
- ToolResult
- TaskKind
- Glow
- Edge
- .Use
- ProposedAnnotation
- imaging.cpp
- InlineData
- .Ask
- .Clean
- FakeExtractor
- .An_answer_that_had_to_be_researched_and_was_not_is_asked_again_once
- Agent harness plan (revision 4)
- OperatorGuide.cs
- StageScale
- FakeUser32
- .Options
- ToolException
- IDisposable
- PxRect
- RoutedEventArgs
- SettingsStore
- DiagramRenderer
- .Create
- DiagramTests
- Aurora.xaml
- Glacier.xaml
- Nebula.xaml
- RoutedEventArgs
- NativeGlow
- Vernier.Tests
- .Build
- .Redact
- FollowTests
- CodeTool
- AppProfile
- InkMap
- .Check
- .Decide
- .A_drawing_is_required_by_code_and_the_right_tool_and_kind_are_chosen
- ConceptsView
- NoteDismissWindow
- Office
- ToolTurn
- SKILL.md
- OperatorGuide.md
- ContextSelection
- Examples
- ContextOptions
- WindowCapturePolicyService
- NoteContent
- .OnRate
- .Claude_code_cli_answers_a_connection_test
- WndProc
- Button
- SessionsView

## God Nodes (most connected - your core abstractions)
1. `PxRect` - 116 edges
2. `ToolTurn` - 88 edges
3. `TutorSession` - 80 edges
4. `LearningStore` - 76 edges
5. `UserControl` - 71 edges
6. `AnnotationOverlayManager` - 70 edges
7. `PxPoint` - 68 edges
8. `Win32` - 61 edges
9. `AppSettings` - 57 edges
10. `Annotation` - 56 edges

## Surprising Connections (you probably didn't know these)
- `Decisions` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `Milestones` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `Defects corrected in revision 2 (refused capture aborted question; ask bar read itself)` --references--> `AskBar`  [EXTRACTED]
  docs/VERIFICATION_REPORT.md → src/Vernier.App/Views/AskBar.cs
- `Verified: UI Automation and Windows OCR both read text; Observer prefers accessibility` --references--> `Observer`  [EXTRACTED]
  docs/VERIFICATION_REPORT.md → src/Vernier.Core/Agents/Observer.cs
- `Verified: real screen-aware turn end to end (9 J, 4 checks passed, 2 annotations mapped)` --references--> `TutorPipeline`  [EXTRACTED]
  docs/VERIFICATION_REPORT.md → src/Vernier.Core/Agents/TutorPipeline.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Observer, Mastery Tutor and Verifier cooperate in the tutoring pipeline** — docs_architecture_observer_agent, docs_architecture_mastery_tutor_agent, docs_architecture_verifier_agent, docs_architecture_runtime_pipeline, docs_architecture_vernier_structured_block [EXTRACTED 1.00]
- **Glass, capture light, ask bar and tray form the always-there shell under the yellow/blue rule** — docs_architecture_glass, docs_architecture_capture_light, docs_architecture_ask_bar, docs_architecture_tray, docs_design_system_the_one_rule [INFERRED 0.85]
- **Consent gates, untrusted fencing, action policy and path guard implement the off-by-default security posture** — docs_security_and_privacy_consent_gates, docs_security_and_privacy_threat_prompt_injection, docs_security_and_privacy_threat_harmful_input_actions, docs_security_and_privacy_threat_path_traversal, claude_security_rules [INFERRED 0.85]

## Communities (193 total, 13 thin omitted)

### Community 0 - "LearningStore"
Cohesion: 0.05
Nodes (41): Cards, ExportCard, ExportConcept, ExportDocument, microsoft_data_sqlite, SqliteConnection, SqliteDataReader, SqliteTransaction (+33 more)

### Community 1 - "Vernier.Core.Settings"
Cohesion: 0.15
Nodes (12): anthropic, anthropic_exceptions, anthropic_models_messages, Vernier.App.Context, Vernier.Core.Settings, Vernier.Core.Security, Vernier.Core.Providers, Vernier.Core.Learning (+4 more)

### Community 2 - "UserControl"
Cohesion: 0.12
Nodes (23): Kind, Reason, Back, BackHost, CardMeta, CardView, Front, Grades (+15 more)

### Community 3 - "UserControl"
Cohesion: 0.12
Nodes (21): Available, Excluded, Expected, Missing, Observed, Path, Environment, Integrations (+13 more)

### Community 4 - "ProviderSettings"
Cohesion: 0.10
Nodes (24): ChatMessage, ImageInput, ProviderSettings, ApiKeyName, BaseUrl, DisplayName, Effort, Id (+16 more)

### Community 5 - "TutorStructured"
Cohesion: 0.07
Nodes (29): List, Claim, Expr, UnitsLhs, UnitsRhs, Value, FormulaNote, Derivation (+21 more)

### Community 6 - "PxPoint"
Cohesion: 0.11
Nodes (12): ReadOnlySpan, IReadOnlyList, DpiMath, MonitorInfo, OverlayPlacement, PerceptualHash, PolylineMath, PxPoint (+4 more)

### Community 7 - "Win32"
Cohesion: 0.06
Nodes (18): 6. Review log (Agent B), KEYBDINPUT, LASTINPUTINFO, MonitorEnumProc, MOUSEINPUT, EventArgs, POINT, DllImport (+10 more)

### Community 8 - "Fact"
Cohesion: 0.12
Nodes (22): Action, PipelineCallbacks, Stage, Text, Tool, ScriptedProvider, Capabilities, Delay (+14 more)

### Community 9 - "TrayIcon"
Cohesion: 0.06
Nodes (33): Dismissals, NOTIFYICONDATA, Action, DateTimeOffset, TimeSpan, Proactive, DllImport, Func (+25 more)

### Community 10 - "UserControl"
Cohesion: 0.10
Nodes (28): Degree, Label, Links, AskAboutNote, DisconnectVault, EditInstruction, Explain, FileText (+20 more)

### Community 11 - "UserControl"
Cohesion: 0.09
Nodes (26): Checks, IsUser, Meta, Notices, ActionsBar, ActionsText, AskKey, Bar (+18 more)

### Community 12 - "ReviewPage"
Cohesion: 0.17
Nodes (10): Queue, ResolveButton, Reveal, KeyEventArgs, RoutedEventArgs, SelectionChangedEventArgs, ReviewPage, Button (+2 more)

### Community 13 - "GraphifySource"
Cohesion: 0.16
Nodes (13): Output, JsonElement, Ok, Task, GraphEdge, GraphifySource, Name, GraphNode (+5 more)

### Community 14 - "SettingsPage"
Cohesion: 0.14
Nodes (10): DateTimeOffset, Func, RoutedEventArgs, Task, AccountRow, ActionName, UseName, SettingsPage (+2 more)

### Community 15 - "system_windows"
Cohesion: 0.14
Nodes (22): Vernier.App.Views, Vernier.Core.Knowledge, microsoft_win32, system_io, system_runtime_interopservices_windowsruntime, system_windows, system_windows_automation, system_windows_automation_peers (+14 more)

### Community 16 - "UserControl"
Cohesion: 0.08
Nodes (32): ActionLabel, ActionName, CanAct, Id, SignedIn, State, UseName, Accounts (+24 more)

### Community 17 - ".Swipe_underline_brace_and_arrow_stay_with_their_target"
Cohesion: 0.19
Nodes (9): Geometry, IReadOnlyList, Point, MarkShapes, StreamGeometry, Fact, InlineData, Theory (+1 more)

### Community 18 - "ProviderException"
Cohesion: 0.16
Nodes (15): ProviderException, IsTransient, Kind, RetryAfter, CancellationToken, HttpMethod, HttpRequestMessage, HttpResponseMessage (+7 more)

### Community 19 - "ChatRequest"
Cohesion: 0.08
Nodes (28): ChatRunner (timeout, bounded retry, vision gate), Stage: Teach (IChatProvider.StreamAsync via ChatRunner), Message, Raw, LearningSummary, CancellationToken, CaptureGeometry, Task (+20 more)

### Community 20 - "Runtime pipeline (TutorPipeline.RunAsync)"
Cohesion: 0.11
Nodes (25): Step 4: Check your own work (second route, units, signs, limiting cases), Do-not rules (no solution dumps, no vague praise, no invented file contents, no unopened citations), Hint escalation ladder (point, leading question, principle, show the step), Step 5: Finish with a mastery check (transfer question, mental model, what is still open), One right answer is not mastery, Mastery Tutor skill (/mastery-tutor), Misconception matters more than the slip, Subject notes (mathematics, physics/engineering, programming, sciences) (+17 more)

### Community 21 - "GrayImage"
Cohesion: 0.11
Nodes (20): PatchStats, NativePatchMatcher, Best, Guid, IReadOnlyCollection, IReadOnlyDictionary, IReadOnlyList, IReadOnlySet (+12 more)

### Community 22 - "SessionMemory"
Cohesion: 0.17
Nodes (11): DateTimeOffset, GeneratedRegex, IReadOnlyList, List, Regex, SessionMemory, Count, TurnNote (+3 more)

### Community 23 - "AppSettings"
Cohesion: 0.05
Nodes (41): ModelRoute, List, AppSettings, AcknowledgedObserveCost, ActiveProvider, ActiveProviderId, AllowActivityContext, AllowComputerControl (+33 more)

### Community 24 - "TutorSession"
Cohesion: 0.06
Nodes (33): Expiry, Grant, ObservableCollection, ProviderKey, Action, CancellationTokenSource, IDisposable, IEnumerable (+25 more)

### Community 25 - ".Initialize"
Cohesion: 0.14
Nodes (19): ScreenAsk, HasInk, RawImage, CaptureGeometry, ImageBounds, QuantizationUncertaintyPx, ScaleX, ScaleY (+11 more)

### Community 26 - "Annotation"
Cohesion: 0.07
Nodes (32): DateTimeOffset, DiagramSpec, Func, Guid, IReadOnlyDictionary, IReadOnlyList, List, TimeSpan (+24 more)

### Community 27 - "AnthropicProvider"
Cohesion: 0.06
Nodes (35): IAnthropicClient, MessageParam, CancellationToken, Dictionary, Exception, IAsyncEnumerable, IReadOnlyList, JsonElement (+27 more)

### Community 28 - "Fact"
Cohesion: 0.14
Nodes (7): JsonSerializerOptions, Regex, TutorOutput, Fact, SettingsTests, TutorOutputTests, VerifierTests

### Community 29 - ".The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible"
Cohesion: 0.08
Nodes (21): BaseUrl, ButtonBase, ComboBoxItem, Control, DependencyObject, Bodies, Button, Fact (+13 more)

### Community 30 - "UserControl"
Cohesion: 0.11
Nodes (21): Concept, Derivation, Description, Expression, Mastery.Level, Mastery.Reason, Meaning, Problem (+13 more)

### Community 31 - ".LiveAsync"
Cohesion: 0.22
Nodes (10): Sheet, Answer, Fact, IDisposable, ITestOutputHelper, Task, Turn, Cleanup (+2 more)

### Community 32 - ".Parse"
Cohesion: 0.05
Nodes (40): InlineUIContainer, Inner, Next, RichTextBox, Run, DependencyProperty, FormulaControl, Paragraph (+32 more)

### Community 33 - ".List"
Cohesion: 0.07
Nodes (24): MONITORINFO, RECT, Color, R, CaptureExclusionSelfTest, IReadOnlyList, MONITORINFO, Fact (+16 more)

### Community 34 - ".Assess"
Cohesion: 0.12
Nodes (17): ConceptState, DateTimeOffset, IReadOnlyList, CardSchedule, Evidence, EvidenceKinds, MasteryAssessment, MasteryLevel (+9 more)

### Community 35 - "Hopefully-this-goes-somewhere.cpp"
Cohesion: 0.15
Nodes (16): ATOM, BOOL, HINSTANCE, About(), HWND, LPARAM, LRESULT, UINT (+8 more)

### Community 36 - "ResourceDictionary"
Cohesion: 0.10
Nodes (21): IsDropDownOpen, B, Box, C, G, PART_ContentHost, PART_Track, ResourceDictionary (+13 more)

### Community 37 - "Native"
Cohesion: 0.12
Nodes (11): LibraryImport, MatchResult, Png, Height, Lazy, Width, MatchResult, Native (+3 more)

### Community 38 - ".A_real_mouse_drag_on_the_grip_moves_the_card_and_nothing_else"
Cohesion: 0.12
Nodes (15): 2. Diagnosis of v0.3.0 (measured or evidenced), INPUTUNION, CancellationToken, INPUT, X, Y, SendInputDriver, INPUT (+7 more)

### Community 39 - "ScopedFiles"
Cohesion: 0.12
Nodes (16): JournalEntry, Role, ConcurrentDictionary, DateTimeOffset, IEnumerable, List, Text, FileChange (+8 more)

### Community 40 - "MainWindow"
Cohesion: 0.16
Nodes (9): Dictionary, FrameworkElement, RadioButton, SelectionChangedEventArgs, MainWindow, AskHotkey, Bar, Hotkeys (+1 more)

### Community 41 - "Window"
Cohesion: 0.12
Nodes (19): GoalText, Lamp, NoticeBar, NoticeText, ObserveText, PageHost, PolicyBox, PolicyText (+11 more)

### Community 42 - "ScreenCaptureService"
Cohesion: 0.13
Nodes (11): Int32Rect, IReadOnlyList, Func, Task, BitmapSource, ScreenCaptureService, CancellationToken, Task (+3 more)

### Community 43 - "AskBar"
Cohesion: 0.07
Nodes (20): Border, DockPanel, Action, Item, Button, FontFamily, KeyEventArgs, List (+12 more)

### Community 44 - "Design system document"
Cohesion: 0.07
Nodes (38): Convention: Core never references WPF or P/Invoke, Original Win32 C++ template files (unrelated leftovers), graphify-out/ project knowledge graph, Convention: no abstraction without a second implementation or a test seam, Convention: all screen geometry is physical pixels on the virtual desktop, Product shape (tray-resident, Ask and Pen only, yellow/blue rule, capture light on while looking), Rule: always launch tests/screenshots with --data <temp dir>, Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF) (+30 more)

### Community 45 - "Verification report"
Cohesion: 0.12
Nodes (20): Verification workflow (Evaluate, Correct, Confirm), Acceptance checklist (definition of done), Manual: assisted input on a harmless target (Notepad), Manual: capture exclusion with your conferencing tool, Manual verification checklist, Items marked not verified by the author (multi-monitor, scaling, Codex, real mic, pro apps), Manual: Voice (hold to talk, speak answers), AppUiTests (real MainWindow in-process against a loopback stub) (+12 more)

### Community 46 - "ExtractedText"
Cohesion: 0.14
Nodes (17): CancellationToken, IReadOnlyList, Task, TimeSpan, OcrTextExtractor, Provenance, UiaElementExtractor, Provenance (+9 more)

### Community 47 - "Vernier README (tutor that sits over your screen)"
Cohesion: 0.07
Nodes (34): Security rules (non-negotiable), Accounts via vendor CLIs (CliAccounts), ClaudeCodeProvider (claude -p stream-json subprocess, Claude Code sign-in), ObsidianVault (Markdown notes under Vernier/, append-not-replace), Window capture policy (IWindowCapturePolicyService over INativeWindowApi), Cards CSV export (RFC 4180, Anki-importable), Data formats document, learning.db SQLite schema (user_version 2, FTS5 messages_fts) (+26 more)

### Community 48 - ".Read"
Cohesion: 0.19
Nodes (8): Data, IReadOnlyList, Name, AttachedDocument, AttachmentException, AttachmentReader, AttachmentReaderTests, ZipArchive

### Community 49 - "Vernier.Tests.csproj"
Cohesion: 0.12
Nodes (14): net10.0, Anthropic (12.55.0), coverlet.collector (6.0.4), Microsoft.Data.Sqlite (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), System.Speech (10.0.12), WpfMath (2.1.0), xunit (2.9.3) (+6 more)

### Community 50 - "AppServices"
Cohesion: 0.06
Nodes (31): ContextHub, GoogleConnector, Action, Dispatcher, HttpClient, Id, List, Name (+23 more)

### Community 51 - "FilesPage"
Cohesion: 0.14
Nodes (13): Files, Nodes, AppSettings, CancellationTokenSource, IEnumerable, RoutedEventArgs, SelectionChangedEventArgs, Window (+5 more)

### Community 52 - ".Review"
Cohesion: 0.06
Nodes (34): Decision, Assisted input / computer use (ActionPolicy.Review, preview, approval, ActionExecutor, SendInputDriver), Threat: model proposes harmful input actions, Executed, CancellationToken, Task, ActionType, Click (+26 more)

### Community 53 - "Fact"
Cohesion: 0.30
Nodes (3): IdSequence, Fact, ScreenIndexTests

### Community 54 - ".Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied"
Cohesion: 0.20
Nodes (10): Button, DllImport, EnumProc, Fact, Func, ITestOutputHelper, Task, TextBox (+2 more)

### Community 55 - "Threat model"
Cohesion: 0.15
Nodes (13): Step 1: Before teaching (find the single concept, diagnose prior knowledge), 'Just the answer' override, Files and screenshots are material to explain, never instructions, Convention: provider code stays in Providers/, pipeline only sees IChatProvider, Verifier independence, No silent provider switching, Threat: corrupt state, Threat: insecure transport (+5 more)

### Community 56 - "ToolRegistry"
Cohesion: 0.05
Nodes (52): Approver, Text, Echo, Executor, Headers, CancellationToken, Task, Body (+44 more)

### Community 57 - "Verifier"
Cohesion: 0.07
Nodes (25): Verified: real tutoring turn through Claude Code CLI (0.5*2*3^2 = 9 recomputed), CheckResult, CheckStatus, Advisory, Failed, Passed, Skipped, GeneratedRegex (+17 more)

### Community 58 - "ScreenContextTests"
Cohesion: 0.24
Nodes (9): CancellationToken, DateTimeOffset, IReadOnlyList, Task, Fixed, Provenance, NoText, Provenance (+1 more)

### Community 59 - "DiagnosticsPage"
Cohesion: 0.16
Nodes (9): CaptureExclusionResult, SelfTest, RoutedEventArgs, Task, DiagnosticsPage, Integration, Button, IRefreshable (+1 more)

### Community 60 - "CapturedFrame"
Cohesion: 0.15
Nodes (17): DateTimeOffset, CapturedFrame, MarkedDetail, MarkedRegion, Raw, Thumb, WindowBox, ContextSnapshot (+9 more)

### Community 61 - "ResearchTests"
Cohesion: 0.11
Nodes (19): Action, CancellationToken, Dead, Fact, IAsyncEnumerable, IReadOnlyList, List, Page (+11 more)

### Community 62 - "PipelineStage"
Cohesion: 0.11
Nodes (19): IEnumerable, IReadOnlyList, TimeSpan, TokenUsage, PipelineStage, Confirm, Correct, Interpret (+11 more)

### Community 63 - ".SaveSettings"
Cohesion: 0.21
Nodes (3): GoogleConnectButton, RemoveSample, Button

### Community 64 - "AnnotationOverlayManager"
Cohesion: 0.05
Nodes (35): DateTime, Dock, Action, Color, Dictionary, DispatcherTimer, EventArgs, Func (+27 more)

### Community 65 - "ConnectorException"
Cohesion: 0.05
Nodes (48): HttpMessageHandler, Pkce, Redirect, CancellationToken, HttpClient, HttpListener, HttpListenerContext, Listener (+40 more)

### Community 66 - "ITool"
Cohesion: 0.06
Nodes (49): 3. Architecture decisions (ADR log), HttpClient, IEnumerable, ContextTools, GetContext, Definition, MediaControl, Definition (+41 more)

### Community 67 - "TextBox"
Cohesion: 0.21
Nodes (11): AssessText, EdgeText, ExcludedText, GoalsText, GoogleClientIdBox, IntervalsText, LevelText, PaceText (+3 more)

### Community 68 - "Architecture document"
Cohesion: 0.14
Nodes (20): AnnotationOverlayManager (owns glass and strips, collects ink, InkSettled after 1.3 s), AnnotationStore (immutable list, undo/redo, Rebase, DropOutside), Architecture document, Ask bar (the only interactive overlay), Capture light (EdgeStripWindow, four edge strips per monitor), Glass (AnnotationSurfaceWindow, one transparent click-through window per monitor), Pen capture (ink painted into the image, MarkedRegion, MarkedDetail crop), Stack decision: C# on .NET 10 with WPF, no C++ layer (+12 more)

### Community 69 - "match.cpp"
Cohesion: 0.16
Nodes (16): cmath, vector, VN_API, vn_rect, Gray, h, px, w (+8 more)

### Community 70 - "WebResearcher.cs"
Cohesion: 0.14
Nodes (21): Excerpt, CancellationToken, Dead, HttpClient, HttpStatusCode, IReadOnlyList, Page, Status (+13 more)

### Community 71 - "Parser"
Cohesion: 0.28
Nodes (5): Expr, Parser, AtEnd, Peek, Position

### Community 72 - "Ink"
Cohesion: 0.08
Nodes (23): atomic, HANDLE, mutex, thread, vector, Ink, argb, context (+15 more)

### Community 73 - "ClaudeCodeProvider"
Cohesion: 0.08
Nodes (23): ToolSession, Definitions, McpConfig, McpServerName, Turn, Func, IAsyncEnumerable, IEnumerable (+15 more)

### Community 74 - "LibraryPage"
Cohesion: 0.26
Nodes (5): SessionRow, RoutedEventArgs, SelectionChangedEventArgs, LibraryPage, SessionRow

### Community 75 - "Tools.cs"
Cohesion: 0.16
Nodes (10): Vernier.Core.Tools, Vernier.Core.Connectors, Vernier.App.Connectors, Vernier.Core.Research, system_collections_concurrent, system_net, system_net_http, system_net_http_headers (+2 more)

### Community 76 - "PathGuard"
Cohesion: 0.24
Nodes (7): ScopedFiles / PathGuard (confined file access, Propose then Apply), Threat: path traversal / unintended file writes, CancellationToken, IEnumerable, IReadOnlyCollection, PathGuard, Root

### Community 78 - ".OnProviderFieldChanged"
Cohesion: 0.29
Nodes (4): EndpointText, ModelText, TestButton, VisionCheck

### Community 79 - "ScreenIndex"
Cohesion: 0.09
Nodes (23): Item, Kind, Score, Id, Mark, DateTimeOffset, Dictionary, IReadOnlyList (+15 more)

### Community 80 - "OpenAiCompatibleProvider"
Cohesion: 0.09
Nodes (26): AnthropicProvider (official SDK, Messages streaming), CodexCliProvider (codex exec subprocess, ChatGPT sign-in), GeminiProvider (streamGenerateContent SSE), IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities), OpenAiCompatibleProvider (OpenAI, Moonshot/Kimi, Ollama, LM Studio, llama.cpp), ChatGPT-account provider (codex exec) never run for real, ProviderCapabilities, HttpClient (+18 more)

### Community 81 - ".OnStartup"
Cohesion: 0.17
Nodes (5): ExitEventArgs, Application, App, Task, StartupEventArgs

### Community 82 - ".ExecuteAsync"
Cohesion: 0.25
Nodes (10): CancellationToken, Func, IEnumerable, JsonObject, Task, Append, Definition, GoogleTools (+2 more)

### Community 83 - "ProviderErrorKind"
Cohesion: 0.20
Nodes (10): ProviderErrorKind, Auth, BadRequest, Network, NotConfigured, RateLimited, Refused, Server (+2 more)

### Community 84 - "DuplicationSession"
Cohesion: 0.27
Nodes (4): FrameInfo, DuplicationSession, HasFrame, Monitor

### Community 86 - "CredentialManagerSecretStore"
Cohesion: 0.42
Nodes (3): CREDENTIAL, CREDENTIAL, CredentialManagerSecretStore

### Community 87 - "Vernier.Core.Agents"
Cohesion: 0.12
Nodes (13): Vernier.Core.Agents, Vernier.Core.Verification, IInputDriver, ActionExecutor, ProviderKinds, system_globalization, system_text, system_text_json (+5 more)

### Community 88 - "HotkeyManager"
Cohesion: 0.20
Nodes (7): Action, Dictionary, HwndSource, List, HotkeyManager, Unavailable, EventArgs

### Community 89 - ".Resultant"
Cohesion: 0.22
Nodes (8): Across, Along, Degrees, Direction, Fx, Fy, IEnumerable, Size

### Community 90 - "Episode"
Cohesion: 0.06
Nodes (30): From, SemaphoreSlim, Task, EpisodeSummariser, To, Episode, ActiveSeconds, App (+22 more)

### Community 91 - "AnnotationSurfaceWindow"
Cohesion: 0.10
Nodes (16): DpiScale, EventArgs, IReadOnlyList, AnnotationSurfaceWindow, Handle, Ink, MarkCount, Monitor (+8 more)

### Community 92 - ".RunAsync"
Cohesion: 0.11
Nodes (19): Args, ExitCode, File, Process, CancellationToken, IReadOnlyDictionary, IReadOnlyList, Task (+11 more)

### Community 93 - "DiagnosticLog"
Cohesion: 0.33
Nodes (4): Exception, IReadOnlyList, LinkedList, DiagnosticLog

### Community 94 - "TutorPanel"
Cohesion: 0.22
Nodes (7): Talk, KeyEventArgs, MouseButtonEventArgs, TranscriptItem, TutorSession, TutorPanel, Session

### Community 95 - "ContextEngine"
Cohesion: 0.09
Nodes (20): Key, SessionItem, Since, App, DateTimeOffset, HashSet, IReadOnlyList, LearningStore (+12 more)

### Community 96 - ".Hypercube"
Cohesion: 0.22
Nodes (7): Corners, Depth, Edges, A, X, Y, Projection

### Community 97 - ".OnPrivacy"
Cohesion: 0.27
Nodes (10): AllowActivity, AllowCapture, AllowControl, AllowMic, AllowNotes, AllowResearch, AutoRouteBox, Proactive (+2 more)

### Community 98 - ".OnProviderChanged"
Cohesion: 0.27
Nodes (6): CheckerBox, ContextModelBox, ModelBox, ProviderBox, SelectionChangedEventArgs, ComboBox

### Community 99 - "McpServer"
Cohesion: 0.10
Nodes (19): HttpListenerResponse, IAsyncDisposable, JsonArray, Action, CancellationTokenSource, ConcurrentDictionary, DiagnosticLogSink, HttpListener (+11 more)

### Community 100 - ".Clean"
Cohesion: 0.35
Nodes (5): GeneratedRegex, HashSet, Regex, QuerySanitizer, ResearchPolicy

### Community 101 - "Concepts"
Cohesion: 0.50
Nodes (4): Concepts, Mistakes, Sessions, ListBox

### Community 102 - ".OnTab"
Cohesion: 0.53
Nodes (5): TabConcepts, TabFormulas, TabMistakes, TabSessions, RadioButton

### Community 103 - ".ExecuteAsync"
Cohesion: 0.11
Nodes (19): Plan, CancellationToken, DllImport, Func, Guid, HashSet, IEnumerable, IReadOnlyList (+11 more)

### Community 104 - ".Extract"
Cohesion: 0.38
Nodes (5): GeneratedRegex, Regex, Text, Title, Readable

### Community 105 - "ContextHub"
Cohesion: 0.11
Nodes (16): GlobalSystemMediaTransportControlsSessionManager, OcrEngine, ScreenReader, CancellationToken, CancellationTokenSource, DateTimeOffset, DispatcherTimer, Func (+8 more)

### Community 106 - ".BuildRequest"
Cohesion: 0.10
Nodes (23): Attachment, TutorMode, Annotate, Ask, Assist, Review, Tutor, ContextSelection (+15 more)

### Community 107 - ".Split"
Cohesion: 0.29
Nodes (5): FileChangeKind, Append, Create, Modify, LineDiff

### Community 108 - "DeleteSession"
Cohesion: 0.50
Nodes (4): DeleteSession, Toggle, ToVault, Button

### Community 109 - "ObsidianVault"
Cohesion: 0.21
Nodes (9): NoteHit, CancellationToken, GeneratedRegex, HashSet, IReadOnlyList, Regex, NoteHit, NoteRef (+1 more)

### Community 110 - "Vernier.Core.Geometry"
Cohesion: 0.15
Nodes (16): Vernier.App.Agent, Vernier.Core.Windowing, Vernier.App.Platform, Vernier.App, Vernier.Core.Geometry, Vernier.Core.Annotations, system_collections_objectmodel, system_diagnostics (+8 more)

### Community 111 - ".Focus"
Cohesion: 0.17
Nodes (8): Dictionary, GeneratedRegex, Regex, TitleNormaliser, UrlSanitiser, InlineData, Theory, TitleAndAddressTests

### Community 112 - ".Build"
Cohesion: 0.14
Nodes (17): Alpha, Brush, Colour, Dashed, Halo, IEasingFunction, Color, Dictionary (+9 more)

### Community 113 - "MediaState"
Cohesion: 0.20
Nodes (12): DateTimeOffset, IReadOnlyList, LinkedList, TimeSpan, ActivitySpan, Length, ActivityTimeline, Recent (+4 more)

### Community 114 - ".OnSearch"
Cohesion: 0.40
Nodes (4): Search, SessionText, TextBox, TextChangedEventArgs

### Community 120 - "GlassSurface"
Cohesion: 0.12
Nodes (15): DrawingVisual, MouseEventArgs, Action, Guid, List, Mark, MouseButtonEventArgs, GlassSurface (+7 more)

### Community 121 - "DiagramSpec"
Cohesion: 0.11
Nodes (16): HashSet, IReadOnlyList, DiagramBuilder, DiagramItem, DiagramKind, Complex, Forces, Hypercube (+8 more)

### Community 122 - "ink.cpp"
Cohesion: 0.16
Nodes (19): VN_API, vn_rect, callback, thread, Now(), Run(), SegDist(), vn_ink_clear() (+11 more)

### Community 123 - "MemorySecrets"
Cohesion: 0.16
Nodes (11): Serving, Dictionary, MemorySecrets, Bodies, Fact, HttpListener, List, Listener (+3 more)

### Community 124 - "glow.cpp"
Cohesion: 0.19
Nodes (15): d2d1_1, dcomp, dwmapi, VN_API, vn_rect, Dither(), thread, Intensity() (+7 more)

### Community 125 - "Duplication"
Cohesion: 0.11
Nodes (18): ID3D11Device, ID3D11DeviceContext, ID3D11Texture2D, IDXGIOutputDuplication, ComPtr, mutex, Duplication, context (+10 more)

### Community 126 - "common.h"
Cohesion: 0.12
Nodes (13): algorithm, atomic, client, malloc, memory, mutex, sdkddkver, stdint (+5 more)

### Community 127 - "NativeInk"
Cohesion: 0.13
Nodes (6): IReadOnlyList, NativeInk, Hwnd, LastError, Segments, StrokeCallback

### Community 128 - "SapiSpeechInput"
Cohesion: 0.17
Nodes (8): SpeechRecognitionEngine, StringBuilder, Task, ISpeechInput, IsListening, SapiSpeechInput, IsListening, TaskCompletionSource

### Community 129 - "TranscriptItem"
Cohesion: 0.22
Nodes (8): INotifyPropertyChanged, TranscriptItem, Checks, IsUser, Meta, Notices, Role, Text

### Community 130 - "duplication.cpp"
Cohesion: 0.21
Nodes (14): d3d11, dxgi1_2, CoveredFraction(), HRESULT, vector, VN_API, vn_rect, EnsureStaging() (+6 more)

### Community 131 - "KnowledgeStoreTests"
Cohesion: 0.17
Nodes (8): Dir, JournalEntry, Fact, InlineData, IOException, Theory, KnowledgeStoreTests, Vault

### Community 132 - "NoteRenderer"
Cohesion: 0.20
Nodes (13): Card, BitmapSource, Color, Dictionary, FontFamily, FormulaControl, Guid, Scale (+5 more)

### Community 133 - "Rect"
Cohesion: 0.30
Nodes (10): Color, DrawingContext, Pen, Point, SolidColorBrush, Typeface, Vector, Palette (+2 more)

### Community 134 - "Depth"
Cohesion: 0.24
Nodes (8): MARGINS, Result, Dictionary, DllImport, Window, Depth, MARGINS, Result

### Community 135 - "V3"
Cohesion: 0.23
Nodes (8): Distance, Foot, OnFirst, OnSecond, Skew, Geometry3D, V3, Length

### Community 136 - "ToolResult"
Cohesion: 0.08
Nodes (31): Place, CancellationToken, JsonObject, Task, CancellationToken, Func, JsonObject, Scale (+23 more)

### Community 137 - "TaskKind"
Cohesion: 0.13
Nodes (14): GeneratedRegex, Regex, ModelRoute, Effort, Model, ProviderId, Task, ModelRouter (+6 more)

### Community 138 - "Glow"
Cohesion: 0.09
Nodes (21): atomic, HANDLE, HWND, LPARAM, LRESULT, thread, UINT, WPARAM (+13 more)

### Community 139 - "Edge"
Cohesion: 0.14
Nodes (14): ID2D1Bitmap1, IDCompositionSurface, IDCompositionVisual2, ComPtr, vector, Edge, bitmap, h (+6 more)

### Community 140 - ".Use"
Cohesion: 0.41
Nodes (4): DateTimeOffset, Fact, TimeSpan, ContextEngineTests

### Community 141 - "ProposedAnnotation"
Cohesion: 0.17
Nodes (12): ProposedAnnotation, Confidence, H, Kind, Label, Semantic, Target, W (+4 more)

### Community 142 - "imaging.cpp"
Cohesion: 0.22
Nodes (9): combaseapi, ComScope, hr, HRESULT, VN_API, vn_free(), vn_png_encode(), vn_version() (+1 more)

### Community 143 - "InlineData"
Cohesion: 0.19
Nodes (5): Dictionary, Units, InlineData, Theory, ExpressionTests

### Community 144 - ".Ask"
Cohesion: 0.24
Nodes (8): Cancel, Dialog, Button, CancellationToken, Ok, Task, Window, ConfirmDialog

### Community 145 - ".Clean"
Cohesion: 0.35
Nodes (4): Dictionary, GeneratedRegex, Regex, TexPlain

### Community 146 - "FakeExtractor"
Cohesion: 0.28
Nodes (6): IReadOnlyList, FakeExtractor, Calls, Provenance, Throw, ObserverTests

### Community 147 - ".An_answer_that_had_to_be_researched_and_was_not_is_asked_again_once"
Cohesion: 0.13
Nodes (11): IPAddress, ConcurrentDictionary, IReadOnlyCollection, ResearchDemand, ResearchGate, ResearchLedger, Calls, Urls (+3 more)

### Community 148 - "Agent harness plan (revision 4)"
Cohesion: 0.22
Nodes (8): 1. Evidence gathered (reconnaissance), 4. Milestones (order revised after review R1), 4b. Revision 5 plan (owner request of 2026-10-16), 5. Measurements, Agent harness plan (revision 4), Decisions, Milestones, Reflection: what the owner asked for, across both sessions

### Community 149 - "OperatorGuide.cs"
Cohesion: 0.40
Nodes (4): Lazy, OperatorGuide, Text, system_reflection

### Community 150 - "StageScale"
Cohesion: 0.14
Nodes (13): FrameworkElement, DependencyProperty, DrawingContext, LevelScale, Level, DrawingContext, Label, Typeface (+5 more)

### Community 151 - "FakeUser32"
Cohesion: 0.12
Nodes (11): Dictionary, HashSet, FakeUser32, Affinity, Children, CurrentProcessId, ForceEffective, Foreign (+3 more)

### Community 152 - ".Options"
Cohesion: 0.22
Nodes (7): Provider, DateTimeOffset, IReadOnlyList, JsonSerializerOptions, Label, RouteLog, RouteRecord

### Community 153 - "ToolException"
Cohesion: 0.26
Nodes (8): CancellationToken, IReadOnlyList, JsonObject, Task, NoteTools, WriteNote, Definition, ToolException

### Community 154 - "IDisposable"
Cohesion: 0.18
Nodes (6): IDisposable, SpeechSynthesizer, ISpeechOutput, IsSpeaking, SapiSpeechOutput, IsSpeaking

### Community 155 - "PxRect"
Cohesion: 0.06
Nodes (35): Bgra, H, Placement, CaptureTarget, None, Region, Screen, Window (+27 more)

### Community 156 - "RoutedEventArgs"
Cohesion: 0.18
Nodes (5): SpeakToggle, SpeechStop, RadioButton, RoutedEventArgs, ToggleButton

### Community 157 - "SettingsStore"
Cohesion: 0.29
Nodes (4): JsonSerializerOptions, SettingsStore, LoadWarning, Path

### Community 158 - "DiagramRenderer"
Cohesion: 0.50
Nodes (5): Card, Dictionary, Guid, Scale, DiagramRenderer

### Community 159 - ".Create"
Cohesion: 0.17
Nodes (7): Api, FakeUser32, InvalidDataException, Service, InlineData, Theory, WindowCapturePolicyLogicTests

### Community 160 - "DiagramTests"
Cohesion: 0.17
Nodes (6): Axes, Ink, Fact, Task, DiagramTests, WarmTurnTests

### Community 165 - "RoutedEventArgs"
Cohesion: 0.29
Nodes (3): PauseButton, RoutedEventArgs, Button

### Community 166 - "NativeGlow"
Cohesion: 0.29
Nodes (3): NativeGlow, Frames, Hwnd

### Community 167 - "Vernier.Tests"
Cohesion: 0.21
Nodes (4): Vernier.Core.Context, Vernier.Tests, Vernier.Core.Diagrams, xunit

### Community 168 - ".Build"
Cohesion: 0.43
Nodes (4): JsonObject, ResearchDemand, TurnPlan, VisualDemand

### Community 169 - ".Redact"
Cohesion: 0.16
Nodes (8): Exception, GeneratedRegex, Regex, PathGuardException, SecretRedactor, Untrusted, OperationCanceledException, SecurityTests

### Community 170 - "FollowTests"
Cohesion: 0.29
Nodes (5): BitmapSource, Func, ITestOutputHelper, Task, FollowTests

### Community 171 - "CodeTool"
Cohesion: 0.29
Nodes (6): CancellationToken, JsonObject, CodeTool, Definition, Runs, LiveMcpTests

### Community 172 - "AppProfile"
Cohesion: 0.40
Nodes (4): Processes, Profile, AppProfile, AppProfiles

### Community 173 - "InkMap"
Cohesion: 0.14
Nodes (12): End, LostAt, Points, At, Func, List, GraphFrame, InkMap (+4 more)

### Community 174 - ".Check"
Cohesion: 0.50
Nodes (3): IReadOnlyList, IReadOnlySet, DrawingGate

### Community 175 - ".Decide"
Cohesion: 0.45
Nodes (3): GeneratedRegex, Regex, DimensionPolicy

### Community 177 - "ConceptsView"
Cohesion: 0.67
Nodes (3): ConceptsView, MistakesView, DockPanel

### Community 178 - "NoteDismissWindow"
Cohesion: 0.33
Nodes (5): ControlTemplate, Guid, NoteDismissWindow, Handle, NoteId

### Community 179 - "Office"
Cohesion: 0.07
Nodes (31): Cell, CellTarget, App, Dictionary, DllImport, dynamic, EnumProc, Func (+23 more)

### Community 180 - "ToolTurn"
Cohesion: 0.08
Nodes (29): ConcurrentQueue, TimeSpan, UiApprover, CancellationToken, CancellationTokenSource, ConcurrentDictionary, DateTimeOffset, IReadOnlyList (+21 more)

### Community 181 - "SKILL.md"
Cohesion: 0.13
Nodes (14): Context and memory, Draw with tools, never with code, Follow the plan in the question, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain (+6 more)

### Community 182 - "OperatorGuide.md"
Cohesion: 0.13
Nodes (14): Context and memory, Draw with tools, never with code, Follow the plan in the question, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain (+6 more)

### Community 183 - "ContextSelection"
Cohesion: 0.31
Nodes (3): TimeSpan, ContextSelection, IsEmpty

### Community 184 - "Examples"
Cohesion: 0.67
Nodes (3): Examples, Formulas, ItemsControl

### Community 185 - "ContextOptions"
Cohesion: 0.25
Nodes (8): ContextOptions, Idle, MaxEntitiesPerEpisode, MaxEpisodesInMemory, MaxLinesPerEpisode, MinimumDwell, Rejoin, Retention

### Community 186 - "WindowCapturePolicyService"
Cohesion: 0.07
Nodes (27): Window, CapturePolicyMode, RequestExclusion, ShareSafe, Standard, Dictionary, IReadOnlyList, DisplayAffinity (+19 more)

### Community 187 - "NoteContent"
Cohesion: 0.17
Nodes (12): IReadOnlyList, NoteContent, NoteLine, NoteLineKind, Check, Error, Note, Result (+4 more)

### Community 188 - ".OnRate"
Cohesion: 0.50
Nodes (3): RoutedPropertyChangedEventArgs, RateSlider, Slider

### Community 189 - ".Claude_code_cli_answers_a_connection_test"
Cohesion: 0.33
Nodes (5): Fact, Task, LiveClaudeCodeTests, Enabled, RealGraphifyTests

### Community 192 - "WndProc"
Cohesion: 0.33
Nodes (6): HWND, LPARAM, LRESULT, UINT, WPARAM, WndProc()

### Community 193 - "Button"
Cohesion: 0.50
Nodes (3): Send, SpeechPause, Button

## Knowledge Gaps
- **789 isolated node(s):** `Hopefully-this-goes-somewhere`, `device`, `context`, `dup`, `latest` (+784 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1480 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PxRect` connect `PxRect` to `PxPoint`, `Win32`, `ToolResult`, `Fact`, `FakeExtractor`, `GrayImage`, `TutorSession`, `.Initialize`, `Annotation`, `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible`, `.LiveAsync`, `DiagramTests`, `.List`, `.A_real_mouse_drag_on_the_grip_moves_the_card_and_nothing_else`, `ScreenCaptureService`, `AskBar`, `InkMap`, `ExtractedText`, `NoteDismissWindow`, `.Review`, `Fact`, `ScreenContextTests`, `CapturedFrame`, `AnnotationOverlayManager`, `ITool`, `Rect`, `ScreenIndex`, `DuplicationSession`, `AnnotationSurfaceWindow`, `.ExecuteAsync`, `ContextHub`?**
  _High betweenness centrality (0.077) - this node is a cross-community bridge._
- **Why does `TutorSession` connect `TutorSession` to `LearningStore`, `TranscriptItem`, `ProviderSettings`, `Win32`, `ProviderException`, `ChatRequest`, `Runtime pipeline (TutorPipeline.RunAsync)`, `SessionMemory`, `AppSettings`, `Annotation`, `AnthropicProvider`, `RoutedEventArgs`, `ScreenCaptureService`, `AskBar`, `Design system document`, `Vernier README (tutor that sits over your screen)`, `AppServices`, `.Review`, `ToolTurn`, `ContextSelection`, `WindowCapturePolicyService`, `CapturedFrame`, `PipelineStage`, `OpenAiCompatibleProvider`, `.Cancel`, `DiagnosticLog`, `.BuildRequest`, `Vernier.Core.Geometry`?**
  _High betweenness centrality (0.062) - this node is a cross-community bridge._
- **Why does `Vernier.Core.Agents` connect `Vernier.Core.Agents` to `.Parse`, `Vernier.Core.Settings`, `Vernier.Tests`, `Tools.cs`, `Vernier.Core.Geometry`, `system_windows`, `OperatorGuide.cs`?**
  _High betweenness centrality (0.050) - this node is a cross-community bridge._
- **Are the 32 inferred relationships involving `PxRect` (e.g. with `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible()` and `.Frame()`) actually correct?**
  _`PxRect` has 32 INFERRED edges - model-reasoned connections that need verification._
- **Are the 6 inferred relationships involving `ToolTurn` (e.g. with `.SmokeCheckAsync()` and `.Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied()`) actually correct?**
  _`ToolTurn` has 6 INFERRED edges - model-reasoned connections that need verification._
- **Are the 3 inferred relationships involving `LearningStore` (e.g. with `.Episodes_survive_a_restart_without_screen_text_and_old_ones_and_forgotten_ones_are_deleted()` and `.A_real_tutoring_turn_runs_through_the_whole_pipeline()`) actually correct?**
  _`LearningStore` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Hopefully-this-goes-somewhere`, `device`, `context` to the rest of the system?**
  _789 weakly-connected nodes found - possible documentation gaps or missing edges._