# Vernier

Screen-aware tutor for Windows. C# / .NET 10 / WPF. The original Win32 C++ template files in the repo root (`Hopefully-this-goes-somewhere.*`, `framework.h`, …) are unrelated leftovers; do not edit or build them.

## Layout

- `src/Vernier.Core` (net10.0, no UI): geometry, annotations, providers, agents (Observer / MasteryTutor / Verifier / TutorPipeline), verification (expression + units), learning store (SQLite), knowledge (vault, graph, scoped files), security, settings, window capture policy logic.
- `src/Vernier.App` (net10.0-windows, WPF, assembly `Vernier`): `Platform/` (P/Invoke, capture, OCR, UIA, input, hotkeys, speech), `Platform/Tray.cs` (notification-area icon), `Overlay/` (glass per monitor, capture-light edge strips, pen ink), `Views/` (`AskBar`, workspace window and pages), `TutorSession.cs` (UI-thread session state), `App.xaml.cs` (`AppServices` composition root).
- `tests/Vernier.Tests`: xUnit. Logic tests, fixture-based provider tests, real-Windows tests (`[Collection("desktop")]`), opt-in live tests.
- `docs/`: architecture, design system, security, plan, verification report, manual checklist.

## Commands

```bash
powershell -ExecutionPolicy Bypass -File scripts/build-native.ps1   # optional native module (needs VS C++ tools)
dotnet build Vernier.sln
dotnet test tests/Vernier.Tests                      # needs an interactive desktop session with the display on
VERNIER_LIVE=1 dotnet test tests/Vernier.Tests       # also runs live Claude Code CLI tests (uses the user's plan)
dotnet run --project src/Vernier.App
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1   # tests + self-contained publish + smoke test
src/Vernier.App/bin/Debug/net10.0-windows10.0.19041.0/Vernier.exe --data <dir> --screenshots docs/screenshots
```

Always pass `--data <temp dir>` when launching for tests or screenshots so the real profile in `%LOCALAPPDATA%\Vernier` is not touched.

## Product shape (do not drift from this)

- The app is tray-resident. Closing a window hides it; only tray › Quit (`AppServices.Quitting`) exits.
- The learner has two tools: Ask and Pen. Arrow/box/circle/bracket/label and note cards are produced by the tutor only. Do not add user drawing tools. The learner's ink is transient: drawn by the native layer (`ink.cpp`), faded after 1.4 s, held only as points for the next question and removed when it is sent; it must not become a stored mark.
- The tutor draws only through tools (`overlay_marks` for two or more marks, `overlay_write` for a note card on free space), never by writing drawing code or an annotations list. The operator instructions are `Core/Agents/OperatorGuide.md`; `.claude/skills/vernier-operator/SKILL.md` must stay identical to it (a test checks).
- Tutor marks are smooth hand-drawn curves (`Overlay/MarkShapes`), never hard boxes. A note card is dismissed by its own × (`NoteDismissWindow`); the glass stays click-through everywhere else.
- Research (owner's direction, 2026-10-18): `web_research` is Vernier's own tool (`Core/Research`), on by default and disclosed; `ResearchPolicy` decides in code when an answer must be grounded (web page, tool apps such as Blender, unknown apps, how/current questions) and `ResearchGate` re-asks once when fewer than 3 working pages were read. All fetching goes through `SafeWeb` (public addresses checked at connect time); queries through `QuerySanitizer`. Do not add a second fetch path.
- Diagrams for space, vectors, complex numbers and higher dimensions are forced by `DimensionPolicy`/`DimensionGate` and drawn by `overlay_diagram`; their numbers come from `Geometry3D`, never the model. Plain text that may contain TeX goes through `TexPlain.Clean`.
- Small models: every tool call goes through `SchemaCheck.Repair` before validation; refusals carry an example call; `TurnPlan` puts a numbered plan with JSON calls into the question; `DrawingGate` re-asks once for a missing required drawing or a failed drawing call that was never retried. Keep tool arguments optional where Vernier can work them out (axes via `AxisFinder`, curve start via `InkMap.FirstCurveY`).
- Tutor marks belong to the window they were drawn for and move with it, cards beside it included (`AnnotationOverlayManager._owner`).
- Graph areas are computed in code (`graph_area`, `Core/Geometry/GraphArea`), never by the model.
- Tutor marks re-fit by content (`MarkRefit` + `IPatchMatcher`: native `vn_match`, managed `PatchMatcher`, identical results): a mark moves with its content or is removed alone; all go only when most are lost or the window title changes.
- Shipping: `scripts/publish.ps1` produces exactly `Vernier.exe` (single file, self-contained) and `Vernier.Native.dll`. The native ABI is `VN_ABI`/`Native.Abi`; bump both together.
- Learning records are deleted one at a time through `LearningStore.Delete(LearningRecord, id)` (the schema cascades); do not add per-table delete methods.
- Colour rule: yellow (`You`, `AnnotationColors.Learner`) is the learner, blue (`Accent`, `AnnotationColors.Focus`) is the tutor.
- The capture light (edge strips) must be on whenever `TutorSession.IsLooking` is true.
- Nothing is sent to a model until the learner presses Enter or Ask. A finished pen mark waits; do not bring back auto-send. (Exception: a local context model, if the learner picks one, may summarise an episode in the background; remote models never run in the background.)
- Context is always on (owner's direction, 2026-10-16): `Context/ContextEngine` keeps episodes of the learner's work (app, cleaned title/address, video and position, rewinds, time, questions) on disk for `ContextRetentionDays`; text read from the screen (local OCR of the window in front after a change settles) stays in memory only. It is disclosed at first run and on upgrade (`ContextNoticeShown`), stops in Pause and Share-Safe, skips private windows, password managers and excluded apps, and reading is shown by the small top-centre light (glow mode 3). What goes with a question is shown in the ask bar. Build it by code; a model is optional.
- Native code (`native/Vernier.Native`, C++20, built by `scripts/build-native.ps1`) is the base for system and heavy work (owner's direction, 2026-10-18): DXGI Desktop Duplication, WIC encoding, the DirectComposition capture light and the pen ink (each on its own thread), and patch matching for mark re-fit. C# is for the UI and high-level logic. Every native call has a managed fallback; the app must run without the DLL.
- A screen question that cannot have its screen (Share-Safe, capture off, capture refused) is not sent to a model; the learner is told why.
- The tutor acts only through tools in `Agent/` run by `ToolExecutor`. Anything with an effect (`computer_act`, `notes_write`, `gdocs_append`) is an Act tool: it previews exactly what it will do (`PreviewAsync`) and runs only after the learner allows that one call. `computer_act` resolves targets by id or text (never a guessed box), re-checks the live window before sending, and refuses terminals, elevated windows, password fields and Vernier itself. Note tools are offered only when a vault is connected and notes may go to that provider (`TutorSession.ToolNamesFor`).
- Suggestions are local and code-only (`Context/Proactive`, `SuggestionPolicy`): at most two an hour, quieter after each dismissal; accepting one only fills in the ask bar.
- Answers render through `AnswerText` + `RichAnswer` (Sitka, WpfMath). The tutor writes TeX between `\(` `\)` / `\[` `\]`. Five themes (`Themes/Dark`, `Aurora`, `Nebula`, `Glacier`, `Light`) share the same keys; a new key must be added to all five.
- The screen is live. Every screen turn carries `ContextSnapshot.SinceLast`, history keeps each question with what it was asked about (`MasteryTutor.Remember`), and tutor marks are removed when the area under them changes (`AnnotationOverlayManager.Watch`). Do not reuse a previous reading of the screen unless `Observer.IsUnchanged` (fine thumbnail) says so.
- `graphify-out/` is the project's knowledge graph. Query it (`graphify explain "X"`, `graphify path "A" "B"`) before reading many files, and run `graphify update .` after code changes.

## Conventions

- Core never references WPF or P/Invoke. Anything OS-specific sits behind an interface defined in Core (`INativeWindowApi`, `ITextExtractor`, `IInputDriver`, `ISecretStore`) and implemented in App.
- All screen geometry is **physical pixels on the virtual desktop**. Convert at the edge: `CaptureGeometry` (image ↔ physical), `DpiMath` (physical ↔ per-monitor DIPs). Never mix units; never treat model coordinates as exact.
- Provider-specific code stays in `Providers/`. The pipeline only sees `IChatProvider`. Never switch provider or model on the user's behalf.
- No abstraction without a second implementation or a test seam. Nullable enabled; no warnings.
- Catch specific exceptions. A failure is shown to the user or logged; it is not swallowed.
- UI: tokens from the theme dictionaries (`Themes/*.xaml`) via `DynamicResource`; styles in `Themes/Controls.xaml`. Follow `docs/DESIGN_SYSTEM.md`. Every interactive control needs an accessible name (a test enforces it).

## Security rules (non-negotiable)

- Screen capture, microphone, sending notes/files to a remote provider, and assisted input are **off by default** and gated in code, not just in UI.
- API keys: environment variable or Windows Credential Manager only. Never in settings, logs, prompts, or source. Anything logged goes through `DiagnosticLog` (redacts).
- Do not log prompts, answers, screen text or images.
- Everything read from screen, files, notes, CLI output or a model is untrusted: wrap with `Untrusted.Wrap` before it reaches a model; parse model output defensively.
- File access only through `ScopedFiles` / `PathGuard`. Writes only via `Propose…` → user-approved `Apply` / `ApplyAll` (journalled; `Recover` at start).
- OAuth (Google Docs): PKCE + state + loopback redirect, `drive.file` only, refresh token and client secret in Credential Manager. Vernier ships no OAuth client.
- Model-proposed input goes through `ActionPolicy.Review` and explicit user approval. No shell execution of model output, ever.
- `SetWindowDisplayAffinity` is applied only to this process's own top-level windows. Never claim it guarantees exclusion from any particular tool.

## Verification workflow (Evaluate → Correct → Confirm)

1. Build, run `dotnet test`.
2. For UI changes, regenerate screenshots (`--screenshots`) and look at them in both themes.
3. For provider / pipeline changes, run with `VERNIER_LIVE=1`.
4. Record what was actually verified, and what was not, in `docs/VERIFICATION_REPORT.md`. Do not weaken or delete a test to make it pass; do not describe a simulated test as proof a real API works.
