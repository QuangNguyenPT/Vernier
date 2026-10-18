# Vernier

A tutor that sits over your screen while you study. Stay in the video, the paper or the drawing; press a key to ask, or pick up the pen and circle what you do not understand. Vernier looks, explains from first principles, marks the screen, and recomputes its own numbers.

- **Two tools, from anywhere:** **Ask** (Ctrl+Alt+Space) and **Pen** (Ctrl+Alt+D). Arrows, boxes, circles and labels are drawn by the tutor, in blue; your ink is yellow.
- **Always there, never in the way:** a transparent click-through glass over every monitor; the app lives in the notification area and closing the window does not quit it.
- **You always know when it is looking:** a light-blue wave, drawn by a small native DirectComposition light, travels round the edge of the screen for as long as a capture is being read.
- **It knows what you are working on:** by code, not by a model. Vernier keeps episodes of your work (app, page, video and position, rewinds, time) and a whole-session view, reads the text of the window in front locally, and remembers the entire conversation: the latest turns word for word, older ones as a compact digest it can look up. You see what goes with each question in the ask bar, and you can forget the last hour, today or everything.
- **Fast to draw, nothing left behind:** the pen is drawn by a native layer on its own thread and fades by itself; only the tutor's marks stay. The tutor points with one batched call and writes its working on a dark handwriting card placed on free space beside the problem, like a teacher writing next to it; it never writes drawing code.
- **Researches instead of guessing:** for a web page, software such as Blender, or anything it is unsure of, the tutor searches and reads at least three working pages before it explains, in its own words (required by code, re-asked once if skipped).
- **Graphs:** asked for an area under a graph, the tutor shades the triangles and trapezoids on the graph and Vernier computes the areas.
- **Marks stay with the content:** they move when the page scrolls, and only the ones whose content is gone are removed.
- **Answers are typeset:** reading text in Sitka, formulas as real mathematics (stacked fractions, exponents, roots) that screen readers speak; five colour themes (Night, Aurora, Nebula, Glacier, Paper) with Windows 11 acrylic and depth.
- **Use the account you already have:** sign in with your Claude or ChatGPT subscription through the vendor's own CLI, or use an API key, or a local model.
- Three cooperating agents: **Observer** (reads the screen locally), **Mastery Tutor** (teaches), **Verifier** (re-checks arithmetic, units and mark placement in code).
- Review: spaced repetition, mistake journal, formula sheet, mastery by demonstrated work.
- The tutor has tools, each checked by one executor: look at and mark the screen, recall context and the session, search and write your Obsidian notes, add to a Google Doc of yours, and (only when you allow each step) operate the application you asked about. Tools reach the Claude Code CLI through a local MCP server (loopback only, a token per answer).
- Optional: Obsidian vault notes, Graphify graph, workspace file explanations and reviewed edits, free models through OpenRouter or a local server.

Status: version 0.4.0 on one Windows 11 machine. Read [docs/VERIFICATION_REPORT.md](docs/VERIFICATION_REPORT.md) for what is and is not verified.

## Requirements

- Windows 10 version 2004 (build 19041) or later, x64. Capture exclusion needs that build or later.
- To build: .NET SDK 10. The optional native module (GPU screen capture, the edge light) also needs Visual Studio's C++ tools; without it Vernier uses managed fallbacks and still works.
- One AI provider: Claude Code installed and signed in, **or** an API key, **or** a local OpenAI-compatible server.

## Build, test, run

```bash
powershell -ExecutionPolicy Bypass -File scripts/build-native.ps1
```

```bash
dotnet build Vernier.sln
```

```bash
dotnet test tests/Vernier.Tests
```

```bash
dotnet run --project src/Vernier.App
```

Tests open real windows (including Notepad and a console) and need an interactive, unlocked desktop session with the display on; run them when you are not using the computer. Live tests that call Claude Code with your sign-in, including one that drives the real CLI through the MCP server, only run when `VERNIER_LIVE=1` is set.

### Publish a standalone folder

```bash
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
```

Builds the native module if the C++ tools are present, runs the tests, publishes a self-contained build to `artifacts\publish\Vernier-win-x64\`, and smoke-tests the published `Vernier.exe` (every page builds, the native module loads, the MCP server answers `initialize` and `tools/list`). Copy that folder to use it. The build is not code signed; there is no installer and no auto-update.

## Set up

1. Start Vernier. The first-run dialog explains what is sent where; screen and microphone start **off**.
2. **Settings › Accounts**: if you have Claude Pro/Max or ChatGPT Plus/Pro, press **Sign in…**. A terminal opens running that company's own tool (`claude auth login` or `codex login`), which signs you in through your browser. Vernier never sees your password or token. Press **Check again**, then **Use for tutoring**.
   - Or **Settings › AI provider**: paste an API key (stored in Windows Credential Manager) for Anthropic, OpenAI, Gemini or Moonshot and choose a model with **List models**; or point at a local server (default `http://localhost:11434/v1`).
3. **Test connection**.
4. Close the window. Vernier stays in the notification area.

Costs: a subscription account uses your plan's allowance; an API key bills per request. Nothing is captured or sent except when you ask or finish a pen mark.

An example settings file (no secrets) is in [config/settings.example.json](config/settings.example.json). Data lives in `%LOCALAPPDATA%\Vernier`.

## Using it

| You do | What happens |
|---|---|
| **Ctrl+Alt+Space** (Ask) | The ask bar opens at the top of the screen. Type a question, or just press Enter for "explain this / what should I fix". Vernier captures that monitor once and answers in the bar |
| **Ctrl+Alt+D** (Pen) | The screen edge lights steadily and the pointer becomes a pen. Circle or underline something. Nothing is sent yet: click in the bar, type a question (or leave it empty) and press Enter |
| Paperclip in the bar | Attach pictures, slides (.pptx), Word documents or text files; paste a picture with Ctrl+V or drop files on the bar. They stay pinned until you remove them |
| Esc | Stops an answer, puts the pen down, then closes the bar |
| Tray icon | Left click opens the workspace; right click: Ask, Pen, Clear marks, Pause, Hide everything, Quit |

Examples: watching a lecture, circle a term on the slide; drawing a free-body diagram, press Ask and Enter for feedback on your own work; reading a paper, underline an equation and type "where does this come from?".

The workspace (from the tray) holds the full conversation with every check result, plus Review, Library, Notes & files, Settings and Diagnostics. Its modes (Ask, Tutor, Review, Assist) apply to questions typed there and to Ask-key questions; a pen mark is always answered by pointing.

Header selector: **Standard window**, **Request capture exclusion**, **Share-Safe mode** (exclusion + no new captures). **Hide** (Ctrl+Alt+H) removes every Vernier window and mark at once. **Pause** (Ctrl+Alt+P) stops capture, microphone, speech and the running answer. Capture exclusion is a best-effort Windows feature, not a guarantee; see [docs/SECURITY_AND_PRIVACY.md](docs/SECURITY_AND_PRIVACY.md).

If another application already owns a hotkey, Vernier registers a fallback (Ctrl+Alt+A for Ask, Ctrl+Alt+S for Pen) and shows the one in use everywhere.

## Claude Code skill

`.claude/skills/mastery-tutor/SKILL.md` adds `/mastery-tutor` when Claude Code is opened in this repository. Copy the folder to `~/.claude/skills/` to have it everywhere. It is independent of the desktop app.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Design system](docs/DESIGN_SYSTEM.md) and [screenshots](docs/screenshots)
- [Security and privacy](docs/SECURITY_AND_PRIVACY.md)
- [Verification report](docs/VERIFICATION_REPORT.md) and [manual checklist](docs/MANUAL_VERIFICATION.md)
- [Data formats](docs/DATA_FORMATS.md)
- [Plan, risks, acceptance](docs/IMPLEMENTATION_PLAN.md)

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| "No API key found" | Save the key in Settings or set the named environment variable, then restart Vernier if you used an environment variable |
| Claude Code "not signed in" | Run `claude` in a terminal once and sign in |
| Answers ignore the screen | The question was typed in the workspace (those are text-only), screen permission is off, Share-Safe is on, Vernier is paused, or Windows refused the capture (locked session). The note under the answer says which |
| "…is not configured as an image-capable model" | The model is marked text-only. Tick "accepts images" if it really does, or rely on extracted text |
| "ChatGPT account is not signed in" | Settings › Accounts › Sign in…, finish in the browser, then Check again |
| The window closed but Vernier is still running | By design. Right-click the notification-area icon and choose Quit |
| A hotkey does nothing | Another application owns it. Diagnostics lists which; the buttons do the same thing |
| Capture exclusion shows Failed / Unsupported | See the detail text in Diagnostics. Unsupported means the Windows build is older than 19041 |
| Assisted input "Windows blocked the input" | The target runs as administrator; Vernier runs unelevated by design |
| "The learning database … was restored from the backup" | The file was damaged; the damaged copy is kept beside it as `learning.db.damaged-*` |

## Known limitations

Only the Claude account (Claude Code) provider has made live calls; the ChatGPT-account (Codex CLI) provider could not be run because no ChatGPT account is signed in on the build machine, and the API providers are tested against fixtures. The Google Docs connector is tested only against simulated Google responses (it needs your own OAuth client). Single-display testing only. Local SAPI speech; no cloud speech. No PDF/Office parsing, no code execution in the verifier. The tutor operating an application is tested on Notepad only, and its typing path needs Windows to hand it focus (it refuses rather than type into another window). Professional applications get tailored vocabulary, not plugins. Conferencing tools' handling of capture exclusion is untested. Full list in the verification report.

## Repository note

The files in the repository root named `Hopefully-this-goes-somewhere.*` (plus `framework.h`, `Resource.h`, `targetver.h`, `small.ico`) are the original empty Visual Studio C++ template. Vernier does not use them; open `Vernier.sln`.
