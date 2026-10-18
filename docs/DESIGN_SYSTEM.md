# Design system

## Why it was redesigned

The first version used warm paper, a serif reading face and an orange accent. That combination is the house look of several AI products, so it read as "another AI app", and it was built around a workspace window, when nearly all the use happens with that window closed. Version 2 starts from how the product is used: the learner is in a video, a paper or a drawing, and Vernier is a sheet of glass over it.

## Three directions explored

| | A. Instrument (v1) | B. Glass and two inks (chosen) | C. Companion cursor |
|---|---|---|---|
| Idea | Drafting paper, serif prose, vermilion index mark, workspace-first | An invisible glass over the screen; the learner writes in yellow, the tutor in blue; a blue light round the edge when it is looking | A character that follows the pointer and speaks (the Clicky model) |
| Fits "90 % of use is while working elsewhere" | No: built around its own window | Yes: nothing on screen until called | Yes |
| Distinctive | Reads as an AI product | Yes: the two-ink rule is the identity | Yes, but cute; wrong for long study sessions |
| Honest about capture | A small lamp | The whole screen edge lights | Easy to miss |
| Risk | — | Marks must stay legible over any content | Distraction |

**B was chosen.**

## The one rule

**Yellow is you. Blue is the tutor.**

- Your pen ink on the screen is marker yellow. Your messages carry a yellow rule. The ask bar's stripe is yellow while it waits for you.
- The tutor's arrows, boxes, circles, brackets and labels are signal blue. Its answers carry a blue rule. The stripe turns blue while it works. The capture light round the screen is the same blue.
- Red and green appear only inside tutor marks that mean "wrong here" and "right here".

Everything else is slate and text. There is no third accent.

## Surfaces

| Surface | What it is | When it is visible |
|---|---|---|
| **Glass** | One transparent, click-through window per monitor. Holds tutor marks and your ink | Always up; shows nothing until there are marks |
| **Capture light** | Four 12 px light-blue bands on the screen edges | Steady at 55 % while the pen is armed; a wave travelling clockwise while a capture is being read; off otherwise |
| **Ask bar** | 580 px bar: PEN, one input line, Ask; the answer opens beneath it | On the Ask or Pen hotkey; Esc closes it. Top-centre of the screen, or beside what you marked |
| **Workspace** | Conversation, Review, Library, Notes & files, Settings, Diagnostics | When opened from the tray. Closing hides it |
| **Tray icon** | The app's home | Always |

The learner has exactly two tools: **Ask** and **Pen**. Arrows, boxes, circles, brackets and labels are the tutor's vocabulary, not buttons.

## Themes and tokens (`Themes/*.xaml`, chosen in Settings › Theme)

Five presets, all cool, all with the same roles. The learner's yellow never changes; the tutor's colour is each theme's cool accent. Every text pair was checked with a contrast script (text ≥ 4.5:1, including `InkFaint` on `SurfaceAlt`).

| Token | Night (default, `Dark`) | Aurora | Nebula | Glacier | Paper (`Light`) | Use |
|---|---|---|---|---|---|---|
| `Bg` | `#0C141B` | `#08171A` | `#0E0C1A` | `#EEF3F7` | `#F3F5F6` | Window and bar background |
| `Surface` / `SurfaceAlt` | `#121C25` / `#1A2731` | `#0D2226` / `#122C31` | `#151228` / `#1C1834` | `#F8FBFD` / `#E1E9EF` | `#FFFFFF` / `#E6EAEC` | Inputs, panels / selected row |
| `Ink` / `InkSoft` / `InkFaint` | `#E4ECF2` / `#B2C0CB` / `#8496A3` | `#E2F1F0` / `#A9C7C5` / `#7FA19F` | `#ECE9FA` / `#BDB6DE` / `#9089B5` | `#0F1D29` / `#3C5466` / `#536879` | `#0F1C28` / `#3B4A57` / `#5A6975` | Text, secondary, readouts |
| `Rule` / `RuleStrong` | `#25343F` / `#45596A` | `#1C3A3F` / `#3D6266` | `#2A2548` / `#4D4680` | `#CBD7E0` / `#9DB2C2` | `#D3D9DD` / `#8F9BA5` | Hairlines / control borders |
| `Accent` / `AccentText` (tutor) | `#6CC4FF` / `#8AD0FF` | `#4FD1C5` / `#7FE0D6` | `#9DA8FF` / `#B8C0FF` | `#1769C9` / `#145CB0` | `#1479C4` / `#0F5E9A` | Tutor rule, selected tab |
| `You` (learner) | `#FFD84D` | `#FFD84D` | `#FFD84D` | `#E6B800` | `#E6B800` | Your rule, hotkeys, waiting stripe |

`Ok` / `Warn` / `Err` are `#6FD98A` / `#F2C14E` / `#FF8A8A` in the dark themes and `#1E6B3A` / `#7A5C00` / `#B42323` in the light ones, always with a word. Each theme also defines `IsDark`, `BgGlass` (the bar's tint over acrylic), `Edge` and `Sheen` (below).

Night is the default because the bar floats over other people's content and a dark slate bar disturbs a video or a white page least.

On-screen ink (fixed, not themed): tutor `#2F9BE6`, learner `#FFD400`, error `#C92A2A`, correct `#2B8A3E`, note `#1864AB`. Every stroked mark is drawn over a soft dark halo (2.5 px wider, 38 % black) so it reads on light and dark content alike; the highlighter swipe has none. Capture light `#6CC4FF`.

## Typography

| Role | Family | Size |
|---|---|---|
| Titles | Sitka Heading (optical size for 20–28 pt), fallback Cambria, Georgia | 23 |
| Headings | Sitka Subheading | 16 |
| Reading (answers) | Sitka Text, with its old-style figures | 15.5, line height 1.5 |
| Mathematics | TeX typeset by WpfMath; `\text{}` in Sitka Text | 1.08× inline, 1.25× display |
| Interface | Segoe UI Variable Text, fallback Segoe UI | 13 |
| Readouts (status, tags, hotkeys, tabs) | Cascadia Mono, fallback Consolas | 10.5 small caps / 11.5 |

Sitka ships with Windows in optical sizes, so each size uses a face drawn for it; nothing is bundled. Answers are documents, not strings: headings, lists, code, inline and display formulas (stacked fractions, roots, real exponents), and a caret exponent the model typed in plain text (`x^2`) is set as a superscript. Each formula's accessible name is its spoken form ("a over b"), its tooltip is the TeX source, and speech reads it as words.

## Shape, depth, states

- 4 px grid; 3 px corner radius on controls, 4 px on panels; 1 px borders.
- Depth comes from the compositor first: the ask bar gets Windows 11 acrylic under the theme's `BgGlass` tint, rounded corners and the system shadow (DWM attributes; on Windows 10 or with transparency off it stays opaque and square). Inside, light comes from above: raised panels have an `Edge` border lit at the top, and the one filled button carries a `Sheen`. The workspace caption follows the theme (dark or light title bar).
- Buttons: hairline (secondary), filled ink (`Primary`, one per view), borderless (`Quiet`), red outline (`Danger`). Hover = `Hover` fill; pressed = `Pressed` fill and scale 0.96; keyboard focus = 2 px ink ring; disabled = 45 % opacity.
- Tabs and modes are mono labels with a 2 px blue underline when selected.
- Success, warning and error are text plus a word (`pass`, `fail`, `signed in`, `signed out`), never colour alone.

## Motion

One expressive motion exists: the capture wave, because it carries the most important fact in the product (it is looking at your screen now). With the native module it is a real travelling wave drawn by DirectComposition on its own thread: a Gaussian crest with a fainter follower and a slow shimmer, 2.6 s per lap, smooth falloff into the screen, dithered so it never bands, about 60 fps even while the UI thread is busy. Without the native module, WPF strips with a sliding mask stand in. If Windows' "show animations" setting is off, the light breathes in place instead of travelling. While Vernier is only keeping context (reading text locally), a small pill at the top centre is lit instead of the edges.

The ask bar rises 8 px into place as it fades in (180–220 ms, ease-out); with animations off it only fades. Everything else changes state instantly.

## The note card and the pen

- **Note card** (`overlay_write`): always dark (`#0C141B` at 94 % with a `#45596A` hairline and a 10 px radius), the tutor's blue rule on the left, a small-caps blue title, steps in handwriting (Ink Free, 19 px, ink colour), formulas typeset at 1.25×, the result in a blue-outlined box, checks in green with a tick, errors in red with a cross, asides in italic soft ink. It wipes in from the top in 420 ms (instantly with Windows animations off). Samples: `docs/screenshots/note-card-handwriting.png`, `note-card-clean.png`.
- **Pen ink**: marker yellow with a thin dark halo, 4 px, drawn the moment the cursor moves; it holds 1.4 s after the pen lifts and fades over 0.35 s. It never stays on the screen; only the tutor's marks and cards do.

## Signature elements

- **The capture light**, above.
- **Stage scale** in the workspace: the pipeline as a ruled scale with the running stage ticked in blue.
- **Level scale** in the Library: mastery as four graduations.
- **Wordmark**: a fixed scale over a sliding vernier scale.

## Keyboard

Ask: Ctrl+Alt+Space (falls back to Ctrl+Alt+A) · Pen: Ctrl+Alt+D (falls back to Ctrl+Alt+S; press again to put it down) · Pause: Ctrl+Alt+P · Hide everything: Ctrl+Alt+H · In the ask bar: Enter ask, Esc stop / pen down / close · Review: Space reveal, 1–4 grade. The interface always shows the chords that were actually registered.

## Review screenshots

`docs/screenshots/`: every workspace page and the ask bar (with a typeset sample answer) in all five themes (`dark`, `aurora`, `nebula`, `glacier`, `light`), rendered from the real application against sample data (`Vernier.exe --data <dir> --screenshots docs/screenshots`). The renders are of WPF content: the acrylic backdrop, the system shadow, the glass and the capture light come from the compositor and are not in them.

## Revision 3: ask bar

- **Icons, not words, for the two tools in the bar.** Pen and paperclip come from Windows' own icon font (`Segoe Fluent Icons`, falling back to `Segoe MDL2 Assets`): pen `E70F`, attach `E723`, 17 px, inheriting the control's foreground. Each has a tooltip and an accessible name. No other icon set is used.
- **Attachments are chips above the input**: file-type glyph in `You` yellow (they are the learner's), name in the mono readout style, a `×` to remove. They stay until removed.
- **The answer area shows a refusal as a note, not as an answer.** When a question was not sent, the reading box is hidden and the reason is the only thing shown.
- **Circles enclose.** A tutor circle goes round every corner of the target's box instead of across it. Labels are clamped to the glass.

## Revision 4: shape language (no boxes)

The tutor writes on the screen the way a teacher writes on paper, so no mark is a hard rectangle (`Overlay/MarkShapes.cs`, sample `docs/screenshots/tutor-marks.png`):

| Mark | Shape |
|---|---|
| Circle | A hand-drawn loop: starts upper left and overshoots its start a little; a superellipse through the box corners (an ellipse for squat targets, a flatter oval, never squarer than exponent 2.8, for a line of text), with a slight wobble |
| Highlight | A highlighter swipe: translucent (28 %), round ends, no outline, no halo |
| Underline | A quick pen stroke that sags a touch and lifts at its end |
| Bracket | A curly brace down the left side |
| Arrow | A gently bowed shaft with an open head aligned to where the shaft arrives |
| Label | A rounded tag (corner = half its height, at most 10 px) with a dot of the mark's colour and a faint light rim |
| Note card | 12 px radius, lit from above (paper a shade lighter at the top, rim brighter at the top), on a soft shadow (22 px blur, 5 px down); the boxed result has a 9 px radius |

Every shape is smooth (Catmull-Rom through its samples, so there are no corners where samples join) and deterministic for its mark, so a mark rebuilt on another monitor is the same mark. A new mark is **drawn on** along its length in 260–560 ms (eased out) as it fades in, like a pen moving; a dashed (uncertain) mark and everything with Windows animations off only fades.

**Dismissing a note card.** Each card has a small round × in its top-right corner (24 px, dark in every theme, blue rim on hover). It is the only part of the overlay that takes a click; everything else stays click-through. Ask bar › Clear marks still removes every mark at once.
