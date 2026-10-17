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
    public const string ScreenKey = "screen", AskKey = "ask", MarksKey = "marks", IdsKey = "ids";

    /// <summary>The turn's id sequence: screen items, phrases and marks all draw from it, so ids never repeat within a turn.</summary>
    public static IdSequence Ids(ToolTurn turn) => (IdSequence)turn.State.GetOrAdd(IdsKey, _ => new IdSequence());
    private static IdSequence MarkIds(ToolTurn turn) => (IdSequence)turn.State.GetOrAdd("markIds", _ => new IdSequence());

    public static IEnumerable<ITool> All() => [new FindOnScreen(), new OverlayMark(), new OverlayClear(), new Look()];
    public static readonly string[] Names = ["find_on_screen", "overlay_mark", "overlay_clear", "look"];

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

    private sealed class OverlayMark : ITool
    {
        public ToolDefinition Definition { get; } = new("overlay_mark",
            "Draws a mark on the learner's screen at once, while you explain. circle = one compact thing; highlight = a line of text or an area; underline = text; arrow = points at it from nearby empty space; point = a ring with a dot for a control to press; bracket = a block of lines; label = a short note beside it. Mark at the moment you refer to the thing. Returns a mark id.",
            Schema(new JsonObject
            {
                ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("circle", "highlight", "underline", "arrow", "point", "bracket", "label") },
                ["target"] = Target(),
                ["label"] = Str("A few words shown beside the mark (required for kind label).", 80),
                ["meaning"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("focus", "error", "correct", "note") },
            }, "kind", "target"),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var kind = args["kind"]!.GetValue<string>();
            var label = args["label"]?.GetValue<string>()?.Trim();
            if (kind == "label" && string.IsNullOrEmpty(label)) throw new ToolException("A label mark needs label text.");
            var target = Resolve(screen, args["target"] as JsonObject);
            // The whole monitor the question is about, even after a closer look at one part of it.
            var monitor = turn.State.TryGetValue(AskKey, out var a) && a is ScreenAsk ask ? ask.Monitor : screen.Geometry.SourcePhysical;
            var b = target.Bounds;
            // The answer can take seconds; if the learner scrolled meanwhile, a mark here would point at the wrong thing.
            if (turn.State.TryGetValue(FrameKey, out var f) && f is CapturedFrame frame && await Task.Run(() => ScreenCaptureService.HasChanged(frame, b), ct).ConfigureAwait(false))
                throw new ToolException("That part of the screen has changed since the capture (scrolled, zoomed or moved on), so the mark would point at the wrong place. Call look to see it as it is now, then mark again.");
            AnnotationKind annotationKind = kind switch
            {
                "circle" => AnnotationKind.Circle, "highlight" => AnnotationKind.Highlight, "underline" => AnnotationKind.Underline,
                "arrow" => AnnotationKind.Arrow, "point" => AnnotationKind.Point, "bracket" => AnnotationKind.Bracket, _ => AnnotationKind.Label,
            };
            IReadOnlyList<PxPoint> points = annotationKind switch
            {
                AnnotationKind.Arrow => ArrowTo(b, monitor),
                AnnotationKind.Label => [new PxPoint(b.X, b.Y)],
                _ => [new PxPoint(b.X, b.Y), new PxPoint(b.Right, b.Bottom)],
            };
            var mark = new Annotation
            {
                Kind = annotationKind, Points = points, Label = string.IsNullOrEmpty(label) ? null : label,
                Color = AnnotationColors.ForMeaning(args["meaning"]?.GetValue<string>()), Meaning = args["meaning"]?.GetValue<string>() ?? "focus",
                TargetDescription = target.Item?.Text, TargetRegion = b, Confidence = target.Confidence,
                Source = "tutor", Lifetime = TimeSpan.FromMinutes(10), StrokeWidth = 3,
            };
            var marks = (ConcurrentDictionary<string, Guid>)turn.State.GetOrAdd(MarksKey, _ => new ConcurrentDictionary<string, Guid>());
            var id = MarkIds(turn).Next('m');
            marks[id] = mark.Id;
            await OnUi(() =>
            {
                AppServices.Annotations.Add(mark);
                AppServices.Overlays.WatchTutorMarks(monitor);
                return true;
            }).ConfigureAwait(false);
            var what = target.Item is { } item ? $"\"{item.Text}\" ({item.Id}, {target.Method})" : $"{ScreenIndex.Box(screen.Image(b))} ({target.Method}; drawn dashed as an estimate)";
            return ToolResult.Ok($"Drew {kind} {id} on {what}. It stays until that part of the screen changes or the learner clears it.");
        }

        // From the nearest free corner outside the target to just short of its edge.
        private static PxPoint[] ArrowTo(PxRect t, PxRect monitor)
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
