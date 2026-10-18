# Graph Report - Hopefully-this-goes-somewhere  (2026-10-18)

## Corpus Check
- 136 files · ~155,078 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 2, .rc 1, .vcxproj 1)

## Summary
- 3807 nodes · 9035 edges · 182 communities (171 shown, 11 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 950 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f3065db0`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- LearningStore
- system_text
- UserControl
- UserControl
- ProviderSettings
- Contracts.cs
- PxPoint
- Win32
- Fact
- TrayIcon
- UserControl
- UserControl
- ReviewPage
- GraphifySource
- SettingsPage
- Vernier.Core.Geometry
- UserControl
- .Build
- ProviderException
- IChatProvider
- Runtime pipeline (TutorPipeline.RunAsync)
- PathGuard
- SessionMemory
- AppSettings
- TutorSession
- .Initialize
- AnnotationStore
- ChatRequest
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
- ToolExecutor
- Vernier README (tutor that sits over your screen)
- Fact
- Vernier.Tests.csproj
- AppServices
- FilesPage
- ComputerAction
- PxRect
- RoutedEventArgs
- Threat model
- ToolRegistry
- CaptureGeometry
- ExtractedText
- DiagnosticsPage
- CapturedFrame
- FakeUser32
- .StreamAsync
- Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)
- AnnotationOverlayManager
- ConnectorException
- ITool
- TextBox
- Architecture document
- StreamedBlock
- CapturePolicyMode
- InlineData
- Ink
- ClaudeCodeProvider
- LibraryPage
- .A_real_tutoring_turn_runs_through_the_whole_pipeline
- .Redact
- Manual verification checklist
- .OnProviderFieldChanged
- ScreenIndex
- INativeWindowApi
- Parser
- OpenAiCompatibleProvider
- ProviderErrorKind
- EdgeStripWindow
- .Cancel
- CredentialManagerSecretStore
- Vernier.Core.Agents
- HotkeyManager
- TutorPanel
- Episode
- WindowCapturePolicy.cs
- IDisposable
- DiagnosticLog
- ScreenContextTests
- ContextEngine
- NoteDismissWindow
- .OnPrivacy
- .OnProviderChanged
- McpServer
- .Fraction
- .OnSessionSelected
- .OnTab
- .ExecuteAsync
- Examples
- ContextHub
- .BuildRequest
- .OnRate
- DeleteSession
- .List
- Capture.cs
- .Focus
- MarkPainter
- MediaState
- Search
- Hopefully-this-goes-somewhere
- Dark.xaml
- Light.xaml
- FormulasView
- GlassSurface
- Annotation
- ink.cpp
- MemorySecrets
- glow.cpp
- Duplication
- common.h
- .StatusAsync
- SapiSpeechInput
- TranscriptItem
- duplication.cpp
- KnowledgeStoreTests
- NoteRenderer
- AnnotationSurfaceWindow
- Depth
- SapiSpeechOutput
- ToolException
- TaskKind
- Glow
- Edge
- .Use
- LayeredSecretStore
- imaging.cpp
- CheckStatus
- .Build
- .CaptureAsync
- FakeExtractor
- ContextOptions
- .Overlay_latency_and_cost
- OperatorGuide.cs
- PipelineStage
- Screen geometry (CaptureGeometry, MonitorInfo, DpiMath)
- RouteLog
- WriteNote
- ScreenItemKind
- FreeSpace
- .Monitors
- .RunAsync
- INPUTUNION
- .Create
- Button
- Aurora.xaml
- Glacier.xaml
- Nebula.xaml
- FreeSpaceTests
- Vernier.Tests
- DuplicationSession
- NoteLineKind
- Agent harness plan (revision 4)
- ToolTurn
- .Ink_is_kept_for_the_question_shown_only_briefly_and_removed_when_it_is_sent_or_taken_back
- SKILL.md
- OperatorGuide.md
- ContextSelection
- .Split
- NoteContent
- Side
- Tools.cs
- WndProc
- .ShowAccountsAsync
- WindowCapturePolicyService

## God Nodes (most connected - your core abstractions)
1. `PxRect` - 102 edges
2. `TutorSession` - 77 edges
3. `LearningStore` - 75 edges
4. `ToolTurn` - 75 edges
5. `UserControl` - 70 edges
6. `Win32` - 61 edges
7. `AnnotationOverlayManager` - 59 edges
8. `AppServices` - 55 edges
9. `PxPoint` - 53 edges
10. `AppSettings` - 52 edges

## Surprising Connections (you probably didn't know these)
- `Decisions` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `Milestones` --references--> `Native`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Native.cs
- `3. Architecture decisions (ADR log)` --references--> `Window`  [INFERRED]
  docs/AGENT_HARNESS_PLAN.md → src/Vernier.App/Platform/Capture.cs
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

## Communities (182 total, 11 thin omitted)

### Community 0 - "LearningStore"
Cohesion: 0.06
Nodes (42): Cards, ExportCard, ExportConcept, ExportDocument, microsoft_data_sqlite, SqliteConnection, SqliteDataReader, SqliteTransaction (+34 more)

### Community 1 - "system_text"
Cohesion: 0.19
Nodes (11): Vernier.Core.Tools, Vernier.Core.Connectors, Vernier.App.Agent, Vernier.App.Connectors, system_net, system_net_http, system_net_http_headers, system_net_sockets (+3 more)

### Community 2 - "UserControl"
Cohesion: 0.12
Nodes (23): Kind, Reason, Back, BackHost, CardMeta, CardView, Front, Grades (+15 more)

### Community 3 - "UserControl"
Cohesion: 0.12
Nodes (21): Available, Excluded, Expected, Missing, Observed, Path, Environment, Integrations (+13 more)

### Community 4 - "ProviderSettings"
Cohesion: 0.10
Nodes (24): ChatMessage, ImageInput, ProviderSettings, ApiKeyName, BaseUrl, DisplayName, Effort, Id (+16 more)

### Community 5 - "Contracts.cs"
Cohesion: 0.05
Nodes (43): List, Claim, Expr, UnitsLhs, UnitsRhs, Value, FormulaNote, Derivation (+35 more)

### Community 6 - "PxPoint"
Cohesion: 0.17
Nodes (8): IReadOnlyList, DpiMath, MonitorInfo, OverlayPlacement, PolylineMath, PxPoint, GeometryTests, OverlayDataTests

### Community 7 - "Win32"
Cohesion: 0.09
Nodes (9): 6. Review log (Agent B), LASTINPUTINFO, MonitorEnumProc, DllImport, StringBuilder, TimeSpan, WinEventProc, LASTINPUTINFO (+1 more)

### Community 8 - "Fact"
Cohesion: 0.14
Nodes (22): Action, PipelineCallbacks, Stage, Text, Tool, ScriptedProvider, Capabilities, Delay (+14 more)

### Community 9 - "TrayIcon"
Cohesion: 0.06
Nodes (34): At, Dismissals, NOTIFYICONDATA, Action, DateTimeOffset, TimeSpan, Proactive, Action (+26 more)

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
Cohesion: 0.14
Nodes (14): Output, NodeRow, JsonElement, Ok, Task, GraphEdge, GraphifySource, Name (+6 more)

### Community 14 - "SettingsPage"
Cohesion: 0.12
Nodes (10): GoogleConnectButton, RemoveSample, TestButton, DateTimeOffset, Func, RoutedEventArgs, SettingsPage, Current (+2 more)

### Community 15 - "Vernier.Core.Geometry"
Cohesion: 0.15
Nodes (20): Vernier.App.Views, Vernier.App.Platform, Vernier.Core.Geometry, microsoft_win32, system_diagnostics, system_runtime_interopservices, system_speech_recognition, system_speech_synthesis (+12 more)

### Community 16 - "UserControl"
Cohesion: 0.08
Nodes (32): ActionLabel, ActionName, CanAct, Id, SignedIn, State, UseName, Accounts (+24 more)

### Community 17 - ".Build"
Cohesion: 0.14
Nodes (16): Geometry, Func, Size, IReadOnlyList, Point, Rect, MarkShapes, Rect (+8 more)

### Community 18 - "ProviderException"
Cohesion: 0.15
Nodes (16): ProviderException, IsTransient, Kind, RetryAfter, CancellationToken, HttpMethod, HttpRequestMessage, HttpResponseMessage (+8 more)

### Community 19 - "IChatProvider"
Cohesion: 0.14
Nodes (18): ChatRunner (timeout, bounded retry, vision gate), Stage: Teach (IChatProvider.StreamAsync via ChatRunner), Message, CancellationToken, Func, IAsyncEnumerable, IReadOnlyList, JsonNode (+10 more)

### Community 20 - "Runtime pipeline (TutorPipeline.RunAsync)"
Cohesion: 0.10
Nodes (26): Step 4: Check your own work (second route, units, signs, limiting cases), Do-not rules (no solution dumps, no vague praise, no invented file contents, no unopened citations), Hint escalation ladder (point, leading question, principle, show the step), Step 5: Finish with a mastery check (transfer question, mental model, what is still open), One right answer is not mastery, Mastery Tutor skill (/mastery-tutor), Misconception matters more than the slip, Subject notes (mathematics, physics/engineering, programming, sciences) (+18 more)

### Community 21 - "PathGuard"
Cohesion: 0.24
Nodes (7): ScopedFiles / PathGuard (confined file access, Propose then Apply), Threat: path traversal / unintended file writes, IReadOnlyCollection, CancellationToken, IEnumerable, PathGuard, Root

### Community 22 - "SessionMemory"
Cohesion: 0.17
Nodes (11): DateTimeOffset, GeneratedRegex, IReadOnlyList, List, Regex, SessionMemory, Count, TurnNote (+3 more)

### Community 23 - "AppSettings"
Cohesion: 0.05
Nodes (41): ModelRoute, JsonSerializerOptions, List, AppSettings, AcknowledgedObserveCost, ActiveProvider, ActiveProviderId, AllowActivityContext (+33 more)

### Community 24 - "TutorSession"
Cohesion: 0.06
Nodes (30): Grant, ObservableCollection, ProviderKey, CancellationTokenSource, IDisposable, IEnumerable, IReadOnlyList, List (+22 more)

### Community 25 - ".Initialize"
Cohesion: 0.10
Nodes (15): ExitEventArgs, Application, App, ScreenAsk, HasInk, StartupEventArgs, TempDir, Path (+7 more)

### Community 26 - "AnnotationStore"
Cohesion: 0.17
Nodes (10): Guid, IReadOnlyList, List, AnnotationStore, CanRedo, CanUndo, Items, Visible (+2 more)

### Community 27 - "ChatRequest"
Cohesion: 0.07
Nodes (27): IAnthropicClient, MessageParam, CancellationToken, Exception, IAsyncEnumerable, IReadOnlyList, Task, Tool (+19 more)

### Community 28 - "Fact"
Cohesion: 0.16
Nodes (7): JsonSerializerOptions, Regex, TutorOutput, Fact, SettingsTests, TutorOutputTests, VerifierTests

### Community 29 - ".The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible"
Cohesion: 0.07
Nodes (29): BaseUrl, ButtonBase, ComboBoxItem, Control, DependencyObject, EnumProc, Task, Window (+21 more)

### Community 30 - "UserControl"
Cohesion: 0.10
Nodes (24): Concept, Derivation, Description, Expression, Mastery.Level, Mastery.Reason, Meaning, Problem (+16 more)

### Community 31 - ".Review"
Cohesion: 0.12
Nodes (18): Decision, Assisted input / computer use (ActionPolicy.Review, preview, approval, ActionExecutor, SendInputDriver), Threat: model proposes harmful input actions, Executed, ProposedAction, Amount, Keys, Text (+10 more)

### Community 32 - ".Parse"
Cohesion: 0.05
Nodes (40): InlineUIContainer, Inner, Next, RichTextBox, Run, DependencyProperty, FormulaControl, Paragraph (+32 more)

### Community 33 - ".OnSta"
Cohesion: 0.13
Nodes (13): POINT, R, POINT, Dispatcher, DllImport, Fact, InvalidOperationException, ITestOutputHelper (+5 more)

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
Nodes (18): LibraryImport, Png, Height, IReadOnlyList, Lazy, Width, FrameInfo, Native (+10 more)

### Community 38 - ".ExecuteAsync"
Cohesion: 0.19
Nodes (9): INPUT, INPUTUNION, CancellationToken, SendInputDriver, INPUT, KEYBDINPUT, MOUSEINPUT, X (+1 more)

### Community 39 - "ScopedFiles"
Cohesion: 0.16
Nodes (12): JournalEntry, ConcurrentDictionary, List, FileChange, Diff, Summary, ScopedFiles, Guard (+4 more)

### Community 40 - "MainWindow"
Cohesion: 0.13
Nodes (10): Dictionary, FrameworkElement, RadioButton, SelectionChangedEventArgs, Task, MainWindow, AskHotkey, Bar (+2 more)

### Community 41 - "Window"
Cohesion: 0.09
Nodes (22): GoalText, Lamp, NoticeBar, NoticeText, ObserveText, PageHost, PauseButton, PolicyBox (+14 more)

### Community 42 - "ScreenCaptureService"
Cohesion: 0.23
Nodes (5): Int32Rect, BitmapSource, ScreenCaptureService, Height, Width

### Community 43 - "AskBar"
Cohesion: 0.09
Nodes (16): Border, DockPanel, Button, FontFamily, KeyEventArgs, List, Rectangle, TextBox (+8 more)

### Community 44 - "Design system document"
Cohesion: 0.13
Nodes (21): Product shape (tray-resident, Ask and Pen only, yellow/blue rule, capture light on while looking), Motion: the capture wave is the one expressive motion, Design system document, Direction A: Instrument (v1, workspace-first), Direction B: Glass and two inks (chosen), Direction C: Companion cursor (Clicky model), Keyboard chords, On-screen ink colours (fixed, drawn over a 3 px dark halo) (+13 more)

### Community 45 - "Verification report"
Cohesion: 0.16
Nodes (14): Verification workflow (Evaluate, Correct, Confirm), Acceptance checklist (definition of done), AppUiTests (real MainWindow in-process against a loopback stub), Verified: capture exclusion at OS level (BitBlt marker absent, affinity 0x11, build 26100), Verified: capture geometry maps image pixels back to screen (+/-1 px quantisation), ECC audit record (Evaluate, Correct, Confirm): 9 defects found and corrected, Implemented with known limits (Verifier scope, SAPI voice, no typeset maths, no PDF/Office, unsigned), Verified: real tutoring turn through Claude Code CLI (0.5*2*3^2 = 9 recomputed) (+6 more)

### Community 46 - "ToolExecutor"
Cohesion: 0.10
Nodes (19): CancellationToken, Task, TimeSpan, UiApprover, CancellationToken, DateTimeOffset, DiagnosticLogSink, Func (+11 more)

### Community 47 - "Vernier README (tutor that sits over your screen)"
Cohesion: 0.09
Nodes (27): Security rules (non-negotiable), ObsidianVault (Markdown notes under Vernier/, append-not-replace), Window capture policy (IWindowCapturePolicyService over INativeWindowApi), Cards CSV export (RFC 4180, Anki-importable), Data formats document, learning.db SQLite schema (user_version 2, FTS5 messages_fts), Obsidian notes layout (Concepts, Sessions, Mistake log under <vault>/Vernier/), API keys: environment variable, else Windows Credential Manager (+19 more)

### Community 48 - "Fact"
Cohesion: 0.19
Nodes (9): Data, IReadOnlyList, Name, AttachedDocument, AttachmentException, AttachmentReader, Fact, AttachmentReaderTests (+1 more)

### Community 49 - "Vernier.Tests.csproj"
Cohesion: 0.12
Nodes (14): net10.0, Anthropic (12.55.0), coverlet.collector (6.0.4), Microsoft.Data.Sqlite (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), System.Speech (10.0.12), WpfMath (2.1.0), xunit (2.9.3) (+6 more)

### Community 50 - "AppServices"
Cohesion: 0.06
Nodes (31): ContextHub, GoogleConnector, Action, Dispatcher, HttpClient, Id, List, Name (+23 more)

### Community 51 - "FilesPage"
Cohesion: 0.14
Nodes (13): Task, Files, Nodes, AppSettings, CancellationTokenSource, IEnumerable, RoutedEventArgs, SelectionChangedEventArgs (+5 more)

### Community 52 - "ComputerAction"
Cohesion: 0.10
Nodes (19): CancellationToken, Task, ActionType, Click, DoubleClick, Keys, Move, RightClick (+11 more)

### Community 53 - "PxRect"
Cohesion: 0.15
Nodes (13): CaptureTarget, None, Region, Screen, Window, IdSequence, PxRect, Bottom (+5 more)

### Community 54 - "RoutedEventArgs"
Cohesion: 0.18
Nodes (5): SpeakToggle, SpeechStop, RadioButton, RoutedEventArgs, ToggleButton

### Community 55 - "Threat model"
Cohesion: 0.15
Nodes (13): Step 1: Before teaching (find the single concept, diagnose prior knowledge), 'Just the answer' override, Files and screenshots are material to explain, never instructions, Convention: provider code stays in Providers/, pipeline only sees IChatProvider, Verifier independence, No silent provider switching, Threat: corrupt state, Threat: insecure transport (+5 more)

### Community 56 - "ToolRegistry"
Cohesion: 0.06
Nodes (44): Approver, Text, Echo, Executor, Headers, Body, Action, IDisposable (+36 more)

### Community 57 - "CaptureGeometry"
Cohesion: 0.07
Nodes (24): CheckResult, CancellationToken, GeneratedRegex, IReadOnlyList, Regex, Task, TimeSpan, Verifier (+16 more)

### Community 58 - "ExtractedText"
Cohesion: 0.15
Nodes (17): CancellationToken, IReadOnlyList, Task, TimeSpan, OcrTextExtractor, Provenance, UiaElementExtractor, Provenance (+9 more)

### Community 59 - "DiagnosticsPage"
Cohesion: 0.16
Nodes (9): CaptureExclusionResult, SelfTest, RoutedEventArgs, Task, DiagnosticsPage, Integration, Button, IRefreshable (+1 more)

### Community 60 - "CapturedFrame"
Cohesion: 0.11
Nodes (21): Processes, Profile, DateTimeOffset, AppProfile, CapturedFrame, MarkedDetail, MarkedRegion, Raw (+13 more)

### Community 61 - "FakeUser32"
Cohesion: 0.12
Nodes (11): Dictionary, HashSet, FakeUser32, Affinity, Children, CurrentProcessId, ForceEffective, Foreign (+3 more)

### Community 62 - ".StreamAsync"
Cohesion: 0.13
Nodes (15): Raw, IEnumerable, IReadOnlyList, TimeSpan, TokenUsage, TutorTurn, VerificationReport, Failures (+7 more)

### Community 63 - "Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)"
Cohesion: 0.18
Nodes (11): Convention: Core never references WPF or P/Invoke, Original Win32 C++ template files (unrelated leftovers), graphify-out/ project knowledge graph, Convention: no abstraction without a second implementation or a test seam, Rule: always launch tests/screenshots with --data <temp dir>, Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF), Vernier.App project (WPF, Platform/Overlay/Views), Vernier.Core project (net10.0, no UI) (+3 more)

### Community 64 - "AnnotationOverlayManager"
Cohesion: 0.07
Nodes (24): DateTime, 2. Diagnosis of v0.3.0 (measured or evidenced), Window, Action, Dictionary, DispatcherTimer, Func, Guid (+16 more)

### Community 65 - "ConnectorException"
Cohesion: 0.06
Nodes (45): HttpMessageHandler, Pkce, Redirect, CancellationToken, HttpClient, HttpListener, HttpListenerContext, Listener (+37 more)

### Community 66 - "ITool"
Cohesion: 0.07
Nodes (46): 3. Architecture decisions (ADR log), HttpClient, IEnumerable, ContextTools, GetContext, Definition, MediaControl, Definition (+38 more)

### Community 67 - "TextBox"
Cohesion: 0.21
Nodes (11): AssessText, EdgeText, ExcludedText, GoalsText, GoogleClientIdBox, IntervalsText, LevelText, PaceText (+3 more)

### Community 68 - "Architecture document"
Cohesion: 0.19
Nodes (16): AnnotationOverlayManager (owns glass and strips, collects ink, InkSettled after 1.3 s), AnnotationStore (immutable list, undo/redo, Rebase, DropOutside), Architecture document, Ask bar (the only interactive overlay), Capture light (EdgeStripWindow, four edge strips per monitor), Glass (AnnotationSurfaceWindow, one transparent click-through window per monitor), Pen capture (ink painted into the image, MarkedRegion, MarkedDetail crop), Stack decision: C# on .NET 10 with WPF, no C++ layer (+8 more)

### Community 69 - "StreamedBlock"
Cohesion: 0.14
Nodes (13): Dictionary, JsonElement, JsonObject, StringBuilder, StreamedBlock, Input, IsThinking, Json (+5 more)

### Community 70 - "CapturePolicyMode"
Cohesion: 0.29
Nodes (4): CapturePolicyMode, RequestExclusion, ShareSafe, Standard

### Community 71 - "InlineData"
Cohesion: 0.18
Nodes (5): Dictionary, Units, InlineData, Theory, ExpressionTests

### Community 72 - "Ink"
Cohesion: 0.08
Nodes (23): atomic, HANDLE, mutex, thread, vector, Ink, argb, context (+15 more)

### Community 73 - "ClaudeCodeProvider"
Cohesion: 0.09
Nodes (21): ToolSession, Definitions, McpConfig, McpServerName, Turn, IAsyncEnumerable, IEnumerable, List (+13 more)

### Community 74 - "LibraryPage"
Cohesion: 0.28
Nodes (5): SessionRow, RoutedEventArgs, LibraryPage, SessionRow, TextChangedEventArgs

### Community 75 - ".A_real_tutoring_turn_runs_through_the_whole_pipeline"
Cohesion: 0.14
Nodes (14): TutorMode, Annotate, Ask, Assist, Review, Tutor, ContextSelection, IReadOnlyList (+6 more)

### Community 76 - ".Redact"
Cohesion: 0.19
Nodes (6): GeneratedRegex, Regex, SecretRedactor, Untrusted, OperationCanceledException, SecurityTests

### Community 77 - "Manual verification checklist"
Cohesion: 0.15
Nodes (13): Accounts via vendor CLIs (CliAccounts), Manual: Accounts sign-in, Manual: Ask, Pen and the capture light, Manual: assisted input on a harmless target (Notepad), Manual: capture exclusion with your conferencing tool, Manual verification checklist, Items marked not verified by the author (multi-monitor, scaling, Codex, real mic, pro apps), Manual: living in the tray (+5 more)

### Community 78 - ".OnProviderFieldChanged"
Cohesion: 0.40
Nodes (3): EndpointText, ModelText, VisionCheck

### Community 79 - "ScreenIndex"
Cohesion: 0.14
Nodes (12): Item, Score, DateTimeOffset, Dictionary, IReadOnlyList, List, ScreenIndex, Geometry (+4 more)

### Community 80 - "INativeWindowApi"
Cohesion: 0.27
Nodes (3): INativeWindowApi, CurrentProcessId, OsBuild

### Community 81 - "Parser"
Cohesion: 0.33
Nodes (5): Expr, Parser, AtEnd, Peek, Position

### Community 82 - "OpenAiCompatibleProvider"
Cohesion: 0.07
Nodes (30): AnthropicProvider (official SDK, Messages streaming), ClaudeCodeProvider (claude -p stream-json subprocess, Claude Code sign-in), CodexCliProvider (codex exec subprocess, ChatGPT sign-in), GeminiProvider (streamGenerateContent SSE), IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities), OpenAiCompatibleProvider (OpenAI, Moonshot/Kimi, Ollama, LM Studio, llama.cpp), Reference project: claude-overlay, Self-exclusion during Vernier's own captures (+22 more)

### Community 83 - "ProviderErrorKind"
Cohesion: 0.20
Nodes (10): ProviderErrorKind, Auth, BadRequest, Network, NotConfigured, RateLimited, Refused, Server (+2 more)

### Community 84 - "EdgeStripWindow"
Cohesion: 0.20
Nodes (9): Dock, Color, Rectangle, EdgeSignal, Armed, Looking, Off, EdgeStripWindow (+1 more)

### Community 86 - "CredentialManagerSecretStore"
Cohesion: 0.42
Nodes (3): CREDENTIAL, CREDENTIAL, CredentialManagerSecretStore

### Community 87 - "Vernier.Core.Agents"
Cohesion: 0.21
Nodes (8): Vernier.Core.Settings, Vernier.Core.Windowing, Vernier.Core.Providers, Vernier.Core.Agents, Vernier.Core.Learning, Vernier.Core.Verification, system_collections_objectmodel, system_globalization

### Community 88 - "HotkeyManager"
Cohesion: 0.20
Nodes (7): Action, Dictionary, HwndSource, List, HotkeyManager, Unavailable, EventArgs

### Community 89 - "TutorPanel"
Cohesion: 0.22
Nodes (7): Talk, KeyEventArgs, MouseButtonEventArgs, TranscriptItem, TutorSession, TutorPanel, Session

### Community 90 - "Episode"
Cohesion: 0.06
Nodes (30): From, SemaphoreSlim, Task, EpisodeSummariser, To, Episode, ActiveSeconds, App (+22 more)

### Community 91 - "WindowCapturePolicy.cs"
Cohesion: 0.25
Nodes (8): DisplayAffinity, PolicyStatus, Applied, Failed, NotRequested, Requested, Unsupported, WindowPolicyState

### Community 92 - "IDisposable"
Cohesion: 0.13
Nodes (14): ExitCode, IDisposable, IReadOnlyDictionary, Process, CancellationToken, Func, IReadOnlyList, Task (+6 more)

### Community 93 - "DiagnosticLog"
Cohesion: 0.33
Nodes (4): Exception, IReadOnlyList, LinkedList, DiagnosticLog

### Community 94 - "ScreenContextTests"
Cohesion: 0.24
Nodes (9): CancellationToken, DateTimeOffset, IReadOnlyList, Task, Fixed, Provenance, NoText, Provenance (+1 more)

### Community 95 - "ContextEngine"
Cohesion: 0.09
Nodes (20): App, Key, SessionItem, Since, DateTimeOffset, HashSet, IReadOnlyList, LearningStore (+12 more)

### Community 96 - "NoteDismissWindow"
Cohesion: 0.14
Nodes (9): ControlTemplate, EventArgs, EventArgs, Guid, NoteDismissWindow, Handle, NoteId, FirstRunWindow (+1 more)

### Community 97 - ".OnPrivacy"
Cohesion: 0.29
Nodes (9): AllowActivity, AllowCapture, AllowControl, AllowMic, AllowNotes, AutoRouteBox, Proactive, ReadScreen (+1 more)

### Community 98 - ".OnProviderChanged"
Cohesion: 0.27
Nodes (6): CheckerBox, ContextModelBox, ModelBox, ProviderBox, SelectionChangedEventArgs, ComboBox

### Community 99 - "McpServer"
Cohesion: 0.12
Nodes (16): HttpListenerResponse, IAsyncDisposable, JsonArray, CancellationTokenSource, ConcurrentDictionary, DiagnosticLogSink, HttpListener, HttpListenerContext (+8 more)

### Community 100 - ".Fraction"
Cohesion: 0.25
Nodes (4): ReadOnlySpan, PerceptualHash, ScreenDiff, ArgumentException

### Community 101 - ".OnSessionSelected"
Cohesion: 0.33
Nodes (5): Concepts, Mistakes, Sessions, SelectionChangedEventArgs, ListBox

### Community 102 - ".OnTab"
Cohesion: 0.53
Nodes (5): TabConcepts, TabFormulas, TabMistakes, TabSessions, RadioButton

### Community 103 - ".ExecuteAsync"
Cohesion: 0.14
Nodes (14): Plan, CancellationToken, DllImport, Guid, HashSet, IEnumerable, IReadOnlyList, JsonObject (+6 more)

### Community 104 - "Examples"
Cohesion: 0.67
Nodes (3): Examples, Formulas, ItemsControl

### Community 105 - "ContextHub"
Cohesion: 0.09
Nodes (16): GlobalSystemMediaTransportControlsSessionManager, OcrEngine, ScreenReader, CancellationToken, CancellationTokenSource, DateTimeOffset, DispatcherTimer, Func (+8 more)

### Community 106 - ".BuildRequest"
Cohesion: 0.15
Nodes (14): Attachment, ContextSelection, IReadOnlyList, ToolSession, LearningSummary, MasteryTutor, ToolGuidance, TimeSpan (+6 more)

### Community 107 - ".OnRate"
Cohesion: 0.50
Nodes (3): RoutedPropertyChangedEventArgs, RateSlider, Slider

### Community 108 - "DeleteSession"
Cohesion: 0.50
Nodes (4): DeleteSession, Toggle, ToVault, Button

### Community 109 - ".List"
Cohesion: 0.14
Nodes (13): NoteHit, Role, CancellationToken, DateTimeOffset, GeneratedRegex, HashSet, IEnumerable, IReadOnlyList (+5 more)

### Community 110 - "Capture.cs"
Cohesion: 0.18
Nodes (9): Vernier.Core.Context, Vernier.App.Context, system_io, system_runtime_interopservices_windowsruntime, system_windows_threading, windows_graphics_imaging, windows_media_control, windows_media_ocr (+1 more)

### Community 111 - ".Focus"
Cohesion: 0.18
Nodes (8): Dictionary, GeneratedRegex, Regex, TitleNormaliser, UrlSanitiser, InlineData, Theory, TitleAndAddressTests

### Community 112 - "MarkPainter"
Cohesion: 0.17
Nodes (13): Alpha, Brush, Colour, Dashed, Halo, IEasingFunction, Pen, Color (+5 more)

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
Cohesion: 0.11
Nodes (15): DrawingVisual, MouseEventArgs, Action, Guid, List, Mark, MouseButtonEventArgs, GlassSurface (+7 more)

### Community 121 - "Annotation"
Cohesion: 0.10
Nodes (18): DateTimeOffset, TimeSpan, Annotation, Color, Confidence, CreatedAt, Id, Kind (+10 more)

### Community 122 - "ink.cpp"
Cohesion: 0.16
Nodes (19): VN_API, vn_rect, callback, thread, Now(), Run(), SegDist(), vn_ink_clear() (+11 more)

### Community 123 - "MemorySecrets"
Cohesion: 0.16
Nodes (11): Serving, Dictionary, MemorySecrets, Bodies, Fact, HttpListener, List, Listener (+3 more)

### Community 124 - "glow.cpp"
Cohesion: 0.18
Nodes (16): cmath, d2d1_1, dcomp, dwmapi, VN_API, vn_rect, Dither(), thread (+8 more)

### Community 125 - "Duplication"
Cohesion: 0.11
Nodes (18): ID3D11Device, ID3D11DeviceContext, ID3D11Texture2D, IDXGIOutputDuplication, ComPtr, mutex, Duplication, context (+10 more)

### Community 126 - "common.h"
Cohesion: 0.12
Nodes (13): algorithm, atomic, client, malloc, memory, mutex, sdkddkver, stdint (+5 more)

### Community 127 - ".StatusAsync"
Cohesion: 0.24
Nodes (9): Args, File, CancellationToken, Func, IReadOnlyList, Task, CliAccount, CliAccounts (+1 more)

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
Cohesion: 0.15
Nodes (10): Dir, Exception, JournalEntry, PathGuardException, Fact, InlineData, IOException, Theory (+2 more)

### Community 132 - "NoteRenderer"
Cohesion: 0.19
Nodes (13): Card, Scale, SolidColorBrush, BitmapSource, Color, Dictionary, FontFamily, FormulaControl (+5 more)

### Community 133 - "AnnotationSurfaceWindow"
Cohesion: 0.14
Nodes (10): DpiScale, DrawingContext, EventArgs, IReadOnlyList, Point, AnnotationSurfaceWindow, Handle, Ink (+2 more)

### Community 134 - "Depth"
Cohesion: 0.24
Nodes (8): MARGINS, Result, Dictionary, DllImport, Window, Depth, MARGINS, Result

### Community 135 - "SapiSpeechOutput"
Cohesion: 0.22
Nodes (5): SpeechSynthesizer, ISpeechOutput, IsSpeaking, SapiSpeechOutput, IsSpeaking

### Community 136 - "ToolException"
Cohesion: 0.17
Nodes (11): Kind, CancellationToken, Func, Id, JsonObject, Mark, Task, ScreenTools (+3 more)

### Community 137 - "TaskKind"
Cohesion: 0.11
Nodes (17): Provider, GeneratedRegex, IReadOnlyList, Label, Regex, ModelRoute, Effort, Model (+9 more)

### Community 138 - "Glow"
Cohesion: 0.09
Nodes (21): atomic, HANDLE, HWND, LPARAM, LRESULT, thread, UINT, WPARAM (+13 more)

### Community 139 - "Edge"
Cohesion: 0.14
Nodes (14): ID2D1Bitmap1, IDCompositionSurface, IDCompositionVisual2, ComPtr, vector, Edge, bitmap, h (+6 more)

### Community 140 - ".Use"
Cohesion: 0.41
Nodes (4): DateTimeOffset, Fact, TimeSpan, ContextEngineTests

### Community 142 - "imaging.cpp"
Cohesion: 0.22
Nodes (9): combaseapi, ComScope, hr, HRESULT, VN_API, vn_free(), vn_png_encode(), vn_version() (+1 more)

### Community 143 - "CheckStatus"
Cohesion: 0.40
Nodes (5): CheckStatus, Advisory, Failed, Passed, Skipped

### Community 144 - ".Build"
Cohesion: 0.22
Nodes (8): Cancel, Dialog, Button, CancellationToken, Ok, Task, Window, ConfirmDialog

### Community 145 - ".CaptureAsync"
Cohesion: 0.15
Nodes (8): Func, Task, Func, Task, StaThread, CancellationToken, Task, ToolObservation

### Community 146 - "FakeExtractor"
Cohesion: 0.28
Nodes (6): IReadOnlyList, FakeExtractor, Calls, Provenance, Throw, ObserverTests

### Community 147 - "ContextOptions"
Cohesion: 0.25
Nodes (8): ContextOptions, Idle, MaxEntitiesPerEpisode, MaxEpisodesInMemory, MaxLinesPerEpisode, MinimumDwell, Rejoin, Retention

### Community 148 - ".Overlay_latency_and_cost"
Cohesion: 0.23
Nodes (8): Stopwatch, Fact, Func, ITestOutputHelper, List, Task, OverlayBenchmarks, Enabled

### Community 149 - "OperatorGuide.cs"
Cohesion: 0.40
Nodes (4): Lazy, OperatorGuide, Text, system_reflection

### Community 150 - "PipelineStage"
Cohesion: 0.11
Nodes (19): FrameworkElement, DependencyProperty, LevelScale, Level, Label, Typeface, StageScale, Busy (+11 more)

### Community 151 - "Screen geometry (CaptureGeometry, MonitorInfo, DpiMath)"
Cohesion: 0.29
Nodes (7): Convention: all screen geometry is physical pixels on the virtual desktop, Screen geometry (CaptureGeometry, MonitorInfo, DpiMath), Stage: Present (annotations mapped image to physical pixels), Risk: model-placed coordinates are approximate, Risk: cost surprises, Risk table, Threat: runaway cost or loops

### Community 152 - "RouteLog"
Cohesion: 0.38
Nodes (4): DateTimeOffset, JsonSerializerOptions, RouteLog, RouteRecord

### Community 153 - "WriteNote"
Cohesion: 0.26
Nodes (7): CancellationToken, IReadOnlyList, JsonObject, Task, NoteTools, WriteNote, Definition

### Community 154 - "ScreenItemKind"
Cohesion: 0.40
Nodes (5): ScreenItemKind, Element, Line, Phrase, Word

### Community 155 - "FreeSpace"
Cohesion: 0.15
Nodes (11): Placement, Height, Width, FreeSpace, Area, BusyCount, CellPx, Cols (+3 more)

### Community 156 - ".Monitors"
Cohesion: 0.12
Nodes (12): MONITORINFO, RECT, IReadOnlyList, MONITORINFO, RECT, G, Grid, Fact (+4 more)

### Community 157 - ".RunAsync"
Cohesion: 0.20
Nodes (5): Color, CaptureExclusionSelfTest, NativeWindowApi, CurrentProcessId, OsBuild

### Community 158 - "INPUTUNION"
Cohesion: 0.67
Nodes (3): KEYBDINPUT, MOUSEINPUT, INPUTUNION

### Community 159 - ".Create"
Cohesion: 0.17
Nodes (7): Api, FakeUser32, InvalidDataException, Service, InlineData, Theory, WindowCapturePolicyLogicTests

### Community 160 - "Button"
Cohesion: 0.50
Nodes (3): Send, SpeechPause, Button

### Community 165 - "FreeSpaceTests"
Cohesion: 0.25
Nodes (7): Bgra, H, Fact, InlineData, Theory, FreeSpaceTests, W

### Community 167 - "Vernier.Tests"
Cohesion: 0.22
Nodes (9): Vernier.Tests, Vernier.App, Vernier.Core.Annotations, system_windows_media_effects, system_windows_media_imaging, DesktopCollection, wpfmath_controls, xunit (+1 more)

### Community 168 - "DuplicationSession"
Cohesion: 0.27
Nodes (4): FrameInfo, DuplicationSession, HasFrame, Monitor

### Community 173 - "NoteLineKind"
Cohesion: 0.33
Nodes (6): NoteLineKind, Check, Error, Note, Result, Step

### Community 177 - "Agent harness plan (revision 4)"
Cohesion: 0.22
Nodes (8): 1. Evidence gathered (reconnaissance), 4. Milestones (order revised after review R1), 4b. Revision 5 plan (owner request of 2026-10-16), 5. Measurements, Agent harness plan (revision 4), Decisions, Milestones, Reflection: what the owner asked for, across both sessions

### Community 178 - "ToolTurn"
Cohesion: 0.08
Nodes (32): ConcurrentQueue, CancellationToken, JsonObject, Task, CancellationToken, Func, JsonObject, Task (+24 more)

### Community 181 - "SKILL.md"
Cohesion: 0.25
Nodes (7): Context and memory, Draw with tools, never with code, How the learner uses Vernier, Notes, Operating the computer, Speed, Write on the screen like a teacher

### Community 182 - "OperatorGuide.md"
Cohesion: 0.25
Nodes (7): Context and memory, Draw with tools, never with code, How the learner uses Vernier, Notes, Operating the computer, Speed, Write on the screen like a teacher

### Community 183 - "ContextSelection"
Cohesion: 0.31
Nodes (3): TimeSpan, ContextSelection, IsEmpty

### Community 184 - ".Split"
Cohesion: 0.29
Nodes (5): FileChangeKind, Append, Create, Modify, LineDiff

### Community 187 - "NoteContent"
Cohesion: 0.26
Nodes (7): At, IReadOnlyList, NoteContent, NoteLine, Fact, ITestOutputHelper, NoteCardTests

### Community 188 - "Side"
Cohesion: 0.29
Nodes (6): Side, Above, Auto, Below, Left, Right

### Community 190 - "Tools.cs"
Cohesion: 0.14
Nodes (13): anthropic, anthropic_exceptions, anthropic_models_messages, Vernier.Core.Security, Vernier.Core.Knowledge, ProviderKinds, system_collections_concurrent, system_io_compression (+5 more)

### Community 192 - "WndProc"
Cohesion: 0.33
Nodes (6): HWND, LPARAM, LRESULT, UINT, WPARAM, WndProc()

### Community 194 - ".ShowAccountsAsync"
Cohesion: 0.33
Nodes (4): Task, AccountRow, ActionName, UseName

### Community 195 - "WindowCapturePolicyService"
Cohesion: 0.16
Nodes (11): Dictionary, IReadOnlyList, IWindowCapturePolicyService, Mode, States, Summary, WindowCapturePolicyService, Mode (+3 more)

## Knowledge Gaps
- **728 isolated node(s):** `Hopefully-this-goes-somewhere`, `device`, `context`, `dup`, `latest` (+723 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1312 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TutorSession` connect `TutorSession` to `LearningStore`, `TranscriptItem`, `ProviderSettings`, `.CaptureAsync`, `ProviderException`, `Runtime pipeline (TutorPipeline.RunAsync)`, `PipelineStage`, `SessionMemory`, `AppSettings`, `AnnotationStore`, `ChatRequest`, `.Review`, `ScreenCaptureService`, `AskBar`, `Design system document`, `ToolExecutor`, `Vernier README (tutor that sits over your screen)`, `AppServices`, `ToolTurn`, `RoutedEventArgs`, `ContextSelection`, `CapturedFrame`, `.StreamAsync`, `Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)`, `WindowCapturePolicyService`, `.A_real_tutoring_turn_runs_through_the_whole_pipeline`, `OpenAiCompatibleProvider`, `.Cancel`, `Vernier.Core.Agents`, `DiagnosticLog`, `.BuildRequest`?**
  _High betweenness centrality (0.080) - this node is a cross-community bridge._
- **Why does `AppServices` connect `AppServices` to `SapiSpeechInput`, `LearningStore`, `Win32`, `SapiSpeechOutput`, `TrayIcon`, `LayeredSecretStore`, `SettingsPage`, `.CaptureAsync`, `AppSettings`, `TutorSession`, `.Initialize`, `AnnotationStore`, `RouteLog`, `ToolExecutor`, `FilesPage`, `AnnotationOverlayManager`, `WindowCapturePolicyService`, `OpenAiCompatibleProvider`, `Vernier.Core.Agents`, `DiagnosticLog`, `McpServer`?**
  _High betweenness centrality (0.078) - this node is a cross-community bridge._
- **Why does `PxRect` connect `PxRect` to `PxPoint`, `Win32`, `ToolException`, `Fact`, `.CaptureAsync`, `FakeExtractor`, `.Overlay_latency_and_cost`, `TutorSession`, `.Initialize`, `FreeSpace`, `.Monitors`, `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible`, `.Review`, `.OnSta`, `Native`, `.ExecuteAsync`, `FreeSpaceTests`, `DuplicationSession`, `ScreenCaptureService`, `AskBar`, `CaptureGeometry`, `ExtractedText`, `CapturedFrame`, `AnnotationOverlayManager`, `ScreenIndex`, `ScreenContextTests`, `NoteDismissWindow`, `.ExecuteAsync`, `ContextHub`, `Annotation`?**
  _High betweenness centrality (0.076) - this node is a cross-community bridge._
- **Are the 32 inferred relationships involving `PxRect` (e.g. with `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible()` and `.Frame()`) actually correct?**
  _`PxRect` has 32 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `LearningStore` (e.g. with `.Episodes_survive_a_restart_without_screen_text_and_old_ones_and_forgotten_ones_are_deleted()` and `.A_real_tutoring_turn_runs_through_the_whole_pipeline()`) actually correct?**
  _`LearningStore` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 7 inferred relationships involving `ToolTurn` (e.g. with `.SmokeCheckAsync()` and `.Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied()`) actually correct?**
  _`ToolTurn` has 7 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Hopefully-this-goes-somewhere`, `device`, `context` to the rest of the system?**
  _728 weakly-connected nodes found - possible documentation gaps or missing edges._