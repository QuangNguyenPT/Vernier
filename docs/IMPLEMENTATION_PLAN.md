# Implementation plan, risks, acceptance

## Repository audit (start state)

- Contents: the unmodified Visual Studio "Windows Desktop Application" C++ template (`Hopefully-this-goes-somewhere.cpp/.h/.rc/.vcxproj/.sln`, icons). No product code, no tests, no `CLAUDE.md`, not a git repository.
- Toolchain: .NET SDK 9.0.317 and 10.0.201; WindowsDesktop runtimes 6–10; Windows 11 24H2 build 26100; Claude Code CLI 2.1.285 and `graphify` CLI on PATH; no provider API keys in the environment.
- Decision: the C++ template is not a sound base for this product. Left untouched; new solution `Vernier.sln` (C# / .NET 10 / WPF) beside it.

## Reference projects studied

| Project | What was taken |
|---|---|
| screenpipe | Accessibility tree first, OCR as fallback; capture on change, not continuously; local SQLite with full-text search |
| clicky-windows | Downscale screenshots before sending; an explicit image → physical → logical coordinate pipeline; click-through overlay; push-to-talk; SM-2 journal |
| claude-overlay | Drive the `claude` CLI as a subprocess with the user's own sign-in; perceptual-hash dedup of unchanged screens; keep the overlay out of its own captures |
| hacky (Havruta) | Separate pass-through and draw modes on a transparent surface; observation without app plugins |

Deliberately not taken: always-on microphones and wake words, `bypassPermissions` agent execution, telemetry, tiered background model cascades that spend tokens unprompted.

## Slices

| # | Slice | Demonstrable outcome | Status |
|---|---|---|---|
| 1 | Audit, architecture, design directions | This document, `ARCHITECTURE.md`, `DESIGN_SYSTEM.md` | Done |
| 2 | Core: geometry, annotations, security, settings | Unit tests | Done |
| 3 | Providers + pipeline + one verified provider | Live turn through Claude Code CLI with a recomputed result | Done |
| 4 | Capture policy service, overlay, coordinate mapping | Real-window tests: affinity read-back, BitBlt exclusion, mapped pixel check | Done |
| 5 | Observer / Tutor / Verifier / ECC orchestration | Pipeline tests incl. correction, cancellation, malformed output | Done |
| 6 | Shell, design system, settings, diagnostics | App launches; screenshots reviewed and corrected | Done |
| 7 | Review system, library, learning store | Restart, migration, recovery, export/import tests; UI test grades a card | Done |
| 8 | Voice, more providers, local models | SAPI TTS test; fixture tests for OpenAI-compatible and Gemini; loopback stub through the real UI | Done (see limits) |
| 9 | Obsidian, Graphify, workspace files | Vault/diff/graph tests | Done (see limits) |
| 10 | Assisted input | Policy and driver tests | Done (see limits) |
| 11 | Packaging, docs, verification report | `scripts/publish.ps1` publishes and smoke-tests | Done |

## Risks

| Risk | Handling |
|---|---|
| Model-placed coordinates are approximate | Uncertainty carried on every annotation; low confidence drawn dashed; actions need approval; documented |
| Capture exclusion varies by capture path | Status reports only what the OS says; self-test in Diagnostics; manual steps for conferencing tools; no guarantee claimed |
| Mixed-DPI multi-monitor placement | Physical pixels throughout, `SetWindowPos`; only a single 2560×1600 @100 % display was available to test on (unverified beyond unit tests) |
| Provider APIs drift | Model lists fetched from the service; adapters small; only Claude Code verified live |
| SAPI dictation accuracy | Marked as a known ceiling; behind `ISpeechInput` for replacement |
| Cost surprises | No background capture or model calls; one capture per screen question; bounded retries; one correction; consent prompts |
| Prompt injection from screen content | Fencing, no tools, approval-gated actions |

## Acceptance checklist (definition of done)

| Criterion | Evidence | Met |
|---|---|---|
| Builds and launches by documented steps | `dotnet build`, `--smoke` on the published exe | Yes |
| Core UI interactions work and are coherent | `AppUiTests`, screenshots | Yes |
| One real provider completes a tutoring turn | `LiveClaudeCodeTests`, `LiveScreenTutoringTests` | Yes (Claude Code CLI) |
| Tutor follows a learning workflow and checks answers | `MasteryTutor.Method`, verifier results in live output | Yes |
| Overlay shows real, controllable annotations | `Annotation_surface_…` real-window test, live test places 2 marks | Yes |
| Capture exclusion via documented API, results reported accurately | Real-window tests, Diagnostics self-test, report | Yes for BitBlt; conferencing apps not tested |
| Screen capture and voice are consent-based, visible, stoppable | Defaults test, UI test (pause, edge light), real edge-light test, gates in code | Yes |
| Secrets handled properly | Redaction and credential tests | Yes |
| Learning records survive restart | `Records_and_review_state_survive_restart` | Yes |
| Meaningful test coverage | 194 tests | Yes |
| Errors and unsupported features handled honestly | Provider error tests, statuses | Yes |
| Docs match implementation | Reviewed against code at the end | Yes |
| No placeholder presented as complete | Limits listed in `VERIFICATION_REPORT.md` | Yes |

## Revision 2 (after owner feedback and a full read of the four reference projects)

| Change | Why |
|---|---|
| Tray-resident; closing hides | Almost all use happens while working in something else |
| Two learner tools only (Ask, Pen); drawing vocabulary belongs to the tutor | The toolbox was the tutor's, not the learner's (Havruta's "you draw, Claude draws back") |
| Glass always on; capture shown by a light-blue wave on all four screen edges | The state that matters most must be impossible to miss |
| Pen flow: ink painted into the capture, marked area enlarged, default question | "Circle the thing I do not know in a video and get it explained" |
| Region/window picker and timed Observe removed | Replaced by the pen and one-capture-per-question; less to understand, nothing running in the background |
| Account sign-in via Claude Code and Codex CLIs | Use existing subscriptions without API keys, through supported tools only |
| New visual identity (slate, yellow = you, blue = tutor, Bahnschrift headings, no serif) | The first design read as a generic AI product |
| `graphify-out/` knowledge graph of the project | A data bank for people and models: what a part is and what a change touches |

## Revision 3 (owner feedback after using 0.2.0)

| Report | Cause found | Change |
|---|---|---|
| "It says it can't see my screenshot" | A screen question was still sent when capture was blocked (Share-Safe had been switched on) | Such a question is no longer sent; the reason and the fix are shown |
| "It keeps saying its previous assumption was wrong" | Each turn sent a new still with no word that the screen had changed; history kept bare questions; and a small change on a large screen was judged "unchanged", so the old screen text was sent with the new picture | `SinceLast`, a live-screen rule, `Remember`, fine thumbnail comparison |
| "Zoom in and the label goes off; ovals go off" | Marks are fixed to the screen and outlived their target; a circle was drawn inside the target's box, cutting across it; labels could leave the glass | Stale-mark removal, enclosing ellipse, clamped labels |
| "The pen should wait for me" | A finished stroke auto-sent after 1.3 s | Nothing is sent until Enter or Ask |
| "Use my computer properly" | Assisted input only worked in Assist mode from the workspace | Available from any screen question when switched on; review button in the ask bar; the tutor is told when it is off |
| Attach pictures and slides; icons for pen and attach | New | Paperclip, chips, paste and drop; `.pptx`, `.docx`, text, pictures |
