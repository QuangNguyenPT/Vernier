# Security and privacy

## What leaves the computer, and when

| Data | Default | When it is sent | To whom |
|---|---|---|---|
| Your typed question and the conversation so far (last 16 messages word for word; older turns of the same conversation as a one-line-per-turn digest written by code, cleared by New conversation) | — | When you press Ask | The provider you selected |
| A capture of the monitor you are on (with your pen ink drawn in, plus an enlarged crop of what you marked) and the text read from the application in front | Screen **off** | Only when you send from the ask bar (image only if the model is marked image-capable) | The provider you selected |
| Context of your work: the episodes relevant to the question (app, cleaned page address, normalised title, video and position, rewinds, time spent, an optional one-line summary), a one-line view of this run's work ("whole session"), lines with maths read from the window in front (only while screen reading is on; withheld while capture is off or in Share-Safe), and the live state (video position, page address, selected text) | **On**, disclosed at first run and on upgrade | Only with a question you send, or when the tutor calls `get_context` during an answer. The ask bar shows what goes with it ("context · …") and "Leave out earlier" drops the earlier episodes and the session line, for the tool too | The provider you selected |
| Tool results while the tutor answers a screen question: a fresh look at the screen, find results, context | With the screen permission | During that answer only; the tools stop when the answer ends | The provider you selected |
| A short digest of your learning record (concept names, levels, open mistakes) | **Off** for remote providers | With each question, once you allow "notes, files and learning summary" | The provider you selected |
| Notes from your vault (searched or read by the tutor, or a note you ask about) / files from your workspace | **Off** for remote providers | Only once notes may be sent: the note tools are not even offered to a remote model before that | The provider you selected |
| The address of the video you are watching (YouTube or Vimeo only) | With context on | When the tutor calls `video_info` during an answer, to get the video's title and channel | YouTube's or Vimeo's public oEmbed endpoint |
| Text added to your Google Doc "Vernier notes" | Not connected | Each addition only after you approve its exact text | Google, in your account |
| Microphone audio | Mic **off** | Never. Recognition is local (Windows SAPI) | Nobody |
| Telemetry, analytics, crash reports | — | Never. There is none | Nobody |

With a local provider (Ollama, LM Studio) nothing leaves the machine. Nothing is sent until you press Enter or Ask; a pen mark alone sends nothing, and a suggestion (below) only fills in a question for you to send. One model call can happen without a question: if you choose a **local** context model in Settings, it writes a one-line summary of a long episode when you move on. A remote model is never called in the background.

Between questions, Vernier keeps context by code, on this machine:

- **Metadata** (on by default): which app and window is in front and for how long (Windows event hooks, no polling of other processes' memory), the browser's address (accessibility tree, browsers only; query strings, fragments and sign-in parts removed), the media session's title and position (Windows media controls). Private browser windows (title masked; address and screen text not kept), password managers and apps you list are skipped.
- **Screen text** (needs the screen permission and "also read the text"): when the window in front changes and then settles, its text is read by local OCR at most every 4 seconds. That text is kept in memory only and is never written to disk. A small light at the top centre of the screen is on while it reads.
- **Pen ink**: your strokes are drawn by Vernier's own overlay (excluded from captures when you ask for exclusion), kept in memory as points until you send the question or take them back, and never written to disk.
- **Marks**: while the tutor's marks are on screen, a 48×48 grayscale thumbnail of the marked area is compared about once a second so marks that no longer point at the right thing are removed. Memory only.

All of it stops in Pause and in Share-Safe. Questions typed in the workspace never capture the screen.

## What is stored locally (`%LOCALAPPDATA%\Vernier`)

| File | Contents | Retention |
|---|---|---|
| `learning.db` (+ `backups\`, 7 kept; `.before-vN` copy before a schema upgrade) | Sessions (your questions and the tutor's answers), concepts, evidence, mistakes, cards, formulas; **episodes** (app, cleaned address, title, video and position, rewinds, durations, questions asked, optional summary; never screen text) | Sessions until you delete them or set a retention period; episodes 14 days by default, and Settings › Privacy forgets the last hour, today or all. Erase everything deletes all of it, backups and upgrade copies included |
| `settings.json` | Preferences, provider list, key *names*, the Google OAuth client id (not a secret). No secrets | — |
| `logs\vernier.log` (≤ 512 KB ×2) | Events only: start, policy changes, provider error kinds, tool names and outcomes. Never questions, answers, screen text, typed input or images. Redacted | Rotated |
| `tmp\` | Per-answer working folder for the Claude Code CLI: its system prompt and the MCP config with that answer's tool token | Deleted after each answer and at every start |

Screenshots are sent inline and are not written to disk. Raw audio is never written anywhere. In your vault, `.vernier-journal.json` exists while a note change is being written, and after a crash until the next start (see below).

**Attachments.** A file you attach is read on this machine (slides and documents are unpacked as zip and XML; nothing in them is run; credential-like files are refused). With a remote provider its text and pictures are sent with your question, and with later questions while it stays pinned. Vernier asks before the first such send each time it is started, unless "send notes and files" is already allowed in Settings.

**Account sign-in** (Claude, ChatGPT) is done entirely by the vendor's own CLI in your browser. Vernier starts `claude auth login` / `codex login` in a visible terminal and later asks the tool whether it is signed in. It never sees, stores or reads a password, token or the tools' credential files.

**Google Docs** sign-in uses your own OAuth client (Desktop app type). You sign in in your browser; Google redirects to `http://127.0.0.1:<random port>/` with a one-time code, which is exchanged with a PKCE verifier (S256) and checked against a random state. The only scope asked for is `drive.file`: Vernier can open the documents it created and nothing else in your Drive. The refresh token and the client secret are kept in Windows Credential Manager; access tokens stay in memory. Disconnect removes them here and asks Google to revoke the grant.

API keys are read from an environment variable if set, otherwise from **Windows Credential Manager** (generic credential `Vernier:<NAME>`, current user). They are not written to any file by Vernier.

## Consent gates (enforced in code)

| Capability | Setting | Enforced in |
|---|---|---|
| Screen capture | `AllowScreenCapture` | `TutorSession.CaptureBlocked` (also true in Share-Safe and when paused); the `look` and `computer_act` tool gates |
| Context (metadata) / reading screen text for context | `AllowActivityContext` / `ReadScreenForContext` | `ContextHub` `allowed` / `mayRead`, checked on every event (off in Pause and Share-Safe; reading also needs screen capture); `get_context` and `media_control` gates |
| Background summaries | `ContextModelProviderId` | `EpisodeSummariser`: only a provider marked local, only for episodes of 3 minutes or more |
| Microphone | `AllowMicrophone` | `TutorPanel.StartListening`; engine created only on first press |
| Notes, files and learning-record digest to a remote provider | `AllowSendNotesToRemote` | `FilesPage.ConsentToSend`, `TutorPipeline` (digest withheld), `TutorSession.ToolNamesFor` (note tools and `gdocs_read` not offered) |
| Assisted input (the tutor's hands) | `AllowComputerControl` | `computer_act` gate; every step needs your approval (`ToolExecutor` → `UiApprover`) |
| Writing to your vault / your Google Doc | — | `notes_write` / `gdocs_append` are Act tools: each needs your approval of the exact change |
| Suggestions | `ProactiveSuggestions` | `Proactive.Offer`; local only, at most two an hour |

Pause (button, tray or Ctrl+Alt+P) cancels the running request, puts the pen down, cancels the microphone and stops speech, blocks capture and stops context until resumed. If Windows refuses a capture (locked session, secure desktop) a screen question is not sent, and you are told why.

## The tutor's tools

During a screen question the model can call Vernier's tools, through native tool calling or, for the Claude Code CLI, a loopback MCP server (127.0.0.1, a bearer token issued per answer, Origin and Host checked, grants revoked when the answer ends). Every call goes through one executor: the tool must be granted for this answer, its arguments are validated against the schema, the setting gate is checked, Act tools are previewed and approved, and text from the screen, notes or documents comes back fenced as untrusted. At most 24 calls per answer, and calls with an effect run one at a time, each exactly as previewed. Esc in the ask bar or on an approval card ends the answer and revokes its tools; while a step of the tutor's hands runs, Esc works wherever the focus is.

| Tool | Risk | What protects you |
|---|---|---|
| `find_on_screen`, `look` | Observe | Screen permission; the capture light is on |
| `overlay_mark`, `overlay_marks`, `overlay_write`, `overlay_clear` | Annotate | Marks and note cards only (text the tutor wrote, never anything read from your files); refused when the area changed since the capture; a card is placed on free space and never over Vernier's own windows; the tutor cannot remove your ink |
| `session_recall` | Observe | Earlier turns of this conversation only; output fenced |
| `get_context` | Observe | Context setting; output fenced |
| `video_info` | Observe | Only YouTube and Vimeo addresses, sent to their own oEmbed endpoints; output fenced |
| `media_control` | Control | Play/pause/seek of the media session only |
| `notes_search`, `notes_read` | Observe | Offered only with a vault and notes allowed; `PathGuard`; Markdown only; output fenced |
| `notes_write` | Act | Preview shows the diff; one note per concept (existing notes are added to, never replaced); applied as one transaction (below) |
| `gdocs_read` / `gdocs_append` | Observe / Act | Only Vernier's own document (`drive.file`); additions approved with their exact text |
| `computer_act` | Act | See "The tutor's hands" |

### The tutor's hands (`computer_act`)

One input step per call (click, double-click, right-click, type, keys, scroll), only in the window the question was asked about:

1. **Planned before you are asked.** The target must be named by an id or exact text from the screen list; a pixel box the model guessed is refused. `ActionPolicy` checks the step (system chords such as Win+R, Alt+F4, Ctrl+Alt+Del and Alt+Tab are never sent; no control characters; text ≤ 400 characters). Known terminal and console programs (Windows Terminal, cmd, PowerShell, WSL, PuTTY and others, by process name), administrator or protected processes, and Vernier itself are refused outright; typing or keys are also refused when the focused element is a terminal pane inside another program (VS Code, Visual Studio, JetBrains), as far as its accessibility information shows. A terminal not recognised by either check is not refused.
2. **Shown, then approved.** The target is marked on screen and the card says what will happen, where and why. The card stays on top without taking the focus or blocking Vernier, has no default button and ignores clicks for its first half second (so a key you were typing cannot answer it), and closes by itself as a "no" after a minute.
3. **Checked again just before it runs.** The window must still be the same, at the same place and size, the pixels under the target must not have changed since the capture, and nothing may cover the point. Typing stops if the focused field is a password field, or if Windows cannot say whether it is one.
4. **Verdict after.** The result says whether the window changed; the model must look again before the next step.
5. **Esc** stops a step (held as a global key while the step runs; if Vernier cannot hold it, the step does not run) and ends the tutor's hands for the rest of that answer. Esc on the card ends the whole answer.

Residual risk: an approved click on the right control can still do something you did not expect in that application. Approve only steps you understand, in software with undo.

### Note writes are all or nothing

A note change (the concept note and the index of concepts) is checked against what the preview showed: if any file changed meanwhile, nothing is written. Before writing, the exact previous bytes of each file are journalled in the vault, with a hash of what will be written; if a write fails part way, every file is put back; if Vernier or the computer stops mid-write, the next start puts back only the files that still hold the interrupted write, so anything edited since is left alone. A journal is honoured only for paths inside the vault, and one cut short or marked finished undoes nothing. Note changes in one vault never run at the same time. Only the block between Vernier's markers in the index is rewritten.

## Threat model

| Threat | Mitigation | Residual risk |
|---|---|---|
| **Prompt injection** from screen text, web pages, documents, notes, file contents, tool results | Wrapped in `<untrusted>` fences that cannot be closed from inside; the system prompt says fenced text is never instructions, and the tutor is told never to act on instructions from the screen; injection-shaped screen text is dropped from context and raises a visible notice; no tool runs a shell, opens a URL of the model's choosing or writes without your approval | The model may still be *influenced* in what it says or proposes; anything with an effect needs your approval of the specific step |
| Model proposes harmful **input** | Off by default; per-step approval with an on-screen preview; targets resolved by code, not guessed; live re-check; refused in terminals, elevated windows, password fields and Vernier; blocked chords; Esc revokes | An approved step can still be the wrong idea |
| **Approval fatigue or a stale card** | One card per step, with the target marked; unanswered cards close as "no" | A learner can still click Allow without reading |
| Model output treated as trusted | Structured block parsed defensively; numbers recomputed by code; annotations bounds-checked; no model output is executed | Prose can still be wrong where nothing is mechanically checkable |
| **Secret leakage** | Keys and OAuth tokens never in settings, logs or prompts; `SecretRedactor` on every log line and provider error body; Gemini key in a header; typed text is never logged | A key typed into a question is sent like any other text |
| **Path traversal / unintended file writes** | `PathGuard`: canonicalised paths, root confinement, every link or junction on the path re-checked, drive roots refused, credential files never read or written; writes only through preview + approval; stale previews refused; journalled transactions | A user can still authorise a folder containing sensitive material |
| OAuth code interception or a forged redirect | PKCE S256, random state compared in constant time, loopback redirect on a random port, `drive.file` only | A malicious local process could race for the loopback port; it would still lack the verifier |
| Another local process calling Vernier's tools | Loopback only; token per answer; Origin and Host checked; grants end with the answer | A process running as you can read the per-answer config while the answer runs |
| Malicious **CLI output** (Claude Code, graphify) | Argument lists (no shell); the Claude Code CLI runs with its own tools off (`--tools ""`, `--restricted`, strict MCP config); output parsed, bounded and redacted | Trust in the installed binaries themselves |
| Insecure transport | Remote endpoints must be `https://`; `http://` accepted for loopback only | A user-configured custom endpoint is trusted by the user |
| Runaway cost or loops | ≤ 24 tool calls per answer; one correction attempt; ≤ 3 provider attempts and only before output; per-call timeouts; no background remote calls; history capped at 16 messages | Long answers on expensive models still cost what they cost |
| Corrupt state | Corrupt settings set aside; damaged database restored from the latest backup; copy before each schema upgrade; vault journal recovery; all reported on start | Work since the last backup can be lost |
| Silent provider switching | Not implemented anywhere. A failure is shown; the provider only changes when you change it | — |

Vernier runs unelevated (`asInvoker`). It cannot read or send input to elevated windows, and it refuses to try.

## Window capture exclusion: what it is and is not

Vernier calls the documented `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` on its **own** top-level windows (workspace, ask bar, glass, capture light, dialogs), checks the return value, and reads the result back with `GetWindowDisplayAffinity`.

- **Standard window**: no request (`WDA_NONE`).
- **Request capture exclusion**: request made for every Vernier window, re-applied to windows created later.
- **Share-Safe mode**: the same request, plus the pen is put down, no new captures are taken and context stops. **Hide** (Ctrl+Alt+H) minimises/hides every Vernier window.

"Applied" in the UI means: Windows accepted the call and reports affinity `0x11` for that window. It does **not** mean a given meeting, recording or remote-desktop product leaves the window out. That depends on how the product captures and on the Windows build. Display affinity is a best-effort content-protection feature, not a security boundary. It does nothing against a camera pointed at the screen, a hardware capture device, or software that deliberately ignores it. Vernier never injects into or modifies another process.

Requires Windows 10 version 2004 (build 19041) or later; earlier builds report **Unsupported**.

Independently of the user's mode, Vernier excludes its own windows for the instant of each of its own captures so the tutor does not read its own interface back.

What was actually tested is recorded in `VERIFICATION_REPORT.md`; how to test your own conferencing tool is in `MANUAL_VERIFICATION.md`.
