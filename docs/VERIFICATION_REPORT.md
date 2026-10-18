# Verification report

Date: 2026-10-11 · Machine: Windows 11 24H2, build 26100, x64, one display 2560×1600 at 100 % · .NET SDK 10.0.201 · Vernier 0.1.0

This report separates what was **run against the real thing**, what was **simulated**, and what was **not verified**. A simulated test shows the logic is right; it is not evidence that a real service or OS path works.

## Revision 7 status (version 0.4.0, 2026-10-18): read this first

Run on Windows 11 build 26100, display on, Claude Code CLI signed in. The desktop tests briefly open windows on the screen; the pen's end-to-end path (a real mouse drag) was **not** exercised because that would move the user's real pointer.

| Command | Result |
|---|---|
| `scripts/publish.ps1` | **358 of 358 tests passed** in Release; self-contained publish 169.5 MB; smoke test of the published exe passed (native module loads, MCP server answers `initialize` and lists the tools) |
| `VERNIER_LIVE=1`, `LiveOperatorTests` | Passed: a real model (Claude Code) given only the operator guide called `overlay_marks` once (three marks, 12 ms) and `overlay_write` once (a six-line card, 346 ms) and answered the worksheet correctly (B, 4 m/s). Two live runs of the earlier MCP test also passed |

### What changed and how it was checked

| Claim | Evidence |
|---|---|
| The pen is drawn by a native layer, independent of the UI thread | `NativeTests`: a 40-segment stroke was on the screen 27 ms after the call (exact ink colour read back from the screen) while the UI thread was blocked; the finished stroke was reported back with every point; it faded and the layer hid by itself; Clear was immediate. A first version using partial-rectangle surface updates failed (`E_INVALIDARG`) and was replaced by 256 px tiles |
| The learner's ink does not stay | `InkPolicyTests`: ink is held for the question and shown only briefly; Undo takes back one stroke and its visible copy (this test found that Undo left the visible stroke behind; fixed); sending removes the ink and keeps the tutor's marks. Native fade timing in the test above |
| A note card is dark, handwritten, typeset, placed on free space | `NoteCardTests` (renders to a bitmap; images in `docs/screenshots/note-card-*.png`, looked at); `FreeSpaceTests` (7: plain vs busy cells, beside the problem on the empty side, side preference, known obstacles, crowded screen covered as little as possible, oversize card cut down); `ToolOverlayTests` through the real executor and store on a synthetic worksheet: the card landed on the empty right half |
| Several marks in one call | `ToolOverlayTests`: three marks in one call, one unplaceable target reported without stopping the rest, bad input refused with a usable message, clear removes marks and card |
| One guide feeds the tutor, the MCP server and the skill | Test: `.claude/skills/vernier-operator/SKILL.md` equals `Core/Agents/OperatorGuide.md`, the guide names every tool offered, the system prompt contains it, and the MCP `initialize` result carries the compact `instructions` |

### Not verified / known limits

- A real mouse or pen drag through the native layer (the cursor poll, `GetAsyncKeyState`, pen and touch ending via `Stop`) was not exercised; the feed path that draws and reports a stroke was. The first real use should be checked with the manual list (section 8b2).
- Photon-level latency (pixels on a monitor after a physical movement) was not measured; 27 ms is from the call to the pixels read back by a screen capture.
- Free-space placement was tested on synthetic worksheets and on one live model run with a synthetic screen; it has not been judged on real worksheets like the three the owner supplied. The card never covers Vernier's own windows but can cover content the pixel contrast cannot see (a flat colour block).
- The handwriting face (Ink Free) ships with Windows 10 and later; where it is missing the card falls back to Segoe Print or Segoe UI.

## Revision 6 status (version 0.4.0, 2026-10-17): read this first

Run on Windows 11 build 26100 with the session unlocked and the display on; Claude Code CLI signed in.

| Command | Result |
|---|---|
| `dotnet build Vernier.sln` | 0 warnings, 0 errors |
| `scripts/publish.ps1` | Builds the native module; **344 of 344 tests passed in Release** (including every desktop test that failed on the locked session before); self-contained publish 169.4 MB; the native DLL is in the published folder; smoke test of the published `Vernier.exe` passed (every page builds, `Vernier.Native.dll` loads, the MCP server answers `initialize` and lists the tools) |
| `VERNIER_LIVE=1`, `LiveMcpTests` | Passed: the real Claude Code CLI connected to Vernier's MCP server, listed a tool and called it; the answer contained a random code only the tool result held |

### What changed and how it was checked

| Claim | Evidence |
|---|---|
| The tutor remembers the whole live conversation, not the last eight turns | `SessionMemory`: recent turns verbatim, older turns as a code-built digest that folds the oldest and keeps failed checks, misconceptions and open questions; `session_recall` finds a folded turn in full. 7 tests (bound, fold, recall, fencing in the request) |
| The whole live session of work is described by this run's own time | `ContextEngine.SessionLine`; test that an episode continued from an earlier run does not claim the earlier minutes |
| MCP follows the Streamable HTTP rules | `McpServerTests`: protocol-version header, batches and responses, `-32602`, 405s with `Allow`, tool annotations, malformed version answered under its own id; the live test above |
| `computer_act` brings the target forward and refuses when Windows will not | `Win32.BringToFront`; while you were in another application the Notepad test saw Windows keep Brave in front and `computer_act` refused to type (nothing typed anywhere); the test now asserts that refusal when focus cannot be taken and reports the typing path INCONCLUSIVE. The full typing-and-saving path passed on the earlier run, when Notepad had focus |
| Independent review by Opus 5.5 against the two original prompts found and fixed 8 defects | Private-window addresses were stored and sent; private-window and excluded-app screen text was read and kept; episodes were read live from another thread; screen text was sent after capture was switched off; `get_context` ignored "Leave out earlier"; the "local" preset stayed exempt from consent gates after its endpoint was edited to a remote host; the publish script and smoke test looked for the native DLL in the wrong folder. Tests added or tightened for each that is testable |

### Not verified / known limits

- The Windows media session can report a video playing in a private window; it becomes an episode because the session does not say which window it came from.
- An episode's "asked N× (last: …)" still includes questions from before "New conversation".
- `track_target` and `group_annotations` tools from Revision 4 were not built.
- The Google Docs connector has never run against Google; GitHub publishing has not been done.
- The typing path of `computer_act` was re-run only once this revision (on Notepad); it depends on Windows granting focus.

## Revisions 4 and 5 status (version 0.4.0): read this first

Run 2026-10-16 on Windows 11 build 26100, one 2560×1600 display, Claude Code CLI signed in. **The Windows session was locked for the last part of this run** (LogonUI running, no input for 40+ minutes), so Windows refused every screen capture and there was no foreground window. Tests that need the live desktop could not pass and are listed as not verified; none of them is counted as passing.

| Command | Result |
|---|---|
| `dotnet build Vernier.sln` | 0 warnings, 0 errors |
| `scripts/build-native.ps1` | `Vernier.Native.dll` built with `/W4 /WX` |
| `dotnet test tests/Vernier.Tests` (session locked) | **324 passed, 10 failed, 334 total** (after the Agent B fixes). All 10 failures are desktop tests failing with "BitBlt failed … a locked session blocks capture" or no foreground window: capture exclusion, capture mapping, edge light (WPF and native), glass, UIA/OCR, pen ink, ContextHub real, the end-to-end UI test, the Notepad scenario |
| `VERNIER_LIVE=1`, `LiveClaudeCodeTests` | 2 passed: CLI connection; a real tutoring turn through the whole pipeline (numeric claim recomputed, structured block present, **the formula came back as TeX and parses into a typeset formula**) |
| `scripts/publish.ps1` | **Not run**: it runs the full suite first, which cannot pass while the session is locked |

### Verified in this revision

| Claim | Evidence |
|---|---|
| Native edge light is a real travelling wave that keeps animating while the UI thread is blocked | Earlier in this session with the display awake: click-through, per-pixel transparency, 1,870 of 2,560 edge pixels changed within 250 ms, about 60 fps with the UI thread blocked, excluded from capture, invisible when off |
| Native duplication returns correct pixels and honours capture exclusion | One run with the display awake. With the display asleep it delivers no frames (tests report INCONCLUSIVE, not pass); on this machine dirty rectangles always cover the whole screen, so change detection uses the thumbnail |
| WIC encode scales and round-trips | `Png_encoding_scales_and_round_trips` |
| Context engine: episode boundaries, rewinds, idle, privacy, ranking, persistence, retention, forgetting | 20 unit tests. The real-window `ContextHubRealTests` failed in this run (no foreground window while locked) |
| Answers typeset: inline and display formulas, superscripts, subscripts, lists, code; malformed or unfinished TeX stays text; formulas have spoken accessible names | 19 `AnswerTextTests`, `RichAnswerTests`; screenshots of the ask bar with a sample answer in all five themes, looked at |
| Five themes meet text contrast | Contrast script on every text pair, including `InkFaint` on `SurfaceAlt` (Glacier's was 3.96:1 and was darkened to 4.72:1) |
| Windows accepts the ask bar's rounded-corner and acrylic-backdrop requests | `AppUiTests` printed `rounded=True acrylic=True` (asserted on build ≥ 22621). DWM accepting the attributes is what was checked; how it looks on screen was not seen |
| Approval shows the tool's own preview; a refusing preview never asks | `ToolExecutorTests` (theory) |
| Note writes: one note per concept, abort on a changed file, rollback on a failed write, crash recovery, journal confined to the vault, index keeps the learner's text, search ranking | 10 `KnowledgeStoreTests` against real files |
| Suggestions: budget, gap, dismissal decay, accept resets, rewind trigger | 3 `SuggestionTests` |
| Google Docs requests are what Google documents (PKCE S256 per RFC 7636's example, `drive.file` only, state check, token exchange and refresh, create/read/append, 403 explained) | 9 `GoogleDocsTests` against a fake HTTP handler. **Simulated**: never run against Google |
| Agent B's findings on the new code are fixed where Core can show it | Journal recovery leaves later edits alone and ignores a cut-short or committed journal; exact prior bytes return; a directory link in a middle folder is refused; device names are suffixed; chords in any order are blocked (new tests in `KnowledgeStoreTests`, `CoreLogicTests`). The App-side fixes (card focus and arming, Esc, serialised Act calls, OAuth loop) build but are exercised only by the desktop tests that could not run |
| A v1 database still migrates forward | `LearningStoreTests` (the test's rollback was incomplete and was fixed; the v3 migration is now idempotent) |

### Not verified in this revision

- **The tutor's hands on a real application.** `ComputerUseTests` (type and save in Notepad through the real approval card, checked on disk; denial runs nothing; blocked chord; terminal refused before asking) is written and builds, but every run so far hit the locked session. Until it passes, `computer_act` is unproven on a real window.
- Everything else that needs the live desktop in this run (the 10 failures above), including the end-to-end UI test after this revision's changes.
- Suggestions on a real media session; the tray notification click path.
- Google Docs against Google (no OAuth client available here).
- The duplication-vs-GDI capture benchmark (needs an awake display). The encode benchmark did run: WIC 56.5 ms vs WPF 66.2 ms median for 2560×1600 → 1568 px PNG on a synthetic frame (`VERNIER_BENCH=1`), so the app keeps WPF encoding.
- The acrylic backdrop and shadow as they look on screen (screenshots are WPF renders and do not include compositor effects).
- The heavy-glass p95 (63.5 ms against a 33 ms target) is unchanged from Revision 4.

## Revision 3 status (version 0.3.0)

Run on Windows 11 build 26100, one 2560x1600 display at 100 %, Claude Code CLI signed in.

| Command | Result |
|---|---|
| `dotnet build Vernier.sln` | 0 warnings, 0 errors |
| `VERNIER_LIVE=1 dotnet test tests/Vernier.Tests` | 218 passed, 0 failed |
| `scripts/publish.ps1` | Release tests 218 passed; published 168.3 MB; smoke test of the published `Vernier.exe` passed |

### Verified in this revision

| Claim | Evidence |
|---|---|
| A changed screen is followed without retracting the earlier answer | Live, Claude Code: a window showed a red square, then different text. Second answer: "shows d/dx of x^2 = 2x ... I can't see a red square in it now"; no apology. `SinceLast` read "9 s ago; same window; part of the picture has changed". |
| The old reading of the screen is not reused after a small change | Same live test: the second snapshot's text contains "2x" and not "torque". Before the fix the same run reported "the picture is the same as before". |
| A circle lands on and encloses its target | Same live test: asked to circle a 110 px red square, the circle's box matched the square to within 1 px (centre error 0 px). One target, one run; not a general accuracy figure. |
| A screen question in Share-Safe mode is not sent | In-process UI test: request count at the stub provider unchanged; the transcript shows the reason. |
| Attachments are read, pinned, sent and removable | Unit tests (slide order, .docx, cut-off, PDF/credential/broken/XXE refusals); UI test attaches a .pptx through the bar and finds its text in the provider request; live test confirms a remote provider gets nothing until allowed. |
| Stale marks are removed | UI test: a tutor mark over a real window survives 3 s of a still page and is removed within 6 s of the page changing; status says why. |
| Every ask-bar control has an accessible name, including the icon buttons | UI test. |
| Pen and attach icons render | `docs/screenshots/dark-ask-bar.png`, `dark-ask-bar-attached.png`, looked at. |

### Not verified in this revision

- Assisted input from the ask bar into a real application. The policy, executor and driver tests still pass; no real "do it for me" run was made, live or otherwise.
- Stale-mark removal on real video, real scrolling and browser zoom, and whether 10 % / 1.2 s feels right. A hover highlight or an animation inside the marked area may clear marks early; a very small change may not clear them.
- Pasting a picture from the clipboard and dropping files (code paths exist; not exercised by a test).
- Pictures embedded in real PowerPoint and Word files; `.webp` and other formats that need a Windows codec.
- Attachments and the two-turn behaviour through any provider other than Claude Code.
- Whether the cause of the reported "can't see your screenshot" was Share-Safe mode. The log shows Share-Safe was switched on at the time, and that path did send screenless questions; no other cause was found.
- Everything already listed as not verified below.

## Revision 2 status (version 0.2.0): read this first

Version 0.2.0 reworked the shell (tray-resident, always-on glass, Ask and Pen only, capture light, ask bar), added account sign-in and the ChatGPT-account provider, and replaced the visual design.

| Command, on the final 0.2.0 code | Result |
|---|---|
| `dotnet build Vernier.sln` | Succeeded, 0 warnings |
| `VERNIER_LIVE=1 dotnet test tests/Vernier.Tests` | 209 passed, 0 failed (live tests really ran) |
| `powershell -File scripts/publish.ps1` | Release tests 209 passed → self-contained publish (168 MB) → smoke test of the published exe passed (starts, builds every page, opens the ask bar with the pen armed, exits) |
| `Vernier.exe --screenshots` | Exit 0; 13 screenshots inspected |

New in 0.2.0 and verified against the real thing:

| Area | Test | Observed |
|---|---|---|
| **Pen flow, live, end to end** | `A_pen_mark_on_a_real_window_is_explained_and_annotated_inside_it` (Claude via Claude Code) | A box was "drawn" round a physics problem on a real window and nothing was typed. Vernier asked "Explain what I marked.", captured the monitor, read the text (source: OCR), the model identified the marked box as the homework problem and ignored the unrelated windows on screen, checks passed, and 3 tutor marks landed inside the marked window |
| **Ink reaches the model** | `Pen_ink_is_painted_into_the_capture…` | 2560×1600 screen sent as 1280×800; yellow ink found in the image where it was drawn; marked region maps back onto the stroke; enlarged detail 396×216 for a 198×108 area |
| **Capture light** | `Edge_light_sits_on_the_screen_edge…` | Strip is on the top screen edge, full width, click-through, invisible when off; while "looking", 400 of 400 sampled top-edge screen pixels were blue-tinted; back to invisible when off |
| **Glass** | `Glass_is_click_through_until_the_pen_is_armed…` | Click-through by default, takes the mouse only while the pen is armed, covers the monitor in physical pixels, a mark's pixels appear on screen |
| **Tray** | `Tray_icon_is_added_to_the_notification_area_and_removed` | `Shell_NotifyIcon` add and delete succeed |
| **Whole app, in-process** | `AppUiTests` (model = loopback stub) | Pages build; no control without an accessible name (ask bar included); glass on every monitor with 4 edge strips each; Ask through the bar captures and sends an image, the edge signal goes Looking → Off, the answer shows in the bar; Pen arms and disarms on the same chord; Share-Safe reports Applied on all 8 windows (workspace, ask bar, glass, edge strips) and blocks capture; Pause puts the pen down; **closing the workspace hides it and the app keeps running**; Hide removes bar, workspace and marks |
| **Account status** | Settings › Accounts on this machine; `claude auth status`, `codex login status` | Claude: signed in. ChatGPT: signed out. Both shown correctly |

Not verified in 0.2.0:

- **The ChatGPT-account provider has never produced an answer.** No ChatGPT account is signed in here. Only its signed-out failure was observed (real output: HTTP 401 from `codex exec`), which is classified as "not signed in" with the fix. Request shape, image passing and answer reading are untested against a signed-in Codex.
- The **Sign in…** button (opens a terminal with the vendor's login command) was not clicked; signing in is the owner's action.
- Nobody has looked at the capture wave moving, the tray menu, or the pen drawing by hand. Tests confirm pixels and state, not feel.
- Everything listed under "Not verified" in the 0.1.0 report below still applies (other API providers, multi-monitor and mixed DPI, a real microphone, conferencing tools, professional applications, assisted input into a real application).

Defects found and corrected during revision 2:

| Defect | How it was found | Fix |
|---|---|---|
| A closed window stayed registered with the capture policy and showed as Failed | `AppUiTests` (Share-Safe on all windows) | The handle is captured at registration and used at close |
| A refused capture (locked session, secure desktop) aborted the whole question | The build machine's session locked mid-run | The question is answered without the screen, with a notice |
| The ask bar would have been read as "the application in front" | Review while writing the pen flow | The bar records the application that was in front before it opened |

Everything below this line is the 0.1.0 report. Its statements about the region/window picker, Observe mode, the floating tutor and user drawing tools describe features that 0.2.0 removed; its capture-exclusion, coordinate-mapping, OCR, accessibility and live-Claude results are on code paths 0.2.0 kept, and those tests pass on 0.2.0.

---

## Commands and results

| Command | Result |
|---|---|
| `dotnet build Vernier.sln` | Succeeded, 0 warnings, 0 errors |
| `dotnet test tests/Vernier.Tests` | 196 passed, 0 failed, 0 skipped (3 live tests return immediately without `VERNIER_LIVE=1`) |
| `VERNIER_LIVE=1 dotnet test tests/Vernier.Tests` | 196 passed, 0 failed (39 s; the 3 live tests really ran) |
| `powershell -File scripts/publish.ps1` | Release tests 196 passed → self-contained `win-x64` publish (168 MB) → smoke test of the published `Vernier.exe` passed |
| `Vernier.exe --data <tmp> --screenshots docs/screenshots` | Exit 0, 13 screenshots written and inspected |

## Verified against the real thing

| Area | How | Observed |
|---|---|---|
| **Real tutoring turn, text only** | `LiveClaudeCodeTests` → Claude Code CLI 2.1.285, this machine's sign-in | Answer returned; structured block parsed; `0.5*2*3^2 = 9` recomputed and confirmed by the Verifier; saved to the session |
| **Real screen-aware turn, end to end** | `LiveScreenTutoringTests`: real WPF window showing a physics problem → GDI capture → accessibility-tree text → screenshot + text to Claude Code → Verifier → annotations | Stages `Observe > Interpret > Teach > Verify > Present > UpdateMastery`; text source `accessibility`; answer "9 J" with derivation and a transfer question; 4 checks passed (arithmetic, units `kg*m^2/s^2` vs `J`, 2 annotations); both highlights mapped to screen points inside the target window |
| **Capture exclusion, OS level** | `Capture_exclusion_really_removes_the_window…` (also Diagnostics › Run capture self-test) | See table below |
| **Policy service on real windows** | `Policy_service_applies_reads_back_and_clears…` with real `user32` | Applied + read-back `0x11` on two windows (one created after the mode was set); cleared to `0x00` on Standard; the shell's window refused as "another process"; a closed window reported Failed |
| **Capture geometry** | `Screen_capture_maps_image_pixels_back…` | 586×293 window downscaled to 300×150; a red square found in the image mapped back to a screen pixel that is red; ±1.0 px quantisation reported |
| **Text extraction** | `Accessibility_and_ocr_both_read…` | UI Automation and Windows OCR both read the sentence; OCR boxes lie inside the image; Observer preferred accessibility |
| **Annotation surface** | `Annotation_surface_is_click_through…` | `WS_EX_TRANSPARENT/NOACTIVATE/TOOLWINDOW` set; window rect equals the monitor in physical px; `WindowFromPoint` sees through it; draw mode removes click-through; a highlight's edge pixel found on screen at its physical coordinates |
| **Application UI** | `AppUiTests`: the real `MainWindow`, pages and services in-process; model = loopback HTTP stub speaking Chat Completions | Every page builds; no interactive control lacks an accessible name; a question typed in the real panel streams an answer through the OpenAI-compatible adapter over real HTTP, hides the JSON block, shows the pass check and token count, creates the concept and a card; a card is revealed and graded; Share-Safe sets real affinity `0x11` on the main window and blocks capture/observe; Standard clears it; Pause stops observation and Resume does not restart it; floating tutor shows the same conversation and is topmost; Hide minimises everything |
| **Windows Credential Manager** | `Credential_manager_round_trips…` | Write, read (non-ASCII), delete |
| **Speech output** | `Speech_output_starts_and_can_be_interrupted` | SAPI voice started, paused, resumed, stopped |
| **Graphify CLI** | `RealGraphifyTests`: real `graphify update` on a temp folder | Exit 0; `graphify-out/graph.json` parsed; `area()` node and a `calls` edge found |
| **Packaging** | `scripts/publish.ps1` | Published exe starts, constructs every page, exits 0 |

### Capture-exclusion tests actually performed (build 26100)

| Capture path | Window state | Expected | Observed |
|---|---|---|---|
| GDI `BitBlt` from the screen DC | Standard | Marker colour visible | Visible |
| `GetWindowDisplayAffinity` after `SetWindowDisplayAffinity(0x11)` | Exclusion requested | `0x11` | `0x11` |
| GDI `BitBlt` from the screen DC | Exclusion requested | Marker colour absent | **Absent** |
| `PrintWindow(PW_RENDERFULLCONTENT)` on the window itself | Exclusion requested | (informational) | Marker colour not returned |

**Not tested:** Windows.Graphics.Capture, DXGI Desktop Duplication, Snipping Tool / Win+Shift+S, Xbox Game Bar, OBS, Zoom, Google Meet, Microsoft Teams, any remote-desktop product, and any Windows build other than 26100. These cannot be driven from this environment (they need a second participant or interactive pickers). Reproducible steps are in `MANUAL_VERIFICATION.md` §5. No claim is made about them.

## Simulated (logic proven, real service not contacted)

| Area | What stands in | Label in code |
|---|---|---|
| OpenAI-compatible and Gemini adapters (request shape, SSE parsing, usage, error classification, key never echoed, https rule) | Fake `HttpMessageHandler` with recorded-shape payloads | `ProviderAdapterTests` |
| Pipeline: correction loop bound, cancellation, timeouts, malformed output, mastery updates, untrusted fencing, consent for the learning digest | `ScriptedProvider` | "SIMULATED provider" |
| Capture-policy decisions (unsupported OS build, API error codes, mismatched read-back) | Fake `user32` | "SIMULATED user32" |

## Not verified

| Item | Why |
|---|---|
| Anthropic API adapter against the live API | No API key on this machine. Built on the official SDK; only the missing-key path is tested |
| OpenAI, Google Gemini, Moonshot/Kimi live calls | No keys. Fixture tests only. The OpenAI reference page could not be fetched (HTTP 403), so `max_completion_tokens` for OpenAI follows prior knowledge; Gemini and Moonshot shapes were checked against their documentation pages on this date |
| A real local model server (Ollama / LM Studio) | Not installed. The adapter was exercised over real loopback HTTP against a stub |
| Multi-monitor and mixed-DPI behaviour; resolution/scale changes while running | One display at 100 %. Covered by unit tests of the geometry and by code paths only |
| Speech recognition with a real microphone | Not exercised. The no-device error path is handled in code but was not triggered |
| Assisted input sending real clicks/keys into a third-party application | Only policy, coordinate maths, chord parsing and the "target not in front" refusal were run |
| Behaviour inside Blender, Maya, Logger Pro, Adobe applications | Not installed or launched here. Support is generic screen capture plus application-specific vocabulary in the prompt |
| Target picker (drag region / click window) and the first-run dialog | Need a human pointer; in the manual checklist |
| Obsidian application itself | Vault access is plain file I/O and was tested on folders; no Obsidian instance was involved |

## Implemented with known limits

- **Observe mode** keeps a current local reading of the target and shows change; it does not send unprompted hints. A model is called only when the learner asks.
- **Verifier**: recomputes arithmetic, compares SI dimensions (common units only), checks annotation geometry, flags absolute wording and unretrieved links. It does not check symbolic algebra, does not execute code from programming answers, and does not retrieve references.
- **Voice**: local Windows SAPI for both directions. No cloud speech provider is implemented; the interfaces (`ISpeechInput`, `ISpeechOutput`) are where one would go. Dictation accuracy is modest.
- **Equations** are shown as plain text; there is no typeset maths.
- **Documents**: text and source files only. No PDF or Office parsing.
- **Professional applications**: recognised by process name for vocabulary; there are no plugins, scripting bridges or per-application automation.
- **Graphify**: reads `graph.json`, runs `graphify update` (code extraction). Building a semantic graph of notes is done with the `/graphify` skill outside Vernier.
- **Packaging**: a self-contained folder. Not code signed, no installer, no auto-update.
- **Global hotkey**: Ctrl+Alt+Space was already owned by another application on this machine; Vernier fell back to Ctrl+Alt+V and reports it.

## ECC audit record (Evaluate → Correct → Confirm)

| # | Requirement | Evidence of defect | Correction | Confirmed by |
|---|---|---|---|---|
| 1 | Diff preview must say when nothing changes | Test: identical files rendered as "…" | Early return for all-equal input | `Diff_marks_added_and_removed_lines` |
| 2 | Unit comparison must follow written precedence | Review before first run: every unit after a slash was treated as a divisor | Slash applies to the next unit only | `Units_compare_by_dimension` (`kg·m²/s²`, `J/s`) |
| 3 | Provider must be identifiable in Settings | Screenshot: combo boxes showed `Vernier.Core.Settings.ProviderSettings` | Combo template honours the item template selector | Screenshot `dark-settings.png` |
| 4 | Coherent control states at overlay width | Screenshots: disabled quiet buttons grew borders; BRACKET tool clipped; model row too tall; raw enum text | Opacity-based disabled state; wrapping tool row; fixed row height; readable status word | Screenshots regenerated |
| 5 | Hints must be true | Log: Ctrl+Alt+Space owned by another app, hint still advertised it | Fallback registration; hint shows the chord actually registered | Screenshot `dark-floating-tutor.png` (Ctrl+Alt+V) |
| 6 | Windows must load regardless of host | UI test: icon and theme URIs resolved against the host process | Pack URIs naming the assembly | `AppUiTests` |
| 7 | Learning records stay local without consent | Review of docs against code: the mastery digest was added to every remote request | Digest withheld from remote providers unless the learner allows it; setting text updated | `The_learning_record_digest_stays_local…` |
| 8 | The picker must let the learner reach the target | Review: the main window stayed on top of the content while picking | Main window minimises and the overlay hides during picking | Code; manual checklist §2 |
| 9 | CLI answers must not be altered by the user's Claude Code customisations | Risk identified from this machine's session hooks | `--safe-mode --strict-mcp-config`, tools off | Live answers in normal prose |

No test was weakened or removed to obtain a pass.

## Status against "production-ready"

The definition-of-done criteria are met for the implemented scope on the tested configuration (see `IMPLEMENTATION_PLAN.md`). It is **not** claimed to be release-ready for distribution: it is unsigned, has no installer, was run on one machine with one display, four of five provider families have never made a live call, and the conferencing-tool behaviour of capture exclusion is untested. Whether that is good enough to ship is a decision for the owner, not for this report.
