using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Tools;

namespace Vernier.App.Agent;

/// <summary>
/// The tutor's hands on the glass: find things on screen, mark them, clear marks, look closer.
/// The model names what to mark (an id from the screen list, or its text); Vernier finds where it
/// is from OCR and the accessibility tree and draws it natively, at once. A pixel box from the
/// model is accepted only as a fallback and drawn dashed as an estimate.
/// </summary>
public static class ScreenTools
{
    public const string FrameKey = "frame";
    /// <summary>The last capture of the whole monitor (a closer look at one part replaces <see cref="FrameKey"/>, but free space is judged on the whole screen).</summary>
    public const string FullFrameKey = "fullframe";
    public const string ScreenKey = "screen", AskKey = "ask", MarksKey = "marks", IdsKey = "ids";

    /// <summary>The turn's id sequence: screen items, phrases and marks all draw from it, so ids never repeat within a turn.</summary>
    public static IdSequence Ids(ToolTurn turn) => (IdSequence)turn.State.GetOrAdd(IdsKey, _ => new IdSequence());
    private static IdSequence MarkIds(ToolTurn turn) => (IdSequence)turn.State.GetOrAdd("markIds", _ => new IdSequence());

    public static IEnumerable<ITool> All() => [new FindOnScreen(), new OverlayMark(), new OverlayMarks(), new OverlayWrite(), new OverlayClear(), new Look()];
    public static readonly string[] Names = ["find_on_screen", "overlay_mark", "overlay_marks", "overlay_write", "overlay_clear", "look"];

    internal static ScreenIndex Screen(ToolTurn turn) =>
        turn.State.TryGetValue(ScreenKey, out var s) && s is ScreenIndex index ? index
            : throw new ToolException("No screen has been captured in this turn, so there is nothing to point at.");

    private static JsonObject Schema(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray([.. required.Select(r => (JsonNode)r)]), ["additionalProperties"] = false,
    };

    private static JsonObject Str(string description, int maxLength = 200) => new() { ["type"] = "string", ["description"] = description, ["maxLength"] = maxLength };

    internal static JsonObject TargetSchema() => Target();

    private static JsonObject Target() => new()
    {
        ["type"] = "object",
        ["description"] = "What to point at. Give exactly one: id (from the screen list or find_on_screen), text (as it appears on screen), or box (screenshot pixels, only for things with no text).",
        ["properties"] = new JsonObject
        {
            ["id"] = Str("An id such as t12, e4, w7 or p2 from the current screen.", 12),
            ["text"] = Str("The text to mark, e.g. \"F = ma\" or \"Extrude\".", 200),
            ["occurrence"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 20, ["description"] = "Which match when the text appears more than once (1 = first listed by find_on_screen)." },
            ["box"] = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray("x", "y", "w", "h"),
                ["properties"] = new JsonObject
                {
                    ["x"] = new JsonObject { ["type"] = "number" }, ["y"] = new JsonObject { ["type"] = "number" },
                    ["w"] = new JsonObject { ["type"] = "number", ["minimum"] = 1 }, ["h"] = new JsonObject { ["type"] = "number", ["minimum"] = 1 },
                },
            },
        },
        ["additionalProperties"] = false,
    };

    internal static ResolvedTarget Resolve(ScreenIndex screen, JsonObject? target)
    {
        if (target is null) throw new ToolException("A target is required.");
        PxRect? box = target["box"] is JsonObject b
            ? new PxRect(Json.Number(b["x"]) ?? 0, Json.Number(b["y"]) ?? 0, Json.Number(b["w"]) ?? 0, Json.Number(b["h"]) ?? 0)
            : null;
        return screen.Resolve(target["id"]?.GetValue<string>(), target["text"]?.GetValue<string>(), Json.Int(target["occurrence"]), box);
    }

    private static Task<T> OnUi<T>(Func<T> work) => AppServices.Ui.InvokeAsync(work).Task;

    private sealed class FindOnScreen : ITool
    {
        public ToolDefinition Definition { get; } = new("find_on_screen",
            "Finds text or a control on the learner's current screen and returns ids with their boxes (screenshot pixels). Use it when the screen list does not show what you need or a text appears more than once.",
            Schema(new JsonObject { ["text"] = Str("Text to look for."), ["max"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 12 } }, "text"),
            ToolRisk.Observe, UntrustedOutput: true);

        public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var matches = screen.Find(args["text"]!.GetValue<string>(), Json.Int(args["max"]) ?? 8);
            if (matches.Count == 0) return Task.FromResult(ToolResult.Ok($"Nothing matching that text on the current screen (observation {screen.ObservationId})."));
            var sb = new StringBuilder($"Matches on observation {screen.ObservationId}, best first:\n");
            foreach (var (item, score) in matches)
                sb.Append(CultureInfo.InvariantCulture, $"{item.Id} \"{item.Text}\" {ScreenIndex.Box(screen.Image(item.Bounds))} via {item.Source} (match {score:0.00})\n");
            return Task.FromResult(ToolResult.Ok(sb.ToString()));
        }
    }

    private static readonly string[] MarkKinds = ["circle", "highlight", "underline", "arrow", "point", "bracket", "label"];

    private static JsonObject MarkSpec() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. MarkKinds.Select(k => (JsonNode)k)]) },
            ["target"] = Target(),
            ["label"] = Str("A few words shown beside the mark (required for kind label).", 80),
            ["meaning"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("focus", "error", "correct", "note") },
        },
        ["required"] = new JsonArray("kind", "target"),
        ["additionalProperties"] = false,
    };

    private const string KindsHelp = "circle = one compact thing; highlight = a line of text or an area; underline = text; arrow = points at it from nearby empty space; point = a ring with a dot for a control to press; bracket = a block of lines; label = a short note beside it.";

    /// <summary>Resolves one mark request into a mark ready to draw. Throws <see cref="ToolException"/> with a message the model can act on.</summary>
    private static (Annotation Mark, ResolvedTarget Target, string Kind, string Id) PlanMark(JsonObject spec, ToolTurn turn, ScreenIndex screen, PxRect monitor)
    {
        var kind = spec["kind"]?.GetValue<string>() ?? throw new ToolException("A mark needs a kind: " + string.Join(", ", MarkKinds) + ".");
        if (!MarkKinds.Contains(kind)) throw new ToolException($"Unknown kind \"{kind}\". Use one of: {string.Join(", ", MarkKinds)}.");
        var label = spec["label"]?.GetValue<string>()?.Trim();
        if (kind == "label" && string.IsNullOrEmpty(label)) throw new ToolException("A label mark needs label text.");
        var target = Resolve(screen, spec["target"] as JsonObject);
        var b = target.Bounds;
        AnnotationKind annotationKind = kind switch
        {
            "circle" => AnnotationKind.Circle, "highlight" => AnnotationKind.Highlight, "underline" => AnnotationKind.Underline,
            "arrow" => AnnotationKind.Arrow, "point" => AnnotationKind.Point, "bracket" => AnnotationKind.Bracket, _ => AnnotationKind.Label,
        };
        IReadOnlyList<PxPoint> points = annotationKind switch
        {
            AnnotationKind.Arrow => OverlayMark.ArrowTo(b, monitor),
            AnnotationKind.Label => [new PxPoint(b.X, b.Y)],
            _ => [new PxPoint(b.X, b.Y), new PxPoint(b.Right, b.Bottom)],
        };
        var mark = new Annotation
        {
            Kind = annotationKind, Points = points, Label = string.IsNullOrEmpty(label) ? null : label,
            Color = AnnotationColors.ForMeaning(spec["meaning"]?.GetValue<string>()), Meaning = spec["meaning"]?.GetValue<string>() ?? "focus",
            TargetDescription = target.Item?.Text, TargetRegion = b, Confidence = target.Confidence,
            Source = "tutor", Lifetime = TimeSpan.FromMinutes(10), StrokeWidth = 3,
        };
        var marks = (ConcurrentDictionary<string, Guid>)turn.State.GetOrAdd(MarksKey, _ => new ConcurrentDictionary<string, Guid>());
        var id = MarkIds(turn).Next('m');
        marks[id] = mark.Id;
        return (mark, target, kind, id);
    }

    private static PxRect MonitorOf(ToolTurn turn, ScreenIndex screen) =>
        turn.State.TryGetValue(AskKey, out var a) && a is ScreenAsk ask ? ask.Monitor : screen.Geometry.SourcePhysical;

    // The answer can take seconds; if the learner scrolled meanwhile, a mark here would point at the wrong thing.
    private static async Task RefuseIfChangedAsync(ToolTurn turn, IEnumerable<PxRect> areas, CancellationToken ct)
    {
        if (!turn.State.TryGetValue(FrameKey, out var f) || f is not CapturedFrame frame) return;
        var list = areas.ToList();
        if (await Task.Run(() => list.Any(r => ScreenCaptureService.HasChanged(frame, r)), ct).ConfigureAwait(false))
            throw new ToolException("That part of the screen has changed since the capture (scrolled, zoomed or moved on), so the mark would point at the wrong place. Call look to see it as it is now, then mark again.");
    }

    private static string Describe(ScreenIndex screen, ResolvedTarget target) =>
        target.Item is { } item ? $"\"{item.Text}\" ({item.Id}, {target.Method})" : $"{ScreenIndex.Box(screen.Image(target.Bounds))} ({target.Method}; drawn dashed as an estimate)";

    private sealed class OverlayMark : ITool
    {
        public ToolDefinition Definition { get; } = new("overlay_mark",
            "Draws one mark on the learner's screen at once. " + KindsHelp + " For two or more marks use overlay_marks (one call, much faster). Returns a mark id.",
            Schema(MarkSpec()["properties"]!.DeepClone().AsObject(), "kind", "target"),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var monitor = MonitorOf(turn, screen);
            var (mark, target, kind, id) = PlanMark(args, turn, screen, monitor);
            await RefuseIfChangedAsync(turn, [target.Bounds], ct).ConfigureAwait(false);
            await OnUi(() =>
            {
                AppServices.Annotations.Add(mark);
                AppServices.Overlays.WatchTutorMarks(monitor);
                return true;
            }).ConfigureAwait(false);
            return ToolResult.Ok($"Drew {kind} {id} on {Describe(screen, target)}. It stays until that part of the screen changes or the learner clears it.");
        }

        // From the nearest free corner outside the target to just short of its edge.
        internal static PxPoint[] ArrowTo(PxRect t, PxRect monitor)
        {
            const double reach = 90, gap = 4;
            foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                var start = new PxPoint(dx < 0 ? t.X - reach : t.Right + reach, dy < 0 ? t.Y - reach * 0.6 : t.Bottom + reach * 0.6);
                if (!monitor.Contains(start)) continue;
                var end = new PxPoint(Math.Clamp(start.X, t.X - gap, t.Right + gap), Math.Clamp(start.Y, t.Y - gap, t.Bottom + gap));
                return [start, end];
            }
            return [new PxPoint(t.X, t.Y - 40), new PxPoint(t.X, t.Y - gap)];
        }
    }

    private sealed class OverlayMarks : ITool
    {
        public ToolDefinition Definition { get; } = new("overlay_marks",
            "Draws several marks on the learner's screen in ONE call: use this whenever you will mark two or more things (the fastest way to point). " + KindsHelp + " Marks that cannot be placed are reported by number and the rest are still drawn. Returns a mark id for each.",
            Schema(new JsonObject { ["marks"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 12, ["items"] = MarkSpec() } }, "marks"),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var monitor = MonitorOf(turn, screen);
            var report = new StringBuilder();
            var drawn = new List<Annotation>();
            var areas = new List<PxRect>();
            var number = 0;
            foreach (var node in args["marks"]!.AsArray())
            {
                number++;
                try
                {
                    var (mark, target, kind, id) = PlanMark((JsonObject)node!, turn, screen, monitor);
                    drawn.Add(mark);
                    areas.Add(target.Bounds);
                    report.AppendLine($"{number}. {kind} {id} on {Describe(screen, target)}");
                }
                catch (ToolException e) { report.AppendLine($"{number}. not drawn: {e.Message}"); }
            }
            if (drawn.Count == 0) return ToolResult.Fail("Nothing was drawn.\n" + report);
            await RefuseIfChangedAsync(turn, areas, ct).ConfigureAwait(false);
            await OnUi(() =>
            {
                AppServices.Annotations.Add([.. drawn]);
                AppServices.Overlays.WatchTutorMarks(monitor);
                return true;
            }).ConfigureAwait(false);
            return ToolResult.Ok($"Drew {drawn.Count} of {number} marks.\n{report}They stay until that part of the screen changes or the learner clears them.");
        }
    }

    private sealed class OverlayWrite : ITool
    {
        private static readonly string[] Kinds = ["step", "result", "note", "check", "error"];

        public ToolDefinition Definition { get; } = new("overlay_write",
            "Writes a note card on the learner's screen, in free space you do not have to find: Vernier looks at the screen, picks empty room beside what the note is about, and draws a dark card with your lines in handwriting and formulas typeset. " +
            "Use it to write a worked solution, an explanation or a checklist next to the problem, the way a teacher writes beside it. One line per step; put the equation in math (TeX), the words in text. " +
            "Mark things with overlay_marks; write reasoning here. Returns where the card went. Remove it with overlay_clear.",
            Schema(new JsonObject
            {
                ["title"] = Str("A short heading, e.g. \"Step 1: forces along the slope\".", 80),
                ["lines"] = new JsonObject
                {
                    ["type"] = "array", ["minItems"] = 1, ["maxItems"] = NoteContent.MaxLines,
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. Kinds.Select(k => (JsonNode)k)]), ["description"] = "step (default), result (boxed final answer), note (aside), check (a verified thing), error (a mistake)." },
                            ["text"] = Str("The words, in a few lines at most.", NoteContent.MaxTextChars),
                            ["math"] = Str("A formula in TeX, e.g. \\frac{1}{2}mv^{2} or a = \\frac{F-mg\\sin\\theta}{m}. Typeset beside the words.", NoteContent.MaxMathChars),
                        },
                        ["additionalProperties"] = false,
                    },
                },
                ["near"] = Target(),
                ["side"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("auto", "right", "below", "left", "above"), ["description"] = "Which side of near to put the card; default auto (the right, where there is room)." },
                ["width"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("narrow", "normal", "wide") },
                ["style"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("handwriting", "clean"), ["description"] = "handwriting (default) or clean print." },
            }, "lines"),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var monitor = MonitorOf(turn, screen);
            var lines = new List<NoteLine>();
            foreach (var node in args["lines"]!.AsArray())
            {
                var o = (JsonObject)node!;
                var kindName = o["kind"]?.GetValue<string>() ?? "step";
                var text = o["text"]?.GetValue<string>()?.Trim() ?? "";
                var math = o["math"]?.GetValue<string>()?.Trim();
                if (text.Length == 0 && string.IsNullOrEmpty(math)) throw new ToolException($"Line {lines.Count + 1} has neither text nor math. Give each line words, a formula, or both.");
                if (!Kinds.Contains(kindName)) throw new ToolException($"Unknown line kind \"{kindName}\". Use one of: {string.Join(", ", Kinds)}.");
                lines.Add(new NoteLine(Enum.Parse<NoteLineKind>(kindName, ignoreCase: true), text, string.IsNullOrEmpty(math) ? null : math));
            }
            var content = new NoteContent(args["title"]?.GetValue<string>()?.Trim(), lines, args["style"]?.GetValue<string>() != "clean");

            PxRect? near = null;
            ResolvedTarget? nearTarget = null;
            if (args["near"] is JsonObject nearSpec) { nearTarget = Resolve(screen, nearSpec); near = nearTarget.Bounds; }
            near ??= AppServices.Overlays.InkBounds; // what the learner marked is what the note is about
            var side = (args["side"]?.GetValue<string>() ?? "auto") switch { "right" => Side.Right, "below" => Side.Below, "left" => Side.Left, "above" => Side.Above, _ => Side.Auto };
            var widthDip = (args["width"]?.GetValue<string>() ?? "normal") switch { "narrow" => 340.0, "wide" => 560.0, _ => 440.0 };

            if (nearTarget is not null) await RefuseIfChangedAsync(turn, [nearTarget.Bounds], ct).ConfigureAwait(false);
            var scale = Win32.Monitors().FirstOrDefault(m => m.BoundsPhysical == monitor)?.DpiScale ?? 1.0;
            var space = await Task.Run(() => FreeSpaceOf(turn, screen, monitor), ct).ConfigureAwait(false);
            await OnUi(() => { foreach (var r in AppServices.Overlays.OwnWindowRects()) space.Occupy(r, 16); return true; }).ConfigureAwait(false);

            // Try the width asked for, then narrower and taller, until the card fits on free ground.
            (NoteRenderer.Card Card, FreeSpace.Placement Place)? chosen = null;
            foreach (var w in new[] { widthDip, widthDip * 0.82, widthDip * 0.68 })
            {
                var width = Math.Min(w, monitor.Width / scale * 0.45);
                var card = await OnUi(() => NoteRenderer.Render(content, width, scale)).ConfigureAwait(false);
                var placed = space.Place((card.SizeDip.Width * scale, card.SizeDip.Height * scale), near, side);
                if (chosen is null || placed.BusyFraction < chosen.Value.Place.BusyFraction) chosen = (card, placed);
                if (!placed.Covers) break;
            }
            var (final, place) = chosen!.Value;
            var mark = new Annotation
            {
                Kind = AnnotationKind.Note, Points = [new PxPoint(place.Rect.X, place.Rect.Y), new PxPoint(place.Rect.X + final.SizeDip.Width * scale, place.Rect.Y + final.SizeDip.Height * scale)],
                Note = content, Color = AnnotationColors.Focus, Meaning = "note", TargetDescription = nearTarget?.Item?.Text, TargetRegion = near, Confidence = 1,
                Source = "tutor", Lifetime = TimeSpan.FromMinutes(10), StrokeWidth = 0,
            };
            NoteRenderer.Remember(mark.Id, final, scale);
            var marks = (ConcurrentDictionary<string, Guid>)turn.State.GetOrAdd(MarksKey, _ => new ConcurrentDictionary<string, Guid>());
            var id = MarkIds(turn).Next('n');
            marks[id] = mark.Id;
            await OnUi(() =>
            {
                AppServices.Annotations.Add(mark);
                AppServices.Overlays.WatchTutorMarks(monitor);
                return true;
            }).ConfigureAwait(false);
            return ToolResult.Ok($"Wrote note {id} ({lines.Count} lines) {place.Where} at {ScreenIndex.Box(screen.Image(new PxRect(mark.Points[0].X, mark.Points[0].Y, mark.Points[1].X - mark.Points[0].X, mark.Points[1].Y - mark.Points[0].Y)))} in screenshot pixels." +
                (place.Covers ? " Nowhere was completely empty, so it covers a little of the screen: write less, or clear something first." : near is null ? "" : " It is on free space.") +
                " It stays until that part of the screen changes or the learner clears it.");
        }

        // What is busy on the screen: detail in the pixels, located text and controls, marks already drawn, the learner's ink.
        private static FreeSpace FreeSpaceOf(ToolTurn turn, ScreenIndex screen, PxRect monitor)
        {
            var frame = (turn.State.TryGetValue(FullFrameKey, out var full) ? full : turn.State.GetValueOrDefault(FrameKey)) as CapturedFrame;
            var space = frame?.Raw is { } raw && frame.Geometry.SourcePhysical is var area && area.Width >= monitor.Width * 0.9
                ? FreeSpace.Analyse(raw.Bgra, raw.Width, raw.Height, area)
                : new FreeSpace(monitor, 24);
            foreach (var item in screen.Items) space.Occupy(item.Bounds, 4);
            foreach (var m in AppServices.Annotations.Items.Where(a => a.Source == "tutor" && a.Points.Count > 0))
                space.Occupy(new PxRect(m.Points.Min(p => p.X), m.Points.Min(p => p.Y), Math.Max(1, m.Points.Max(p => p.X) - m.Points.Min(p => p.X)), Math.Max(1, m.Points.Max(p => p.Y) - m.Points.Min(p => p.Y))), 8);
            if (AppServices.Overlays.InkBounds is { } ink) space.Occupy(ink, 12);
            return space;
        }
    }

    private sealed class OverlayClear : ITool
    {
        public ToolDefinition Definition { get; } = new("overlay_clear",
            "Removes your marks from the screen: the given mark ids, or all of your marks when ids is omitted. The learner's own ink is never removed.",
            Schema(new JsonObject { ["ids"] = new JsonObject { ["type"] = "array", ["maxItems"] = 40, ["items"] = new JsonObject { ["type"] = "string", ["maxLength"] = 12 } } }),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var marks = (ConcurrentDictionary<string, Guid>)turn.State.GetOrAdd(MarksKey, _ => new ConcurrentDictionary<string, Guid>());
            var ids = (args["ids"] as JsonArray)?.Select(n => n?.GetValue<string>() ?? "").ToList();
            var guids = ids is null ? null : ids.Where(marks.ContainsKey).Select(i => marks[i]).ToHashSet();
            var removed = await OnUi(() =>
            {
                var before = AppServices.Annotations.Items.Count;
                AppServices.Annotations.RemoveWhere(a => a.Source == "tutor" && (guids is null || guids.Contains(a.Id)));
                return before - AppServices.Annotations.Items.Count;
            }).ConfigureAwait(false);
            var unknown = ids?.Where(i => !marks.ContainsKey(i)).ToList() ?? [];
            return ToolResult.Ok($"Removed {removed} mark(s)." + (unknown.Count > 0 ? $" Not your marks from this turn: {string.Join(", ", unknown)}." : ""));
        }
    }

    private sealed class Look : ITool
    {
        public ToolDefinition Definition { get; } = new("look",
            "Takes a fresh capture of the learner's screen and returns it with a new screen list. Give a target to look closer: that area is captured at full resolution (diagrams, small symbols, dense code). Use it when the screen may have changed or you need detail. Ids from earlier looks stop being valid.",
            Schema(new JsonObject { ["target"] = Target(), ["reason"] = Str("Why you are looking (shown to the learner).", 120) }),
            ToolRisk.Observe, UntrustedOutput: true);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var previous = Screen(turn);
            var ask = turn.State.TryGetValue(AskKey, out var a) && a is ScreenAsk s ? s : throw new ToolException("This turn did not start from the screen.");
            var region = args["target"] is JsonObject target ? Pad(Resolve(previous, target).Bounds, ask.Monitor) : ask.Monitor;
            var index = await AppServices.Session.ObserveForToolAsync(ask, region, Ids(turn), ct).ConfigureAwait(false);
            turn.State[ScreenKey] = index.Screen;
            turn.State[FrameKey] = index.Frame;
            if (region.Width >= ask.Monitor.Width * 0.95) turn.State[FullFrameKey] = index.Frame!;
            var frame = index.Frame!;
            return new ToolResult(
            [
                new ToolImage(frame.ImageBytes, frame.MediaType),
                new ToolText($"Observation {index.Screen.ObservationId}: {frame.Geometry.ImageWidth}x{frame.Geometry.ImageHeight} pixels covering {ScreenIndex.Box(region)} of the screen. Boxes below are in this image's pixels.\n" + index.Screen.Describe(150)),
            ], false);
        }

        // A small target is shown with room around it, at least 320 px wide, so it can be read in context.
        private static PxRect Pad(PxRect r, PxRect monitor)
        {
            var w = Math.Max(320, r.Width * 1.6);
            var h = Math.Max(200, r.Height * 1.6);
            var c = r.Center;
            return new PxRect(c.X - w / 2, c.Y - h / 2, w, h).Intersect(monitor);
        }
    }
}
