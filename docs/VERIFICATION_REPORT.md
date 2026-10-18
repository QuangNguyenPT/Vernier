# Verification report

Date: 2026-10-11 · Machine: Windows 11 24H2, build 26100, x64, one display 2560×1600 at 100 % · .NET SDK 10.0.201 · Vernier 0.1.0

This report separates what was **run against the real thing**, what was **simulated**, and what was **not verified**. A simulated test shows the logic is right; it is not evidence that a real service or OS path works.

## Revision 11 status (version 0.4.0, 2026-10-18): read this first

Debugged from evidence first (the owner's log and route records, then failing tests), then fixed at the root. Run on Windows 11 build 26100, display on; the owner allowed desktop and live runs.

| Command | Result |
|---|---|
| `scripts/publish.ps1` | **444 of 444 tests passed**; single-file publish 162.9 MB (exactly `Vernier.exe` + `Vernier.Native.dll`); smoke test passed (native ABI 2 loaded) |
| `VERNIER_LIVE=1 VERNIER_LIVE_MODEL=haiku` (the owner's model) | All four passed with **Haiku**: forces on a ramp (drew a free-body diagram, used Vernier's 16.55 N); a curved v–t graph from real pixels with only the x range given (Vernier read the axes and traced the curve; area within the test's 82–88.5 band around the exact 85.3 m); skew lines (2.8284); Blender research on the real web (≥ 3 working sources) |

| Symptom | Root cause (evidence) | Fix and proof |
|---|---|---|
| After a while the tutor stops drawing and refuses | The tool grant made while the ask bar opens was tied to a 2-minute timer that started then; the owner's turns took 115–123 s (`routes.jsonl`), and the log shows a research re-ask that read 0 sources at the end of such a turn | The warm turn now has a separate discard timer that is cancelled when a question takes it (`TutorSession.WarmTurn`); test `A_warm_turn_that_a_question_took_keeps_its_tools_after_the_warm_lifetime`. Every tool call is now logged (name, ok/failed, ms; never arguments or results) so the next failure leaves evidence |
| Tutor gives up after a failed drawing ("the graph is wrong") | A refused call (bad axes, strict arguments) ended the attempt: strict validation refused "4" for 4 and any extra key, and nothing asked the model to retry | `SchemaCheck.Repair` fixes those shapes before validation and refusals show a valid call shape; `DrawingGate` re-asks once when a drawing call failed and was never retried, quoting the error and the fix; the guide says never blame the learner's graph for a tool error. Tests: argument repair, executor, gate. Hypothesis "Vernier's own shading reads as a screen change" was tested and **rejected** (the old check returned false with shading on screen); the test stays as a guard |
| Drawings do not follow the window | Only marks inside the old window bounds moved; a card on free space beside the window stayed | Each tutor mark records the window it was drawn for and moves with it (`Rebase(..., belongs)`); desktop test reproduced the bug (card stayed at x = 593) and passes after the fix |
| Not forced to draw for physics | Force/ramp problems were excluded and no diagram existed for them | `overlay_diagram` kind **forces** (free-body diagram on a slope; net force and components along/across the slope computed in code, tested exactly); `DimensionPolicy` now covers forces and graph questions (→ `graph_area`) and picks the kind; screen text alone triggers only with two distinct problem terms and never in code editors or terminals |
| Small models struggle with the tools | Many arguments and a free-form task | A code-made **plan** in every relevant question (numbered steps with ready JSON calls), a copy-ready **cheat sheet** in the operator guide, axes **found automatically** from tick labels (`AxisFinder`, every label must lie on one line), and the curve found automatically (`FirstCurveY`), so `graph_area {"trace":{"x_from":0,"x_to":10}}` is enough |
| Raw markup in answers | TeX outside `\( \)` and a half-streamed `\(` were drawn as text | `AnswerText` shows them as symbols through `TexPlain`; an old test that pinned the raw-while-streaming behaviour was replaced by one that requires symbols |
| (review) Diagnostics did not say whether the C++ module was in use | — | A "Native module (C++)" row with its ABI |

Not verified / known limits: the intermittent end-to-end UI failure seen once in this session (the stub answer not shown) did not recur after the stricter screen rule and could not be reproduced in isolation; its cause is not proven. Graph tracing assumes a dark or coloured curve on a light background; a curve drawn in pale grey, or several crossing curves, may be missed or confused (the tool then reports it). Automatic axes need readable tick labels; log axes are not supported.

## Revision 10 status (version 0.4.0, 2026-10-18)

Run on Windows 11 build 26100, display on; the owner allowed desktop and live runs in this session.

| Command | Result |
|---|---|
| `scripts/publish.ps1` | **429 of 429 tests passed**; single-file publish (162.9 MB, exactly `Vernier.exe` + `Vernier.Native.dll`); smoke test passed (native ABI 2 loaded) |
| `VERNIER_LIVE=1`, `A_real_model_is_made_to_draw_a_3D_diagram_and_uses_the_distance_vernier_computed` | Passed (17 s): a real model, told a diagram was required for a skew-lines question, called `overlay_diagram` with `measure: skew_lines`, Vernier computed 2.8284, and the answer used it |

| Claim | Evidence |
|---|---|
| Graph regions sit on the real shape | `InkMap`/`CurveTrace` find the drawn line in the screenshot's pixels (dark or strongly coloured, not the pale grid). `graph_area` now has `trace` (follows the drawn curve pixel by pixel; area by the trapezoid rule, net and absolute reported) and snaps shape corners onto the drawn line when it is within a few pixels, reporting each move and any corner not on a line. Tests: a corner 0.4 units off a √x curve is pulled onto it and a grid line is not mistaken for it; tracing a synthetic √x graph gives the area within −3 %/+2 % of the exact integral. Not verified on a real photographed or screenshotted graph |
| Garbled symbols | Cause: TeX written in note text, labels or an unparsed formula was drawn raw. `TexPlain` turns it into symbols (Δ, ×, ², →, v⃗, √(…), °) and is applied to note text and titles, the formula fallback, mark labels and diagram labels; Cambria Math / Segoe UI Symbol added to the font chains; the guide tells the tutor to write symbols. 7 cases tested. Which exact output the owner saw ("bath/ab") was not reproduced |
| 3D, complex and higher-dimensional problems are drawn every time | `DimensionPolicy` (vectors, cross/dot product, skew lines, planes, torque, fields, complex numbers, 3D/4D, higher dimensions, from the question or the screen text; inclined planes excluded) puts "Diagram: REQUIRED" in the question; `DimensionGate` re-asks once (combined with the research re-ask) when no `overlay_diagram` succeeded. `overlay_diagram`: space3d (points, vectors, lines, planes, segments), complex (Argand plane), hypercube (cube/tesseract for 4D and above by analogy). `Geometry3D` computes skew-line distance with the common perpendicular, point–plane distance, angle and cross product; tests include the exact values. Cards looked at: `docs/screenshots/diagram-{skew,torque,argand,tesseract}.png` |
| Placement and dismissal | The diagram goes through the same free-space placement as note cards (refactored into one `PlaceCardAsync`) and has the same × button; a tool test shows it lands on the empty right half |

Not verified: the diagram cards on a real screen over real content (rendered to bitmaps only); a 4D tesseract is a projection and the learner may need the explanation to read it; text in the owner's example screenshots was not run through OCR here.

## Revision 9 status (version 0.4.0, 2026-10-18)

Run on Windows 11 build 26100, display on, Claude Code CLI signed in. The owner allowed the desktop tests and live runs (real model, real web) for this run. Designs were reviewed twice by a four-role panel (PM, architect, developer, QA) before building; their agreed cuts are listed under "Not done".

| Command | Result |
|---|---|
| `scripts/publish.ps1` | **405 of 405 tests passed** in Release; single-file publish; the output folder holds **exactly `Vernier.exe` and `Vernier.Native.dll`** (162.8 MB; the script fails on any other file); smoke test of the published exe passed and logged `native module (ABI 2, loaded=True)` |
| `VERNIER_LIVE=1`, `A_real_model_researches_unfamiliar_software_on_the_real_web_before_explaining` | Passed (25 s): the real model, told research was REQUIRED for a Blender Bevel-modifier question, called `web_research` through MCP against the real DuckDuckGo and real pages; at least 3 working sources were read; the answer explained Segments and ended with Sources |
| `VERNIER_LIVE=1`, `A_real_model_shades_the_area_under_a_velocity_time_graph_and_uses_the_computed_number` | Passed (23 s) on the third run: the model called `graph_area` with three shapes, Vernier computed 24 + 60 + 6 = 90 m, the answer used 90. The first two runs are recorded honestly: in the first the model deliberately left the last piece to the learner (the guide's guided-learning rule; the question had not asked for the full answer); in the second the fixture's description line had been shortened by the screen list ("then fa…") and the model refused to guess the last second rather than invent it. The fixture was split into short lines; the assertion was not weakened |

| Claim | Evidence |
|---|---|
| One exe plus one DLL | Publish script check above; `Tray_icon_…` now reads the icon from the executable and asserts it was found (`HasOwnIcon`) |
| C++ is the base for heavy work: patch matching is native | `match.cpp` (`vn_match`) built at /W4 /WX; `The_native_matcher_gives_exactly_the_managed_answer`: 12 random cases, native and managed results identical to the last bit; the DLL reports `VN_ABI` 2 and a DLL with another number is not used |
| Marks re-fit by context instead of all being removed | `MarkRefitTests`: a scroll moves each mark with its content (both matchers); only the mark whose content was replaced is removed and nothing else moves; a different page clears all; repeated rows (a table) are ambiguous, so the mark is removed, not moved to a lookalike (this test found that the ambiguity margin compared a fine score with a coarse one; fixed in both implementations); a blank area keeps its mark while it stays the same blank and loses it when it changes (the desktop test found that blank anchors were never removed; fixed). The desktop test `The_real_windows_work_end_to_end…` exercises the real capture path (this run found that marks outside the tracked window got no anchor; fixed: the search area always contains the marks) |
| Graph areas computed in code | `GraphAreaTests` (pixel mapping with the y axis up, exact areas either winding, degenerate ticks and self-crossing outlines refused); `ToolOverlayTests` through the real executor: two shapes drawn as Region marks at the right physical pixels, labels "A1 ≈ 24 m", total 84 m, too-close ticks refused |
| Research harness | `ResearchTests` (31): policy truth table (Blender, browser, unknown app → required; worksheet PDF → not, unless asking how a tool works); gate counts distinct working pages and re-asks exactly once with tools kept, adding a notice when still short; personal details stripped from queries; 16 address cases for the public-address rule (including `::ffff:127.0.0.1` and 169.254.169.254); a name resolving to loopback is refused at connect time and nothing reaches the local server; readable text drops scripts (including an injection string), navigation and markup; DuckDuckGo results unwrapped from their redirect, ads skipped; dead and empty pages do not count and at most two sources per site |
| Card readable | `docs/screenshots/note-card-handwriting.png` (looked at): Segoe Print 17.5 px, near-white ink, larger formulas; a boxed formula that overflowed the card is now scaled to fit |
| Faster | Reveal 110 ms fade, draw-on 140–300 ms (was 180 and 260–560), card wipe 260 ms (was 420); WpfMath is warmed at idle start; one `graph_area` call replaces draw + compute + label. Measured in the live runs: `graph_area` 13–20 ms, `overlay_write` 330–358 ms (the warm-up runs in the app, not in these tests) |

### Not done / not verified

- Agreed cuts from the review: no zoom (multi-scale) re-matching, because of false matches; no second search backend (DuckDuckGo only, behind `ISearchBackend`); no hedge-word trigger for research ("probably" in an answer does not force a re-ask), because of false re-asks; uncertainty is a prompt rule.
- DuckDuckGo's HTML page can change or challenge automated use; the tool then says the search engine did not answer, and the answer carries a notice. The redirect re-check runs on every hop by construction (each hop is a new connection through the same check), but no test serves a real public redirect to a private address.
- The research re-ask with Claude Code starts a second CLI process (several seconds); the per-turn prompt line is there so it is rarely needed. Not measured on many turns.
- Re-fit was tested on synthetic pages and one real window; not judged on real scrolling documents, videos or zoom. A wrong move is guarded by the ambiguity margin, not proven impossible.
- Single-file start-up time against the previous folder build was not measured; the first start unpacks the .NET runtime's native parts to a per-user cache.
- The research notice dialog and the new Settings checkbox were not looked at on screen (the Settings screenshot shows the top of the page); the accessibility test covers the checkbox.

## Revision 8 status (version 0.4.0, 2026-10-18)

Run on Windows 11 build 26100, display on. The owner allowed the desktop tests to open windows for this run.

| Command | Result |
|---|---|
| `scripts/publish.ps1` | **366 of 366 tests passed** in Release; self-contained publish 169.5 MB; smoke test of the published exe passed |
| `Vernier.exe --screenshots` | Every page in all five themes regenerated; Library looked at in Night and Paper (Delete buttons) |

| Claim | Evidence |
|---|---|
| Tutor marks are smooth curves, not boxes, and still enclose their target | `MarkShapesTests`: a loop contains all four corners of a squat, a wide and a tall target without exceeding 1.45× its size; swipe, underline, brace and arrow stay with their target; shapes are identical for a mark and differ between marks; a sample sheet of every kind over a white page and a dark video (`docs/screenshots/tutor-marks.png`, looked at). A real-screen test reads the ink colour back at the underline's physical position (this test used to sample the old highlight outline, which no longer exists; it was moved to the underline, not weakened) |
| A note card can be dismissed on its own | `ToolOverlayTests`: with the overlay running, a card gets one dismiss window in its top-right corner, not counted as a window cards must avoid; invoking it removes that card only and closes the window |
| The learner can delete learning records one at a time | `Deleting_one_record_takes_exactly_what_belongs_to_it`: deleting a concept takes its formulas, examples, mistakes and cards and leaves another concept's; deleting a formula, example, mistake or card takes only that row; an unknown id reports false. Library and Review buttons confirm first |
| Shallow code removed | `AnnotationSurfaceWindow.Redraw` (a pass-through to `Sync`), the duplicated own-window scan in the overlay manager, and the per-table `DeleteSession`/`DeleteCard` (now `LearningStore.Delete`) |

Not verified: the draw-on reveal and the hover state of the × were not watched on a real screen (only rendered without animation); clicking the × with a real mouse was not done (it was invoked through UI Automation).

## Revision 7 status (version 0.4.0, 2026-10-18)

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
