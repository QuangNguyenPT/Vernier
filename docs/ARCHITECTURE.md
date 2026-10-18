# Architecture

## Stack decision

The repository held only the untouched Visual Studio Win32 C++ project template. Nothing in it was product code, so the app was built as a new solution beside it (`Vernier.sln`) in C# on .NET 10 with WPF. WPF was chosen because per-pixel transparent, click-through, topmost windows and Win32 interop are straightforward and well documented there.

C++ is used only where it reaches a Windows facility WPF cannot use well, in one small DLL (`native/Vernier.Native`, C++20, C ABI, built by `scripts/build-native.ps1` at `/W4 /WX`):

| Part | Why native | Fallback |
|---|---|---|
| DXGI Desktop Duplication (`duplication.cpp`) | Frames stay on the GPU; only the region asked for is copied; a box-filtered luma thumbnail for change detection | GDI BitBlt |
| WIC scale + PNG encode (`imaging.cpp`) | High-quality cubic downscale and encode without WPF's imaging stack. Measured only ~15 % faster than WPF, so the app does not use it yet | WPF `PngBitmapEncoder` (what the app uses) |
| Pen ink (`ink.cpp`) | The stroke follows the cursor on a thread of its own (a 1 ms poll while the button is down), each segment is rasterised into the 256 px tiles under it and handed to DWM, so a stroke never waits for WPF; it holds 1.4 s after the pen lifts, fades over 0.35 s by itself, and the finished stroke is reported back for the question. Measured 27 ms from call to pixels on screen with the UI thread blocked | WPF live stroke, shown as marks that expire by themselves |
| Edge light (`glow.cpp`) | DirectComposition surfaces on their own thread paced by `DwmFlush`, so the travelling wave keeps ~60 fps while the UI thread is busy; a no-redirection-bitmap window that is click-through and excluded from capture | Four WPF strip windows per monitor |

`Platform/Native.cs` wraps it (`Native.Available`, `DuplicationSession`, `NativeGlow`, `EncodePng`); a missing or failing DLL degrades, it never breaks the app. Overlay marks, OCR and UI Automation stay managed: they are already OS services or within a frame in WPF.

## Projects

| Project | Target | Role |
|---|---|---|
| `Vernier.Core` | `net10.0` | All logic. No UI, no P/Invoke. |
| `Vernier.App` | `net10.0-windows10.0.19041.0` | WPF UI and Windows implementations of Core interfaces. Assembly name `Vernier`. |
| `Vernier.Tests` | `net10.0-windows10.0.19041.0` | xUnit tests for both. |

Dependencies: `Microsoft.Data.Sqlite` (MIT), `Anthropic` (official C# SDK, MIT), `System.Speech` (MIT), `WpfMath` 2.1 (MIT, fonts OFL). Windows OCR, UI Automation, DWM, DXGI, WIC and DirectComposition come with the OS.

## Runtime pipeline

```
 UI thread                         background
 ─────────                         ──────────
 AskBar (Ask key / Pen ink) → TutorSession.AskAsync(question, ScreenAsk)
   └─ capture monitor (GDI, self-excluded, ink painted in, marked area cropped) ─► Observer ──► ContextSnapshot
                                         │ accessibility tree, else Windows OCR; dHash change detection
                                         ▼
                                   TutorPipeline.RunAsync
                                     Interpret  MasteryTutor.BuildRequest (profile, learning digest, untrusted fences)
                                     Teach      IChatProvider.StreamAsync via ChatRunner (timeout, bounded retry)
                                     Verify     Verifier.Check  (code: arithmetic, units, annotation geometry)
                                                └ optional second model, only if code could check nothing
                                     Correct    at most once, with the verifier's findings
                                     Confirm    re-verify
                                     Present    annotations mapped image → physical pixels
                                     Mastery    LearningStore (evidence, misconception, card, formula)
   ◄──────────────────────────────── TutorTurn
```

Before the pipeline runs, `TutorSession` decides whether the question can be sent at all: a screen question in Share-Safe mode, with capture off, or whose capture Windows refused, and a question with attachments the learner has not agreed to send to a remote provider, are answered with the reason and never reach a model.

Stages are skipped when they do not apply: no Observe for a question typed in the workspace; no Correct/Confirm when checks pass; no second-model call when a deterministic check passed.

## Keeping up with a screen that changes

The tutor sees one still per question while the learner's screen keeps moving. Three things keep the two in step:

| Problem | Mechanism |
|---|---|
| A new capture differs from the last one and the model reads that as its own earlier mistake | `Observer` compares each capture with the previous one and writes `ContextSnapshot.SinceLast` ("12 s ago; same window; part of the picture has changed"). The system prompt states the screen is live and forbids retracting an answer because the picture changed. |
| Earlier turns lose their picture | `MasteryTutor.Remember` stores each question in history with the time, application and window title it was asked about, and whether the learner had marked something. |
| A small change on a large screen was treated as "unchanged" and the old text reused | `CapturedFrame.Thumb` (256x160 grayscale) and `ScreenDiff.Fraction` replace the 64-bit hash for this decision; under 0.15 % changed cells counts as the same picture. |
| Marks drawn at fixed screen positions point at the wrong thing after a scroll, zoom or video frame | After the tutor draws, `AnnotationOverlayManager.Watch` samples the marked area about every 1.2 s (48x48 grayscale, local, never stored or sent). When 10 % of it differs from the baseline, all marks are cleared and the ask bar says so. Sampling pauses while a capture is being read or the ask bar covers the area, and follows a window that moves. |

## Attachments

`AttachmentReader` (Core) reads `.pptx` and `.docx` with the standard zip and XML readers (DTDs prohibited, sizes capped), and text files directly; pictures are decoded and re-encoded as PNG no larger than the screenshot limit (`ScreenCaptureService.NormalizePicture`). Up to four pictures embedded in a deck or document come along. Text reaches the model inside an `<untrusted>` fence; pictures follow the screenshot and are labelled as not the screen. PDF is not read. Attachments stay pinned to the ask bar until removed.

## The three agents and their contracts (`Core/Agents`)

| Agent | Class | Input | Output | Talks to a model? |
|---|---|---|---|---|
| Observer | `Observer` | `CapturedFrame` | `ContextSnapshot` (texts with provenance and confidence, equations, code lines, changed flag, ambiguities, app profile) | No |
| Mastery Tutor | `MasteryTutor` | mode, question, snapshot, history, `LearnerProfile`, `LearningSummary` | `ChatRequest`; reply parsed to `TutorOutput` (prose + `TutorStructured`) | Yes (via provider) |
| Verifier | `Verifier` | `TutorOutput`, `CaptureGeometry` | `VerificationReport` of `CheckResult` (Passed / Failed / Advisory / Skipped, with method) | Only the optional second opinion |

The tutor's structured output is a fenced ```` ```vernier ```` JSON block: concept, numeric `claims` (expression + stated value + units), `annotations` in image pixels, mastery `check`, graded `evidence`, `misconception`, `actions`. A missing or malformed block degrades to plain prose with an advisory.

Independence: the Verifier's deterministic checks do not use the tutor's reasoning at all. A second-model opinion is labelled `second-model` only when provider or model differs from the tutor's; otherwise `same-model` and never reported as a pass.

## Providers (`Core/Providers`)

`IChatProvider` = `StreamAsync`, `ListModelsAsync` (capability discovery), `Capabilities` (vision, local, usage reporting, plain-language notes).

| Adapter | Transport | Notes |
|---|---|---|
| `AnthropicProvider` | Official `Anthropic` SDK, Messages streaming | Default model `claude-opus-5-5`, adaptive thinking (model default). `stop_reason: refusal` is surfaced; no fallback model is substituted. SDK retries disabled (ChatRunner owns retries). |
| `OpenAiCompatibleProvider` | `POST {base}/chat/completions`, SSE | OpenAI, Moonshot/Kimi (`https://api.moonshot.ai/v1`), and local servers (Ollama, LM Studio, llama.cpp). `max_tokens` field name is per provider. |
| `GeminiProvider` | `POST {base}/models/{m}:streamGenerateContent?alt=sse` | Key sent in `x-goog-api-key` header, never in the URL. |
| `CodexCliProvider` | `codex exec --json --ephemeral --sandbox read-only -o <file>` subprocess | Uses the user's ChatGPT sign-in in the Codex CLI. Images passed with `-i`. Not streamed: the final message is read from the output file. Codex has no system-prompt flag, so instructions lead the prompt. |
| `ClaudeCodeProvider` | `claude -p --input-format stream-json --output-format stream-json` subprocess | Uses the user's Claude Code sign-in. Runs with `--restricted --tools "" --setting-sources "" --disable-slash-commands --strict-mcp-config`, its own tools off; images go inline in the input stream (no files); Vernier's tools reach it through the loopback MCP server, allowed as `mcp__vernier__*` only. The process is started when the ask bar opens (prewarm) and lives for one answer. |

Model names are not hard-coded for OpenAI, Gemini, Moonshot or local servers: the user picks from **List models**. `ChatRunner` applies a per-call timeout, retries transient failures at most 3 times and only before any text is shown, never retries auth errors or long rate-limit windows, and refuses to send images to a model not marked vision-capable.

## Screen geometry

One unit everywhere: **physical pixels on the virtual desktop**.

- `CaptureGeometry(SourcePhysical, ImageWidth, ImageHeight)` records what was captured and the size actually sent. `ImageToPhysical` / `PhysicalToImage` are the only conversions for model coordinates. `QuantizationUncertaintyPx` is carried onto every tutor annotation.
- `MonitorInfo` + `DpiMath` convert physical ↔ DIPs for one monitor. The app is Per-Monitor-V2 aware (`app.manifest`).
- Windows that must sit at exact screen positions (glass, capture-light strips, ask bar) are placed with `SetWindowPos` in physical pixels, not WPF units.

## Accounts (`Core/Providers/CliAccounts.cs`)

"Sign in with your Claude / ChatGPT account" is implemented only through each vendor's own CLI. `CliAccounts.StatusAsync` runs `claude auth status` / `codex login status` and reads the answer; **Sign in…** opens a visible terminal running `claude auth login` / `codex login`. Vernier holds no OAuth client, token or password, and reads none of the tools' credential files.

## Shell: tray, glass, ask bar

- **Tray** (`Platform/Tray.cs`, `Shell_NotifyIcon`): the app's home. `ShutdownMode` is explicit; `MainWindow.Closing` cancels and hides unless `AppServices.Quitting`. The icon is re-added when Explorer restarts.
- **Glass** (`AnnotationSurfaceWindow`): one transparent, topmost, `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` window per monitor, started once and kept for the life of the app. It draws the `AnnotationStore` (tutor marks and learner ink), each mark over a dark halo. Arming the pen removes `WS_EX_TRANSPARENT` so the glass takes the mouse; nothing else changes.
- **Capture light**: the native DirectComposition light per monitor when `Vernier.Native` loads (modes: off, steady when the pen is armed, travelling wave while looking, a small top-centre pill while context is being read), otherwise `EdgeStripWindow` strips with a sliding mask. Honours the Windows animation setting (a breath instead of travel).
- **`AnnotationOverlayManager`** owns both, rebuilds them on `DisplaySettingsChanged`, collects pen strokes (`Ink`, `InkBounds`), follows the tracked window so marks move with it, and removes marks whose target changed (see above).
- **Ask bar** (`Views/AskBar.cs`): the only interactive overlay. It holds the pen toggle, the attach button and the pinned attachments. Nothing is sent until Enter or Ask. It remembers which application was in front when it was called, sends `ScreenAsk(monitor, ink, foreground)`, and places itself beside the ink (`OverlayPlacement.Beside`) or at top centre.
- **Pen capture** (`ScreenCaptureService.Capture(..., ink)`): the monitor is grabbed with Vernier's windows excluded, the learner's strokes are painted into the image so the model sees them as drawn, `MarkedRegion` gives their bounds in image pixels, and `MarkedDetail` is a full-resolution crop of the un-inked area sent as a second image.
- `AnnotationStore`: immutable list with undo/redo (Clear included), hide, expiry, `Rebase` when the target window moves, `DropOutside` when a monitor disappears.

## Window capture policy

`IWindowCapturePolicyService` (Core) over `INativeWindowApi` (user32 in App). Windows register at `SourceInitialized` through `AppServices.TrackWindow` and unregister on close. Status is one of NotRequested / Requested / Applied / Unsupported / Failed, derived from the API return value and a `GetWindowDisplayAffinity` read-back. See `SECURITY_AND_PRIVACY.md` for what "Applied" does and does not mean.

## Persistence

`LearningStore`: one SQLite file, WAL, schema versioned by `PRAGMA user_version`, migrations run in transactions. `integrity_check` on open; a damaged file is set aside and the newest backup restored. A backup is taken at each start (7 kept). FTS5 index over messages. Documented export in `DATA_FORMATS.md`.

Settings: `settings.json` (no secrets). Secrets: environment variable, then Windows Credential Manager.

## Context engine (`Core/Context`, `App/Context`)

Context is a system that runs all the time, built by code; a model is never required for it.

- **Sources** (`ContextHub`): WinEvent hooks for foreground, title and name changes (out of context, Vernier's own process skipped; `InternalGetWindowText` so a hung window cannot block), `GetLastInputInfo` for idle, the Windows media session (GSMTC) every 2 s, the browser address and selection through UI Automation (browsers only, 1.5 s budget), and a screen reader that diffs a 32×20 thumbnail (native duplication or GDI) and runs local OCR once a change has settled for 1.2 s, at most every 4 s.
- **Episodes** (`ContextEngine`): a stretch of work on one app and document. Keys come from `TitleNormaliser` (counters, progress, unsaved markers, browser and profile suffixes removed) and `UrlSanitiser` (query, fragment and sign-in parts dropped; YouTube keeps `v`/`list`). A 5 s dwell before a switch counts, a return within 20 min rejoins the episode, 2 min without input is idle. Each episode accumulates active time, media title and positions, rewinds, entities found on screen (equations, headings, identifiers; injection-shaped text dropped), recent lines (memory only), questions asked and an optional one-line summary.
- **Persistence**: SQLite schema v3 `episodes` (no screen text), 14-day retention, forget hour/today/all.
- **Selection**: at ask time episodes are ranked by recency × term overlap with the question plus priors (current, media, asked before), rendered as a bounded, sourced, aged block fenced as `context`, and shown in the ask bar as a chip. `EpisodeSummariser` may add a summary with a local model only.

## Drawing and writing on the screen

The tutor never writes drawing code. Everything it puts on the glass is a tool call that Vernier draws natively:

| Tool | What it does | Cost |
|---|---|---|
| `overlay_marks` | up to 12 marks in one call (circle, highlight, underline, arrow, point, bracket, label); a mark that cannot be placed is reported by number and the rest are still drawn; one stale check per mark, one UI hop for all | one round trip instead of one per mark (12 ms for three marks in the live test) |
| `overlay_mark` | a single mark | |
| `overlay_write` | a **note card**: a dark template (dark in every theme, because it floats over arbitrary content) with the tutor's blue rule, a title, lines in a handwriting face (Ink Free) or clean print (Sitka), formulas typeset by WpfMath, the final answer boxed. The card is drawn once to a bitmap and wipes in | the card lands on free space with no coordinates from the model (346 ms in the live test, mostly the first formula layout) |
| `overlay_clear` | removes the tutor's marks and cards (never the learner's ink) | |

The learner removes a single card with its × (`NoteDismissWindow`: a 24 px non-activating topmost window per card, kept in the card's corner by `AnnotationOverlayManager`; the glass itself stays click-through). Mark geometry is `Overlay/MarkShapes` (smooth, deterministic per mark); `MarkPainter` only chooses pens and the reveal.

**Free space** (`Core/Geometry/FreeSpace`): the captured pixels are divided into 24 px cells; a cell is busy when it holds detail (a wide spread of brightness: text, lines, diagrams). Located text and controls, the tutor's earlier marks, the learner's ink and Vernier's own always-on-top windows (the ask bar) are marked busy too, and busy cells are spread one cell outward. A summed-area table finds the window of the card's size with no busy cell nearest the thing the note is about, preferring its right (where people write), then below. The card tries its width, then two narrower and taller sizes; if nowhere is empty it takes the least busy spot and says it covers something.

**The learner's ink is a question in progress, not a mark.** It is drawn by the native layer, leaves the screen by itself, and is kept (as points, never in the annotation store when the native layer runs) to be painted into the capture and sent with the next question. Sending the question removes it at once; Undo and Clear marks take strokes back.

**Production instructions** live in one file, `Core/Agents/OperatorGuide.md` (embedded). It feeds the tutor's system prompt, the MCP server's `instructions` (a compact version, so any MCP client is told how to use it) and the installable skill `.claude/skills/vernier-operator/SKILL.md`; a test keeps the skill identical to the guide and checks the guide names every tool.

## Research, graph areas and re-fitting (Revision 9)

- **Research** (`Core/Research`): `ResearchPolicy` decides per answer, in code, whether it must be grounded (web page in front, a tool app such as Blender, an app Vernier does not know, a how/current question) and the question carries "Research: REQUIRED (reason)". `web_research` (`App/Agent/WebTools`) runs `WebResearcher`: `DuckDuckGoSearch` (HTML results, redirect unwrapped, ads skipped, challenge pages reported as unavailable) then up to 8 candidates fetched 4 at a time through `SafeWeb` until 5 load with at least 400 characters of readable text (`Readable`), at most two per site. Each working page goes into the turn's `ResearchLedger`. After the answer, `ResearchGate` re-asks once (tools kept) if a required answer read fewer than 3 working pages, and a notice says so if it still did not.
- **Graph areas**: `graph_area` takes two ticks per axis and up to 3 polygons in data units; `AxisCalibration` maps them to screenshot pixels and computes each area by the shoelace formula; the shapes are drawn as `Region` marks with their areas as labels.
- **Re-fit**: each tutor mark gets a `MarkAnchor` (half-scale grey patch of its target, from a capture with Vernier's windows excluded) once it settles. When the watched thumbnail changes, `MarkRefit.Decide` searches each patch (±50 % of the view's height, ±15 % of its width) with `IPatchMatcher`: native `vn_match` (match.cpp) or the managed `PatchMatcher`, the same algorithm with identical results. A confident, unambiguous match (score ≥ 0.8, at least 0.12 above the best elsewhere) moves the mark; otherwise it is removed alone; more than 70 % lost, or a new window title, clears all. Blank patches only follow their window.
- **Seeing the problem** (`Core/Diagrams`, `App/Overlay/DiagramRenderer`): `overlay_diagram` draws a space3d / complex / hypercube card on free space and `Geometry3D` works out the numbers; `DimensionPolicy` + `DimensionGate` force it for vector, complex, 3D and higher-dimension problems. `graph_area` follows the drawn curve (`InkMap.Trace`) or snaps corners onto it. `TexPlain` turns stray TeX into symbols wherever plain text is drawn.
- **Packaging**: single-file self-contained `Vernier.exe` (the .NET runtime's native parts inside, unpacked by .NET on first start) plus `Vernier.Native.dll`; the native module reports `VN_ABI` and a DLL with another number is not used.

## Tools (`Core/Tools`, `App/Agent`)

`ToolRegistry` + `ToolExecutor`: resolve → per-turn grant → schema validation → setting gate → (Act/Consequential) `PreviewAsync` then approval → execute → untrusted fencing. Native tool calling for Anthropic, OpenAI-compatible and Gemini; a loopback MCP server for the Claude Code CLI. Tool sets: `ScreenTools` (find, mark, marks, write, graph area, clear, look), `WebTools` (research), `ContextTools` (context, media control, video info), `ComputerTools` (`computer_act`), `NoteTools` (search, read, write) and `GoogleTools` (read, append); `TutorSession.ToolNamesFor` decides which are offered for a provider. `UiApprover` shows a non-modal topmost card that closes on timeout.

## Answers

`AnswerText` (Core) parses an answer into blocks (paragraph, heading, list, code, display maths) and spans (bold, italic, code, inline maths, superscript, subscript) and gives the spoken form of a formula. `RichAnswer` (App) renders it as a FlowDocument in Sitka with WpfMath formulas; it re-renders as the answer streams, and anything unterminated stays as text. Themes are resource dictionaries swapped by `AppServices.ApplyTheme`; `Platform/Depth.cs` asks DWM for rounded corners, the caption colour, the border colour and (ask bar) an acrylic backdrop.

## Knowledge

- `ScopedFiles` / `PathGuard`: all file access confined to one authorised folder; `Propose…` returns a `FileChange` with a diff. `ApplyAll` applies several changes as one transaction: every file is checked against its preview first, previous contents are journalled, a failed write restores them, and `Recover` (run at start) undoes a journal left by a crash.
- `ObsidianVault`: Markdown notes under `Vernier/`; one note per concept (`FindNote` by normalised title or alias), append-not-replace; `Search` by title and term frequency; `ProposeIndex` keeps `Vernier/Index.md` grouped by subject, rewriting only its marked block.
- `IKnowledgeGraphSource`: `GraphifySource` reads `graphify-out/graph.json` and can run the documented `graphify update <path>`; `WikiLinkGraphSource` is the always-available fallback.

## Assisted input ("computer use")

With tools, the tutor operates the application through `computer_act`, one step per call: the target is resolved from the screen index (no guessed boxes), `ActionPolicy.Review` checks the step, `ComputerTools.Refusal` refuses terminals, elevated or protected processes and Vernier itself, the preview marks the target, the learner approves, the step is re-checked against the live window (bounds, pixels under the target, nothing covering it) and sent by `SendInputDriver`, then the window is compared with the capture for an effect verdict. Esc stops it for the rest of the answer. Providers without tools keep the older path: the reply's `actions` → `ActionPolicy.Review` → numbered preview and one approval for the plan → `ActionExecutor`. `AppProfiles` recognises common professional applications by process name and gives the tutor the right vocabulary for them; there is no per-application plugin or scripting bridge.

## Suggestions and connectors

- `Proactive` (App) + `SuggestionPolicy` / `RewindWatch` (Core): code-only triggers (rewinding the same part of a video twice in five minutes), at most two an hour fifteen minutes apart, each dismissed kind waiting twice as long (20 min to a day). Shown as a tray notification; a click opens the ask bar with the question filled in.
- `GoogleOAuth` / `GoogleDocsClient` (Core) and `GoogleConnector` (App): installed-app OAuth with PKCE and a loopback redirect, `drive.file` scope, refresh token in Credential Manager; create, read and append to Vernier's own document.
