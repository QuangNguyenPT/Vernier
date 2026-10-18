# Graph Report - Hopefully-this-goes-somewhere  (2026-10-18)

## Corpus Check
- 154 files · ~179,631 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 2, .rc 1, .vcxproj 1)

## Summary
- 4181 nodes · 10015 edges · 198 communities (186 shown, 12 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1025 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e6ae15a5`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Exec
- system_text
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
- .List
- SettingsPage
- system_windows
- UserControl
- Rect
- ProviderException
- ChatRequest
- Runtime pipeline (TutorPipeline.RunAsync)
- GrayImage
- SessionMemory
- AppSettings
- TutorSession
- .Initialize
- AnnotationStore
- AnthropicProvider
- Fact
- .The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible
- UserControl
- .Review
- .Parse
- .OnSta
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
- Fact
- Vernier.Tests.csproj
- AppServices
- FilesPage
- ActionType
- Fact
- .ConceptStates
- Threat model
- ToolRegistry
- AnnotationKind
- ExtractedText
- DiagnosticsPage
- CapturedFrame
- ResearchTests
- VerificationReport
- Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)
- AnnotationOverlayManager
- ConnectorException
- ITool
- TextBox
- Architecture document
- match.cpp
- SafeWeb
- Parser
- Ink
- ClaudeCodeProvider
- LibraryPage
- CodeTool
- PathGuard
- Accounts via vendor CLIs (CliAccounts)
- .OnProviderFieldChanged
- ScreenIndex
- GeminiProvider
- .OnStartup
- .ExecuteAsync
- ProviderErrorKind
- AxisCalibration
- .SendAsync
- CredentialManagerSecretStore
- Vernier.Core.Agents
- HotkeyManager
- LearningStore
- Episode
- Annotation
- .StatusAsync
- DiagnosticLog
- ScreenContextTests
- ContextEngine
- PxRect
- .OnPrivacy
- .SaveSettings
- McpServer
- .Clean
- .OnSessionSelected
- .OnTab
- ComputerAction
- .Extract
- ContextHub
- .BuildRequest
- LearningStore.cs
- DeleteSession
- ObsidianVault
- Vernier.App.Platform
- TitleNormaliser
- .Build
- MediaState
- Search
- Hopefully-this-goes-somewhere
- Dark.xaml
- Light.xaml
- FormulasView
- GlassSurface
- DiagramRenderer
- ink.cpp
- MemorySecrets
- glow.cpp
- Duplication
- common.h
- DuckDuckGoSearch
- SapiSpeechInput
- TranscriptItem
- duplication.cpp
- KnowledgeStoreTests
- NoteRenderer
- .DrawSpace
- Depth
- V3
- .ExecuteAsync
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
- ResearchLedger
- .Overlay_latency_and_cost
- OperatorGuide.cs
- PipelineStage
- FakeUser32
- .Options
- ToolException
- SapiSpeechOutput
- FreeSpace
- DuplicationSession
- .RefitAsync
- .Focus
- .Create
- DiagramTests
- Aurora.xaml
- Glacier.xaml
- Nebula.xaml
- FreeSpaceTests
- .SecondOpinionAsync
- Vernier.Core.Geometry
- SettingsTests
- .Redact
- .OnPolicyChanged
- .CertaintyWords
- EdgeStripWindow
- InkMap
- ComputerAct
- .Decide
- INativeWindowApi
- .Hypercube
- NoteDismissWindow
- .Fraction
- .Check
- SKILL.md
- OperatorGuide.md
- ContextSelection
- WindowCapturePolicy.cs
- ContextOptions
- CapturePolicyMode
- NoteContent
- ITextExtractor
- .Claude_code_cli_answers_a_connection_test
- Vernier.Core.Context
- .Gist
- WndProc
- ToolRisk
- .ApproveAsync
- WindowCapturePolicyService
- Task
- Examples

## God Nodes (most connected - your core abstractions)
1. `PxRect` - 108 edges
2. `ToolTurn` - 79 edges
3. `TutorSession` - 78 edges
4. `LearningStore` - 76 edges
5. `UserControl` - 71 edges
6. `AnnotationOverlayManager` - 67 edges
7. `Win32` - 61 edges
8. `PxPoint` - 59 edges
9. `AppServices` - 55 edges
10. `AppSettings` - 55 edges

## Surprising Connections (you probably didn't know these)
- `3. Architecture decisions (ADR log)` --references--> `Window`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Capture.cs
- `Decisions` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `Milestones` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `Defects corrected in revision 2 (refused capture aborted question; ask bar read itself)` --references--> `AskBar`  [EXTRACTED]
  docs/VERIFICATION_REPORT.md → src/Vernier.App/Views/AskBar.cs
- `2. Diagnosis of v0.3.0 (measured or evidenced)` --references--> `AssistedInput`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Views/AssistedInput.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Observer, Mastery Tutor and Verifier cooperate in the tutoring pipeline** — docs_architecture_observer_agent, docs_architecture_mastery_tutor_agent, docs_architecture_verifier_agent, docs_architecture_runtime_pipeline, docs_architecture_vernier_structured_block [EXTRACTED 1.00]
- **Glass, capture light, ask bar and tray form the always-there shell under the yellow/blue rule** — docs_architecture_glass, docs_architecture_capture_light, docs_architecture_ask_bar, docs_architecture_tray, docs_design_system_the_one_rule [INFERRED 0.85]
- **Consent gates, untrusted fencing, action policy and path guard implement the off-by-default security posture** — docs_security_and_privacy_consent_gates, docs_security_and_privacy_threat_prompt_injection, docs_security_and_privacy_threat_harmful_input_actions, docs_security_and_privacy_threat_path_traversal, claude_security_rules [INFERRED 0.85]

## Communities (198 total, 12 thin omitted)

### Community 0 - ".Exec"
Cohesion: 0.17
Nodes (5): Cards, ExportDocument, SqliteTransaction, DateTimeOffset, Episode

### Community 1 - "system_text"
Cohesion: 0.14
Nodes (19): anthropic, anthropic_exceptions, anthropic_models_messages, Vernier.Core.Tools, Vernier.Core.Connectors, Vernier.Core.Settings, Vernier.App.Connectors, Vernier.Core.Research (+11 more)

### Community 2 - "UserControl"
Cohesion: 0.12
Nodes (23): Kind, Reason, Back, BackHost, CardMeta, CardView, Front, Grades (+15 more)

### Community 3 - "UserControl"
Cohesion: 0.12
Nodes (21): Available, Excluded, Expected, Missing, Observed, Path, Environment, Integrations (+13 more)

### Community 4 - "ProviderSettings"
Cohesion: 0.10
Nodes (27): OpenAiCompatibleProvider, Capabilities, DisplayName, Id, Model, ProviderSettings, ApiKeyName, BaseUrl (+19 more)

### Community 5 - "TutorStructured"
Cohesion: 0.07
Nodes (29): List, Claim, Expr, UnitsLhs, UnitsRhs, Value, FormulaNote, Derivation (+21 more)

### Community 6 - "PxPoint"
Cohesion: 0.13
Nodes (11): IReadOnlyList, DpiMath, MonitorInfo, OverlayPlacement, PolylineMath, PxPoint, Stopwatch, GeometryTests (+3 more)

### Community 7 - "Win32"
Cohesion: 0.06
Nodes (18): 6. Review log (Agent B), KEYBDINPUT, LASTINPUTINFO, MonitorEnumProc, MONITORINFO, MOUSEINPUT, Color, CaptureExclusionSelfTest (+10 more)

### Community 8 - "Fact"
Cohesion: 0.14
Nodes (22): Action, PipelineCallbacks, Stage, Text, Tool, ScriptedProvider, Capabilities, Delay (+14 more)

### Community 9 - "TrayIcon"
Cohesion: 0.06
Nodes (35): At, Dismissals, NOTIFYICONDATA, Action, DateTimeOffset, TimeSpan, Proactive, Action (+27 more)

### Community 10 - "UserControl"
Cohesion: 0.10
Nodes (28): Degree, Label, Links, AskAboutNote, DisconnectVault, EditInstruction, Explain, FileText (+20 more)

### Community 11 - "UserControl"
Cohesion: 0.05
Nodes (43): Checks, IsUser, Meta, Notices, ActionsBar, ActionsText, AskKey, Bar (+35 more)

### Community 12 - "ReviewPage"
Cohesion: 0.17
Nodes (10): Queue, ResolveButton, Reveal, KeyEventArgs, RoutedEventArgs, SelectionChangedEventArgs, ReviewPage, Button (+2 more)

### Community 13 - ".List"
Cohesion: 0.15
Nodes (14): Output, IReadOnlyList, JsonElement, Ok, Task, GraphEdge, GraphifySource, Name (+6 more)

### Community 14 - "SettingsPage"
Cohesion: 0.11
Nodes (14): GoogleConnectButton, RemoveSample, TestButton, DateTimeOffset, Func, RoutedEventArgs, Task, AccountRow (+6 more)

### Community 15 - "system_windows"
Cohesion: 0.16
Nodes (19): Vernier.App.Views, Vernier.App, microsoft_win32, system_windows, system_windows_automation, system_windows_automation_peers, system_windows_controls, system_windows_controls_primitives (+11 more)

### Community 16 - "UserControl"
Cohesion: 0.08
Nodes (32): ActionLabel, ActionName, CanAct, Id, SignedIn, State, UseName, Accounts (+24 more)

### Community 17 - "Rect"
Cohesion: 0.15
Nodes (14): Geometry, IReadOnlyList, Point, Rect, MarkShapes, Rect, DrawingContext, DrawingContext (+6 more)

### Community 18 - "ProviderException"
Cohesion: 0.17
Nodes (15): ProviderException, IsTransient, Kind, RetryAfter, CancellationToken, HttpMethod, HttpRequestMessage, HttpResponseMessage (+7 more)

### Community 19 - "ChatRequest"
Cohesion: 0.08
Nodes (28): ChatRunner (timeout, bounded retry, vision gate), Stage: Teach (IChatProvider.StreamAsync via ChatRunner), Message, MessageParam, Raw, CancellationToken, CaptureGeometry, Task (+20 more)

### Community 20 - "Runtime pipeline (TutorPipeline.RunAsync)"
Cohesion: 0.10
Nodes (26): Step 4: Check your own work (second route, units, signs, limiting cases), Do-not rules (no solution dumps, no vague praise, no invented file contents, no unopened citations), Hint escalation ladder (point, leading question, principle, show the step), Step 5: Finish with a mastery check (transfer question, mental model, what is still open), One right answer is not mastery, Mastery Tutor skill (/mastery-tutor), Misconception matters more than the slip, Subject notes (mathematics, physics/engineering, programming, sciences) (+18 more)

### Community 21 - "GrayImage"
Cohesion: 0.12
Nodes (19): PatchStats, NativePatchMatcher, Best, Guid, IReadOnlyDictionary, IReadOnlyList, IReadOnlySet, MarkAnchor (+11 more)

### Community 22 - "SessionMemory"
Cohesion: 0.21
Nodes (9): DateTimeOffset, IReadOnlyList, List, SessionMemory, Count, TurnNote, Fact, SessionMemoryTests (+1 more)

### Community 23 - "AppSettings"
Cohesion: 0.05
Nodes (39): ModelRoute, List, AppSettings, AcknowledgedObserveCost, ActiveProvider, ActiveProviderId, AllowActivityContext, AllowComputerControl (+31 more)

### Community 24 - "TutorSession"
Cohesion: 0.06
Nodes (30): Grant, ObservableCollection, ProviderKey, CancellationTokenSource, IDisposable, IEnumerable, IReadOnlyList, List (+22 more)

### Community 25 - ".Initialize"
Cohesion: 0.13
Nodes (17): Answer, ScreenAsk, HasInk, CaptureGeometry, ImageBounds, QuantizationUncertaintyPx, ScaleX, ScaleY (+9 more)

### Community 26 - "AnnotationStore"
Cohesion: 0.14
Nodes (12): EventArgs, Guid, IReadOnlyDictionary, IReadOnlyList, List, AnnotationStore, CanRedo, CanUndo (+4 more)

### Community 27 - "AnthropicProvider"
Cohesion: 0.06
Nodes (34): IAnthropicClient, CancellationToken, Dictionary, Exception, IAsyncEnumerable, IReadOnlyList, JsonElement, JsonObject (+26 more)

### Community 28 - "Fact"
Cohesion: 0.19
Nodes (6): JsonSerializerOptions, Regex, TutorOutput, Fact, TutorOutputTests, VerifierTests

### Community 29 - ".The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible"
Cohesion: 0.08
Nodes (27): BaseUrl, ButtonBase, ComboBoxItem, Control, DependencyObject, EnumProc, Bodies, Button (+19 more)

### Community 30 - "UserControl"
Cohesion: 0.10
Nodes (24): Concept, Derivation, Description, Expression, Mastery.Level, Mastery.Reason, Meaning, Problem (+16 more)

### Community 31 - ".Review"
Cohesion: 0.15
Nodes (14): Assisted input / computer use (ActionPolicy.Review, preview, approval, ActionExecutor, SendInputDriver), Manual: assisted input on a harmless target (Notepad), Threat: model proposes harmful input actions, ProposedAction, Amount, Keys, Text, Type (+6 more)

### Community 32 - ".Parse"
Cohesion: 0.05
Nodes (40): InlineUIContainer, Inner, Next, RichTextBox, Run, DependencyProperty, FormulaControl, Paragraph (+32 more)

### Community 33 - ".OnSta"
Cohesion: 0.08
Nodes (22): POINT, R, OcrTextExtractor, Provenance, NativeWindowApi, CurrentProcessId, OsBuild, POINT (+14 more)

### Community 34 - ".Assess"
Cohesion: 0.12
Nodes (16): DateTimeOffset, IReadOnlyList, CardSchedule, Evidence, EvidenceKinds, MasteryAssessment, MasteryLevel, Demonstrated (+8 more)

### Community 35 - "Hopefully-this-goes-somewhere.cpp"
Cohesion: 0.15
Nodes (16): ATOM, BOOL, HINSTANCE, About(), HWND, LPARAM, LRESULT, UINT (+8 more)

### Community 36 - "ResourceDictionary"
Cohesion: 0.12
Nodes (19): IsDropDownOpen, B, Box, C, PART_ContentHost, PART_Track, ResourceDictionary, S (+11 more)

### Community 37 - "Native"
Cohesion: 0.06
Nodes (23): 4b. Revision 5 plan (owner request of 2026-10-16), Decisions, Milestones, Reflection: what the owner asked for, across both sessions, IDisposable, LibraryImport, MatchResult, IReadOnlyList (+15 more)

### Community 38 - ".ExecuteAsync"
Cohesion: 0.12
Nodes (13): INPUT, INPUTUNION, CancellationToken, X, Y, SendInputDriver, INPUT, KEYBDINPUT (+5 more)

### Community 39 - "ScopedFiles"
Cohesion: 0.11
Nodes (16): JournalEntry, Role, ConcurrentDictionary, DateTimeOffset, IEnumerable, List, Text, FileChange (+8 more)

### Community 40 - "MainWindow"
Cohesion: 0.12
Nodes (11): ReviewNav, Dictionary, FrameworkElement, RadioButton, RoutedEventArgs, MainWindow, AskHotkey, Bar (+3 more)

### Community 41 - "Window"
Cohesion: 0.14
Nodes (17): GoalText, Lamp, NoticeBar, NoticeText, ObserveText, PageHost, PauseButton, PolicyText (+9 more)

### Community 42 - "ScreenCaptureService"
Cohesion: 0.15
Nodes (9): Int32Rect, Func, BitmapSource, ScreenCaptureService, CancellationToken, Task, ToolObservation, Height (+1 more)

### Community 43 - "AskBar"
Cohesion: 0.09
Nodes (16): Border, DockPanel, Button, FontFamily, List, Rectangle, TextBox, TranscriptItem (+8 more)

### Community 44 - "Design system document"
Cohesion: 0.14
Nodes (20): Product shape (tray-resident, Ask and Pen only, yellow/blue rule, capture light on while looking), Design system document, Direction A: Instrument (v1, workspace-first), Direction B: Glass and two inks (chosen), Direction C: Companion cursor (Clicky model), Keyboard chords, On-screen ink colours (fixed, drawn over a 3 px dark halo), Why it was redesigned (v1 warm paper, serif, orange accent) (+12 more)

### Community 45 - "Verification report"
Cohesion: 0.10
Nodes (22): Verification workflow (Evaluate, Correct, Confirm), Acceptance checklist (definition of done), Manual: Ask, Pen and the capture light, Manual: capture exclusion with your conferencing tool, Manual verification checklist, Items marked not verified by the author (multi-monitor, scaling, Codex, real mic, pro apps), Manual: living in the tray, Manual: Voice (hold to talk, speak answers) (+14 more)

### Community 46 - "ToolTurn"
Cohesion: 0.07
Nodes (37): ConcurrentQueue, CancellationToken, JsonObject, Task, CancellationToken, JsonObject, Task, JsonNode (+29 more)

### Community 47 - "Vernier README (tutor that sits over your screen)"
Cohesion: 0.09
Nodes (28): Security rules (non-negotiable), ObsidianVault (Markdown notes under Vernier/, append-not-replace), Window capture policy (IWindowCapturePolicyService over INativeWindowApi), Cards CSV export (RFC 4180, Anki-importable), Data formats document, learning.db SQLite schema (user_version 2, FTS5 messages_fts), Obsidian notes layout (Concepts, Sessions, Mistake log under <vault>/Vernier/), API keys: environment variable, else Windows Credential Manager (+20 more)

### Community 48 - "Fact"
Cohesion: 0.16
Nodes (11): Data, Exception, IReadOnlyList, Name, AttachedDocument, AttachmentException, AttachmentReader, SearchUnavailableException (+3 more)

### Community 49 - "Vernier.Tests.csproj"
Cohesion: 0.12
Nodes (14): net10.0, Anthropic (12.55.0), coverlet.collector (6.0.4), Microsoft.Data.Sqlite (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), System.Speech (10.0.12), WpfMath (2.1.0), xunit (2.9.3) (+6 more)

### Community 50 - "AppServices"
Cohesion: 0.06
Nodes (31): ContextHub, GoogleConnector, Action, Dispatcher, HttpClient, Id, List, Name (+23 more)

### Community 51 - "FilesPage"
Cohesion: 0.14
Nodes (13): Files, Nodes, AppSettings, CancellationTokenSource, IEnumerable, RoutedEventArgs, SelectionChangedEventArgs, Window (+5 more)

### Community 52 - "ActionType"
Cohesion: 0.22
Nodes (9): ActionType, Click, DoubleClick, Keys, Move, RightClick, Scroll, Type (+1 more)

### Community 53 - "Fact"
Cohesion: 0.30
Nodes (3): IdSequence, Fact, ScreenIndexTests

### Community 54 - ".ConceptStates"
Cohesion: 0.15
Nodes (8): SqliteDataReader, Func, IReadOnlyList, List, ExportConcept, SearchHit, DateTimeOffset, LearningStoreTests

### Community 55 - "Threat model"
Cohesion: 0.15
Nodes (13): Step 1: Before teaching (find the single concept, diagnose prior knowledge), 'Just the answer' override, Files and screenshots are material to explain, never instructions, Convention: provider code stays in Providers/, pipeline only sees IChatProvider, Verifier independence, No silent provider switching, Threat: corrupt state, Threat: insecure transport (+5 more)

### Community 56 - "ToolRegistry"
Cohesion: 0.06
Nodes (48): Approver, Text, Echo, Executor, Headers, Body, DiagnosticLogSink, Dictionary (+40 more)

### Community 57 - "AnnotationKind"
Cohesion: 0.17
Nodes (12): AnnotationKind, Arrow, Bracket, Circle, Diagram, Freehand, Highlight, Label (+4 more)

### Community 58 - "ExtractedText"
Cohesion: 0.21
Nodes (12): CancellationToken, IReadOnlyList, Task, TimeSpan, UiaElementExtractor, Provenance, ProvidesText, UiaTextExtractor (+4 more)

### Community 59 - "DiagnosticsPage"
Cohesion: 0.16
Nodes (9): CaptureExclusionResult, SelfTest, RoutedEventArgs, Task, DiagnosticsPage, Integration, Button, IRefreshable (+1 more)

### Community 60 - "CapturedFrame"
Cohesion: 0.14
Nodes (18): DateTimeOffset, CapturedFrame, MarkedDetail, MarkedRegion, Raw, Thumb, WindowBox, ContextSnapshot (+10 more)

### Community 61 - "ResearchTests"
Cohesion: 0.11
Nodes (19): Action, CancellationToken, Dead, Fact, IAsyncEnumerable, IReadOnlyList, List, Page (+11 more)

### Community 62 - "VerificationReport"
Cohesion: 0.22
Nodes (10): IEnumerable, IReadOnlyList, TimeSpan, TokenUsage, TutorTurn, VerificationReport, Failures, HasFailures (+2 more)

### Community 63 - "Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)"
Cohesion: 0.12
Nodes (17): Convention: Core never references WPF or P/Invoke, Original Win32 C++ template files (unrelated leftovers), graphify-out/ project knowledge graph, Convention: no abstraction without a second implementation or a test seam, Convention: all screen geometry is physical pixels on the virtual desktop, Rule: always launch tests/screenshots with --data <temp dir>, Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF), Vernier.App project (WPF, Platform/Overlay/Views) (+9 more)

### Community 64 - "AnnotationOverlayManager"
Cohesion: 0.08
Nodes (21): DateTime, Window, Action, Dictionary, DispatcherTimer, Func, Guid, IReadOnlyList (+13 more)

### Community 65 - "ConnectorException"
Cohesion: 0.05
Nodes (48): HttpMessageHandler, Pkce, Redirect, CancellationToken, HttpClient, HttpListener, HttpListenerContext, Listener (+40 more)

### Community 66 - "ITool"
Cohesion: 0.09
Nodes (41): 3. Architecture decisions (ADR log), HttpClient, IEnumerable, ContextTools, GetContext, Definition, MediaControl, Definition (+33 more)

### Community 67 - "TextBox"
Cohesion: 0.21
Nodes (11): AssessText, EdgeText, ExcludedText, GoalsText, GoogleClientIdBox, IntervalsText, LevelText, PaceText (+3 more)

### Community 68 - "Architecture document"
Cohesion: 0.19
Nodes (16): AnnotationOverlayManager (owns glass and strips, collects ink, InkSettled after 1.3 s), AnnotationStore (immutable list, undo/redo, Rebase, DropOutside), Architecture document, Ask bar (the only interactive overlay), Capture light (EdgeStripWindow, four edge strips per monitor), Glass (AnnotationSurfaceWindow, one transparent click-through window per monitor), Pen capture (ink painted into the image, MarkedRegion, MarkedDetail crop), Stack decision: C# on .NET 10 with WPF, no C++ layer (+8 more)

### Community 69 - "match.cpp"
Cohesion: 0.16
Nodes (16): cmath, vector, VN_API, vn_rect, Gray, h, px, w (+8 more)

### Community 70 - "SafeWeb"
Cohesion: 0.16
Nodes (17): Excerpt, CancellationToken, Dead, HttpClient, HttpStatusCode, Page, Status, Task (+9 more)

### Community 71 - "Parser"
Cohesion: 0.33
Nodes (5): Expr, Parser, AtEnd, Peek, Position

### Community 72 - "Ink"
Cohesion: 0.08
Nodes (23): atomic, HANDLE, mutex, thread, vector, Ink, argb, context (+15 more)

### Community 73 - "ClaudeCodeProvider"
Cohesion: 0.06
Nodes (33): ExitCode, Process, ToolSession, Definitions, McpConfig, McpServerName, Turn, CancellationToken (+25 more)

### Community 74 - "LibraryPage"
Cohesion: 0.28
Nodes (5): SessionRow, RoutedEventArgs, LibraryPage, SessionRow, TextChangedEventArgs

### Community 75 - "CodeTool"
Cohesion: 0.29
Nodes (6): CancellationToken, JsonObject, CodeTool, Definition, Runs, LiveMcpTests

### Community 76 - "PathGuard"
Cohesion: 0.17
Nodes (10): ScopedFiles / PathGuard (confined file access, Propose then Apply), Threat: path traversal / unintended file writes, CancellationToken, IEnumerable, IReadOnlyCollection, PathGuard, Root, PathGuardException (+2 more)

### Community 77 - "Accounts via vendor CLIs (CliAccounts)"
Cohesion: 0.33
Nodes (6): Accounts via vendor CLIs (CliAccounts), ClaudeCodeProvider (claude -p stream-json subprocess, Claude Code sign-in), Reference project: claude-overlay, Manual: Accounts sign-in, Self-exclusion during Vernier's own captures, Provider options: subscription account via vendor CLI, API key, or local model

### Community 78 - ".OnProviderFieldChanged"
Cohesion: 0.40
Nodes (3): EndpointText, ModelText, VisionCheck

### Community 79 - "ScreenIndex"
Cohesion: 0.10
Nodes (20): Item, Plan, Score, DateTimeOffset, Dictionary, IReadOnlyList, List, ResolvedTarget (+12 more)

### Community 80 - "GeminiProvider"
Cohesion: 0.09
Nodes (21): AnthropicProvider (official SDK, Messages streaming), CodexCliProvider (codex exec subprocess, ChatGPT sign-in), GeminiProvider (streamGenerateContent SSE), IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities), OpenAiCompatibleProvider (OpenAI, Moonshot/Kimi, Ollama, LM Studio, llama.cpp), ChatGPT-account provider (codex exec) never run for real, ProviderCapabilities, HttpClient (+13 more)

### Community 81 - ".OnStartup"
Cohesion: 0.16
Nodes (7): ExitEventArgs, Application, Task, App, FirstRunWindow, StartupEventArgs, Window

### Community 82 - ".ExecuteAsync"
Cohesion: 0.25
Nodes (10): CancellationToken, Func, IEnumerable, JsonObject, Task, Append, Definition, GoogleTools (+2 more)

### Community 83 - "ProviderErrorKind"
Cohesion: 0.20
Nodes (10): ProviderErrorKind, Auth, BadRequest, Network, NotConfigured, RateLimited, Refused, Server (+2 more)

### Community 84 - "AxisCalibration"
Cohesion: 0.23
Nodes (7): IReadOnlyList, X, Y, AxisCalibration, AxisTick, Fact, GraphAreaTests

### Community 86 - "CredentialManagerSecretStore"
Cohesion: 0.42
Nodes (3): CREDENTIAL, CREDENTIAL, CredentialManagerSecretStore

### Community 87 - "Vernier.Core.Agents"
Cohesion: 0.10
Nodes (18): Vernier.Core.Security, Vernier.Core.Knowledge, Vernier.Core.Agents, Vernier.Core.Verification, IInputDriver, ActionExecutor, ProviderKinds, system_collections_concurrent (+10 more)

### Community 88 - "HotkeyManager"
Cohesion: 0.20
Nodes (7): Action, Dictionary, HwndSource, List, HotkeyManager, Unavailable, EventArgs

### Community 89 - "LearningStore"
Cohesion: 0.10
Nodes (11): SqliteConnection, JsonSerializerOptions, ExportCard, LearningStore, BackupDirectory, HasSampleData, IsEmpty, LatestVersion (+3 more)

### Community 90 - "Episode"
Cohesion: 0.06
Nodes (30): From, SemaphoreSlim, Task, EpisodeSummariser, To, Episode, ActiveSeconds, App (+22 more)

### Community 91 - "Annotation"
Cohesion: 0.09
Nodes (20): DateTimeOffset, DiagramSpec, TimeSpan, Annotation, Color, Confidence, CreatedAt, Diagram (+12 more)

### Community 92 - ".StatusAsync"
Cohesion: 0.24
Nodes (9): Args, File, CancellationToken, Func, IReadOnlyList, Task, CliAccount, CliAccounts (+1 more)

### Community 93 - "DiagnosticLog"
Cohesion: 0.33
Nodes (4): Exception, IReadOnlyList, LinkedList, DiagnosticLog

### Community 94 - "ScreenContextTests"
Cohesion: 0.24
Nodes (9): CancellationToken, DateTimeOffset, IReadOnlyList, Task, Fixed, Provenance, NoText, Provenance (+1 more)

### Community 95 - "ContextEngine"
Cohesion: 0.09
Nodes (20): App, Key, SessionItem, Since, DateTimeOffset, HashSet, IReadOnlyList, LearningStore (+12 more)

### Community 96 - "PxRect"
Cohesion: 0.13
Nodes (11): RECT, CaptureTarget, None, Region, Screen, Window, PxRect, Bottom (+3 more)

### Community 97 - ".OnPrivacy"
Cohesion: 0.27
Nodes (10): AllowActivity, AllowCapture, AllowControl, AllowMic, AllowNotes, AllowResearch, AutoRouteBox, Proactive (+2 more)

### Community 98 - ".SaveSettings"
Cohesion: 0.17
Nodes (9): RoutedPropertyChangedEventArgs, CheckerBox, ContextModelBox, ModelBox, ProviderBox, RateSlider, SelectionChangedEventArgs, ComboBox (+1 more)

### Community 99 - "McpServer"
Cohesion: 0.10
Nodes (19): HttpListenerResponse, IAsyncDisposable, JsonArray, Action, CancellationTokenSource, ConcurrentDictionary, DiagnosticLogSink, HttpListener (+11 more)

### Community 100 - ".Clean"
Cohesion: 0.20
Nodes (8): IPAddress, GeneratedRegex, HashSet, Regex, QuerySanitizer, ResearchPolicy, InlineData, Theory

### Community 101 - ".OnSessionSelected"
Cohesion: 0.33
Nodes (5): Concepts, Mistakes, Sessions, SelectionChangedEventArgs, ListBox

### Community 102 - ".OnTab"
Cohesion: 0.53
Nodes (5): TabConcepts, TabFormulas, TabMistakes, TabSessions, RadioButton

### Community 103 - "ComputerAction"
Cohesion: 0.21
Nodes (8): DllImport, Guid, HashSet, IReadOnlyList, ComputerTools, Plan, PreviewMark, ComputerAction

### Community 104 - ".Extract"
Cohesion: 0.44
Nodes (4): GeneratedRegex, Regex, Title, Readable

### Community 105 - "ContextHub"
Cohesion: 0.10
Nodes (16): GlobalSystemMediaTransportControlsSessionManager, OcrEngine, ScreenReader, CancellationToken, CancellationTokenSource, DateTimeOffset, DispatcherTimer, Func (+8 more)

### Community 106 - ".BuildRequest"
Cohesion: 0.09
Nodes (27): Vernier.Core project (net10.0, no UI), Attachment, TutorMode, Annotate, Ask, Assist, Review, Tutor (+19 more)

### Community 107 - "LearningStore.cs"
Cohesion: 0.11
Nodes (18): ExportCard, ExportConcept, microsoft_data_sqlite, Card, ConceptState, ExportDocument, Formula, LearningRecord (+10 more)

### Community 108 - "DeleteSession"
Cohesion: 0.50
Nodes (4): DeleteSession, Toggle, ToVault, Button

### Community 109 - "ObsidianVault"
Cohesion: 0.14
Nodes (13): NoteHit, CancellationToken, GeneratedRegex, HashSet, Regex, FileChangeKind, Append, Create (+5 more)

### Community 110 - "Vernier.App.Platform"
Cohesion: 0.11
Nodes (15): Vernier.App.Agent, Vernier.App.Platform, Func, Task, StaThread, system_diagnostics, system_io, system_runtime_interopservices (+7 more)

### Community 111 - "TitleNormaliser"
Cohesion: 0.45
Nodes (3): GeneratedRegex, Regex, TitleNormaliser

### Community 112 - ".Build"
Cohesion: 0.14
Nodes (17): Alpha, Brush, Colour, Dashed, Halo, IEasingFunction, Color, Dictionary (+9 more)

### Community 113 - "MediaState"
Cohesion: 0.20
Nodes (12): DateTimeOffset, IReadOnlyList, LinkedList, TimeSpan, ActivitySpan, Length, ActivityTimeline, Recent (+4 more)

### Community 114 - "Search"
Cohesion: 0.67
Nodes (3): Search, SessionText, TextBox

### Community 118 - "FormulasView"
Cohesion: 0.33
Nodes (4): FormulasView, SessionsView, Grid, ScrollViewer

### Community 120 - "GlassSurface"
Cohesion: 0.08
Nodes (23): DpiScale, DrawingVisual, MouseEventArgs, Action, EventArgs, Guid, IReadOnlyList, List (+15 more)

### Community 121 - "DiagramRenderer"
Cohesion: 0.18
Nodes (13): Card, Dictionary, Guid, Scale, DiagramRenderer, IReadOnlyList, DiagramKind, Complex (+5 more)

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

### Community 127 - "DuckDuckGoSearch"
Cohesion: 0.52
Nodes (3): IReadOnlyList, DuckDuckGoSearch, WebHit

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

### Community 133 - ".DrawSpace"
Cohesion: 0.31
Nodes (8): Color, DrawingContext, Pen, Point, SolidColorBrush, Typeface, Palette, Scene

### Community 134 - "Depth"
Cohesion: 0.24
Nodes (8): MARGINS, Result, Dictionary, DllImport, Window, Depth, MARGINS, Result

### Community 135 - "V3"
Cohesion: 0.23
Nodes (8): Distance, Foot, OnFirst, OnSecond, Skew, Geometry3D, V3, Length

### Community 136 - ".ExecuteAsync"
Cohesion: 0.11
Nodes (14): Kind, Place, CancellationToken, Func, Id, IEnumerable, JsonObject, Mark (+6 more)

### Community 137 - "TaskKind"
Cohesion: 0.14
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
Cohesion: 0.10
Nodes (18): CheckResult, CheckStatus, Advisory, Failed, Passed, Skipped, ProposedAnnotation, Confidence (+10 more)

### Community 142 - "imaging.cpp"
Cohesion: 0.22
Nodes (9): combaseapi, ComScope, hr, HRESULT, VN_API, vn_free(), vn_png_encode(), vn_version() (+1 more)

### Community 143 - "InlineData"
Cohesion: 0.18
Nodes (5): Dictionary, Units, InlineData, Theory, ExpressionTests

### Community 144 - ".Ask"
Cohesion: 0.24
Nodes (8): Cancel, Dialog, Button, CancellationToken, Ok, Task, Window, ConfirmDialog

### Community 145 - ".Clean"
Cohesion: 0.35
Nodes (4): Dictionary, GeneratedRegex, Regex, TexPlain

### Community 146 - "FakeExtractor"
Cohesion: 0.16
Nodes (10): Processes, Profile, AppProfile, AppProfiles, IReadOnlyList, FakeExtractor, Calls, Provenance (+2 more)

### Community 147 - "ResearchLedger"
Cohesion: 0.21
Nodes (8): ConcurrentDictionary, IReadOnlyCollection, ResearchDemand, ResearchGate, ResearchLedger, Calls, Urls, Working

### Community 148 - ".Overlay_latency_and_cost"
Cohesion: 0.17
Nodes (10): 1. Evidence gathered (reconnaissance), 2. Diagnosis of v0.3.0 (measured or evidenced), 4. Milestones (order revised after review R1), 5. Measurements, Agent harness plan (revision 4), Fact, ITestOutputHelper, List (+2 more)

### Community 149 - "OperatorGuide.cs"
Cohesion: 0.40
Nodes (4): Lazy, OperatorGuide, Text, system_reflection

### Community 150 - "PipelineStage"
Cohesion: 0.11
Nodes (19): FrameworkElement, DependencyProperty, LevelScale, Level, Label, Typeface, StageScale, Busy (+11 more)

### Community 151 - "FakeUser32"
Cohesion: 0.12
Nodes (11): Dictionary, HashSet, FakeUser32, Affinity, Children, CurrentProcessId, ForceEffective, Foreign (+3 more)

### Community 152 - ".Options"
Cohesion: 0.22
Nodes (7): Provider, DateTimeOffset, IReadOnlyList, JsonSerializerOptions, Label, RouteLog, RouteRecord

### Community 153 - "ToolException"
Cohesion: 0.26
Nodes (8): CancellationToken, IReadOnlyList, JsonObject, Task, NoteTools, WriteNote, Definition, ToolException

### Community 154 - "SapiSpeechOutput"
Cohesion: 0.22
Nodes (5): SpeechSynthesizer, ISpeechOutput, IsSpeaking, SapiSpeechOutput, IsSpeaking

### Community 155 - "FreeSpace"
Cohesion: 0.10
Nodes (17): Placement, Height, Width, FreeSpace, Area, BusyCount, CellPx, Cols (+9 more)

### Community 156 - "DuplicationSession"
Cohesion: 0.12
Nodes (11): FrameInfo, Png, Height, Width, DuplicationSession, HasFrame, Monitor, Fact (+3 more)

### Community 157 - ".RefitAsync"
Cohesion: 0.18
Nodes (4): IEnumerable, Task, CanSample, Func

### Community 158 - ".Focus"
Cohesion: 0.25
Nodes (5): Dictionary, UrlSanitiser, InlineData, Theory, TitleAndAddressTests

### Community 159 - ".Create"
Cohesion: 0.17
Nodes (7): Api, FakeUser32, InvalidDataException, Service, InlineData, Theory, WindowCapturePolicyLogicTests

### Community 160 - "DiagramTests"
Cohesion: 0.23
Nodes (6): Axes, Ink, Fact, InlineData, Theory, DiagramTests

### Community 165 - "FreeSpaceTests"
Cohesion: 0.25
Nodes (7): Bgra, H, Fact, InlineData, Theory, FreeSpaceTests, W

### Community 166 - ".SecondOpinionAsync"
Cohesion: 0.16
Nodes (9): Decision, Executed, CancellationToken, Func, IReadOnlyList, Task, TimeSpan, AnnotationColors (+1 more)

### Community 167 - "Vernier.Core.Geometry"
Cohesion: 0.22
Nodes (7): Vernier.Tests, Vernier.Core.Geometry, Vernier.Core.Diagrams, Vernier.Core.Annotations, system_windows_media_imaging, xunit, xunit_abstractions

### Community 168 - "SettingsTests"
Cohesion: 0.20
Nodes (5): JsonSerializerOptions, SettingsStore, LoadWarning, Path, SettingsTests

### Community 169 - ".Redact"
Cohesion: 0.29
Nodes (4): GeneratedRegex, Regex, SecretRedactor, Untrusted

### Community 170 - ".OnPolicyChanged"
Cohesion: 0.50
Nodes (3): PolicyBox, SelectionChangedEventArgs, ComboBox

### Community 172 - "EdgeStripWindow"
Cohesion: 0.18
Nodes (10): Dock, Motion: the capture wave is the one expressive motion, Color, Rectangle, EdgeSignal, Armed, Looking, Off (+2 more)

### Community 173 - "InkMap"
Cohesion: 0.24
Nodes (6): LostAt, Points, List, InkMap, Height, Width

### Community 174 - "ComputerAct"
Cohesion: 0.25
Nodes (6): CancellationToken, IEnumerable, JsonObject, Task, ComputerAct, Definition

### Community 175 - ".Decide"
Cohesion: 0.35
Nodes (5): GeneratedRegex, Regex, DimensionGate, DimensionPolicy, VisualDemand

### Community 176 - "INativeWindowApi"
Cohesion: 0.27
Nodes (3): INativeWindowApi, CurrentProcessId, OsBuild

### Community 177 - ".Hypercube"
Cohesion: 0.22
Nodes (7): A, Corners, Depth, Edges, X, Y, Projection

### Community 178 - "NoteDismissWindow"
Cohesion: 0.28
Nodes (6): ControlTemplate, EventArgs, Guid, NoteDismissWindow, Handle, NoteId

### Community 179 - ".Fraction"
Cohesion: 0.25
Nodes (4): ReadOnlySpan, PerceptualHash, ScreenDiff, ArgumentException

### Community 180 - ".Check"
Cohesion: 0.39
Nodes (3): JsonNode, Json, SchemaCheck

### Community 181 - "SKILL.md"
Cohesion: 0.17
Nodes (11): Context and memory, Draw with tools, never with code, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain, See it: diagrams for space, vectors, complex numbers and higher dimensions (+3 more)

### Community 182 - "OperatorGuide.md"
Cohesion: 0.17
Nodes (11): Context and memory, Draw with tools, never with code, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain, See it: diagrams for space, vectors, complex numbers and higher dimensions (+3 more)

### Community 183 - "ContextSelection"
Cohesion: 0.31
Nodes (3): TimeSpan, ContextSelection, IsEmpty

### Community 184 - "WindowCapturePolicy.cs"
Cohesion: 0.25
Nodes (8): DisplayAffinity, PolicyStatus, Applied, Failed, NotRequested, Requested, Unsupported, WindowPolicyState

### Community 185 - "ContextOptions"
Cohesion: 0.25
Nodes (8): ContextOptions, Idle, MaxEntitiesPerEpisode, MaxEpisodesInMemory, MaxLinesPerEpisode, MinimumDwell, Rejoin, Retention

### Community 186 - "CapturePolicyMode"
Cohesion: 0.29
Nodes (4): CapturePolicyMode, RequestExclusion, ShareSafe, Standard

### Community 187 - "NoteContent"
Cohesion: 0.17
Nodes (12): IReadOnlyList, NoteContent, NoteLine, NoteLineKind, Check, Error, Note, Result (+4 more)

### Community 188 - "ITextExtractor"
Cohesion: 0.33
Nodes (5): CancellationToken, Task, ITextExtractor, Provenance, ProvidesText

### Community 189 - ".Claude_code_cli_answers_a_connection_test"
Cohesion: 0.33
Nodes (5): Fact, Task, LiveClaudeCodeTests, Enabled, RealGraphifyTests

### Community 192 - "WndProc"
Cohesion: 0.33
Nodes (6): HWND, LPARAM, LRESULT, UINT, WPARAM, WndProc()

### Community 193 - "ToolRisk"
Cohesion: 0.33
Nodes (6): ToolRisk, Act, Annotate, Consequential, Control, Observe

### Community 194 - ".ApproveAsync"
Cohesion: 0.40
Nodes (4): CancellationToken, Task, TimeSpan, UiApprover

### Community 195 - "WindowCapturePolicyService"
Cohesion: 0.16
Nodes (11): Dictionary, IReadOnlyList, IWindowCapturePolicyService, Mode, States, Summary, WindowCapturePolicyService, Mode (+3 more)

### Community 196 - "Task"
Cohesion: 0.50
Nodes (3): CancellationToken, IReadOnlyList, Task

### Community 197 - "Examples"
Cohesion: 0.67
Nodes (3): Examples, Formulas, ItemsControl

## Knowledge Gaps
- **769 isolated node(s):** `Hopefully-this-goes-somewhere`, `device`, `context`, `dup`, `latest` (+764 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1413 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **12 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PxRect` connect `PxRect` to `PxPoint`, `Win32`, `.ExecuteAsync`, `Fact`, `FakeExtractor`, `GrayImage`, `TutorSession`, `.Initialize`, `FreeSpace`, `DuplicationSession`, `.RefitAsync`, `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible`, `.Review`, `.OnSta`, `Native`, `.ExecuteAsync`, `FreeSpaceTests`, `ScreenCaptureService`, `AskBar`, `ComputerAct`, `NoteDismissWindow`, `Fact`, `ExtractedText`, `CapturedFrame`, `AnnotationOverlayManager`, `ScreenIndex`, `Annotation`, `ScreenContextTests`, `ComputerAction`, `ContextHub`?**
  _High betweenness centrality (0.094) - this node is a cross-community bridge._
- **Why does `AppServices` connect `AppServices` to `SapiSpeechInput`, `system_text`, `Win32`, `TrayIcon`, `AppSettings`, `TutorSession`, `.Initialize`, `AnnotationStore`, `SapiSpeechOutput`, `.Options`, `SettingsTests`, `ScreenCaptureService`, `ToolRegistry`, `AnnotationOverlayManager`, `ConnectorException`, `WindowCapturePolicyService`, `GeminiProvider`, `.OnStartup`, `LearningStore`, `DiagnosticLog`, `.SaveSettings`, `McpServer`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **Why does `TutorSession` connect `TutorSession` to `system_text`, `TranscriptItem`, `ProviderSettings`, `UserControl`, `ChatRequest`, `Runtime pipeline (TutorPipeline.RunAsync)`, `PipelineStage`, `SessionMemory`, `AppSettings`, `AnnotationStore`, `AnthropicProvider`, `.SecondOpinionAsync`, `ScreenCaptureService`, `AskBar`, `Design system document`, `ToolTurn`, `Vernier README (tutor that sits over your screen)`, `AppServices`, `ContextSelection`, `CapturedFrame`, `VerificationReport`, `Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)`, `WindowCapturePolicyService`, `GeminiProvider`, `.SendAsync`, `LearningStore`, `DiagnosticLog`, `.BuildRequest`?**
  _High betweenness centrality (0.058) - this node is a cross-community bridge._
- **Are the 29 inferred relationships involving `PxRect` (e.g. with `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible()` and `.Frame()`) actually correct?**
  _`PxRect` has 29 INFERRED edges - model-reasoned connections that need verification._
- **Are the 6 inferred relationships involving `ToolTurn` (e.g. with `.SmokeCheckAsync()` and `.Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied()`) actually correct?**
  _`ToolTurn` has 6 INFERRED edges - model-reasoned connections that need verification._
- **Are the 3 inferred relationships involving `LearningStore` (e.g. with `.Episodes_survive_a_restart_without_screen_text_and_old_ones_and_forgotten_ones_are_deleted()` and `.A_real_tutoring_turn_runs_through_the_whole_pipeline()`) actually correct?**
  _`LearningStore` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Hopefully-this-goes-somewhere`, `device`, `context` to the rest of the system?**
  _769 weakly-connected nodes found - possible documentation gaps or missing edges._