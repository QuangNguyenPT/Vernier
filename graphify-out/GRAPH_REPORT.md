# Graph Report - Hopefully-this-goes-somewhere  (2026-10-18)

## Corpus Check
- 133 files · ~150,941 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 2, .rc 1, .vcxproj 1)

## Summary
- 3755 nodes · 8874 edges · 197 communities (184 shown, 13 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 925 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `33c33754`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Exec
- system_text
- UserControl
- UserControl
- .User
- TutorStructured
- PxRect
- Win32
- Fact
- TrayIcon
- UserControl
- UserControl
- ReviewPage
- GraphifySource
- SettingsPage
- Vernier.Core.Annotations
- UserControl
- Security and privacy document
- ProviderException
- ChatRequest
- Runtime pipeline (TutorPipeline.RunAsync)
- TempDir
- SessionMemory
- AppSettings
- TutorSession
- .Initialize
- AnnotationStore
- .List
- Fact
- .The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible
- UserControl
- .Review
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
- FilesPage
- ComputerAction
- Fact
- RoutedEventArgs
- Threat model
- ToolRegistry
- CaptureGeometry
- ExtractedText
- DiagnosticsPage
- CapturedFrame
- FakeUser32
- PipelineStage
- Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)
- AnnotationOverlayManager
- ConnectorException
- ITool
- TextBox
- Architecture document
- .A_real_tutoring_turn_runs_through_the_whole_pipeline
- IWindowCapturePolicyService
- InlineData
- Ink
- ClaudeCodeProvider
- LibraryPage
- TutorRequest
- .Redact
- SettingsStore
- .OnProviderFieldChanged
- ScreenIndex
- INativeWindowApi
- Parser
- IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities)
- ProviderErrorKind
- LearningStore
- .Cancel
- CredentialManagerSecretStore
- Vernier.Core.Agents
- HotkeyManager
- TutorPanel
- Episode
- WindowCapturePolicy.cs
- .RunAsync
- DateTimeOffset
- ScreenContextTests
- ContextEngine
- .OnStartup
- .OnPrivacy
- .OnProviderChanged
- McpServer
- GeometryTests
- .OnSessionSelected
- .OnTab
- .ExecuteAsync
- Examples
- ContextHub
- .BuildRequest
- .OnRate
- DeleteSession
- ObsidianVault
- Vernier.Core.Geometry
- .Focus
- MarkPainter
- MediaState
- Search
- Hopefully-this-goes-somewhere
- Dark.xaml
- Light.xaml
- SessionsView
- GlassSurface
- Annotation
- ink.cpp
- MemorySecrets
- glow.cpp
- Duplication
- common.h
- .StatusAsync
- SapiSpeechInput
- ProviderSettings
- duplication.cpp
- KnowledgeStoreTests
- NoteRenderer
- AnnotationSurfaceWindow
- Depth
- SapiSpeechOutput
- ToolResult
- TaskKind
- Glow
- Edge
- .Use
- .Query
- imaging.cpp
- TitleNormaliser
- .Build
- .RunAsync
- FakeExtractor
- ContextOptions
- .ReviewAndRunAsync
- .OnPolicyChanged
- StageScale
- Screen geometry (CaptureGeometry, MonitorInfo, DpiMath)
- RouteLog
- WriteNote
- Task
- FreeSpace
- NativeTests
- .WindowBounds
- LearningStoreTests
- .Create
- Button
- Aurora.xaml
- Glacier.xaml
- Nebula.xaml
- FreeSpaceTests
- ToolRisk
- Vernier.Tests
- DuplicationSession
- NativeInk
- AnnotationKind
- SessionMemoryTests
- TutorSession.cs
- NoteContent
- TutorPipeline
- .Match
- .Options
- Agent harness plan (revision 4)
- .ExecuteAsync
- .Ink_is_kept_for_the_question_shown_only_briefly_and_removed_when_it_is_sent_or_taken_back
- ActionType
- SKILL.md
- OperatorGuide.md
- ContextSelection
- .Split
- PreviewTool
- Revision 2 status (version 0.2.0)
- .A_card_is_dark_whatever_is_behind_it_and_holds_the_written_lines
- Side
- CodeTool
- Vernier.Core.Knowledge
- IDisposable
- WndProc
- .TrackWindow
- .ShowAccountsAsync
- WindowCapturePolicyService
- ConceptsView

## God Nodes (most connected - your core abstractions)
1. `PxRect` - 100 edges
2. `TutorSession` - 77 edges
3. `LearningStore` - 76 edges
4. `ToolTurn` - 75 edges
5. `UserControl` - 70 edges
6. `Win32` - 61 edges
7. `AppServices` - 55 edges
8. `AnnotationOverlayManager` - 55 edges
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

## Communities (197 total, 13 thin omitted)

### Community 0 - ".Exec"
Cohesion: 0.24
Nodes (3): Cards, ExportDocument, SqliteTransaction

### Community 1 - "system_text"
Cohesion: 0.17
Nodes (17): anthropic, anthropic_exceptions, anthropic_models_messages, Vernier.Core.Tools, Vernier.Core.Settings, Vernier.Core.Security, Vernier.Core.Providers, Vernier.Core.Learning (+9 more)

### Community 2 - "UserControl"
Cohesion: 0.12
Nodes (23): Kind, Reason, Back, BackHost, CardMeta, CardView, Front, Grades (+15 more)

### Community 3 - "UserControl"
Cohesion: 0.12
Nodes (21): Available, Excluded, Expected, Missing, Observed, Path, Environment, Integrations (+13 more)

### Community 4 - ".User"
Cohesion: 0.18
Nodes (13): ChatMessage, ImageInput, Func, HttpRequestMessage, HttpResponseMessage, HttpStatusCode, List, Request (+5 more)

### Community 5 - "TutorStructured"
Cohesion: 0.07
Nodes (29): List, Claim, Expr, UnitsLhs, UnitsRhs, Value, FormulaNote, Derivation (+21 more)

### Community 6 - "PxRect"
Cohesion: 0.10
Nodes (17): IEnumerable, RECT, G, Grid, IReadOnlyList, DpiMath, MonitorInfo, OverlayPlacement (+9 more)

### Community 7 - "Win32"
Cohesion: 0.07
Nodes (21): POINT, 6. Review log (Agent B), KEYBDINPUT, LASTINPUTINFO, MonitorEnumProc, MOUSEINPUT, Func, DllImport (+13 more)

### Community 8 - "Fact"
Cohesion: 0.19
Nodes (16): Action, PipelineCallbacks, Stage, Text, Tool, ScriptedProvider, Capabilities, Delay (+8 more)

### Community 9 - "TrayIcon"
Cohesion: 0.06
Nodes (32): At, Dismissals, NOTIFYICONDATA, Action, DateTimeOffset, TimeSpan, Proactive, DllImport (+24 more)

### Community 10 - "UserControl"
Cohesion: 0.10
Nodes (28): Degree, Label, Links, AskAboutNote, DisconnectVault, EditInstruction, Explain, FileText (+20 more)

### Community 11 - "UserControl"
Cohesion: 0.09
Nodes (26): Checks, IsUser, Meta, Notices, ActionsBar, ActionsText, AskKey, Bar (+18 more)

### Community 12 - "ReviewPage"
Cohesion: 0.18
Nodes (10): Queue, ResolveButton, Reveal, KeyEventArgs, RoutedEventArgs, SelectionChangedEventArgs, ReviewPage, Button (+2 more)

### Community 13 - "GraphifySource"
Cohesion: 0.14
Nodes (14): Output, NodeRow, JsonElement, Ok, Task, GraphEdge, GraphifySource, Name (+6 more)

### Community 14 - "SettingsPage"
Cohesion: 0.12
Nodes (10): GoogleConnectButton, RemoveSample, TestButton, DateTimeOffset, Func, RoutedEventArgs, SettingsPage, Current (+2 more)

### Community 15 - "Vernier.Core.Annotations"
Cohesion: 0.17
Nodes (22): Vernier.App.Views, Vernier.Core.Windowing, Vernier.App, Vernier.Core.Annotations, microsoft_win32, system_io, system_windows, system_windows_automation (+14 more)

### Community 16 - "UserControl"
Cohesion: 0.08
Nodes (32): ActionLabel, ActionName, CanAct, Id, SignedIn, State, UseName, Accounts (+24 more)

### Community 17 - "Security and privacy document"
Cohesion: 0.21
Nodes (12): Security rules (non-negotiable), Window capture policy (IWindowCapturePolicyService over INativeWindowApi), API keys: environment variable, else Windows Credential Manager, Window capture exclusion (SetWindowDisplayAffinity WDA_EXCLUDEFROMCAPTURE on own windows), Consent gates enforced in code (AllowScreenCapture, AllowMicrophone, AllowSendNotesToRemote, AllowComputerControl), No telemetry, analytics or crash reports, Runs unelevated (asInvoker), Security and privacy document (+4 more)

### Community 18 - "ProviderException"
Cohesion: 0.11
Nodes (23): ChatChunk, ProviderException, IsTransient, Kind, RetryAfter, ToolCallNote, IAsyncEnumerable, CancellationToken (+15 more)

### Community 19 - "ChatRequest"
Cohesion: 0.09
Nodes (31): Message, CancellationToken, Func, IAsyncEnumerable, IReadOnlyList, JsonNode, Ok, Task (+23 more)

### Community 20 - "Runtime pipeline (TutorPipeline.RunAsync)"
Cohesion: 0.10
Nodes (26): Step 4: Check your own work (second route, units, signs, limiting cases), Do-not rules (no solution dumps, no vague praise, no invented file contents, no unopened citations), Hint escalation ladder (point, leading question, principle, show the step), Step 5: Finish with a mastery check (transfer question, mental model, what is still open), One right answer is not mastery, Mastery Tutor skill (/mastery-tutor), Misconception matters more than the slip, Subject notes (mathematics, physics/engineering, programming, sciences) (+18 more)

### Community 21 - "TempDir"
Cohesion: 0.12
Nodes (14): Dir, ScopedFiles / PathGuard (confined file access, Propose then Apply), Threat: path traversal / unintended file writes, IReadOnlyCollection, CancellationToken, IEnumerable, PathGuard, Root (+6 more)

### Community 22 - "SessionMemory"
Cohesion: 0.17
Nodes (10): DateTimeOffset, GeneratedRegex, IReadOnlyList, List, Regex, SessionMemory, Count, TurnNote (+2 more)

### Community 23 - "AppSettings"
Cohesion: 0.06
Nodes (36): ModelRoute, AppSettings, AcknowledgedObserveCost, ActiveProvider, ActiveProviderId, AllowActivityContext, AllowComputerControl, AllowMicrophone (+28 more)

### Community 24 - "TutorSession"
Cohesion: 0.05
Nodes (43): Grant, INotifyPropertyChanged, ObservableCollection, ProviderKey, CancellationTokenSource, IDisposable, IEnumerable, IReadOnlyList (+35 more)

### Community 25 - ".Initialize"
Cohesion: 0.15
Nodes (9): RawImage, Exception, IReadOnlyList, LinkedList, DiagnosticLog, Fact, ITestOutputHelper, LiveOperatorTests (+1 more)

### Community 26 - "AnnotationStore"
Cohesion: 0.16
Nodes (10): Guid, IReadOnlyList, List, AnnotationStore, CanRedo, CanUndo, Items, Visible (+2 more)

### Community 27 - ".List"
Cohesion: 0.06
Nodes (33): IAnthropicClient, MessageParam, CancellationToken, Dictionary, Exception, IAsyncEnumerable, IReadOnlyList, JsonElement (+25 more)

### Community 28 - "Fact"
Cohesion: 0.15
Nodes (7): JsonSerializerOptions, Regex, TutorOutput, Fact, SettingsTests, TutorOutputTests, VerifierTests

### Community 29 - ".The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible"
Cohesion: 0.08
Nodes (27): BaseUrl, ButtonBase, ComboBoxItem, Control, DependencyObject, EnumProc, Bodies, Button (+19 more)

### Community 30 - "UserControl"
Cohesion: 0.11
Nodes (21): Concept, Derivation, Description, Expression, Mastery.Level, Mastery.Reason, Meaning, Problem (+13 more)

### Community 31 - ".Review"
Cohesion: 0.18
Nodes (11): ProposedAction, Amount, Keys, Text, Type, Why, X, Y (+3 more)

### Community 32 - ".Parse"
Cohesion: 0.05
Nodes (40): InlineUIContainer, Inner, Next, RichTextBox, Run, DependencyProperty, FormulaControl, Paragraph (+32 more)

### Community 33 - ".Monitors"
Cohesion: 0.12
Nodes (17): MONITORINFO, R, IReadOnlyList, MONITORINFO, Dispatcher, DllImport, Fact, Func (+9 more)

### Community 34 - ".Assess"
Cohesion: 0.11
Nodes (18): Card, ConceptState, DateTimeOffset, IReadOnlyList, CardSchedule, Evidence, EvidenceKinds, MasteryAssessment (+10 more)

### Community 35 - "Hopefully-this-goes-somewhere.cpp"
Cohesion: 0.15
Nodes (16): ATOM, BOOL, HINSTANCE, About(), HWND, LPARAM, LRESULT, UINT (+8 more)

### Community 36 - "ResourceDictionary"
Cohesion: 0.12
Nodes (19): IsDropDownOpen, B, Box, C, PART_ContentHost, PART_Track, ResourceDictionary, S (+11 more)

### Community 37 - "Native"
Cohesion: 0.14
Nodes (7): LibraryImport, Rect, Lazy, FrameInfo, Native, Available, LastError

### Community 38 - ".ExecuteAsync"
Cohesion: 0.19
Nodes (9): INPUT, INPUTUNION, CancellationToken, SendInputDriver, INPUT, KEYBDINPUT, MOUSEINPUT, X (+1 more)

### Community 39 - "ScopedFiles"
Cohesion: 0.15
Nodes (12): JournalEntry, ConcurrentDictionary, List, FileChange, Diff, Summary, ScopedFiles, Guard (+4 more)

### Community 40 - "MainWindow"
Cohesion: 0.12
Nodes (11): ReviewNav, Dictionary, FrameworkElement, RadioButton, RoutedEventArgs, MainWindow, AskHotkey, Bar (+3 more)

### Community 41 - "Window"
Cohesion: 0.14
Nodes (17): GoalText, Lamp, NoticeBar, NoticeText, ObserveText, PageHost, PauseButton, PolicyText (+9 more)

### Community 42 - "ScreenCaptureService"
Cohesion: 0.18
Nodes (6): Int32Rect, BitmapSource, ScreenCaptureService, Height, Width, ScreenDiff

### Community 43 - "AskBar"
Cohesion: 0.06
Nodes (29): Border, Data, DockPanel, Exception, Action, Item, Button, FontFamily (+21 more)

### Community 44 - "Design system document"
Cohesion: 0.13
Nodes (21): Product shape (tray-resident, Ask and Pen only, yellow/blue rule, capture light on while looking), Motion: the capture wave is the one expressive motion, Design system document, Direction A: Instrument (v1, workspace-first), Direction B: Glass and two inks (chosen), Direction C: Companion cursor (Clicky model), Keyboard chords, On-screen ink colours (fixed, drawn over a 3 px dark halo) (+13 more)

### Community 45 - "Verification report"
Cohesion: 0.16
Nodes (14): Verification workflow (Evaluate, Correct, Confirm), Acceptance checklist (definition of done), Manual: capture exclusion with your conferencing tool, AppUiTests (real MainWindow in-process against a loopback stub), Verified: capture geometry maps image pixels back to screen (+/-1 px quantisation), Not tested: WGC, DXGI duplication, Snipping Tool, Game Bar, OBS, Zoom, Meet, Teams, remote desktop, ECC audit record (Evaluate, Correct, Confirm): 9 defects found and corrected, Implemented with known limits (Verifier scope, SAPI voice, no typeset maths, no PDF/Office, unsigned) (+6 more)

### Community 46 - "ToolTurn"
Cohesion: 0.08
Nodes (26): ConcurrentQueue, CancellationToken, CancellationTokenSource, ConcurrentDictionary, DateTimeOffset, IReadOnlyList, IReadOnlySet, JsonNode (+18 more)

### Community 47 - "Vernier README (tutor that sits over your screen)"
Cohesion: 0.11
Nodes (19): ObsidianVault (Markdown notes under Vernier/, append-not-replace), Cards CSV export (RFC 4180, Anki-importable), Data formats document, learning.db SQLite schema (user_version 2, FTS5 messages_fts), Obsidian notes layout (Concepts, Sessions, Mistake log under <vault>/Vernier/), Items marked not verified by the author (multi-monitor, scaling, Codex, real mic, pro apps), Local storage (%LOCALAPPDATA%\Vernier: learning.db, settings.json, logs, tmp), Pause (cancels request, pen, microphone, speech; blocks capture) (+11 more)

### Community 48 - "ProposedAnnotation"
Cohesion: 0.17
Nodes (12): ProposedAnnotation, Confidence, H, Kind, Label, Semantic, Target, W (+4 more)

### Community 49 - "Vernier.Tests.csproj"
Cohesion: 0.12
Nodes (14): net10.0, Anthropic (12.55.0), coverlet.collector (6.0.4), Microsoft.Data.Sqlite (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), System.Speech (10.0.12), WpfMath (2.1.0), xunit (2.9.3) (+6 more)

### Community 50 - "AppServices"
Cohesion: 0.06
Nodes (31): ContextHub, GoogleConnector, Action, Dispatcher, HttpClient, Id, List, Name (+23 more)

### Community 51 - "FilesPage"
Cohesion: 0.15
Nodes (12): Files, Nodes, AppSettings, CancellationTokenSource, IEnumerable, RoutedEventArgs, SelectionChangedEventArgs, Window (+4 more)

### Community 52 - "ComputerAction"
Cohesion: 0.12
Nodes (15): Decision, Executed, CancellationToken, Task, ComputerAction, IInputDriver, Func, ActionExecutor (+7 more)

### Community 53 - "Fact"
Cohesion: 0.27
Nodes (3): IdSequence, Fact, ScreenIndexTests

### Community 54 - "RoutedEventArgs"
Cohesion: 0.18
Nodes (5): SpeakToggle, SpeechStop, RadioButton, RoutedEventArgs, ToggleButton

### Community 55 - "Threat model"
Cohesion: 0.18
Nodes (11): Step 1: Before teaching (find the single concept, diagnose prior knowledge), 'Just the answer' override, Files and screenshots are material to explain, never instructions, Verifier independence, Threat: corrupt state, Threat: insecure transport, Threat: malicious CLI output, Threat model (+3 more)

### Community 56 - "ToolRegistry"
Cohesion: 0.06
Nodes (45): Approver, Text, Echo, Executor, Headers, CancellationToken, Task, TimeSpan (+37 more)

### Community 57 - "CaptureGeometry"
Cohesion: 0.09
Nodes (18): CheckResult, CheckStatus, Advisory, Failed, Passed, Skipped, CancellationToken, GeneratedRegex (+10 more)

### Community 58 - "ExtractedText"
Cohesion: 0.15
Nodes (17): CancellationToken, IReadOnlyList, Task, TimeSpan, OcrTextExtractor, Provenance, UiaElementExtractor, Provenance (+9 more)

### Community 59 - "DiagnosticsPage"
Cohesion: 0.16
Nodes (9): CaptureExclusionResult, SelfTest, RoutedEventArgs, Task, DiagnosticsPage, Integration, Button, IRefreshable (+1 more)

### Community 60 - "CapturedFrame"
Cohesion: 0.15
Nodes (17): DateTimeOffset, CapturedFrame, MarkedDetail, MarkedRegion, Raw, Thumb, WindowBox, ContextSnapshot (+9 more)

### Community 61 - "FakeUser32"
Cohesion: 0.12
Nodes (11): Dictionary, HashSet, FakeUser32, Affinity, Children, CurrentProcessId, ForceEffective, Foreign (+3 more)

### Community 62 - "PipelineStage"
Cohesion: 0.11
Nodes (19): IEnumerable, IReadOnlyList, TimeSpan, TokenUsage, PipelineStage, Confirm, Correct, Interpret (+11 more)

### Community 63 - "Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)"
Cohesion: 0.18
Nodes (11): Convention: Core never references WPF or P/Invoke, Original Win32 C++ template files (unrelated leftovers), graphify-out/ project knowledge graph, Convention: no abstraction without a second implementation or a test seam, Rule: always launch tests/screenshots with --data <temp dir>, Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF), Vernier.App project (WPF, Platform/Overlay/Views), Vernier.Core project (net10.0, no UI) (+3 more)

### Community 64 - "AnnotationOverlayManager"
Cohesion: 0.06
Nodes (29): DateTime, Dock, Action, Color, DispatcherTimer, EventArgs, Func, IReadOnlyList (+21 more)

### Community 65 - "ConnectorException"
Cohesion: 0.05
Nodes (48): HttpMessageHandler, Pkce, Redirect, CancellationToken, HttpClient, HttpListener, HttpListenerContext, Listener (+40 more)

### Community 66 - "ITool"
Cohesion: 0.08
Nodes (40): 3. Architecture decisions (ADR log), HttpClient, IEnumerable, ContextTools, GetContext, Definition, MediaControl, Definition (+32 more)

### Community 67 - "TextBox"
Cohesion: 0.21
Nodes (11): AssessText, EdgeText, ExcludedText, GoalsText, GoogleClientIdBox, IntervalsText, LevelText, PaceText (+3 more)

### Community 68 - "Architecture document"
Cohesion: 0.19
Nodes (16): AnnotationOverlayManager (owns glass and strips, collects ink, InkSettled after 1.3 s), AnnotationStore (immutable list, undo/redo, Rebase, DropOutside), Architecture document, Ask bar (the only interactive overlay), Assisted input / computer use (ActionPolicy.Review, preview, approval, ActionExecutor, SendInputDriver), Capture light (EdgeStripWindow, four edge strips per monitor), Glass (AnnotationSurfaceWindow, one transparent click-through window per monitor), Stack decision: C# on .NET 10 with WPF, no C++ layer (+8 more)

### Community 69 - ".A_real_tutoring_turn_runs_through_the_whole_pipeline"
Cohesion: 0.36
Nodes (5): Fact, Task, LiveClaudeCodeTests, Enabled, RealGraphifyTests

### Community 70 - "IWindowCapturePolicyService"
Cohesion: 0.19
Nodes (9): CapturePolicyMode, RequestExclusion, ShareSafe, Standard, IReadOnlyList, IWindowCapturePolicyService, Mode, States (+1 more)

### Community 71 - "InlineData"
Cohesion: 0.18
Nodes (5): Dictionary, Units, InlineData, Theory, ExpressionTests

### Community 72 - "Ink"
Cohesion: 0.08
Nodes (23): atomic, HANDLE, mutex, thread, vector, Ink, argb, context (+15 more)

### Community 73 - "ClaudeCodeProvider"
Cohesion: 0.11
Nodes (16): IAsyncEnumerable, IEnumerable, List, TimeSpan, ClaudeCodeProvider, Capabilities, DisplayName, HasWarm (+8 more)

### Community 74 - "LibraryPage"
Cohesion: 0.29
Nodes (5): SessionRow, RoutedEventArgs, LibraryPage, SessionRow, TextChangedEventArgs

### Community 75 - "TutorRequest"
Cohesion: 0.22
Nodes (9): TutorMode, Annotate, Ask, Assist, Review, Tutor, ContextSelection, IReadOnlyList (+1 more)

### Community 76 - ".Redact"
Cohesion: 0.29
Nodes (4): GeneratedRegex, Regex, SecretRedactor, Untrusted

### Community 77 - "SettingsStore"
Cohesion: 0.22
Nodes (5): JsonSerializerOptions, List, SettingsStore, LoadWarning, Path

### Community 78 - ".OnProviderFieldChanged"
Cohesion: 0.40
Nodes (3): EndpointText, ModelText, VisionCheck

### Community 79 - "ScreenIndex"
Cohesion: 0.10
Nodes (22): Item, Kind, Score, Id, Mark, DateTimeOffset, Dictionary, IReadOnlyList (+14 more)

### Community 80 - "INativeWindowApi"
Cohesion: 0.27
Nodes (3): INativeWindowApi, CurrentProcessId, OsBuild

### Community 81 - "Parser"
Cohesion: 0.33
Nodes (5): Expr, Parser, AtEnd, Peek, Position

### Community 82 - "IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities)"
Cohesion: 0.15
Nodes (14): Convention: provider code stays in Providers/, pipeline only sees IChatProvider, Accounts via vendor CLIs (CliAccounts), AnthropicProvider (official SDK, Messages streaming), ClaudeCodeProvider (claude -p stream-json subprocess, Claude Code sign-in), CodexCliProvider (codex exec subprocess, ChatGPT sign-in), GeminiProvider (streamGenerateContent SSE), IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities), OpenAiCompatibleProvider (OpenAI, Moonshot/Kimi, Ollama, LM Studio, llama.cpp) (+6 more)

### Community 83 - "ProviderErrorKind"
Cohesion: 0.20
Nodes (10): ProviderErrorKind, Auth, BadRequest, Network, NotConfigured, RateLimited, Refused, Server (+2 more)

### Community 84 - "LearningStore"
Cohesion: 0.10
Nodes (10): SqliteConnection, JsonSerializerOptions, LearningStore, BackupDirectory, HasSampleData, IsEmpty, LatestVersion, Path (+2 more)

### Community 86 - "CredentialManagerSecretStore"
Cohesion: 0.27
Nodes (3): CREDENTIAL, CREDENTIAL, CredentialManagerSecretStore

### Community 87 - "Vernier.Core.Agents"
Cohesion: 0.15
Nodes (9): Vernier.Core.Agents, Vernier.Core.Verification, Lazy, OperatorGuide, Text, system_globalization, system_reflection, system_text_json_serialization (+1 more)

### Community 88 - "HotkeyManager"
Cohesion: 0.20
Nodes (7): Action, Dictionary, HwndSource, List, HotkeyManager, Unavailable, EventArgs

### Community 89 - "TutorPanel"
Cohesion: 0.22
Nodes (7): Talk, KeyEventArgs, MouseButtonEventArgs, TranscriptItem, TutorSession, TutorPanel, Session

### Community 90 - "Episode"
Cohesion: 0.07
Nodes (26): From, SemaphoreSlim, Task, EpisodeSummariser, To, Episode, ActiveSeconds, App (+18 more)

### Community 91 - "WindowCapturePolicy.cs"
Cohesion: 0.25
Nodes (8): DisplayAffinity, PolicyStatus, Applied, Failed, NotRequested, Requested, Unsupported, WindowPolicyState

### Community 92 - ".RunAsync"
Cohesion: 0.14
Nodes (13): ExitCode, IReadOnlyDictionary, Process, CancellationToken, Func, IReadOnlyList, Task, ClaudeCodeLocator (+5 more)

### Community 93 - "DateTimeOffset"
Cohesion: 0.19
Nodes (7): microsoft_data_sqlite, DateTimeOffset, Episode, IReadOnlyList, Misconception, SearchHit, SessionMessage

### Community 94 - "ScreenContextTests"
Cohesion: 0.24
Nodes (9): CancellationToken, DateTimeOffset, IReadOnlyList, Task, Fixed, Provenance, NoText, Provenance (+1 more)

### Community 95 - "ContextEngine"
Cohesion: 0.09
Nodes (19): App, Key, SessionItem, Since, DateTimeOffset, HashSet, IReadOnlyList, LearningStore (+11 more)

### Community 96 - ".OnStartup"
Cohesion: 0.18
Nodes (6): ExitEventArgs, Application, Task, App, FirstRunWindow, StartupEventArgs

### Community 97 - ".OnPrivacy"
Cohesion: 0.29
Nodes (9): AllowActivity, AllowCapture, AllowControl, AllowMic, AllowNotes, AutoRouteBox, Proactive, ReadScreen (+1 more)

### Community 98 - ".OnProviderChanged"
Cohesion: 0.27
Nodes (6): CheckerBox, ContextModelBox, ModelBox, ProviderBox, SelectionChangedEventArgs, ComboBox

### Community 99 - "McpServer"
Cohesion: 0.10
Nodes (19): HttpListenerResponse, IAsyncDisposable, JsonArray, Action, CancellationTokenSource, ConcurrentDictionary, DiagnosticLogSink, HttpListener (+11 more)

### Community 100 - "GeometryTests"
Cohesion: 0.22
Nodes (4): ReadOnlySpan, PerceptualHash, ArgumentException, GeometryTests

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
Cohesion: 0.30
Nodes (5): Examples, Formulas, FormulasView, ItemsControl, ScrollViewer

### Community 105 - "ContextHub"
Cohesion: 0.11
Nodes (16): GlobalSystemMediaTransportControlsSessionManager, OcrEngine, ScreenReader, CancellationToken, CancellationTokenSource, DateTimeOffset, DispatcherTimer, Func (+8 more)

### Community 106 - ".BuildRequest"
Cohesion: 0.16
Nodes (12): Attachment, ContextSelection, IReadOnlyList, ToolSession, LearningSummary, MasteryTutor, ToolGuidance, LearnerProfile (+4 more)

### Community 107 - ".OnRate"
Cohesion: 0.50
Nodes (3): RoutedPropertyChangedEventArgs, RateSlider, Slider

### Community 108 - "DeleteSession"
Cohesion: 0.50
Nodes (4): DeleteSession, Toggle, ToVault, Button

### Community 109 - "ObsidianVault"
Cohesion: 0.14
Nodes (13): NoteHit, Role, CancellationToken, DateTimeOffset, GeneratedRegex, HashSet, IEnumerable, IReadOnlyList (+5 more)

### Community 110 - "Vernier.Core.Geometry"
Cohesion: 0.18
Nodes (10): Vernier.App.Platform, Vernier.Core.Geometry, system_runtime_interopservices, system_runtime_interopservices_windowsruntime, system_speech_recognition, system_speech_synthesis, windows_graphics_imaging, windows_media_control (+2 more)

### Community 111 - ".Focus"
Cohesion: 0.25
Nodes (5): Dictionary, UrlSanitiser, InlineData, Theory, TitleAndAddressTests

### Community 112 - "MarkPainter"
Cohesion: 0.15
Nodes (16): Alpha, Brush, Colour, Dashed, DrawingVisual, Halo, Pen, Point (+8 more)

### Community 113 - "MediaState"
Cohesion: 0.18
Nodes (12): DateTimeOffset, IReadOnlyList, LinkedList, TimeSpan, ActivitySpan, Length, ActivityTimeline, Recent (+4 more)

### Community 114 - "Search"
Cohesion: 0.67
Nodes (3): Search, SessionText, TextBox

### Community 120 - "GlassSurface"
Cohesion: 0.12
Nodes (14): MouseEventArgs, Action, Guid, List, Mark, MouseButtonEventArgs, GlassSurface, MarkCount (+6 more)

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

### Community 129 - "ProviderSettings"
Cohesion: 0.10
Nodes (19): HashSet, HttpClient, GeminiProvider, Capabilities, DisplayName, Id, Model, ProviderSettings (+11 more)

### Community 130 - "duplication.cpp"
Cohesion: 0.21
Nodes (14): d3d11, dxgi1_2, CoveredFraction(), HRESULT, vector, VN_API, vn_rect, EnsureStaging() (+6 more)

### Community 131 - "KnowledgeStoreTests"
Cohesion: 0.21
Nodes (6): JournalEntry, Fact, InlineData, IOException, Theory, KnowledgeStoreTests

### Community 132 - "NoteRenderer"
Cohesion: 0.19
Nodes (13): Card, Scale, SolidColorBrush, BitmapSource, Color, Dictionary, FontFamily, FormulaControl (+5 more)

### Community 133 - "AnnotationSurfaceWindow"
Cohesion: 0.13
Nodes (10): DpiScale, EventArgs, IReadOnlyList, AnnotationSurfaceWindow, Handle, Ink, MarkCount, Monitor (+2 more)

### Community 134 - "Depth"
Cohesion: 0.24
Nodes (8): MARGINS, Result, Dictionary, DllImport, Window, Depth, MARGINS, Result

### Community 135 - "SapiSpeechOutput"
Cohesion: 0.22
Nodes (5): SpeechSynthesizer, ISpeechOutput, IsSpeaking, SapiSpeechOutput, IsSpeaking

### Community 136 - "ToolResult"
Cohesion: 0.15
Nodes (13): CancellationToken, JsonObject, Task, CancellationToken, Func, JsonObject, Task, ToolContent (+5 more)

### Community 137 - "TaskKind"
Cohesion: 0.19
Nodes (11): ModelRoute, Effort, Model, ProviderId, Task, RouteDecision, TaskKind, Code (+3 more)

### Community 138 - "Glow"
Cohesion: 0.09
Nodes (21): atomic, HANDLE, HWND, LPARAM, LRESULT, thread, UINT, WPARAM (+13 more)

### Community 139 - "Edge"
Cohesion: 0.14
Nodes (14): ID2D1Bitmap1, IDCompositionSurface, IDCompositionVisual2, ComPtr, vector, Edge, bitmap, h (+6 more)

### Community 140 - ".Use"
Cohesion: 0.41
Nodes (4): DateTimeOffset, Fact, TimeSpan, ContextEngineTests

### Community 141 - ".Query"
Cohesion: 0.18
Nodes (10): ExportCard, ExportConcept, SqliteDataReader, Func, List, ExportCard, ExportConcept, ExportDocument (+2 more)

### Community 142 - "imaging.cpp"
Cohesion: 0.22
Nodes (9): combaseapi, ComScope, hr, HRESULT, VN_API, vn_free(), vn_png_encode(), vn_version() (+1 more)

### Community 143 - "TitleNormaliser"
Cohesion: 0.45
Nodes (3): GeneratedRegex, Regex, TitleNormaliser

### Community 144 - ".Build"
Cohesion: 0.22
Nodes (8): Cancel, Dialog, Button, CancellationToken, Ok, Task, Window, ConfirmDialog

### Community 145 - ".RunAsync"
Cohesion: 0.40
Nodes (3): Func, Task, StaThread

### Community 146 - "FakeExtractor"
Cohesion: 0.36
Nodes (5): FakeExtractor, Calls, Provenance, Throw, ObserverTests

### Community 147 - "ContextOptions"
Cohesion: 0.20
Nodes (9): TimeSpan, ContextOptions, Idle, MaxEntitiesPerEpisode, MaxEpisodesInMemory, MaxLinesPerEpisode, MinimumDwell, Rejoin (+1 more)

### Community 148 - ".ReviewAndRunAsync"
Cohesion: 0.16
Nodes (10): 2. Diagnosis of v0.3.0 (measured or evidenced), Task, Window, AssistedInput, Stopwatch, Func, ITestOutputHelper, Task (+2 more)

### Community 149 - ".OnPolicyChanged"
Cohesion: 0.50
Nodes (3): PolicyBox, SelectionChangedEventArgs, ComboBox

### Community 150 - "StageScale"
Cohesion: 0.13
Nodes (14): FrameworkElement, Rect, DependencyProperty, DrawingContext, LevelScale, Level, DrawingContext, Label (+6 more)

### Community 151 - "Screen geometry (CaptureGeometry, MonitorInfo, DpiMath)"
Cohesion: 0.22
Nodes (9): Convention: all screen geometry is physical pixels on the virtual desktop, ChatRunner (timeout, bounded retry, vision gate), Screen geometry (CaptureGeometry, MonitorInfo, DpiMath), Stage: Present (annotations mapped image to physical pixels), Stage: Teach (IChatProvider.StreamAsync via ChatRunner), Risk: model-placed coordinates are approximate, Risk: cost surprises, Risk table (+1 more)

### Community 152 - "RouteLog"
Cohesion: 0.38
Nodes (4): DateTimeOffset, JsonSerializerOptions, RouteLog, RouteRecord

### Community 153 - "WriteNote"
Cohesion: 0.26
Nodes (7): CancellationToken, IReadOnlyList, JsonObject, Task, NoteTools, WriteNote, Definition

### Community 154 - "Task"
Cohesion: 0.26
Nodes (6): CancellationToken, IReadOnlyList, OperationCanceledException, Task, TimeSpan, ChatRunnerTests

### Community 155 - "FreeSpace"
Cohesion: 0.14
Nodes (11): Placement, Height, Width, FreeSpace, Area, BusyCount, CellPx, Cols (+3 more)

### Community 156 - "NativeTests"
Cohesion: 0.23
Nodes (7): Png, Height, Width, Fact, ITestOutputHelper, Task, NativeTests

### Community 157 - ".WindowBounds"
Cohesion: 0.16
Nodes (8): RECT, Color, CaptureExclusionSelfTest, CaptureTarget, None, Region, Screen, Window

### Community 158 - "LearningStoreTests"
Cohesion: 0.14
Nodes (3): SessionInfo, DateTimeOffset, LearningStoreTests

### Community 159 - ".Create"
Cohesion: 0.24
Nodes (4): Api, FakeUser32, Service, WindowCapturePolicyLogicTests

### Community 160 - "Button"
Cohesion: 0.50
Nodes (3): Send, SpeechPause, Button

### Community 165 - "FreeSpaceTests"
Cohesion: 0.25
Nodes (7): Bgra, H, Fact, InlineData, Theory, FreeSpaceTests, W

### Community 166 - "ToolRisk"
Cohesion: 0.33
Nodes (6): ToolRisk, Act, Annotate, Consequential, Control, Observe

### Community 167 - "Vernier.Tests"
Cohesion: 0.24
Nodes (4): Vernier.Core.Context, Vernier.App.Context, Vernier.Tests, xunit

### Community 168 - "DuplicationSession"
Cohesion: 0.21
Nodes (4): FrameInfo, DuplicationSession, HasFrame, Monitor

### Community 169 - "NativeInk"
Cohesion: 0.15
Nodes (6): IReadOnlyList, NativeInk, Hwnd, LastError, Segments, StrokeCallback

### Community 170 - "AnnotationKind"
Cohesion: 0.15
Nodes (11): AnnotationColors, AnnotationKind, Arrow, Bracket, Circle, Freehand, Highlight, Label (+3 more)

### Community 171 - "SessionMemoryTests"
Cohesion: 0.22
Nodes (6): DateTimeOffset, IReadOnlyList, List, TimeSpan, MemoryStore, SessionMemoryTests

### Community 172 - "TutorSession.cs"
Cohesion: 0.18
Nodes (7): Vernier.Core.Connectors, Vernier.App.Agent, Vernier.App.Connectors, system_collections_objectmodel, system_diagnostics, system_net_http, system_net_sockets

### Community 173 - "NoteContent"
Cohesion: 0.23
Nodes (9): IReadOnlyList, NoteContent, NoteLine, NoteLineKind, Check, Error, Note, Result (+1 more)

### Community 174 - "TutorPipeline"
Cohesion: 0.22
Nodes (8): Verified: real screen-aware turn end to end (9 J, 4 checks passed, 2 annotations mapped), Raw, CancellationToken, CaptureGeometry, Task, TimeSpan, Usage, TutorPipeline

### Community 175 - ".Match"
Cohesion: 0.22
Nodes (7): InvalidDataException, Processes, Profile, AppProfile, AppProfiles, InlineData, Theory

### Community 176 - ".Options"
Cohesion: 0.22
Nodes (6): Provider, GeneratedRegex, IReadOnlyList, Label, Regex, ModelRouter

### Community 177 - "Agent harness plan (revision 4)"
Cohesion: 0.22
Nodes (8): 1. Evidence gathered (reconnaissance), 4. Milestones (order revised after review R1), 4b. Revision 5 plan (owner request of 2026-10-16), 5. Measurements, Agent harness plan (revision 4), Decisions, Milestones, Reflection: what the owner asked for, across both sessions

### Community 178 - ".ExecuteAsync"
Cohesion: 0.47
Nodes (4): CancellationToken, Func, JsonObject, Task

### Community 180 - "ActionType"
Cohesion: 0.22
Nodes (9): ActionType, Click, DoubleClick, Keys, Move, RightClick, Scroll, Type (+1 more)

### Community 181 - "SKILL.md"
Cohesion: 0.25
Nodes (7): Context and memory, Draw with tools, never with code, How the learner uses Vernier, Notes, Operating the computer, Speed, Write on the screen like a teacher

### Community 182 - "OperatorGuide.md"
Cohesion: 0.25
Nodes (7): Context and memory, Draw with tools, never with code, How the learner uses Vernier, Notes, Operating the computer, Speed, Write on the screen like a teacher

### Community 184 - ".Split"
Cohesion: 0.29
Nodes (5): FileChangeKind, Append, Create, Modify, LineDiff

### Community 185 - "PreviewTool"
Cohesion: 0.36
Nodes (5): CancellationToken, JsonObject, PreviewTool, Definition, Runs

### Community 186 - "Revision 2 status (version 0.2.0)"
Cohesion: 0.33
Nodes (7): Pen capture (ink painted into the image, MarkedRegion, MarkedDetail crop), Not verified on 0.2.0: pen ink in capture, edge light, glass click-through, live pen test, publish, Blocker: Windows session locked before the final test run, Defects corrected in revision 2 (refused capture aborted question; ask bar read itself), Revision 2 status (version 0.2.0), Verified on 0.2.0: build, 195 non-desktop tests, 6 non-capture desktop tests, screenshots, Pen tool (Ctrl+Alt+D)

### Community 187 - ".A_card_is_dark_whatever_is_behind_it_and_holds_the_written_lines"
Cohesion: 0.43
Nodes (4): At, Fact, ITestOutputHelper, NoteCardTests

### Community 188 - "Side"
Cohesion: 0.29
Nodes (6): Side, Above, Auto, Below, Left, Right

### Community 189 - "CodeTool"
Cohesion: 0.29
Nodes (6): CancellationToken, JsonObject, CodeTool, Definition, Runs, LiveMcpTests

### Community 190 - "Vernier.Core.Knowledge"
Cohesion: 0.40
Nodes (4): Vernier.Core.Knowledge, system_io_compression, system_xml, system_xml_linq

### Community 191 - "IDisposable"
Cohesion: 0.33
Nodes (4): IDisposable, NativeGlow, Frames, Hwnd

### Community 192 - "WndProc"
Cohesion: 0.33
Nodes (6): HWND, LPARAM, LRESULT, UINT, WPARAM, WndProc()

### Community 194 - ".ShowAccountsAsync"
Cohesion: 0.33
Nodes (4): Task, AccountRow, ActionName, UseName

### Community 195 - "WindowCapturePolicyService"
Cohesion: 0.33
Nodes (6): Dictionary, WindowCapturePolicyService, Mode, States, Summary, WantsExclusion

### Community 196 - "ConceptsView"
Cohesion: 0.67
Nodes (3): ConceptsView, MistakesView, DockPanel

## Knowledge Gaps
- **723 isolated node(s):** `Hopefully-this-goes-somewhere`, `device`, `context`, `dup`, `latest` (+718 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1294 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TutorSession` connect `TutorSession` to `ProviderSettings`, `.User`, `Win32`, `Security and privacy document`, `ProviderException`, `ChatRequest`, `Runtime pipeline (TutorPipeline.RunAsync)`, `SessionMemory`, `AppSettings`, `.Initialize`, `AnnotationStore`, `ScreenCaptureService`, `AskBar`, `Design system document`, `TutorSession.cs`, `TutorPipeline`, `ToolTurn`, `AppServices`, `ComputerAction`, `RoutedEventArgs`, `ContextSelection`, `CapturedFrame`, `PipelineStage`, `Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)`, `IWindowCapturePolicyService`, `TutorRequest`, `LearningStore`, `.Cancel`?**
  _High betweenness centrality (0.109) - this node is a cross-community bridge._
- **Why does `PxRect` connect `PxRect` to `Win32`, `ToolResult`, `Fact`, `FakeExtractor`, `.ReviewAndRunAsync`, `TutorSession`, `.Initialize`, `AnnotationStore`, `FreeSpace`, `NativeTests`, `.WindowBounds`, `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible`, `.Review`, `.Monitors`, `FreeSpaceTests`, `.ExecuteAsync`, `DuplicationSession`, `ScreenCaptureService`, `AskBar`, `Fact`, `CaptureGeometry`, `ExtractedText`, `CapturedFrame`, `AnnotationOverlayManager`, `ITool`, `ScreenIndex`, `ScreenContextTests`, `.ExecuteAsync`, `ContextHub`, `Annotation`?**
  _High betweenness centrality (0.088) - this node is a cross-community bridge._
- **Why does `AppServices` connect `AppServices` to `SapiSpeechInput`, `Win32`, `SapiSpeechOutput`, `TrayIcon`, `SettingsPage`, `Vernier.Core.Annotations`, `AppSettings`, `TutorSession`, `.Initialize`, `AnnotationStore`, `RouteLog`, `.Monitors`, `FilesPage`, `ToolRegistry`, `AnnotationOverlayManager`, `.TrackWindow`, `ConnectorException`, `IWindowCapturePolicyService`, `SettingsStore`, `LearningStore`, `McpServer`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **Are the 32 inferred relationships involving `PxRect` (e.g. with `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible()` and `.Frame()`) actually correct?**
  _`PxRect` has 32 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `LearningStore` (e.g. with `.Episodes_survive_a_restart_without_screen_text_and_old_ones_and_forgotten_ones_are_deleted()` and `.A_real_tutoring_turn_runs_through_the_whole_pipeline()`) actually correct?**
  _`LearningStore` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 7 inferred relationships involving `ToolTurn` (e.g. with `.SmokeCheckAsync()` and `.Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied()`) actually correct?**
  _`ToolTurn` has 7 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Hopefully-this-goes-somewhere`, `device`, `context` to the rest of the system?**
  _723 weakly-connected nodes found - possible documentation gaps or missing edges._