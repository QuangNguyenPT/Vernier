# Graph Report - Hopefully-this-goes-somewhere  (2026-10-18)

## Corpus Check
- 154 files · ~186,954 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 2, .rc 1, .vcxproj 1)

## Summary
- 4252 nodes · 10231 edges · 197 communities (183 shown, 14 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1048 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `5a4ffdd9`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Exec
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
- Vernier.App.Platform
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
- Annotation
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
- PxRect
- AskBar
- Design system document
- Verification report
- ToolTurn
- Security and privacy document
- Fact
- Vernier.Tests.csproj
- AppServices
- FilesPage
- ComputerAction
- Fact
- .ConceptStates
- Threat model
- ToolRegistry
- Verifier
- DateTimeOffset
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
- .FetchAsync
- Parser
- Ink
- ClaudeCodeProvider
- LibraryPage
- Tools.cs
- PathGuard
- Vernier README (tutor that sits over your screen)
- .OnProviderFieldChanged
- ScreenIndex
- CodexCliProvider
- .OnStartup
- .ExecuteAsync
- ProviderErrorKind
- AxisCalibration
- .Cancel
- CredentialManagerSecretStore
- Vernier.Core.Agents
- HotkeyManager
- LearningStore
- Episode
- AnnotationSurfaceWindow
- .StatusAsync
- DiagnosticLog
- TutorPanel
- ContextEngine
- Data formats document
- .OnPrivacy
- .OnProviderChanged
- McpServer
- .Clean
- .OnSessionSelected
- .OnTab
- .ExecuteAsync
- .Extract
- ContextHub
- .BuildRequest
- LearningRecord
- DeleteSession
- .List
- Capture.cs
- TitleNormaliser
- .Build
- MediaState
- Search
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
- .AskAsync
- duplication.cpp
- KnowledgeStoreTests
- NoteRenderer
- Scene
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
- RouteLog
- ToolException
- SapiSpeechOutput
- FreeSpace
- RoutedEventArgs
- PipelineStage
- DiagramRenderer
- .Create
- DiagramTests
- Aurora.xaml
- Glacier.xaml
- Nebula.xaml
- FreeSpaceTests
- NativeGlow
- Vernier.Core.Geometry
- .Build
- .Redact
- Side
- IDisposable
- .TrackWindow
- InkMap
- .Check
- .Decide
- INativeWindowApi
- NoteLineKind
- NoteDismissWindow
- .RunAsync
- ToolExecutor
- SKILL.md
- OperatorGuide.md
- ContextSelection
- WindowCapturePolicy.cs
- ContextOptions
- IWindowCapturePolicyService
- NoteContent
- .OnRate
- .A_real_tutoring_turn_runs_through_the_whole_pipeline
- Vernier.Core.Context
- .Gist
- WndProc
- Button
- Attachments.cs
- WindowCapturePolicyService
- SessionsView

## God Nodes (most connected - your core abstractions)
1. `PxRect` - 112 edges
2. `TutorSession` - 80 edges
3. `ToolTurn` - 80 edges
4. `LearningStore` - 76 edges
5. `UserControl` - 71 edges
6. `AnnotationOverlayManager` - 67 edges
7. `Win32` - 61 edges
8. `PxPoint` - 60 edges
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
- `Verified: UI Automation and Windows OCR both read text; Observer prefers accessibility` --references--> `Observer`  [EXTRACTED]
  docs/VERIFICATION_REPORT.md → src/Vernier.Core/Agents/Observer.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Observer, Mastery Tutor and Verifier cooperate in the tutoring pipeline** — docs_architecture_observer_agent, docs_architecture_mastery_tutor_agent, docs_architecture_verifier_agent, docs_architecture_runtime_pipeline, docs_architecture_vernier_structured_block [EXTRACTED 1.00]
- **Glass, capture light, ask bar and tray form the always-there shell under the yellow/blue rule** — docs_architecture_glass, docs_architecture_capture_light, docs_architecture_ask_bar, docs_architecture_tray, docs_design_system_the_one_rule [INFERRED 0.85]
- **Consent gates, untrusted fencing, action policy and path guard implement the off-by-default security posture** — docs_security_and_privacy_consent_gates, docs_security_and_privacy_threat_prompt_injection, docs_security_and_privacy_threat_harmful_input_actions, docs_security_and_privacy_threat_path_traversal, claude_security_rules [INFERRED 0.85]

## Communities (197 total, 14 thin omitted)

### Community 0 - ".Exec"
Cohesion: 0.24
Nodes (3): Cards, ExportDocument, SqliteTransaction

### Community 1 - "Vernier.Core.Settings"
Cohesion: 0.20
Nodes (11): anthropic, anthropic_exceptions, anthropic_models_messages, Vernier.Core.Settings, Vernier.Core.Security, Vernier.Core.Windowing, Vernier.Core.Providers, Vernier.Core.Learning (+3 more)

### Community 2 - "UserControl"
Cohesion: 0.12
Nodes (23): Kind, Reason, Back, BackHost, CardMeta, CardView, Front, Grades (+15 more)

### Community 3 - "UserControl"
Cohesion: 0.11
Nodes (23): Available, Excluded, Expected, Missing, Observed, Path, Environment, Integrations (+15 more)

### Community 4 - "ProviderSettings"
Cohesion: 0.11
Nodes (22): ProviderSettings, ApiKeyName, BaseUrl, DisplayName, Effort, Id, IsLocal, Kind (+14 more)

### Community 5 - "TutorStructured"
Cohesion: 0.07
Nodes (29): List, Claim, Expr, UnitsLhs, UnitsRhs, Value, FormulaNote, Derivation (+21 more)

### Community 6 - "PxPoint"
Cohesion: 0.09
Nodes (18): ReadOnlySpan, IReadOnlyList, DpiMath, MonitorInfo, OverlayPlacement, PerceptualHash, PolylineMath, PxPoint (+10 more)

### Community 7 - "Win32"
Cohesion: 0.06
Nodes (24): 6. Review log (Agent B), KEYBDINPUT, LASTINPUTINFO, MonitorEnumProc, MONITORINFO, MOUSEINPUT, POINT, RECT (+16 more)

### Community 8 - "Fact"
Cohesion: 0.13
Nodes (23): Action, PipelineCallbacks, Stage, Text, Tool, ScriptedProvider, Capabilities, Delay (+15 more)

### Community 9 - "TrayIcon"
Cohesion: 0.06
Nodes (35): At, Dismissals, NOTIFYICONDATA, Action, DateTimeOffset, TimeSpan, Proactive, Action (+27 more)

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
Cohesion: 0.12
Nodes (11): RemoveSample, DateTimeOffset, Func, RoutedEventArgs, Task, AccountRow, ActionName, UseName (+3 more)

### Community 15 - "Vernier.App.Platform"
Cohesion: 0.15
Nodes (19): Vernier.App.Views, Vernier.App.Platform, Vernier.Core.Knowledge, microsoft_win32, system_runtime_interopservices, system_speech_recognition, system_speech_synthesis, system_windows (+11 more)

### Community 16 - "UserControl"
Cohesion: 0.08
Nodes (32): ActionLabel, ActionName, CanAct, Id, SignedIn, State, UseName, Accounts (+24 more)

### Community 17 - "Rect"
Cohesion: 0.15
Nodes (13): Geometry, IReadOnlyList, Point, MarkShapes, Rect, DrawingContext, DrawingContext, Point (+5 more)

### Community 18 - "ProviderException"
Cohesion: 0.10
Nodes (28): ProviderException, IsTransient, Kind, RetryAfter, ToolCallNote, CancellationToken, HashSet, HttpClient (+20 more)

### Community 19 - "ChatRequest"
Cohesion: 0.10
Nodes (24): Message, Task, CancellationToken, Func, IAsyncEnumerable, IReadOnlyList, JsonNode, Ok (+16 more)

### Community 20 - "Runtime pipeline (TutorPipeline.RunAsync)"
Cohesion: 0.14
Nodes (20): Step 4: Check your own work (second route, units, signs, limiting cases), Do-not rules (no solution dumps, no vague praise, no invented file contents, no unopened citations), Step 5: Finish with a mastery check (transfer question, mental model, what is still open), Mastery Tutor skill (/mastery-tutor), Subject notes (mathematics, physics/engineering, programming, sciences), Step 2: Teach from the origin (derive from definitions and assumptions), Work one example, then hand the next step to the learner, Mastery Tutor agent (builds ChatRequest, parses TutorOutput) (+12 more)

### Community 21 - "GrayImage"
Cohesion: 0.10
Nodes (20): MatchResult, PatchStats, NativePatchMatcher, Best, Guid, IReadOnlyDictionary, IReadOnlyList, IReadOnlySet (+12 more)

### Community 22 - "SessionMemory"
Cohesion: 0.21
Nodes (9): DateTimeOffset, IReadOnlyList, List, SessionMemory, Count, TurnNote, Fact, SessionMemoryTests (+1 more)

### Community 23 - "AppSettings"
Cohesion: 0.04
Nodes (44): ModelRoute, JsonSerializerOptions, List, AppSettings, AcknowledgedObserveCost, ActiveProvider, ActiveProviderId, AllowActivityContext (+36 more)

### Community 24 - "TutorSession"
Cohesion: 0.06
Nodes (32): Expiry, Grant, ObservableCollection, ProviderKey, Action, CancellationTokenSource, IDisposable, IEnumerable (+24 more)

### Community 25 - ".Initialize"
Cohesion: 0.15
Nodes (18): Answer, ScreenAsk, HasInk, RawImage, CaptureGeometry, ImageBounds, QuantizationUncertaintyPx, ScaleX (+10 more)

### Community 26 - "Annotation"
Cohesion: 0.07
Nodes (33): DateTimeOffset, DiagramSpec, Func, Guid, IReadOnlyDictionary, IReadOnlyList, List, TimeSpan (+25 more)

### Community 27 - "AnthropicProvider"
Cohesion: 0.05
Nodes (37): IAnthropicClient, MessageParam, CancellationToken, Dictionary, Exception, IAsyncEnumerable, IReadOnlyList, JsonElement (+29 more)

### Community 28 - "Fact"
Cohesion: 0.15
Nodes (8): JsonSerializerOptions, Regex, TutorOutput, Fact, OperationCanceledException, SecurityTests, TutorOutputTests, VerifierTests

### Community 29 - ".The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible"
Cohesion: 0.07
Nodes (25): BaseUrl, ButtonBase, ComboBoxItem, Control, DependencyObject, EnumProc, Bodies, Button (+17 more)

### Community 30 - "UserControl"
Cohesion: 0.09
Nodes (27): Concept, Derivation, Description, Expression, Mastery.Level, Mastery.Reason, Meaning, Problem (+19 more)

### Community 31 - ".Review"
Cohesion: 0.15
Nodes (14): Assisted input / computer use (ActionPolicy.Review, preview, approval, ActionExecutor, SendInputDriver), Manual: assisted input on a harmless target (Notepad), Threat: model proposes harmful input actions, ProposedAction, Amount, Keys, Text, Type (+6 more)

### Community 32 - ".Parse"
Cohesion: 0.05
Nodes (40): InlineUIContainer, Inner, Next, RichTextBox, Run, DependencyProperty, FormulaControl, Paragraph (+32 more)

### Community 33 - ".OnSta"
Cohesion: 0.07
Nodes (27): Color, R, CaptureExclusionResult, CaptureExclusionSelfTest, OcrTextExtractor, Provenance, DuplicationSession, HasFrame (+19 more)

### Community 34 - ".Assess"
Cohesion: 0.11
Nodes (17): ConceptState, DateTimeOffset, IReadOnlyList, CardSchedule, Evidence, EvidenceKinds, MasteryAssessment, MasteryLevel (+9 more)

### Community 35 - "Hopefully-this-goes-somewhere.cpp"
Cohesion: 0.15
Nodes (16): ATOM, BOOL, HINSTANCE, About(), HWND, LPARAM, LRESULT, UINT (+8 more)

### Community 36 - "ResourceDictionary"
Cohesion: 0.12
Nodes (19): IsDropDownOpen, B, Box, C, PART_ContentHost, PART_Track, ResourceDictionary, S (+11 more)

### Community 37 - "Native"
Cohesion: 0.08
Nodes (17): 4b. Revision 5 plan (owner request of 2026-10-16), Decisions, Milestones, Reflection: what the owner asked for, across both sessions, FrameInfo, LibraryImport, Png, Rect (+9 more)

### Community 38 - ".ExecuteAsync"
Cohesion: 0.17
Nodes (10): INPUT, INPUTUNION, CancellationToken, X, Y, SendInputDriver, INPUT, KEYBDINPUT (+2 more)

### Community 39 - "ScopedFiles"
Cohesion: 0.11
Nodes (21): JournalEntry, Role, ConcurrentDictionary, DateTimeOffset, IEnumerable, List, Text, FileChange (+13 more)

### Community 40 - "MainWindow"
Cohesion: 0.11
Nodes (12): ReviewNav, Dictionary, FrameworkElement, RadioButton, RoutedEventArgs, SelectionChangedEventArgs, MainWindow, AskHotkey (+4 more)

### Community 41 - "Window"
Cohesion: 0.12
Nodes (19): GoalText, Lamp, NoticeBar, NoticeText, ObserveText, PageHost, PauseButton, PolicyBox (+11 more)

### Community 42 - "PxRect"
Cohesion: 0.07
Nodes (25): Int32Rect, OcrEngine, Func, Task, CancellationToken, IReadOnlyList, IEnumerable, BitmapSource (+17 more)

### Community 43 - "AskBar"
Cohesion: 0.09
Nodes (17): Border, DockPanel, Button, FontFamily, KeyEventArgs, List, Rectangle, Task (+9 more)

### Community 44 - "Design system document"
Cohesion: 0.14
Nodes (20): Product shape (tray-resident, Ask and Pen only, yellow/blue rule, capture light on while looking), Design system document, Direction A: Instrument (v1, workspace-first), Direction B: Glass and two inks (chosen), Direction C: Companion cursor (Clicky model), Keyboard chords, On-screen ink colours (fixed, drawn over a 3 px dark halo), Why it was redesigned (v1 warm paper, serif, orange accent) (+12 more)

### Community 45 - "Verification report"
Cohesion: 0.15
Nodes (15): Verification workflow (Evaluate, Correct, Confirm), Acceptance checklist (definition of done), Manual: capture exclusion with your conferencing tool, AppUiTests (real MainWindow in-process against a loopback stub), Verified: capture geometry maps image pixels back to screen (+/-1 px quantisation), Not tested: WGC, DXGI duplication, Snipping Tool, Game Bar, OBS, Zoom, Meet, Teams, remote desktop, ECC audit record (Evaluate, Correct, Confirm): 9 defects found and corrected, Implemented with known limits (Verifier scope, SAPI voice, no typeset maths, no PDF/Office, unsigned) (+7 more)

### Community 46 - "ToolTurn"
Cohesion: 0.08
Nodes (25): ConcurrentQueue, CancellationTokenSource, ConcurrentDictionary, DateTimeOffset, IReadOnlyList, IReadOnlySet, SemaphoreSlim, TimeSpan (+17 more)

### Community 47 - "Security and privacy document"
Cohesion: 0.18
Nodes (14): Security rules (non-negotiable), Window capture policy (IWindowCapturePolicyService over INativeWindowApi), API keys: environment variable, else Windows Credential Manager, Window capture exclusion (SetWindowDisplayAffinity WDA_EXCLUDEFROMCAPTURE on own windows), Consent gates enforced in code (AllowScreenCapture, AllowMicrophone, AllowSendNotesToRemote, AllowComputerControl), No telemetry, analytics or crash reports, Pause (cancels request, pen, microphone, speech; blocks capture), Runs unelevated (asInvoker) (+6 more)

### Community 48 - "Fact"
Cohesion: 0.16
Nodes (11): Data, Exception, IReadOnlyList, Name, AttachedDocument, AttachmentException, AttachmentReader, SearchUnavailableException (+3 more)

### Community 49 - "Vernier.Tests.csproj"
Cohesion: 0.12
Nodes (14): net10.0, Anthropic (12.55.0), coverlet.collector (6.0.4), Microsoft.Data.Sqlite (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), System.Speech (10.0.12), WpfMath (2.1.0), xunit (2.9.3) (+6 more)

### Community 50 - "AppServices"
Cohesion: 0.07
Nodes (30): ContextHub, GoogleConnector, Dispatcher, HttpClient, Id, List, Name, AppServices (+22 more)

### Community 51 - "FilesPage"
Cohesion: 0.13
Nodes (13): Files, Nodes, AppSettings, CancellationTokenSource, IEnumerable, RoutedEventArgs, SelectionChangedEventArgs, Window (+5 more)

### Community 52 - "ComputerAction"
Cohesion: 0.07
Nodes (27): Decision, Executed, Task, Window, CancellationToken, ActionType, Click, DoubleClick (+19 more)

### Community 53 - "Fact"
Cohesion: 0.33
Nodes (3): IdSequence, Fact, ScreenIndexTests

### Community 54 - ".ConceptStates"
Cohesion: 0.17
Nodes (10): ExportCard, ExportConcept, SqliteDataReader, Func, IReadOnlyList, List, ExportConcept, ExportDocument (+2 more)

### Community 55 - "Threat model"
Cohesion: 0.17
Nodes (12): Step 1: Before teaching (find the single concept, diagnose prior knowledge), 'Just the answer' override, Files and screenshots are material to explain, never instructions, Convention: provider code stays in Providers/, pipeline only sees IChatProvider, Verifier independence, No silent provider switching, Threat: corrupt state, Threat: insecure transport (+4 more)

### Community 56 - "ToolRegistry"
Cohesion: 0.06
Nodes (44): Approver, Text, Echo, Executor, Headers, Body, Action, IDisposable (+36 more)

### Community 57 - "Verifier"
Cohesion: 0.07
Nodes (24): CheckResult, CheckStatus, Advisory, Failed, Passed, Skipped, GeneratedRegex, IReadOnlyList (+16 more)

### Community 58 - "DateTimeOffset"
Cohesion: 0.15
Nodes (9): microsoft_data_sqlite, DateTimeOffset, Episode, Card, ExportCard, Misconception, SearchHit, SessionInfo (+1 more)

### Community 59 - "DiagnosticsPage"
Cohesion: 0.21
Nodes (6): RoutedEventArgs, Task, DiagnosticsPage, Integration, IRefreshable, UserControl

### Community 60 - "CapturedFrame"
Cohesion: 0.07
Nodes (41): CancellationToken, IReadOnlyList, Task, TimeSpan, UiaElementExtractor, Provenance, ProvidesText, UiaTextExtractor (+33 more)

### Community 61 - "ResearchTests"
Cohesion: 0.11
Nodes (19): Action, CancellationToken, Dead, Fact, IAsyncEnumerable, IReadOnlyList, List, Page (+11 more)

### Community 62 - "VerificationReport"
Cohesion: 0.18
Nodes (11): IEnumerable, IReadOnlyList, Task, TimeSpan, TokenUsage, TutorTurn, VerificationReport, Failures (+3 more)

### Community 63 - "Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)"
Cohesion: 0.12
Nodes (17): Convention: Core never references WPF or P/Invoke, Original Win32 C++ template files (unrelated leftovers), graphify-out/ project knowledge graph, Convention: no abstraction without a second implementation or a test seam, Convention: all screen geometry is physical pixels on the virtual desktop, Rule: always launch tests/screenshots with --data <temp dir>, Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF), Vernier.App project (WPF, Platform/Overlay/Views) (+9 more)

### Community 64 - "AnnotationOverlayManager"
Cohesion: 0.05
Nodes (33): DateTime, Dock, Motion: the capture wave is the one expressive motion, Action, Color, Dictionary, DispatcherTimer, EventArgs (+25 more)

### Community 65 - "ConnectorException"
Cohesion: 0.05
Nodes (48): HttpMessageHandler, Pkce, Redirect, CancellationToken, HttpClient, HttpListener, HttpListenerContext, Listener (+40 more)

### Community 66 - "ITool"
Cohesion: 0.06
Nodes (51): 3. Architecture decisions (ADR log), HttpClient, IEnumerable, ContextTools, GetContext, Definition, MediaControl, Definition (+43 more)

### Community 67 - "TextBox"
Cohesion: 0.21
Nodes (11): AssessText, EdgeText, ExcludedText, GoalsText, GoogleClientIdBox, IntervalsText, LevelText, PaceText (+3 more)

### Community 68 - "Architecture document"
Cohesion: 0.19
Nodes (16): AnnotationOverlayManager (owns glass and strips, collects ink, InkSettled after 1.3 s), AnnotationStore (immutable list, undo/redo, Rebase, DropOutside), Architecture document, Ask bar (the only interactive overlay), Capture light (EdgeStripWindow, four edge strips per monitor), Glass (AnnotationSurfaceWindow, one transparent click-through window per monitor), Pen capture (ink painted into the image, MarkedRegion, MarkedDetail crop), Stack decision: C# on .NET 10 with WPF, no C++ layer (+8 more)

### Community 69 - "match.cpp"
Cohesion: 0.16
Nodes (16): cmath, vector, VN_API, vn_rect, Gray, h, px, w (+8 more)

### Community 70 - ".FetchAsync"
Cohesion: 0.15
Nodes (18): Excerpt, CancellationToken, Dead, HttpStatusCode, IReadOnlyList, Page, Status, Task (+10 more)

### Community 71 - "Parser"
Cohesion: 0.33
Nodes (5): Expr, Parser, AtEnd, Peek, Position

### Community 72 - "Ink"
Cohesion: 0.08
Nodes (23): atomic, HANDLE, mutex, thread, vector, Ink, argb, context (+15 more)

### Community 73 - "ClaudeCodeProvider"
Cohesion: 0.07
Nodes (29): ExitCode, Process, CancellationToken, Func, IAsyncEnumerable, IEnumerable, IReadOnlyDictionary, IReadOnlyList (+21 more)

### Community 74 - "LibraryPage"
Cohesion: 0.26
Nodes (5): SessionRow, RoutedEventArgs, LibraryPage, SessionRow, TextChangedEventArgs

### Community 75 - "Tools.cs"
Cohesion: 0.24
Nodes (6): Vernier.Core.Tools, Vernier.Core.Connectors, Vernier.App.Agent, system_collections_concurrent, system_security_cryptography, system_text_json_nodes

### Community 76 - "PathGuard"
Cohesion: 0.18
Nodes (8): ScopedFiles / PathGuard (confined file access, Propose then Apply), Threat: path traversal / unintended file writes, CancellationToken, IEnumerable, IReadOnlyCollection, PathGuard, Root, PathGuardException

### Community 77 - "Vernier README (tutor that sits over your screen)"
Cohesion: 0.13
Nodes (17): Accounts via vendor CLIs (CliAccounts), Manual: Accounts sign-in, Manual: Ask, Pen and the capture light, Manual verification checklist, Items marked not verified by the author (multi-monitor, scaling, Codex, real mic, pro apps), Manual: living in the tray, Manual: Voice (hold to talk, speak answers), Not verified: live API providers, local model server, multi-monitor/mixed DPI, real microphone, real assisted input, pro apps (+9 more)

### Community 78 - ".OnProviderFieldChanged"
Cohesion: 0.22
Nodes (6): EndpointText, GoogleConnectButton, ModelText, TestButton, VisionCheck, Button

### Community 79 - "ScreenIndex"
Cohesion: 0.09
Nodes (23): Item, Kind, Score, Id, Mark, DateTimeOffset, Dictionary, IReadOnlyList (+15 more)

### Community 80 - "CodexCliProvider"
Cohesion: 0.09
Nodes (20): AnthropicProvider (official SDK, Messages streaming), ChatRunner (timeout, bounded retry, vision gate), ClaudeCodeProvider (claude -p stream-json subprocess, Claude Code sign-in), CodexCliProvider (codex exec subprocess, ChatGPT sign-in), GeminiProvider (streamGenerateContent SSE), IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities), OpenAiCompatibleProvider (OpenAI, Moonshot/Kimi, Ollama, LM Studio, llama.cpp), Stage: Teach (IChatProvider.StreamAsync via ChatRunner) (+12 more)

### Community 81 - ".OnStartup"
Cohesion: 0.14
Nodes (7): ExitEventArgs, Application, App, Task, FirstRunWindow, StartupEventArgs, Window

### Community 82 - ".ExecuteAsync"
Cohesion: 0.47
Nodes (4): CancellationToken, Func, JsonObject, Task

### Community 83 - "ProviderErrorKind"
Cohesion: 0.20
Nodes (10): ProviderErrorKind, Auth, BadRequest, Network, NotConfigured, RateLimited, Refused, Server (+2 more)

### Community 84 - "AxisCalibration"
Cohesion: 0.12
Nodes (14): A, Func, IEnumerable, IReadOnlyList, List, Text, X, Y (+6 more)

### Community 86 - "CredentialManagerSecretStore"
Cohesion: 0.27
Nodes (3): CREDENTIAL, CREDENTIAL, CredentialManagerSecretStore

### Community 87 - "Vernier.Core.Agents"
Cohesion: 0.14
Nodes (11): Vernier.Core.Research, Vernier.Core.Agents, Vernier.Core.Verification, ProviderKinds, system_globalization, system_net_http_headers, system_net_sockets, system_text (+3 more)

### Community 88 - "HotkeyManager"
Cohesion: 0.20
Nodes (7): Action, Dictionary, HwndSource, List, HotkeyManager, Unavailable, EventArgs

### Community 89 - "LearningStore"
Cohesion: 0.12
Nodes (10): SqliteConnection, JsonSerializerOptions, LearningStore, BackupDirectory, HasSampleData, IsEmpty, LatestVersion, Path (+2 more)

### Community 90 - "Episode"
Cohesion: 0.07
Nodes (27): From, To, Episode, ActiveSeconds, App, Dirty, End, Entities (+19 more)

### Community 91 - "AnnotationSurfaceWindow"
Cohesion: 0.17
Nodes (8): DpiScale, EventArgs, IReadOnlyList, AnnotationSurfaceWindow, Handle, Ink, MarkCount, Monitor

### Community 92 - ".StatusAsync"
Cohesion: 0.24
Nodes (9): Args, File, CancellationToken, Func, IReadOnlyList, Task, CliAccount, CliAccounts (+1 more)

### Community 93 - "DiagnosticLog"
Cohesion: 0.33
Nodes (4): Exception, IReadOnlyList, LinkedList, DiagnosticLog

### Community 94 - "TutorPanel"
Cohesion: 0.22
Nodes (7): Talk, KeyEventArgs, MouseButtonEventArgs, TranscriptItem, TutorSession, TutorPanel, Session

### Community 95 - "ContextEngine"
Cohesion: 0.08
Nodes (22): App, Key, SessionItem, Since, SemaphoreSlim, EpisodeSummariser, DateTimeOffset, HashSet (+14 more)

### Community 96 - "Data formats document"
Cohesion: 0.15
Nodes (13): Hint escalation ladder (point, leading question, principle, show the step), One right answer is not mastery, Misconception matters more than the slip, Step 3: When the learner answers (find the first faulty step), ObsidianVault (Markdown notes under Vernier/, append-not-replace), Cards CSV export (RFC 4180, Anki-importable), Data formats document, Evidence kinds (diagnostic, explain, transfer, recall, self_report) (+5 more)

### Community 97 - ".OnPrivacy"
Cohesion: 0.27
Nodes (10): AllowActivity, AllowCapture, AllowControl, AllowMic, AllowNotes, AllowResearch, AutoRouteBox, Proactive (+2 more)

### Community 98 - ".OnProviderChanged"
Cohesion: 0.27
Nodes (6): CheckerBox, ContextModelBox, ModelBox, ProviderBox, SelectionChangedEventArgs, ComboBox

### Community 99 - "McpServer"
Cohesion: 0.12
Nodes (16): HttpListenerResponse, IAsyncDisposable, JsonArray, CancellationTokenSource, ConcurrentDictionary, DiagnosticLogSink, HttpListener, HttpListenerContext (+8 more)

### Community 100 - ".Clean"
Cohesion: 0.35
Nodes (5): GeneratedRegex, HashSet, Regex, QuerySanitizer, ResearchPolicy

### Community 101 - ".OnSessionSelected"
Cohesion: 0.33
Nodes (5): Concepts, Mistakes, Sessions, SelectionChangedEventArgs, ListBox

### Community 102 - ".OnTab"
Cohesion: 0.53
Nodes (5): TabConcepts, TabFormulas, TabMistakes, TabSessions, RadioButton

### Community 103 - ".ExecuteAsync"
Cohesion: 0.14
Nodes (14): Plan, CancellationToken, DllImport, Guid, HashSet, IEnumerable, IReadOnlyList, JsonObject (+6 more)

### Community 104 - ".Extract"
Cohesion: 0.44
Nodes (4): GeneratedRegex, Regex, Title, Readable

### Community 105 - "ContextHub"
Cohesion: 0.12
Nodes (13): GlobalSystemMediaTransportControlsSessionManager, ScreenReader, CancellationTokenSource, DateTimeOffset, DispatcherTimer, Func, Task, TimeSpan (+5 more)

### Community 106 - ".BuildRequest"
Cohesion: 0.07
Nodes (35): Raw, Attachment, TutorMode, Annotate, Ask, Assist, Review, Tutor (+27 more)

### Community 107 - "LearningRecord"
Cohesion: 0.25
Nodes (7): LearningRecord, Card, Concept, Example, Formula, Misconception, Session

### Community 108 - "DeleteSession"
Cohesion: 0.50
Nodes (4): DeleteSession, Toggle, ToVault, Button

### Community 109 - ".List"
Cohesion: 0.16
Nodes (10): NoteHit, CancellationToken, GeneratedRegex, HashSet, IReadOnlyList, Regex, LineDiff, NoteHit (+2 more)

### Community 110 - "Capture.cs"
Cohesion: 0.13
Nodes (16): Vernier.App.Connectors, Vernier.App, system_diagnostics, system_io, system_net_http, system_runtime_interopservices_windowsruntime, system_windows_automation_peers, system_windows_controls_primitives (+8 more)

### Community 111 - "TitleNormaliser"
Cohesion: 0.19
Nodes (8): Dictionary, GeneratedRegex, Regex, TitleNormaliser, UrlSanitiser, InlineData, Theory, TitleAndAddressTests

### Community 112 - ".Build"
Cohesion: 0.14
Nodes (17): Alpha, Brush, Colour, Dashed, Halo, IEasingFunction, Color, Dictionary (+9 more)

### Community 113 - "MediaState"
Cohesion: 0.20
Nodes (12): DateTimeOffset, IReadOnlyList, LinkedList, TimeSpan, ActivitySpan, Length, ActivityTimeline, Recent (+4 more)

### Community 114 - "Search"
Cohesion: 0.67
Nodes (3): Search, SessionText, TextBox

### Community 120 - "GlassSurface"
Cohesion: 0.11
Nodes (15): DrawingVisual, MouseEventArgs, Action, Guid, List, Mark, MouseButtonEventArgs, GlassSurface (+7 more)

### Community 121 - "DiagramSpec"
Cohesion: 0.15
Nodes (12): HashSet, IReadOnlyList, DiagramBuilder, DiagramItem, DiagramKind, Complex, Forces, Hypercube (+4 more)

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
Cohesion: 0.15
Nodes (6): IReadOnlyList, NativeInk, Hwnd, LastError, Segments, StrokeCallback

### Community 128 - "SapiSpeechInput"
Cohesion: 0.17
Nodes (8): SpeechRecognitionEngine, StringBuilder, Task, ISpeechInput, IsListening, SapiSpeechInput, IsListening, TaskCompletionSource

### Community 129 - ".AskAsync"
Cohesion: 0.12
Nodes (10): INotifyPropertyChanged, Action, IReadOnlyList, TranscriptItem, Checks, IsUser, Meta, Notices (+2 more)

### Community 130 - "duplication.cpp"
Cohesion: 0.21
Nodes (14): d3d11, dxgi1_2, CoveredFraction(), HRESULT, vector, VN_API, vn_rect, EnsureStaging() (+6 more)

### Community 131 - "KnowledgeStoreTests"
Cohesion: 0.18
Nodes (7): Dir, Fact, InlineData, IOException, Theory, KnowledgeStoreTests, Vault

### Community 132 - "NoteRenderer"
Cohesion: 0.20
Nodes (13): Card, BitmapSource, Color, Dictionary, FontFamily, FormulaControl, Guid, Scale (+5 more)

### Community 133 - "Scene"
Cohesion: 0.33
Nodes (8): Color, DrawingContext, Pen, Point, SolidColorBrush, Typeface, Palette, Scene

### Community 134 - "Depth"
Cohesion: 0.24
Nodes (8): MARGINS, Result, Dictionary, DllImport, Window, Depth, MARGINS, Result

### Community 135 - "V3"
Cohesion: 0.12
Nodes (15): Corners, Depth, Distance, Edges, Foot, OnFirst, OnSecond, Skew (+7 more)

### Community 136 - "ToolResult"
Cohesion: 0.12
Nodes (18): Place, CancellationToken, Func, IReadOnlyList, JsonObject, Scale, Task, GraphArea (+10 more)

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

### Community 141 - "ProposedAnnotation"
Cohesion: 0.17
Nodes (12): ProposedAnnotation, Confidence, H, Kind, Label, Semantic, Target, W (+4 more)

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
Cohesion: 0.19
Nodes (9): Processes, Profile, AppProfile, AppProfiles, FakeExtractor, Calls, Provenance, Throw (+1 more)

### Community 147 - ".An_answer_that_had_to_be_researched_and_was_not_is_asked_again_once"
Cohesion: 0.16
Nodes (10): ConcurrentDictionary, IReadOnlyCollection, ResearchDemand, ResearchGate, ResearchLedger, Calls, Urls, Working (+2 more)

### Community 148 - "Agent harness plan (revision 4)"
Cohesion: 0.29
Nodes (6): 1. Evidence gathered (reconnaissance), 2. Diagnosis of v0.3.0 (measured or evidenced), 4. Milestones (order revised after review R1), 5. Measurements, Agent harness plan (revision 4), AssistedInput

### Community 149 - "OperatorGuide.cs"
Cohesion: 0.40
Nodes (4): Lazy, OperatorGuide, Text, system_reflection

### Community 150 - "StageScale"
Cohesion: 0.20
Nodes (10): FrameworkElement, DependencyProperty, LevelScale, Level, Label, Typeface, StageScale, Busy (+2 more)

### Community 151 - "FakeUser32"
Cohesion: 0.12
Nodes (11): Dictionary, HashSet, FakeUser32, Affinity, Children, CurrentProcessId, ForceEffective, Foreign (+3 more)

### Community 152 - "RouteLog"
Cohesion: 0.38
Nodes (4): DateTimeOffset, JsonSerializerOptions, RouteLog, RouteRecord

### Community 153 - "ToolException"
Cohesion: 0.17
Nodes (11): CancellationToken, JsonObject, Task, CancellationToken, IReadOnlyList, JsonObject, Task, NoteTools (+3 more)

### Community 154 - "SapiSpeechOutput"
Cohesion: 0.20
Nodes (5): SpeechSynthesizer, ISpeechOutput, IsSpeaking, SapiSpeechOutput, IsSpeaking

### Community 155 - "FreeSpace"
Cohesion: 0.14
Nodes (11): Placement, Height, Width, FreeSpace, Area, BusyCount, CellPx, Cols (+3 more)

### Community 156 - "RoutedEventArgs"
Cohesion: 0.18
Nodes (5): SpeakToggle, SpeechStop, RadioButton, RoutedEventArgs, ToggleButton

### Community 157 - "PipelineStage"
Cohesion: 0.20
Nodes (9): PipelineStage, Confirm, Correct, Interpret, Observe, Present, Teach, UpdateMastery (+1 more)

### Community 158 - "DiagramRenderer"
Cohesion: 0.50
Nodes (5): Card, Dictionary, Guid, Scale, DiagramRenderer

### Community 159 - ".Create"
Cohesion: 0.17
Nodes (7): Api, FakeUser32, InvalidDataException, Service, InlineData, Theory, WindowCapturePolicyLogicTests

### Community 160 - "DiagramTests"
Cohesion: 0.10
Nodes (16): Across, Along, Axes, Degrees, Direction, Fx, Fy, Ink (+8 more)

### Community 165 - "FreeSpaceTests"
Cohesion: 0.25
Nodes (7): Bgra, H, Fact, InlineData, Theory, FreeSpaceTests, W

### Community 166 - "NativeGlow"
Cohesion: 0.29
Nodes (3): NativeGlow, Frames, Hwnd

### Community 167 - "Vernier.Core.Geometry"
Cohesion: 0.21
Nodes (5): Vernier.Tests, Vernier.Core.Geometry, Vernier.Core.Diagrams, Vernier.Core.Annotations, xunit

### Community 168 - ".Build"
Cohesion: 0.38
Nodes (3): JsonObject, ResearchDemand, TurnPlan

### Community 169 - ".Redact"
Cohesion: 0.29
Nodes (5): Threat: secret leakage, GeneratedRegex, Regex, SecretRedactor, Untrusted

### Community 170 - "Side"
Cohesion: 0.29
Nodes (6): Side, Above, Auto, Below, Left, Right

### Community 171 - "IDisposable"
Cohesion: 0.33
Nodes (4): IDisposable, IPAddress, HttpClient, SafeWeb

### Community 173 - "InkMap"
Cohesion: 0.23
Nodes (6): LostAt, Points, List, InkMap, Height, Width

### Community 174 - ".Check"
Cohesion: 0.40
Nodes (4): IReadOnlyList, IReadOnlySet, DrawingGate, VisualDemand

### Community 175 - ".Decide"
Cohesion: 0.58
Nodes (3): GeneratedRegex, Regex, DimensionPolicy

### Community 176 - "INativeWindowApi"
Cohesion: 0.27
Nodes (3): INativeWindowApi, CurrentProcessId, OsBuild

### Community 177 - "NoteLineKind"
Cohesion: 0.33
Nodes (6): NoteLineKind, Check, Error, Note, Result, Step

### Community 178 - "NoteDismissWindow"
Cohesion: 0.24
Nodes (6): ControlTemplate, EventArgs, Guid, NoteDismissWindow, Handle, NoteId

### Community 179 - ".RunAsync"
Cohesion: 0.40
Nodes (3): Func, Task, StaThread

### Community 180 - "ToolExecutor"
Cohesion: 0.12
Nodes (16): CancellationToken, Task, TimeSpan, UiApprover, CancellationToken, DiagnosticLogSink, Func, JsonNode (+8 more)

### Community 181 - "SKILL.md"
Cohesion: 0.13
Nodes (14): Context and memory, Draw with tools, never with code, Follow the plan in the question, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain (+6 more)

### Community 182 - "OperatorGuide.md"
Cohesion: 0.13
Nodes (14): Context and memory, Draw with tools, never with code, Follow the plan in the question, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain (+6 more)

### Community 183 - "ContextSelection"
Cohesion: 0.31
Nodes (3): TimeSpan, ContextSelection, IsEmpty

### Community 184 - "WindowCapturePolicy.cs"
Cohesion: 0.25
Nodes (8): DisplayAffinity, PolicyStatus, Applied, Failed, NotRequested, Requested, Unsupported, WindowPolicyState

### Community 185 - "ContextOptions"
Cohesion: 0.25
Nodes (8): ContextOptions, Idle, MaxEntitiesPerEpisode, MaxEpisodesInMemory, MaxLinesPerEpisode, MinimumDwell, Rejoin, Retention

### Community 186 - "IWindowCapturePolicyService"
Cohesion: 0.19
Nodes (9): CapturePolicyMode, RequestExclusion, ShareSafe, Standard, IReadOnlyList, IWindowCapturePolicyService, Mode, States (+1 more)

### Community 187 - "NoteContent"
Cohesion: 0.29
Nodes (6): IReadOnlyList, NoteContent, NoteLine, Fact, ITestOutputHelper, NoteCardTests

### Community 188 - ".OnRate"
Cohesion: 0.50
Nodes (3): RoutedPropertyChangedEventArgs, RateSlider, Slider

### Community 189 - ".A_real_tutoring_turn_runs_through_the_whole_pipeline"
Cohesion: 0.15
Nodes (4): DateTimeOffset, LearningStoreTests, LiveClaudeCodeTests, Enabled

### Community 192 - "WndProc"
Cohesion: 0.33
Nodes (6): HWND, LPARAM, LRESULT, UINT, WPARAM, WndProc()

### Community 193 - "Button"
Cohesion: 0.50
Nodes (3): Send, SpeechPause, Button

### Community 194 - "Attachments.cs"
Cohesion: 0.50
Nodes (3): system_io_compression, system_xml, system_xml_linq

### Community 195 - "WindowCapturePolicyService"
Cohesion: 0.33
Nodes (6): Dictionary, WindowCapturePolicyService, Mode, States, Summary, WantsExclusion

## Knowledge Gaps
- **776 isolated node(s):** `Hopefully-this-goes-somewhere`, `device`, `context`, `dup`, `latest` (+771 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1439 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PxRect` connect `PxRect` to `.AskAsync`, `PxPoint`, `Win32`, `ToolResult`, `Fact`, `FakeExtractor`, `GrayImage`, `.Initialize`, `Annotation`, `FreeSpace`, `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible`, `.Review`, `.OnSta`, `FreeSpaceTests`, `.ExecuteAsync`, `AskBar`, `NoteDismissWindow`, `Fact`, `CapturedFrame`, `AnnotationOverlayManager`, `ITool`, `ScreenIndex`, `AxisCalibration`, `.ExecuteAsync`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **Why does `TutorSession` connect `TutorSession` to `Vernier.Core.Settings`, `.AskAsync`, `ProviderSettings`, `Win32`, `ProviderException`, `ChatRequest`, `Runtime pipeline (TutorPipeline.RunAsync)`, `SessionMemory`, `AppSettings`, `Annotation`, `AnthropicProvider`, `RoutedEventArgs`, `PipelineStage`, `PxRect`, `AskBar`, `Design system document`, `ToolTurn`, `Security and privacy document`, `AppServices`, `ComputerAction`, `ContextSelection`, `IWindowCapturePolicyService`, `CapturedFrame`, `VerificationReport`, `Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)`, `CodexCliProvider`, `.Cancel`, `LearningStore`, `DiagnosticLog`, `.BuildRequest`?**
  _High betweenness centrality (0.073) - this node is a cross-community bridge._
- **Why does `AppServices` connect `AppServices` to `SapiSpeechInput`, `Vernier.Core.Settings`, `.AskAsync`, `Win32`, `TrayIcon`, `AppSettings`, `TutorSession`, `.Initialize`, `Annotation`, `SapiSpeechOutput`, `RouteLog`, `PxRect`, `.TrackWindow`, `FilesPage`, `ToolExecutor`, `IWindowCapturePolicyService`, `AnnotationOverlayManager`, `ConnectorException`, `CodexCliProvider`, `.OnStartup`, `LearningStore`, `DiagnosticLog`, `McpServer`?**
  _High betweenness centrality (0.070) - this node is a cross-community bridge._
- **Are the 30 inferred relationships involving `PxRect` (e.g. with `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible()` and `.Frame()`) actually correct?**
  _`PxRect` has 30 INFERRED edges - model-reasoned connections that need verification._
- **Are the 6 inferred relationships involving `ToolTurn` (e.g. with `.SmokeCheckAsync()` and `.Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied()`) actually correct?**
  _`ToolTurn` has 6 INFERRED edges - model-reasoned connections that need verification._
- **Are the 3 inferred relationships involving `LearningStore` (e.g. with `.Episodes_survive_a_restart_without_screen_text_and_old_ones_and_forgotten_ones_are_deleted()` and `.A_real_tutoring_turn_runs_through_the_whole_pipeline()`) actually correct?**
  _`LearningStore` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Hopefully-this-goes-somewhere`, `device`, `context` to the rest of the system?**
  _776 weakly-connected nodes found - possible documentation gaps or missing edges._