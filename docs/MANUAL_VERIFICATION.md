# Manual verification checklist

For things automation cannot judge or reach. Use a throwaway profile: `Vernier.exe --data %TEMP%\vernier-manual`.

## 1. First run and provider

- [ ] First launch shows the welcome dialog; capture and microphone are unchecked.
- [ ] Settings › AI provider › **Test connection** succeeds for the provider you use.
- [ ] With a wrong key, the test shows an `Auth` error and the key does not appear in Diagnostics › Log.
- [ ] **List models** fills the model list from the service (OpenAI-compatible, Gemini, Anthropic).

## 2. Living in the tray

- [ ] Close the workspace with the X: it disappears, a balloon says Vernier is still here, and the icon stays in the notification area.
- [ ] Left-click the icon: the workspace returns. Right-click: Ask, Pen, Clear marks, Pause, Hide everything, Open workspace, Quit.
- [ ] With the workspace closed, the Ask and Pen hotkeys still work.
- [ ] Quit from the tray ends the process and removes the icon.

## 3. Ask, Pen and the capture light

- [ ] First screen question asks permission and explains what is sent.
- [ ] **Ask** (Ctrl+Alt+Space or the fallback shown in the app): the bar opens at top centre. Press Enter with nothing typed: Vernier answers about what is on screen. While it works, a light-blue wave travels round all four screen edges and stops when the answer is done.
- [ ] **Pen** (Ctrl+Alt+D): the edges light steadily, the pointer is a pen, clicks no longer reach the app underneath. Circle something in a paused video. Nothing is sent. Click in the bar, type a question (or leave it empty) and press Enter: the wave starts and the answer explains what you circled; the bar moves beside your mark. Tutor marks appear in blue, your ink stays yellow.
- [ ] Ask again after the video has moved on or the page has scrolled: the answer is about the screen as it is now and does not apologise for the earlier one.
- [ ] With tutor marks on screen, scroll or zoom the page, or play the video: within a couple of seconds the marks disappear and the bar says the screen changed. *(Feel and timing not judged by the author; the removal itself is tested.)*
- [ ] Paperclip: attach a picture, a .pptx and a .docx; each appears as a chip; `×` removes it. Paste a screenshot with Ctrl+V; drop a file on the bar. A PDF is refused with a reason.
- [ ] In Share-Safe mode, Ask: nothing is sent and the bar says how to switch it off.
- [ ] Draw a diagram in any drawing app, press Ask, Enter: you get specific feedback on your own work with marks where it applies.
- [ ] Esc: stops a running answer; then puts the pen down; then closes the bar. With the bar closed, clicks reach your apps again.
- [ ] Marks are readable over white pages and over dark video. Uncertain tutor marks are dashed.
- [ ] Undo mark / Clear marks in the bar and Clear marks in the tray act immediately. Move the window you asked about: tutor marks follow it.
- [ ] Windows Settings › Accessibility › Visual effects › Animation effects off: the edge light is steady instead of a wave.
- [ ] Change display scaling or resolution with marks on screen: marks stay in place or are dropped, never drawn somewhere wrong. *(Not verified by the author: single-display machine.)*
- [ ] Second monitor at a different scale: glass, edge lights and marks are right on both; Ask uses the monitor the pointer is on. *(Not verified by the author.)*

## 3a. Accounts

- [ ] Settings › Accounts lists Claude and ChatGPT with `signed in`, `signed out` or `not installed`.
- [ ] **Sign in…** opens a terminal running the vendor's command; after finishing in the browser, **Check again** shows `signed in`; **Use for tutoring** switches the provider.
- [ ] With the ChatGPT account signed in, a screen question is answered through Codex. *(Not verified by the author: no ChatGPT account was signed in.)*

## 4. Voice

- [ ] With the microphone permission off, **Hold to talk** is disabled and says why.
- [ ] Turn it on; hold the button (or focus it and hold Space), speak, release: text appears in the box for you to check before sending. *(Recognition quality with a real microphone was not verified by the author.)*
- [ ] **SPEAK ANSWERS** reads the next answer; **Pause voice**, **Stop voice**, and asking a new question each interrupt it; speed follows Settings.
- [ ] Unplug the microphone: holding the button shows an actionable message, no crash.

## 5. Capture exclusion with your conferencing tool

Windows only promises best effort; test the tool you actually use.

1. Diagnostics › **Run capture self-test**. Expect: read-back `0x11`, and "marker colour absent" for the GDI capture.
2. Set the header selector to **Request capture exclusion**. Diagnostics should list every Vernier window as `applied`.
3. Start a meeting with a second device or account watching (Zoom, Google Meet in a browser, Teams).
4. Share your **entire screen**. On the watching device note whether the Vernier window is: absent (content behind shows), a black rectangle, or visible.
5. Share **a single window** (another app) with Vernier's ask bar and some marks over it. Note whether they appear.
6. Open the tool's own "share a window" list: note whether Vernier is listed, and what sharing it shows.
7. Repeat 4–5 with **Standard window** to confirm the difference is the policy.
8. Also try: Snipping Tool, Win+Shift+S, Xbox Game Bar recording, OBS (Display Capture and Window Capture), and any remote-desktop tool you use.
9. Record tool, version, share mode, Windows build and result. If a tool shows the window, use **Share-Safe mode** together with **Hide** (Ctrl+Alt+H) for that tool.

## 6. Obsidian and Graphify

- [ ] Notes & files › choose a vault. **Write concept notes…** shows a diff per note; Cancel writes nothing; approving creates `Vernier/Concepts/<name>.md`.
- [ ] Edit that note in Obsidian, then write again: your edit is still there and the new material is appended.
- [ ] **Load knowledge graph** lists the most-linked notes. With `graphify-out/graph.json` present it says the source is Graphify.
- [ ] **Ask the tutor about the selected note** asks for consent before sending to a remote provider.

## 7. Workspace files

- [ ] Choose a folder; `.env`, key files and `bin/obj/.git/node_modules` do not appear.
- [ ] **Suggest an edit…** shows a diff; Cancel leaves the file unchanged; modify the file on disk while the diff is open and approve: it refuses.

## 8. Assisted input (optional, off by default)

Use a harmless target (Notepad, with nothing unsaved).

- [ ] Enable it in Settings › Privacy. Click into Notepad, press the Ask key and ask "type hello for me, then save".
- [ ] A card appears on top saying exactly what will be typed and in which window; the target is ringed on screen when it is a click. Vernier stays usable while it is open.
- [ ] Allow once: the text appears. The next step (Ctrl+S) asks again. Leave a card unanswered for a minute: it closes by itself and nothing runs.
- [ ] Press Esc while a step runs: it stops and the tutor does not act again in that answer.
- [ ] Ask in a terminal (Windows Terminal, cmd, PowerShell): the tutor says it will not type there, and no card appears.
- [ ] Ask for something needing Alt+F4 or Win+R: refused without a card.
- [ ] Move the ask bar over the target and ask for a click: refused, "the ask bar is over that point".
- [ ] In real professional software (Blender, Maya, Photoshop, Logger Pro): the tutor uses that application's vocabulary. *(Not verified by the author in those applications.)*

## 8b. Context, suggestions, notes, Google Docs

- [ ] Watch a YouTube video, rewind to the same spot twice within five minutes: a notification "Stuck on this part?" appears. Click it: the ask bar opens with the question filled in and nothing is sent until Enter. Dismiss the next one: the following offer of that kind waits longer.
- [ ] Ask "what was I watching?" a few minutes later: the ask bar's context line names the video; "Leave out earlier" drops it.
- [ ] Settings › Privacy › Forget the last hour: the episode is gone from the next question's context.
- [ ] With a vault connected and notes allowed, ask "make a note on torque": the card shows the diff for `Vernier/Concepts/Torque.md` and `Vernier/Index.md`. Edit the index in Obsidian before allowing: nothing is written and the tutor is told why.
- [ ] Google Docs with your own OAuth client (Desktop app, consent screen in testing with your account as a test user): Connect in Settings › Connectors, sign in in the browser, ask "save this to my Google notes": a "Vernier notes" document appears in your Drive with the text. Disconnect: the grant disappears from your Google account's third-party access.

## 8b2. Pen and note cards

- [ ] Arm the pen and circle something: the stroke follows the pointer with no visible lag, fades about 1.5 s after you lift the pen, and is gone. Press Ask: your marks went with the question and the screen shows only the tutor's marks.
- [ ] Draw two strokes, press Undo mark: only the last stroke is taken back. Clear marks takes back every stroke.
- [ ] Ask the tutor to solve a worksheet problem: a dark card with the working appears beside the problem, not over it, formulas typeset, the answer boxed; marks on the quantities appear in one go. Move the ask bar over the free space and ask again: the card goes elsewhere.

- [ ] A note card shows a small × in its top-right corner. Clicking it removes that card only, without taking focus from the window you were in; the tutor's other marks stay.
- [ ] Tutor marks look hand-drawn: a loop round the target (not a box), a translucent swipe for a highlight, a brace, a bowed arrow; each draws on in about a third of a second.

- [ ] Scroll the page a little with marks on it: the marks move with their content. Scroll so one marked line leaves the screen: only that mark goes. Open another page in the same window: all marks go.
- [ ] Open a velocity–time graph and ask for the displacement: the region is shaded with triangles/rectangles/trapezoids, each labelled with its area, and the answer uses those numbers and says approximate for a curve.
- [ ] Open Blender (or any app Vernier does not know) and ask what a tool does: the ask bar shows "researching on the web", the answer explains in its own words and ends with a short Sources line naming at least three sites.

- [ ] Ask about the shortest distance between two skew lines (or vectors, a complex number, 4D): a dark diagram card appears on free space with axes, the lines, the common perpendicular and the distance, and the tutor explains around it. Its × removes it.
- [ ] On a curved graph, ask for the displacement: the shaded region follows the drawn curve (not a few straight pieces floating off it).
- [ ] No answer, label or card shows raw backslashes or braces.

- [ ] Ask a long question (research and drawing, over two minutes): the tutor still draws at the end; Diagnostics' log shows "Tool … ok" lines.
- [ ] A ramp or pulley question: a free-body diagram appears with the net force; move the window: the diagram card moves with it.
- [ ] The sphere worksheet: the area under the curve is shaded only to 0.8 s and a tank-of-water force diagram appears. The friction worksheet: a line is sketched on the empty grid from 0 to Fmax and a stacked-blocks diagram appears.
- [ ] Scroll a page with marks on it: they move with it, disappear when their content scrolls away, and come back when you scroll back, without asking again.
- [ ] Drag a note card by its grip (left of the ×); drag the answer panel's corner to resize it, restart: the size is kept.
- [ ] With Assisted input on, ask the tutor to put the answer into Word, PowerPoint, Excel and Notepad: each shows the exact text first, and Ctrl+Z in Word removes it in one step.

## 8c. Look and feel

- [ ] Try each theme (Settings › Theme): the ask bar is see-through acrylic with rounded corners and a soft shadow on Windows 11; with "Transparency effects" off it is opaque.
- [ ] Ask something with a formula ("derive the kinetic energy"): fractions are stacked, exponents raised, roots drawn; Narrator reads formulas as words.
- [ ] Ask about the screen: the edge light is a wave travelling round the screen, smooth (no bands), and keeps moving while the bar is busy. With Windows animations off it breathes in place.

## 8d. Shipping

- [ ] `scripts/publish.ps1`: the output folder holds exactly `Vernier.exe` and `Vernier.Native.dll`; copied to another folder (a path with spaces), the exe starts, shows the tray icon, and Diagnostics reports the native module in use.

## 9. Data

- [ ] Review a card, restart: the due count stays reduced.
- [ ] Export, Erase everything, Import: concepts and cards return.
- [ ] Delete a session: it no longer appears in search.
- [ ] Library: Delete on a concept (after confirming) removes it with its formulas, examples, mistakes and cards; Delete on a formula, example or mistake removes only that row. Review: Delete card removes the card from the queue.
