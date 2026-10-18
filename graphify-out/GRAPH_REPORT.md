# Graph Report - Hopefully-this-goes-somewhere  (2026-10-18)

## Corpus Check
- 146 files · ~169,804 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 2, .rc 1, .vcxproj 1)

## Summary
- 4039 nodes · 9636 edges · 182 communities (169 shown, 13 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1008 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ee9ab3e8`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- LearningStore
- system_text_json_nodes
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
- .Build
- OpenAiCompatibleProvider
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
- .Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied
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
- Tools.cs
- Vernier README (tutor that sits over your screen)
- .Read
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
- .The_real_claude_cli_connects_to_vernier_over_mcp_lists_the_tool_and_calls_it
- PathGuard
- Manual verification checklist
- .OnProviderFieldChanged
- ScreenIndex
- NativeInk
- .OnStartup
- .ExecuteAsync
- ProviderErrorKind
- AxisCalibration
- .Cancel
- CredentialManagerSecretStore
- Vernier.Core.Agents
- HotkeyManager
- TutorPanel
- Episode
- Rect
- .RunAsync
- DiagnosticLog
- ScreenContextTests
- ContextEngine
- PxRect
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
- .OnRate
- DeleteSession
- .List
- Vernier.Core.Geometry
- TitleNormaliser
- MarkPainter
- WorkContext
- Search
- Hopefully-this-goes-somewhere
- Dark.xaml
- Light.xaml
- FormulasView
- GlassSurface
- Security and privacy document
- ink.cpp
- .Tools_run_as_soon_as_their_arguments_arrive_and_the_assistant_turn_is_echoed_with_its_thinking
- glow.cpp
- Duplication
- common.h
- WebResearcher.cs
- SapiSpeechInput
- TranscriptItem
- duplication.cpp
- KnowledgeStoreTests
- NoteRenderer
- AnnotationSurfaceWindow
- Depth
- .SaveSettings
- ToolResult
- TaskKind
- Glow
- Edge
- .Use
- ProposedAnnotation
- imaging.cpp
- CheckResult
- .Ask
- .ObserveForToolAsync
- FakeExtractor
- ResearchLedger
- .ReviewAndRunAsync
- OperatorGuide.cs
- PipelineStage
- Screen geometry (CaptureGeometry, MonitorInfo, DpiMath)
- .Options
- WriteNote
- Fact
- FreeSpace
- .Monitors
- .Research_is_required_by_code_for_pages_tools_unknown_apps_and_how_questions
- TitleAndAddressTests
- FakeUser32
- Button
- Aurora.xaml
- Glacier.xaml
- Nebula.xaml
- FreeSpaceTests
- .RunAsync
- Vernier.Tests
- SettingsStore
- .OnPause
- .OnPolicyChanged
- .CertaintyWords
- SessionsView
- Agent harness plan (revision 4)
- ToolTurn
- SKILL.md
- OperatorGuide.md
- ContextOptions
- NoteContent
- Side
- WndProc
- WindowCapturePolicyService

## God Nodes (most connected - your core abstractions)
1. `PxRect` - 107 edges
2. `TutorSession` - 78 edges
3. `ToolTurn` - 77 edges
4. `LearningStore` - 76 edges
5. `UserControl` - 71 edges
6. `AnnotationOverlayManager` - 67 edges
7. `Win32` - 61 edges
8. `PxPoint` - 58 edges
9. `AppServices` - 55 edges
10. `AppSettings` - 55 edges

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

## Communities (182 total, 13 thin omitted)

### Community 0 - "LearningStore"
Cohesion: 0.05
Nodes (42): Cards, ExportCard, ExportConcept, ExportDocument, microsoft_data_sqlite, SqliteConnection, SqliteDataReader, SqliteTransaction (+34 more)

### Community 1 - "system_text_json_nodes"
Cohesion: 0.17
Nodes (11): Vernier.Core.Tools, Vernier.Core.Connectors, Vernier.App.Agent, Vernier.App.Connectors, Vernier.Core.Research, system_collections_concurrent, system_globalization, system_io (+3 more)

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
Cohesion: 0.10
Nodes (12): ReadOnlySpan, IReadOnlyList, DpiMath, MonitorInfo, OverlayPlacement, PerceptualHash, PolylineMath, PxPoint (+4 more)

### Community 7 - "Win32"
Cohesion: 0.06
Nodes (23): ComboBoxItem, Control, 6. Review log (Agent B), KEYBDINPUT, LASTINPUTINFO, MonitorEnumProc, MOUSEINPUT, POINT (+15 more)

### Community 8 - "Fact"
Cohesion: 0.12
Nodes (26): Action, PipelineCallbacks, Stage, Text, Tool, ProviderException, IsTransient, Kind (+18 more)

### Community 9 - "TrayIcon"
Cohesion: 0.06
Nodes (35): At, Dismissals, NOTIFYICONDATA, Action, DateTimeOffset, TimeSpan, Proactive, Action (+27 more)

### Community 10 - "UserControl"
Cohesion: 0.09
Nodes (31): Degree, Label, Links, AskAboutNote, DisconnectVault, EditInstruction, Explain, Files (+23 more)

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
Cohesion: 0.15
Nodes (22): Vernier.App.Views, Vernier.Core.Windowing, Vernier.App, microsoft_win32, system_windows, system_windows_automation, system_windows_automation_peers, system_windows_controls (+14 more)

### Community 16 - "UserControl"
Cohesion: 0.08
Nodes (32): ActionLabel, ActionName, CanAct, Id, SignedIn, State, UseName, Accounts (+24 more)

### Community 17 - ".Build"
Cohesion: 0.25
Nodes (9): Geometry, Func, Size, IReadOnlyList, Point, Rect, MarkShapes, FrameInfo (+1 more)

### Community 18 - "OpenAiCompatibleProvider"
Cohesion: 0.10
Nodes (27): AnthropicProvider (official SDK, Messages streaming), GeminiProvider (streamGenerateContent SSE), IChatProvider interface (StreamAsync, ListModelsAsync, Capabilities), OpenAiCompatibleProvider (OpenAI, Moonshot/Kimi, Ollama, LM Studio, llama.cpp), CancellationToken, HashSet, HttpClient, HttpMethod (+19 more)

### Community 19 - "ChatRequest"
Cohesion: 0.06
Nodes (42): CodexCliProvider (codex exec subprocess, ChatGPT sign-in), ChatGPT-account provider (codex exec) never run for real, Message, Raw, CancellationToken, CaptureGeometry, Task, Usage (+34 more)

### Community 20 - "Runtime pipeline (TutorPipeline.RunAsync)"
Cohesion: 0.11
Nodes (25): Step 4: Check your own work (second route, units, signs, limiting cases), Do-not rules (no solution dumps, no vague praise, no invented file contents, no unopened citations), Hint escalation ladder (point, leading question, principle, show the step), Step 5: Finish with a mastery check (transfer question, mental model, what is still open), One right answer is not mastery, Mastery Tutor skill (/mastery-tutor), Misconception matters more than the slip, Subject notes (mathematics, physics/engineering, programming, sciences) (+17 more)

### Community 21 - "GrayImage"
Cohesion: 0.12
Nodes (19): PatchStats, NativePatchMatcher, Best, Guid, IReadOnlyDictionary, IReadOnlyList, IReadOnlySet, MarkAnchor (+11 more)

### Community 22 - "SessionMemory"
Cohesion: 0.16
Nodes (11): DateTimeOffset, GeneratedRegex, IReadOnlyList, List, Regex, SessionMemory, Count, TurnNote (+3 more)

### Community 23 - "AppSettings"
Cohesion: 0.05
Nodes (39): ModelRoute, List, AppSettings, AcknowledgedObserveCost, ActiveProvider, ActiveProviderId, AllowActivityContext, AllowComputerControl (+31 more)

### Community 24 - "TutorSession"
Cohesion: 0.06
Nodes (32): Grant, ObservableCollection, ProviderKey, CancellationTokenSource, IDisposable, IEnumerable, IReadOnlyList, List (+24 more)

### Community 25 - ".Initialize"
Cohesion: 0.16
Nodes (14): Answer, ScreenAsk, HasInk, TempDir, Path, Fact, ITestOutputHelper, Task (+6 more)

### Community 26 - "Annotation"
Cohesion: 0.06
Nodes (32): DateTimeOffset, Func, Guid, IReadOnlyDictionary, IReadOnlyList, List, TimeSpan, Annotation (+24 more)

### Community 27 - "AnthropicProvider"
Cohesion: 0.06
Nodes (35): IAnthropicClient, CancellationToken, Dictionary, Exception, IAsyncEnumerable, IReadOnlyList, JsonElement, JsonObject (+27 more)

### Community 28 - "Fact"
Cohesion: 0.13
Nodes (7): JsonSerializerOptions, Regex, TutorOutput, Fact, SettingsTests, TutorOutputTests, VerifierTests

### Community 29 - ".Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied"
Cohesion: 0.09
Nodes (22): BaseUrl, ButtonBase, DependencyObject, EnumProc, Bodies, Func, HttpListener, IEnumerable (+14 more)

### Community 30 - "UserControl"
Cohesion: 0.09
Nodes (27): Concept, Derivation, Description, Expression, Mastery.Level, Mastery.Reason, Meaning, Problem (+19 more)

### Community 31 - ".Review"
Cohesion: 0.11
Nodes (20): Decision, Assisted input / computer use (ActionPolicy.Review, preview, approval, ActionExecutor, SendInputDriver), Manual: assisted input on a harmless target (Notepad), Threat: model proposes harmful input actions, Executed, ProposedAction, Amount, Keys (+12 more)

### Community 32 - ".Parse"
Cohesion: 0.05
Nodes (40): InlineUIContainer, Inner, Next, RichTextBox, Run, DependencyProperty, FormulaControl, Paragraph (+32 more)

### Community 33 - ".OnSta"
Cohesion: 0.18
Nodes (10): Dispatcher, DllImport, Fact, InvalidOperationException, ITestOutputHelper, Lazy, UIElement, Window (+2 more)

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
Cohesion: 0.09
Nodes (12): FrameInfo, LibraryImport, MatchResult, Png, Height, Lazy, Width, MatchResult (+4 more)

### Community 38 - ".ExecuteAsync"
Cohesion: 0.19
Nodes (9): INPUT, INPUTUNION, CancellationToken, X, Y, SendInputDriver, INPUT, KEYBDINPUT (+1 more)

### Community 39 - "ScopedFiles"
Cohesion: 0.17
Nodes (9): JournalEntry, ConcurrentDictionary, List, ScopedFiles, Guard, JournalPath, Lock, IOException (+1 more)

### Community 40 - "MainWindow"
Cohesion: 0.16
Nodes (8): Dictionary, FrameworkElement, RadioButton, MainWindow, AskHotkey, Bar, Hotkeys, PenHotkey

### Community 41 - "Window"
Cohesion: 0.12
Nodes (18): GoalText, Lamp, NoticeBar, NoticeText, ObserveText, PageHost, PolicyText, ProviderText (+10 more)

### Community 42 - "ScreenCaptureService"
Cohesion: 0.18
Nodes (8): Int32Rect, BitmapSource, Color, IReadOnlyList, CaptureExclusionSelfTest, ScreenCaptureService, Height, Width

### Community 43 - "AskBar"
Cohesion: 0.09
Nodes (17): Border, DockPanel, Button, FontFamily, KeyEventArgs, List, Rectangle, Task (+9 more)

### Community 44 - "Design system document"
Cohesion: 0.13
Nodes (21): Product shape (tray-resident, Ask and Pen only, yellow/blue rule, capture light on while looking), Motion: the capture wave is the one expressive motion, Design system document, Direction A: Instrument (v1, workspace-first), Direction B: Glass and two inks (chosen), Direction C: Companion cursor (Clicky model), Keyboard chords, On-screen ink colours (fixed, drawn over a 3 px dark halo) (+13 more)

### Community 45 - "Verification report"
Cohesion: 0.22
Nodes (11): Verification workflow (Evaluate, Correct, Confirm), Acceptance checklist (definition of done), AppUiTests (real MainWindow in-process against a loopback stub), Verified: capture geometry maps image pixels back to screen (+/-1 px quantisation), ECC audit record (Evaluate, Correct, Confirm): 9 defects found and corrected, Implemented with known limits (Verifier scope, SAPI voice, no typeset maths, no PDF/Office, unsigned), Verified: policy service on real windows (apply, read-back, clear, other-process refusal), Status against production-ready (+3 more)

### Community 46 - "Tools.cs"
Cohesion: 0.09
Nodes (24): 3. Architecture decisions (ADR log), CancellationToken, Task, TimeSpan, UiApprover, CancellationToken, DateTimeOffset, DiagnosticLogSink (+16 more)

### Community 47 - "Vernier README (tutor that sits over your screen)"
Cohesion: 0.12
Nodes (17): ObsidianVault (Markdown notes under Vernier/, append-not-replace), Cards CSV export (RFC 4180, Anki-importable), Data formats document, learning.db SQLite schema (user_version 2, FTS5 messages_fts), Learning export (vernier-learning, version 1 JSON), Obsidian notes layout (Concepts, Sessions, Mistake log under <vault>/Vernier/), Local storage (%LOCALAPPDATA%\Vernier: learning.db, settings.json, logs, tmp), Pause (cancels request, pen, microphone, speech; blocks capture) (+9 more)

### Community 48 - ".Read"
Cohesion: 0.31
Nodes (5): Data, IReadOnlyList, Name, AttachedDocument, AttachmentReader

### Community 49 - "Vernier.Tests.csproj"
Cohesion: 0.12
Nodes (14): net10.0, Anthropic (12.55.0), coverlet.collector (6.0.4), Microsoft.Data.Sqlite (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), System.Speech (10.0.12), WpfMath (2.1.0), xunit (2.9.3) (+6 more)

### Community 50 - "AppServices"
Cohesion: 0.06
Nodes (31): ContextHub, GoogleConnector, Action, Dispatcher, HttpClient, Id, List, Name (+23 more)

### Community 51 - "FilesPage"
Cohesion: 0.15
Nodes (10): AppSettings, CancellationTokenSource, IEnumerable, RoutedEventArgs, SelectionChangedEventArgs, Window, FilesPage, Owner (+2 more)

### Community 52 - "ComputerAction"
Cohesion: 0.09
Nodes (19): CancellationToken, Task, ActionType, Click, DoubleClick, Keys, Move, RightClick (+11 more)

### Community 53 - "Fact"
Cohesion: 0.24
Nodes (4): IdSequence, Fact, ClaudeCodeInvocationTests, ScreenIndexTests

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
Cohesion: 0.11
Nodes (17): AnnotationColors, AnnotationKind, Arrow, Bracket, Circle, Freehand, Highlight, Label (+9 more)

### Community 58 - "ExtractedText"
Cohesion: 0.14
Nodes (16): CancellationToken, Task, TimeSpan, OcrTextExtractor, Provenance, UiaElementExtractor, Provenance, ProvidesText (+8 more)

### Community 59 - "DiagnosticsPage"
Cohesion: 0.16
Nodes (9): CaptureExclusionResult, SelfTest, RoutedEventArgs, Task, DiagnosticsPage, Integration, Button, IRefreshable (+1 more)

### Community 60 - "CapturedFrame"
Cohesion: 0.14
Nodes (18): DateTimeOffset, CapturedFrame, MarkedDetail, MarkedRegion, Raw, Thumb, WindowBox, ContextSnapshot (+10 more)

### Community 61 - "ResearchTests"
Cohesion: 0.14
Nodes (14): TimeSpan, ISearchBackend, IWebFetcher, WebResearcher, CancellationToken, Dead, Fact, IReadOnlyList (+6 more)

### Community 62 - "VerificationReport"
Cohesion: 0.22
Nodes (10): IEnumerable, IReadOnlyList, TimeSpan, TokenUsage, TutorTurn, VerificationReport, Failures, HasFailures (+2 more)

### Community 63 - "Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)"
Cohesion: 0.20
Nodes (10): Convention: Core never references WPF or P/Invoke, Original Win32 C++ template files (unrelated leftovers), graphify-out/ project knowledge graph, Convention: no abstraction without a second implementation or a test seam, Rule: always launch tests/screenshots with --data <temp dir>, Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF), Vernier.App project (WPF, Platform/Overlay/Views), Vernier.Tests (xUnit: logic, fixture, real-Windows desktop, opt-in live) (+2 more)

### Community 64 - "AnnotationOverlayManager"
Cohesion: 0.06
Nodes (32): DateTime, Dock, Action, Color, Dictionary, DispatcherTimer, EventArgs, Func (+24 more)

### Community 65 - "ConnectorException"
Cohesion: 0.05
Nodes (48): HttpMessageHandler, Pkce, Redirect, CancellationToken, HttpClient, HttpListener, HttpListenerContext, Listener (+40 more)

### Community 66 - "ITool"
Cohesion: 0.08
Nodes (33): HttpClient, IEnumerable, ContextTools, GetContext, Definition, MediaControl, Definition, SessionRecall (+25 more)

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
Cohesion: 0.25
Nodes (12): Excerpt, CancellationToken, Dead, HttpStatusCode, IReadOnlyList, Page, Status, Task (+4 more)

### Community 71 - "Parser"
Cohesion: 0.15
Nodes (8): Expr, Parser, AtEnd, Peek, Position, InlineData, Theory, ExpressionTests

### Community 72 - "Ink"
Cohesion: 0.08
Nodes (23): atomic, HANDLE, mutex, thread, vector, Ink, argb, context (+15 more)

### Community 73 - "ClaudeCodeProvider"
Cohesion: 0.10
Nodes (20): ToolSession, Definitions, McpConfig, McpServerName, Turn, IAsyncEnumerable, IEnumerable, List (+12 more)

### Community 74 - "LibraryPage"
Cohesion: 0.26
Nodes (5): SessionRow, RoutedEventArgs, LibraryPage, SessionRow, TextChangedEventArgs

### Community 75 - ".The_real_claude_cli_connects_to_vernier_over_mcp_lists_the_tool_and_calls_it"
Cohesion: 0.17
Nodes (11): CancellationToken, Fact, JsonObject, Task, CodeTool, Definition, Runs, LiveClaudeCodeTests (+3 more)

### Community 76 - "PathGuard"
Cohesion: 0.10
Nodes (14): ScopedFiles / PathGuard (confined file access, Propose then Apply), Threat: path traversal / unintended file writes, CancellationToken, GeneratedRegex, IEnumerable, IReadOnlyCollection, Regex, PathGuard (+6 more)

### Community 77 - "Manual verification checklist"
Cohesion: 0.13
Nodes (15): Accounts via vendor CLIs (CliAccounts), ClaudeCodeProvider (claude -p stream-json subprocess, Claude Code sign-in), Reference project: claude-overlay, Manual: Accounts sign-in, Manual: Ask, Pen and the capture light, Manual: capture exclusion with your conferencing tool, Manual verification checklist, Items marked not verified by the author (multi-monitor, scaling, Codex, real mic, pro apps) (+7 more)

### Community 78 - ".OnProviderFieldChanged"
Cohesion: 0.29
Nodes (4): EndpointText, ModelText, TestButton, VisionCheck

### Community 79 - "ScreenIndex"
Cohesion: 0.10
Nodes (19): Item, Score, DateTimeOffset, Dictionary, IReadOnlyList, List, ResolvedTarget, ScreenIndex (+11 more)

### Community 80 - "NativeInk"
Cohesion: 0.11
Nodes (10): IDisposable, IReadOnlyList, NativeGlow, Frames, Hwnd, NativeInk, Hwnd, LastError (+2 more)

### Community 81 - ".OnStartup"
Cohesion: 0.16
Nodes (6): ExitEventArgs, Application, App, Task, FirstRunWindow, StartupEventArgs

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
Cohesion: 0.11
Nodes (21): anthropic, anthropic_exceptions, anthropic_models_messages, Vernier.Core.Settings, Vernier.Core.Security, Vernier.Core.Providers, Vernier.Core.Knowledge, Vernier.Core.Agents (+13 more)

### Community 88 - "HotkeyManager"
Cohesion: 0.20
Nodes (7): Action, Dictionary, HwndSource, List, HotkeyManager, Unavailable, EventArgs

### Community 89 - "TutorPanel"
Cohesion: 0.22
Nodes (7): Talk, KeyEventArgs, MouseButtonEventArgs, TranscriptItem, TutorSession, TutorPanel, Session

### Community 90 - "Episode"
Cohesion: 0.06
Nodes (30): From, SemaphoreSlim, Task, EpisodeSummariser, To, Episode, ActiveSeconds, App (+22 more)

### Community 91 - "Rect"
Cohesion: 0.21
Nodes (8): Rect, DrawingContext, DrawingContext, Point, Fact, InlineData, Theory, MarkShapesTests

### Community 92 - ".RunAsync"
Cohesion: 0.09
Nodes (22): Args, ExitCode, File, Process, CancellationToken, Func, IReadOnlyDictionary, IReadOnlyList (+14 more)

### Community 93 - "DiagnosticLog"
Cohesion: 0.33
Nodes (4): Exception, IReadOnlyList, LinkedList, DiagnosticLog

### Community 94 - "ScreenContextTests"
Cohesion: 0.24
Nodes (9): CancellationToken, DateTimeOffset, IReadOnlyList, Task, Fixed, Provenance, NoText, Provenance (+1 more)

### Community 95 - "ContextEngine"
Cohesion: 0.08
Nodes (22): App, Key, SessionItem, Since, DateTimeOffset, Dictionary, HashSet, IReadOnlyList (+14 more)

### Community 96 - "PxRect"
Cohesion: 0.09
Nodes (17): ControlTemplate, IEnumerable, Guid, NoteDismissWindow, Handle, NoteId, CaptureTarget, None (+9 more)

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
Cohesion: 0.38
Nodes (5): GeneratedRegex, Regex, Text, Title, Readable

### Community 105 - "ContextHub"
Cohesion: 0.12
Nodes (13): GlobalSystemMediaTransportControlsSessionManager, ScreenReader, CancellationTokenSource, DateTimeOffset, DispatcherTimer, Func, Task, TimeSpan (+5 more)

### Community 106 - ".BuildRequest"
Cohesion: 0.08
Nodes (29): Vernier.Core project (net10.0, no UI), Verified: real tutoring turn through Claude Code CLI (0.5*2*3^2 = 9 recomputed), Verified: real screen-aware turn end to end (9 J, 4 checks passed, 2 annotations mapped), Attachment, TutorMode, Annotate, Ask, Assist (+21 more)

### Community 107 - ".OnRate"
Cohesion: 0.50
Nodes (3): RoutedPropertyChangedEventArgs, RateSlider, Slider

### Community 108 - "DeleteSession"
Cohesion: 0.50
Nodes (4): DeleteSession, Toggle, ToVault, Button

### Community 109 - ".List"
Cohesion: 0.10
Nodes (22): MessageParam, NoteHit, Role, CancellationToken, DateTimeOffset, GeneratedRegex, HashSet, IEnumerable (+14 more)

### Community 110 - "Vernier.Core.Geometry"
Cohesion: 0.17
Nodes (12): Vernier.App.Platform, Vernier.Core.Geometry, Vernier.Core.Annotations, system_diagnostics, system_runtime_interopservices, system_runtime_interopservices_windowsruntime, system_speech_recognition, system_speech_synthesis (+4 more)

### Community 111 - "TitleNormaliser"
Cohesion: 0.45
Nodes (3): GeneratedRegex, Regex, TitleNormaliser

### Community 112 - "MarkPainter"
Cohesion: 0.17
Nodes (13): Alpha, Brush, Colour, Dashed, Halo, IEasingFunction, Pen, Color (+5 more)

### Community 113 - "WorkContext"
Cohesion: 0.19
Nodes (11): DateTimeOffset, IReadOnlyList, LinkedList, TimeSpan, ActivitySpan, Length, ActivityTimeline, Recent (+3 more)

### Community 114 - "Search"
Cohesion: 0.67
Nodes (3): Search, SessionText, TextBox

### Community 120 - "GlassSurface"
Cohesion: 0.09
Nodes (19): DrawingVisual, MouseEventArgs, Action, DrawingContext, EventArgs, Guid, IReadOnlyList, List (+11 more)

### Community 121 - "Security and privacy document"
Cohesion: 0.21
Nodes (12): Security rules (non-negotiable), Window capture policy (IWindowCapturePolicyService over INativeWindowApi), API keys: environment variable, else Windows Credential Manager, Window capture exclusion (SetWindowDisplayAffinity WDA_EXCLUDEFROMCAPTURE on own windows), Consent gates enforced in code (AllowScreenCapture, AllowMicrophone, AllowSendNotesToRemote, AllowComputerControl), No telemetry, analytics or crash reports, Runs unelevated (asInvoker), Security and privacy document (+4 more)

### Community 122 - "ink.cpp"
Cohesion: 0.16
Nodes (19): VN_API, vn_rect, callback, thread, Now(), Run(), SegDist(), vn_ink_clear() (+11 more)

### Community 123 - ".Tools_run_as_soon_as_their_arguments_arrive_and_the_assistant_turn_is_echoed_with_its_thinking"
Cohesion: 0.12
Nodes (14): Serving, Dictionary, MemorySecrets, Bodies, Fact, HttpListener, List, Listener (+6 more)

### Community 124 - "glow.cpp"
Cohesion: 0.19
Nodes (15): d2d1_1, dcomp, dwmapi, VN_API, vn_rect, Dither(), thread, Intensity() (+7 more)

### Community 125 - "Duplication"
Cohesion: 0.11
Nodes (18): ID3D11Device, ID3D11DeviceContext, ID3D11Texture2D, IDXGIOutputDuplication, ComPtr, mutex, Duplication, context (+10 more)

### Community 126 - "common.h"
Cohesion: 0.12
Nodes (13): algorithm, atomic, client, malloc, memory, mutex, sdkddkver, stdint (+5 more)

### Community 127 - "WebResearcher.cs"
Cohesion: 0.21
Nodes (7): Exception, AttachmentException, HttpClient, DuckDuckGoSearch, SafeWeb, SearchUnavailableException, system_net_sockets

### Community 128 - "SapiSpeechInput"
Cohesion: 0.09
Nodes (13): SpeechRecognitionEngine, SpeechSynthesizer, StringBuilder, Task, ISpeechInput, IsListening, ISpeechOutput, IsSpeaking (+5 more)

### Community 129 - "TranscriptItem"
Cohesion: 0.22
Nodes (8): INotifyPropertyChanged, TranscriptItem, Checks, IsUser, Meta, Notices, Role, Text

### Community 130 - "duplication.cpp"
Cohesion: 0.21
Nodes (14): d3d11, dxgi1_2, CoveredFraction(), HRESULT, vector, VN_API, vn_rect, EnsureStaging() (+6 more)

### Community 131 - "KnowledgeStoreTests"
Cohesion: 0.18
Nodes (8): Dir, JournalEntry, Fact, InlineData, IOException, Theory, KnowledgeStoreTests, Vault

### Community 132 - "NoteRenderer"
Cohesion: 0.19
Nodes (13): Card, Scale, SolidColorBrush, BitmapSource, Color, Dictionary, FontFamily, FormulaControl (+5 more)

### Community 133 - "AnnotationSurfaceWindow"
Cohesion: 0.23
Nodes (6): DpiScale, AnnotationSurfaceWindow, Handle, Ink, MarkCount, Monitor

### Community 134 - "Depth"
Cohesion: 0.24
Nodes (8): MARGINS, Result, Dictionary, DllImport, Window, Depth, MARGINS, Result

### Community 135 - ".SaveSettings"
Cohesion: 0.21
Nodes (3): GoogleConnectButton, RemoveSample, Button

### Community 136 - "ToolResult"
Cohesion: 0.14
Nodes (18): Kind, CancellationToken, JsonObject, Task, CancellationToken, Func, Id, JsonObject (+10 more)

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
Cohesion: 0.38
Nodes (5): MediaState, DateTimeOffset, Fact, TimeSpan, ContextEngineTests

### Community 141 - "ProposedAnnotation"
Cohesion: 0.17
Nodes (12): ProposedAnnotation, Confidence, H, Kind, Label, Semantic, Target, W (+4 more)

### Community 142 - "imaging.cpp"
Cohesion: 0.22
Nodes (9): combaseapi, ComScope, hr, HRESULT, VN_API, vn_free(), vn_png_encode(), vn_version() (+1 more)

### Community 143 - "CheckResult"
Cohesion: 0.18
Nodes (8): CheckResult, CheckStatus, Advisory, Failed, Passed, Skipped, Dictionary, Units

### Community 144 - ".Ask"
Cohesion: 0.24
Nodes (8): Cancel, Dialog, Button, CancellationToken, Ok, Task, Window, ConfirmDialog

### Community 145 - ".ObserveForToolAsync"
Cohesion: 0.29
Nodes (5): Func, Task, CancellationToken, Task, ToolObservation

### Community 146 - "FakeExtractor"
Cohesion: 0.16
Nodes (10): Processes, Profile, AppProfile, AppProfiles, IReadOnlyList, FakeExtractor, Calls, Provenance (+2 more)

### Community 147 - "ResearchLedger"
Cohesion: 0.21
Nodes (8): ConcurrentDictionary, IReadOnlyCollection, ResearchDemand, ResearchGate, ResearchLedger, Calls, Urls, Working

### Community 148 - ".ReviewAndRunAsync"
Cohesion: 0.13
Nodes (11): 2. Diagnosis of v0.3.0 (measured or evidenced), Task, Window, AssistedInput, Stopwatch, Func, ITestOutputHelper, List (+3 more)

### Community 149 - "OperatorGuide.cs"
Cohesion: 0.40
Nodes (4): Lazy, OperatorGuide, Text, system_reflection

### Community 150 - "PipelineStage"
Cohesion: 0.11
Nodes (19): FrameworkElement, DependencyProperty, LevelScale, Level, Label, Typeface, StageScale, Busy (+11 more)

### Community 151 - "Screen geometry (CaptureGeometry, MonitorInfo, DpiMath)"
Cohesion: 0.22
Nodes (9): Convention: all screen geometry is physical pixels on the virtual desktop, ChatRunner (timeout, bounded retry, vision gate), Screen geometry (CaptureGeometry, MonitorInfo, DpiMath), Stage: Present (annotations mapped image to physical pixels), Stage: Teach (IChatProvider.StreamAsync via ChatRunner), Risk: model-placed coordinates are approximate, Risk: cost surprises, Risk table (+1 more)

### Community 152 - ".Options"
Cohesion: 0.22
Nodes (7): Provider, DateTimeOffset, IReadOnlyList, JsonSerializerOptions, Label, RouteLog, RouteRecord

### Community 153 - "WriteNote"
Cohesion: 0.19
Nodes (12): CancellationToken, IEnumerable, IReadOnlyList, JsonObject, Task, NoteTools, ReadNote, Definition (+4 more)

### Community 154 - "Fact"
Cohesion: 0.46
Nodes (3): Fact, AttachmentReaderTests, ZipArchive

### Community 155 - "FreeSpace"
Cohesion: 0.15
Nodes (11): Placement, Height, Width, FreeSpace, Area, BusyCount, CellPx, Cols (+3 more)

### Community 156 - ".Monitors"
Cohesion: 0.10
Nodes (18): MONITORINFO, OcrEngine, RECT, CancellationToken, IReadOnlyList, R, DuplicationSession, HasFrame (+10 more)

### Community 157 - ".Research_is_required_by_code_for_pages_tools_unknown_apps_and_how_questions"
Cohesion: 0.40
Nodes (3): IPAddress, InlineData, Theory

### Community 158 - "TitleAndAddressTests"
Cohesion: 0.47
Nodes (3): InlineData, Theory, TitleAndAddressTests

### Community 159 - "FakeUser32"
Cohesion: 0.08
Nodes (18): Api, FakeUser32, InvalidDataException, Service, Dictionary, HashSet, InlineData, Theory (+10 more)

### Community 160 - "Button"
Cohesion: 0.50
Nodes (3): Send, SpeechPause, Button

### Community 165 - "FreeSpaceTests"
Cohesion: 0.27
Nodes (7): Bgra, H, Fact, InlineData, Theory, FreeSpaceTests, W

### Community 166 - ".RunAsync"
Cohesion: 0.40
Nodes (3): Func, Task, StaThread

### Community 167 - "Vernier.Tests"
Cohesion: 0.23
Nodes (4): Vernier.Core.Context, Vernier.App.Context, Vernier.Tests, xunit

### Community 168 - "SettingsStore"
Cohesion: 0.40
Nodes (4): JsonSerializerOptions, SettingsStore, LoadWarning, Path

### Community 170 - ".OnPolicyChanged"
Cohesion: 0.50
Nodes (3): PolicyBox, SelectionChangedEventArgs, ComboBox

### Community 177 - "Agent harness plan (revision 4)"
Cohesion: 0.22
Nodes (8): 1. Evidence gathered (reconnaissance), 4. Milestones (order revised after review R1), 4b. Revision 5 plan (owner request of 2026-10-16), 5. Measurements, Agent harness plan (revision 4), Decisions, Milestones, Reflection: what the owner asked for, across both sessions

### Community 178 - "ToolTurn"
Cohesion: 0.09
Nodes (22): ConcurrentQueue, CancellationTokenSource, ConcurrentDictionary, IReadOnlyList, IReadOnlySet, SemaphoreSlim, ToolTurn, ActLock (+14 more)

### Community 181 - "SKILL.md"
Cohesion: 0.20
Nodes (9): Context and memory, Draw with tools, never with code, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain, Speed (+1 more)

### Community 182 - "OperatorGuide.md"
Cohesion: 0.20
Nodes (9): Context and memory, Draw with tools, never with code, Graphs: show the area, let Vernier compute it, How the learner uses Vernier, Notes, Operating the computer, Research before you explain, Speed (+1 more)

### Community 183 - "ContextOptions"
Cohesion: 0.14
Nodes (11): TimeSpan, ContextOptions, Idle, MaxEntitiesPerEpisode, MaxEpisodesInMemory, MaxLinesPerEpisode, MinimumDwell, Rejoin (+3 more)

### Community 187 - "NoteContent"
Cohesion: 0.17
Nodes (12): IReadOnlyList, NoteContent, NoteLine, NoteLineKind, Check, Error, Note, Result (+4 more)

### Community 188 - "Side"
Cohesion: 0.29
Nodes (6): Side, Above, Auto, Below, Left, Right

### Community 192 - "WndProc"
Cohesion: 0.33
Nodes (6): HWND, LPARAM, LRESULT, UINT, WPARAM, WndProc()

### Community 195 - "WindowCapturePolicyService"
Cohesion: 0.07
Nodes (27): Window, CapturePolicyMode, RequestExclusion, ShareSafe, Standard, Dictionary, IReadOnlyList, DisplayAffinity (+19 more)

## Knowledge Gaps
- **756 isolated node(s):** `Hopefully-this-goes-somewhere`, `device`, `context`, `dup`, `latest` (+751 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1377 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TutorSession` connect `TutorSession` to `LearningStore`, `TranscriptItem`, `ProviderSettings`, `Win32`, `.ObserveForToolAsync`, `Runtime pipeline (TutorPipeline.RunAsync)`, `SessionMemory`, `PipelineStage`, `AppSettings`, `Annotation`, `AnthropicProvider`, `.Review`, `ScreenCaptureService`, `AskBar`, `Design system document`, `Tools.cs`, `AppServices`, `ToolTurn`, `RoutedEventArgs`, `ContextOptions`, `CapturedFrame`, `VerificationReport`, `Vernier (screen-aware tutor for Windows, C#/.NET 10/WPF)`, `WindowCapturePolicyService`, `.Cancel`, `Vernier.Core.Agents`, `DiagnosticLog`, `.BuildRequest`, `Security and privacy document`?**
  _High betweenness centrality (0.071) - this node is a cross-community bridge._
- **Why does `SettingsPage` connect `SettingsPage` to `.OnPrivacy`, `.OnProviderChanged`, `TextBox`, `ProviderSettings`, `.SaveSettings`, `MainWindow`, `.OnRate`, `.OnProviderFieldChanged`, `system_windows`, `UserControl`, `AppSettings`, `DiagnosticsPage`?**
  _High betweenness centrality (0.062) - this node is a cross-community bridge._
- **Why does `AppServices` connect `AppServices` to `SapiSpeechInput`, `system_text_json_nodes`, `LearningStore`, `.SaveSettings`, `TrayIcon`, `.ObserveForToolAsync`, `AppSettings`, `TutorSession`, `.Initialize`, `Annotation`, `.Options`, `SettingsStore`, `Tools.cs`, `AnnotationOverlayManager`, `ConnectorException`, `WindowCapturePolicyService`, `.OnStartup`, `DiagnosticLog`, `McpServer`?**
  _High betweenness centrality (0.058) - this node is a cross-community bridge._
- **Are the 29 inferred relationships involving `PxRect` (e.g. with `.The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible()` and `.Frame()`) actually correct?**
  _`PxRect` has 29 INFERRED edges - model-reasoned connections that need verification._
- **Are the 6 inferred relationships involving `ToolTurn` (e.g. with `.SmokeCheckAsync()` and `.Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied()`) actually correct?**
  _`ToolTurn` has 6 INFERRED edges - model-reasoned connections that need verification._
- **Are the 3 inferred relationships involving `LearningStore` (e.g. with `.Episodes_survive_a_restart_without_screen_text_and_old_ones_and_forgotten_ones_are_deleted()` and `.A_real_tutoring_turn_runs_through_the_whole_pipeline()`) actually correct?**
  _`LearningStore` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Hopefully-this-goes-somewhere`, `device`, `context` to the rest of the system?**
  _756 weakly-connected nodes found - possible documentation gaps or missing edges._