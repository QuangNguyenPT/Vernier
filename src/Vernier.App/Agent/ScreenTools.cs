using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Diagrams;
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

    public static IEnumerable<ITool> All() => [new FindOnScreen(), new OverlayMark(), new OverlayMarks(), new OverlayWrite(), new GraphArea(), new GraphSketch(), new OverlayDiagram(), new OverlayClear(), new Look()];
    public static readonly string[] Names = ["find_on_screen", "overlay_mark", "overlay_marks", "overlay_write", "graph_area", "graph_sketch", "overlay_diagram", "overlay_clear", "look"];

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
        if (await ChangedSinceCaptureAsync(frame, list).ConfigureAwait(false))
            throw new ToolException("That part of the screen has changed since the capture (scrolled, zoomed or moved on), so the mark would point at the wrong place. Call look to see it as it is now, then mark again.");
    }

    /// <summary>Whether any of these areas looks different now from the turn's capture.</summary>
    internal static Task<bool> ChangedSinceCaptureAsync(CapturedFrame frame, IReadOnlyList<PxRect> areas) =>
        Task.Run(() => areas.Any(r => ScreenCaptureService.HasChanged(frame, r)));

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

    /// <summary>
    /// Shades the region of a graph whose area answers the question (distance under a v–t curve, work under F–x) and computes
    /// it in code. The model reads the axes; the shapes are then put onto the real curve by looking at the screenshot's pixels
    /// (corners snap to the drawn line), or the curve is traced from the pixels outright, so the shaded region is the real
    /// shape and not the model's estimate of it.
    /// </summary>
    private sealed class GraphArea : ITool
    {
        public const int MaxShapes = 3;
        private const double SnapIgnoreRawPx = 3;

        private static JsonObject Ticks(string axis) => new()
        {
            ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 2,
            ["description"] = $"Two labelled ticks on the {axis} axis, far apart: where each is in the screenshot (px: its {axis} pixel coordinate) and the value printed there.",
            ["items"] = new JsonObject
            {
                ["type"] = "object", ["required"] = new JsonArray("px", "value"), ["additionalProperties"] = false,
                ["properties"] = new JsonObject { ["px"] = new JsonObject { ["type"] = "number" }, ["value"] = new JsonObject { ["type"] = "number" } },
            },
        };

        private static JsonObject Num(string description) => new() { ["type"] = "number", ["description"] = description };

        public ToolDefinition Definition { get; } = new("graph_area",
            "Shades the area under (or between) a graph and computes it, for displacement from a velocity–time graph, work from force–distance, charge from current–time. " +
            "Simplest call: {\"trace\": {\"x_from\": 0, \"x_to\": 10}}: Vernier reads the axes from the tick labels on the screen, finds the drawn curve, follows it pixel by pixel and shades exactly what is under it. " +
            "If it cannot read the axes it says so: then also pass x_ticks and y_ticks (two labelled ticks per axis read from the screenshot). Instead of trace you may " +
            "give up to 3 shapes (triangles, rectangles, trapezoids: corners in data units) for straight-line graphs; their corners are moved onto the drawn line when it is within a few pixels. " +
            "Everything is labelled with its area and computed in code: use the numbers it returns, and say they are approximate when they come from a curve. Linear axes only.",
            Schema(new JsonObject
            {
                ["x_ticks"] = Ticks("x"),
                ["y_ticks"] = Ticks("y"),
                ["x_unit"] = Str("Unit on the x axis, e.g. s.", 16),
                ["y_unit"] = Str("Unit on the y axis, e.g. m/s.", 16),
                ["area_unit"] = Str("Unit of the area, e.g. m (s × m/s). Default: y_unit·x_unit.", 16),
                ["trace"] = new JsonObject
                {
                    ["type"] = "object", ["required"] = new JsonArray("x_from", "x_to"), ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["x_from"] = Num("Left end of the region, in x units."), ["x_to"] = Num("Right end, in x units."), ["y_at_start"] = Num("Optional: the curve's value at x_from (roughly). Leave it out and Vernier finds the curve itself."),
                        ["baseline"] = Num("The y value the area is measured from; default 0."), ["name"] = Str("A short name shown on the region, e.g. s.", 12),
                    },
                },
                ["shapes"] = new JsonObject
                {
                    ["type"] = "array", ["minItems"] = 1, ["maxItems"] = MaxShapes,
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object", ["required"] = new JsonArray("vertices"), ["additionalProperties"] = false,
                        ["properties"] = new JsonObject
                        {
                            ["name"] = Str("A short name shown on the shape, e.g. A1.", 12),
                            ["vertices"] = new JsonObject
                            {
                                ["type"] = "array", ["minItems"] = 3, ["maxItems"] = 12, ["description"] = "Corners in data units, in order round the shape, e.g. [[0,0],[4,12],[4,0]].",
                                ["items"] = new JsonObject { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 2, ["items"] = new JsonObject { ["type"] = "number" } },
                            },
                        },
                    },
                },
            }),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var monitor = MonitorOf(turn, screen);
            static AxisTick Tick(JsonNode? n) => new(n?["px"]?.GetValue<double>() ?? double.NaN, n?["value"]?.GetValue<double>() ?? double.NaN);
            // The axes: the ticks the model read, or (the usual case for a small model) found by Vernier from the tick labels on the screen.
            AxisCalibration axes;
            var axesFound = false;
            if (args["x_ticks"] is JsonArray { Count: 2 } xt && args["y_ticks"] is JsonArray { Count: 2 } yt)
            {
                axes = new AxisCalibration(Tick(xt[0]), Tick(xt[1]), Tick(yt[0]), Tick(yt[1]));
                if (axes.Problem() is { } problem) return ToolResult.Fail($"The axes cannot be read like that: {problem}. Look at the screenshot again and give two labelled ticks per axis, or leave x_ticks and y_ticks out and Vernier will read the tick labels itself.");
            }
            else if (AxisFinder.Find(screen.Items.Where(i => i.Kind is ScreenItemKind.Word or ScreenItemKind.Line).Select(i => (i.Text, screen.Image(i.Bounds)))) is { } found)
            {
                axes = found;
                axesFound = true;
            }
            else return ToolResult.Fail("Vernier could not find the graph's tick labels on the screen (they may be too small to read). Read two labelled ticks on each axis from the screenshot and pass them as x_ticks and y_ticks: [{\"px\": pixel, \"value\": number}, {…}]. If the graph is small, call look with the graph as target first.");
            if (args["trace"] is not JsonObject && args["shapes"] is not JsonArray) return ToolResult.Fail("Give a trace of the curve or at least one shape.");
            string xUnit = args["x_unit"]?.GetValue<string>()?.Trim() ?? "", yUnit = args["y_unit"]?.GetValue<string>()?.Trim() ?? "";
            var unit = args["area_unit"]?.GetValue<string>()?.Trim() is { Length: > 0 } u ? u : string.Join("·", new[] { yUnit, xUnit }.Where(s => s.Length > 0));
            string WithUnit(double v) => unit.Length > 0 ? $"{Number(v)} {unit}" : Number(v);

            // The screenshot's pixels, to put the reading onto the real drawn line.
            var frame = (turn.State.TryGetValue(FullFrameKey, out var full) ? full : turn.State.GetValueOrDefault(FrameKey)) as CapturedFrame;
            var ink = InkOf(turn, frame);
            var src = frame?.Geometry.SourcePhysical ?? default;
            PxPoint ToRaw(PxPoint img) { var p = screen.Geometry.ImageToPhysical(img); return new((p.X - src.X) * ink!.Width / src.Width, (p.Y - src.Y) * ink.Height / src.Height); }
            PxPoint RawToPhysical(PxPoint r) => new(src.X + r.X * src.Width / ink!.Width, src.Y + r.Y * src.Height / ink.Height);
            PxPoint RawToImage(PxPoint r) => screen.Geometry.PhysicalToImage(RawToPhysical(r));

            var image = screen.Geometry.ImageBounds;
            var marks = new List<Annotation>();
            var report = new StringBuilder();
            var total = 0.0;

            if (args["trace"] is JsonObject trace)
            {
                if (ink is null || src.Width <= 0) return ToolResult.Fail("Tracing needs the screenshot's pixels, which this turn does not have. Give shapes instead.");
                double xFrom = trace["x_from"]?.GetValue<double>() ?? double.NaN, xTo = trace["x_to"]?.GetValue<double>() ?? double.NaN, y0 = trace["y_at_start"]?.GetValue<double>() ?? double.NaN, baseline = trace["baseline"]?.GetValue<double>() ?? 0;
                if (!double.IsFinite(xFrom) || !double.IsFinite(xTo) || xFrom == xTo) return ToolResult.Fail("trace needs x_from and x_to as two different numbers, e.g. {\"trace\": {\"x_from\": 0, \"x_to\": 10}}.");
                if (xFrom > xTo) (xFrom, xTo) = (xTo, xFrom);
                var left = ToRaw(axes.ToImage(xFrom, 0));
                var right = ToRaw(axes.ToImage(xTo, 0));
                // Where the curve is: near y_at_start if given, else found in a column a little inside the range (not on the axis itself).
                var probeX = (int)Math.Round(left.X + (right.X - left.X) * 0.05);
                double? startY = double.IsFinite(y0) ? ToRaw(axes.ToImage(xFrom, y0)).Y : null;
                if (startY is null)
                {
                    var top = ToRaw(axes.ToImage(xFrom, Math.Max(axes.Y0.Value, axes.Y1.Value))).Y;
                    var bottom = ToRaw(axes.ToImage(xFrom, Math.Min(axes.Y0.Value, axes.Y1.Value))).Y;
                    var pad = Math.Abs(bottom - top) * 0.06;
                    startY = ink.FirstCurveY(probeX, Math.Min(top, bottom) - pad, Math.Max(top, bottom) + pad, ToRaw(axes.ToImage(xFrom, baseline)).Y);
                    if (startY is null) return ToolResult.Fail($"No drawn curve was found just right of x = {Number(xFrom)}. Give y_at_start (the curve's value there) or check x_from against the graph.");
                }
                else probeX = (int)Math.Round(left.X);
                var back = ink.Trace(probeX, (int)Math.Round(left.X), startY.Value);
                var ahead = ink.Trace(probeX, (int)Math.Round(right.X), startY.Value);
                var found = Enumerable.Reverse(back.Points).Skip(1).Concat(ahead.Points).ToList();
                var lostAt = ahead.LostAt;
                if (found.Count < 6) return ToolResult.Fail($"No drawn curve could be followed from x = {Number(xFrom)}. Check x_from and x_to against the graph, give y_at_start (the curve's value at x_from), or give shapes instead.");
                var simple = PolylineMath.Simplify(found, 1.5);
                var data = simple.Select(p => axes.ToData(RawToImage(p).X, RawToImage(p).Y)).ToList();
                // Net area (below the baseline counts negative, as displacement does) and the absolute area, by the trapezoid rule on the traced points.
                double net = 0, abs = 0;
                for (var i = 1; i < data.Count; i++)
                {
                    var (xa, ya) = (data[i - 1].X, data[i - 1].Y - baseline);
                    var (xb, yb) = (data[i].X, data[i].Y - baseline);
                    net += (xb - xa) * (ya + yb) / 2;
                    abs += ya * yb >= 0 ? Math.Abs((xb - xa) * (ya + yb) / 2) : Math.Abs(xb - xa) * (ya * ya + yb * yb) / (2 * (Math.Abs(ya) + Math.Abs(yb)));
                }
                var lo = Math.Min(data[0].X, data[^1].X); var hi = Math.Max(data[0].X, data[^1].X);
                if (data[^1].X < data[0].X) net = -net;
                total = net;
                var baseImg = axes.ToImage(0, baseline);
                var baseRaw = ToRaw(baseImg);
                var outline = new List<PxPoint>(simple.Select(RawToPhysical)) { RawToPhysical(new PxPoint(simple[^1].X, baseRaw.Y)), RawToPhysical(new PxPoint(simple[0].X, baseRaw.Y)) };
                var name = trace["name"]?.GetValue<string>()?.Trim() is { Length: > 0 } tn ? tn : "A";
                marks.Add(new Annotation
                {
                    Kind = AnnotationKind.Region, Points = outline, Color = AnnotationColors.Focus, StrokeWidth = 2.5, Label = $"{name} ≈ {WithUnit(net)}",
                    Meaning = "area under the graph", Source = "tutor", Confidence = 1, Lifetime = TimeSpan.FromMinutes(10),
                });
                report.AppendLine($"{name}: followed the drawn curve from x = {Number(Math.Round(lo, 3))} to {Number(Math.Round(hi, 3))} ({simple.Count} points) → area {WithUnit(Math.Round(net, 3))}" +
                                  (Math.Abs(abs - Math.Abs(net)) > 0.02 * Math.Max(1e-9, abs) ? $" (net; {WithUnit(Math.Round(abs, 3))} counting the part below the baseline as positive)" : ""));
                if (lostAt is { } lost) report.AppendLine($"The curve could not be followed past x ≈ {Number(Math.Round(axes.ToData(RawToImage(new PxPoint(lost, startY.Value)).X, 0).X, 2))}; the region ends there.");
            }

            var number = 0;
            foreach (var node in (args["shapes"] as JsonArray) ?? [])
            {
                number++;
                var name = node?["name"]?.GetValue<string>()?.Trim() is { Length: > 0 } n ? n : $"A{number}";
                var data = (node?["vertices"] as JsonArray)?.Select(v => (X: v?[0]?.GetValue<double>() ?? double.NaN, Y: v?[1]?.GetValue<double>() ?? double.NaN)).ToList() ?? [];
                if (data.Count < 3 || data.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return ToolResult.Fail($"Shape {name} needs at least three [x, y] corners in data units. Nothing was drawn.");

                // Corners off the axis belong on the drawn line: move each onto the nearest ink in its column.
                if (ink is not null && src.Width > 0)
                {
                    var radius = Math.Max(10, (int)(ink.Height * 0.04));
                    for (var i = 0; i < data.Count; i++)
                    {
                        if (Math.Abs(data[i].Y - 0) < 1e-9) continue;
                        var r = ToRaw(axes.ToImage(data[i].X, data[i].Y));
                        if (ink.NearestInkY((int)Math.Round(r.X), r.Y, radius) is not { } seen)
                        {
                            report.AppendLine($"{name}: corner ({Number(data[i].X)}, {Number(data[i].Y)}) is not on a drawn line within {radius} px; kept as given.");
                            continue;
                        }
                        if (Math.Abs(seen - r.Y) < SnapIgnoreRawPx) continue; // already on it
                        var moved = axes.ToData(RawToImage(new PxPoint(r.X, seen)).X, RawToImage(new PxPoint(r.X, seen)).Y);
                        report.AppendLine($"{name}: corner ({Number(data[i].X)}, {Number(data[i].Y)}) moved onto the drawn line at y ≈ {Number(Math.Round(moved.Y, 2))}.");
                        data[i] = (data[i].X, moved.Y);
                    }
                }
                if (AxisCalibration.SelfIntersects(data)) return ToolResult.Fail($"Shape {name} crosses itself; list its corners in order round the shape. Nothing was drawn.");
                var pixels = data.Select(p => axes.ToImage(p.X, p.Y)).ToList();
                if (pixels.Any(p => p.X < -2 || p.Y < -2 || p.X > image.Width + 2 || p.Y > image.Height + 2))
                    return ToolResult.Fail($"Shape {name} would fall outside the screenshot with these axes ({string.Join(" ", pixels.Select(p => $"({p.X:0},{p.Y:0})"))} px): check the tick pixels and the corners. Nothing was drawn.");
                var area = AxisCalibration.Area(data);
                if (area <= 0) return ToolResult.Fail($"Shape {name} has no area: its corners lie on one line. Nothing was drawn.");
                total += area;
                marks.Add(new Annotation
                {
                    Kind = AnnotationKind.Region, Points = [.. pixels.Select(screen.Geometry.ImageToPhysical)], Color = AnnotationColors.Focus, StrokeWidth = 2.5,
                    Label = $"{name} ≈ {WithUnit(area)}", Meaning = "area under the graph", Source = "tutor", Confidence = 1, Lifetime = TimeSpan.FromMinutes(10),
                });
                report.AppendLine($"{name}: corners {string.Join(" ", data.Select(p => $"({Number(p.X)}, {Number(p.Y)})"))} → area {WithUnit(area)}");
            }
            await OnUi(() =>
            {
                AppServices.Annotations.Add([.. marks]);
                AppServices.Overlays.WatchTutorMarks(monitor);
                return true;
            }).ConfigureAwait(false);
            var state = (ConcurrentDictionary<string, Guid>)turn.State.GetOrAdd(MarksKey, _ => new ConcurrentDictionary<string, Guid>());
            var ids = marks.Select(m => { var id = MarkIds(turn).Next('a'); state[id] = m.Id; return id; }).ToList();
            if (axesFound) report.Insert(0, "Axes read by Vernier from the tick labels on the screen.\n");
            return ToolResult.Ok(report +
                (marks.Count > 1 ? $"Total ≈ {WithUnit(total)}\n" : "") +
                $"Computed by Vernier from your reading of the axes (x: {Number(axes.X0.Value)} at {axes.X0.Px:0} px, {Number(axes.X1.Value)} at {axes.X1.Px:0} px; y: {Number(axes.Y0.Value)} at {axes.Y0.Px:0} px, {Number(axes.Y1.Value)} at {axes.Y1.Px:0} px). " +
                $"Drawn as {string.Join(", ", ids)}. Say the answer is approximate when the region follows a curve.");
        }

        internal static string Number(double v) => DiagramBuilder.Number(v);
    }

    /// <summary>
    /// Draws a graph the question asks for ("sketch how Ff varies with F") onto the learner's own axes: points in data units,
    /// placed by code. Axes come from the model's ticks, the tick labels on the screen, or (for an empty grid whose labels
    /// cannot be read) the drawn axis lines themselves, with one labelled tick per axis snapped onto its tick mark.
    /// </summary>
    private sealed class GraphSketch : ITool
    {
        public const int MaxPoints = 40;
        /// <summary>With no value on an axis, the sketch's largest value is drawn this far along it.</summary>
        public const double FitFraction = 0.85;

        private static JsonObject Ticks(string axis) => new()
        {
            ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 2,
            ["description"] = $"Optional: one or two labelled ticks on the {axis} axis: where each is in the screenshot (px: its {axis} pixel coordinate) and its value. " +
                              $"With one tick the axis starts at 0 where the axes cross; a tick labelled with a symbol (Fmax) takes the value you worked out for it.",
            ["items"] = new JsonObject
            {
                ["type"] = "object", ["required"] = new JsonArray("px", "value"), ["additionalProperties"] = false,
                ["properties"] = new JsonObject { ["px"] = new JsonObject { ["type"] = "number" }, ["value"] = new JsonObject { ["type"] = "number" } },
            },
        };

        public ToolDefinition Definition { get; } = new("graph_sketch",
            "Draws a graph the question asks the learner to sketch, onto the axes on the screen: a line or curve through points in data units. " +
            "Simplest call: {\"points\": [[0,0],[13.7,9.8]]}: Vernier finds the axes (tick labels, or the drawn axis lines of an empty grid) and places the points. " +
            "If an axis has a labelled tick but no numbers (e.g. Fmax), pass that tick: \"x_ticks\": [{\"px\": 800, \"value\": 13.7}] (px read from the screenshot; it is snapped onto the tick mark). " +
            "An axis with no values at all is scaled to fit. smooth: true for a curve, otherwise straight segments. Use it for the shape of a relationship, not to shade areas (graph_area does that).",
            Schema(new JsonObject
            {
                ["points"] = new JsonObject
                {
                    ["type"] = "array", ["minItems"] = 2, ["maxItems"] = MaxPoints, ["description"] = "Points in data units, left to right, e.g. [[0,0],[13.7,9.8]].",
                    ["items"] = new JsonObject { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 2, ["items"] = new JsonObject { ["type"] = "number" } },
                },
                ["x_ticks"] = Ticks("x"),
                ["y_ticks"] = Ticks("y"),
                ["smooth"] = new JsonObject { ["type"] = "boolean", ["description"] = "Optional: a smooth curve through the points instead of straight segments." },
                ["label"] = Str("Optional: a short label at the end of the line, e.g. Ff = (5/7)F.", 40),
            }, "points"),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var monitor = MonitorOf(turn, screen);
            static double Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.NaN;
            var points = (args["points"] as JsonArray)?.Select(v => (X: Num(v?[0]), Y: Num(v?[1]))).ToList() ?? [];
            if (points.Count < 2 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return ToolResult.Fail("points needs at least two [x, y] pairs of numbers in data units, e.g. {\"points\": [[0,0],[13.7,9.8]]}. Nothing was drawn.");
            static List<AxisTick> TicksOf(JsonNode? n) => (n as JsonArray)?.Select(t => new AxisTick(Num(t?["px"]), Num(t?["value"]))).Where(t => double.IsFinite(t.Px) && double.IsFinite(t.Value)).ToList() ?? [];
            var xt = TicksOf(args["x_ticks"]);
            var yt = TicksOf(args["y_ticks"]);

            var frame = (turn.State.TryGetValue(FullFrameKey, out var full) ? full : turn.State.GetValueOrDefault(FrameKey)) as CapturedFrame;
            var ink = InkOf(turn, frame);
            var src = frame?.Geometry.SourcePhysical ?? default;
            PxPoint ToRaw(PxPoint img) { var p = screen.Geometry.ImageToPhysical(img); return new((p.X - src.X) * ink!.Width / src.Width, (p.Y - src.Y) * ink.Height / src.Height); }
            PxPoint RawToImage(PxPoint r) => screen.Geometry.PhysicalToImage(new PxPoint(src.X + r.X * src.Width / ink!.Width, src.Y + r.Y * src.Height / ink.Height));

            AxisCalibration axes;
            var how = new StringBuilder();
            if (xt.Count == 2 && yt.Count == 2) { axes = new AxisCalibration(xt[0], xt[1], yt[0], yt[1]); how.Append("Axes from your ticks."); }
            else if (xt.Count < 2 && yt.Count < 2 && AxisFinder.Find(screen.Items.Where(i => i.Kind is ScreenItemKind.Word or ScreenItemKind.Line).Select(i => (i.Text, screen.Image(i.Bounds)))) is { } found)
            { axes = found; how.Append("Axes read by Vernier from the tick labels on the screen."); }
            else
            {
                if (ink is null || src.Width <= 0 || GraphFrame.Find(ink) is not { } lines)
                    return ToolResult.Fail("Vernier could not find the graph's axes: no tick labels it can read and no pair of drawn axis lines. Pass two labelled ticks per axis as x_ticks and y_ticks ([{\"px\": pixel, \"value\": number}, …]). Nothing was drawn.");
                var origin = RawToImage(lines.Origin);
                var xEnd = RawToImage(new PxPoint(lines.XEnd, lines.Origin.Y)).X;
                var yEnd = RawToImage(new PxPoint(lines.Origin.X, lines.YEnd)).Y;
                how.Append($"Axes found from the drawn axis lines (they cross at ({origin.X:0}, {origin.Y:0}) px).");
                AxisTick Snapped(AxisTick t, bool x)
                {
                    var r = ToRaw(x ? new PxPoint(t.Px, origin.Y) : new PxPoint(origin.X, t.Px));
                    if ((x ? lines.SnapXTick(ink, r.X) : lines.SnapYTick(ink, r.Y)) is not { } s) return t;
                    var img = RawToImage(x ? new PxPoint(s, lines.Origin.Y) : new PxPoint(lines.Origin.X, s));
                    var px = x ? img.X : img.Y;
                    if (Math.Abs(px - t.Px) >= 1) how.Append($" The {(x ? "x" : "y")} tick at {t.Px:0} px was moved onto the tick mark at {px:0} px.");
                    return t with { Px = px };
                }
                (AxisTick, AxisTick) Axis(List<AxisTick> given, bool x)
                {
                    var start = x ? origin.X : origin.Y;
                    if (given.Count == 2) return (given[0], given[1]);
                    if (given.Count == 1) return (new AxisTick(start, 0), Snapped(given[0], x));
                    // No value on this axis: the sketch's largest x goes to the last tick mark (the labelled end of the range on a
                    // sketch grid), else most of the way along the axis.
                    var largest = points.Max(p => Math.Abs(x ? p.X : p.Y));
                    if (x && lines.LastXTick(ink) is { } last)
                    {
                        var at = RawToImage(new PxPoint(last, lines.Origin.Y)).X;
                        how.Append($" The x axis has no values: the sketch ends at its last tick mark ({at:0} px); pass x_ticks if the range ends elsewhere.");
                        return (new AxisTick(start, 0), new AxisTick(at, largest > 0 ? largest : 1));
                    }
                    how.Append($" The {(x ? "x" : "y")} axis has no values, so the sketch is scaled to fit it.");
                    return (new AxisTick(start, 0), new AxisTick(start + ((x ? xEnd : yEnd) - start) * FitFraction, largest > 0 ? largest : 1));
                }
                var (x0, x1) = Axis(xt, true);
                var (y0, y1) = Axis(yt, false);
                axes = new AxisCalibration(x0, x1, y0, y1);
            }
            if (axes.Problem() is { } problem) return ToolResult.Fail($"The axes cannot be read like that: {problem}. Nothing was drawn.");

            var pixels = points.Select(p => axes.ToImage(p.X, p.Y)).ToList();
            var image = screen.Geometry.ImageBounds;
            if (pixels.Any(p => p.X < -2 || p.Y < -2 || p.X > image.Width + 2 || p.Y > image.Height + 2))
                return ToolResult.Fail($"The sketch would leave the screenshot with these axes ({string.Join(" ", pixels.Select(p => $"({p.X:0},{p.Y:0})"))} px): check the points and the ticks. Nothing was drawn.");
            if (args["smooth"]?.GetValueKind() == System.Text.Json.JsonValueKind.True && pixels.Count >= 3) pixels = CatmullRom(pixels);
            var label = args["label"]?.GetValue<string>()?.Trim();
            var mark = new Annotation
            {
                Kind = AnnotationKind.Path, Points = [.. pixels.Select(screen.Geometry.ImageToPhysical)], Color = AnnotationColors.Focus, StrokeWidth = 3,
                Label = label is { Length: > 0 } ? label : null, Meaning = "sketched graph", Source = "tutor", Confidence = 1, Lifetime = TimeSpan.FromMinutes(10),
            };
            await OnUi(() =>
            {
                AppServices.Annotations.Add(mark);
                AppServices.Overlays.WatchTutorMarks(monitor);
                return true;
            }).ConfigureAwait(false);
            var id = MarkIds(turn).Next('a');
            ((ConcurrentDictionary<string, Guid>)turn.State.GetOrAdd(MarksKey, _ => new ConcurrentDictionary<string, Guid>()))[id] = mark.Id;
            string N(double v) => GraphArea.Number(Math.Round(v, 3));
            return ToolResult.Ok($"{how}\nSketched {points.Count} points from ({N(points[0].X)}, {N(points[0].Y)}) to ({N(points[^1].X)}, {N(points[^1].Y)}): on the screen ({pixels[0].X:0}, {pixels[0].Y:0}) to ({pixels[^1].X:0}, {pixels[^1].Y:0}) px. Drawn as {id}.");
        }

        // A smooth curve through the points (Catmull–Rom): eight samples per segment.
        private static List<PxPoint> CatmullRom(List<PxPoint> p)
        {
            var o = new List<PxPoint> { p[0] };
            for (var i = 0; i < p.Count - 1; i++)
            {
                PxPoint a = p[Math.Max(0, i - 1)], b = p[i], c = p[i + 1], d = p[Math.Min(p.Count - 1, i + 2)];
                for (var k = 1; k <= 8; k++)
                {
                    double t = k / 8.0, t2 = t * t, t3 = t2 * t;
                    o.Add(new PxPoint(
                        0.5 * (2 * b.X + (-a.X + c.X) * t + (2 * a.X - 5 * b.X + 4 * c.X - d.X) * t2 + (-a.X + 3 * b.X - 3 * c.X + d.X) * t3),
                        0.5 * (2 * b.Y + (-a.Y + c.Y) * t + (2 * a.Y - 5 * b.Y + 4 * c.Y - d.Y) * t2 + (-a.Y + 3 * b.Y - 3 * c.Y + d.Y) * t3)));
                }
            }
            return o;
        }
    }

    // The screenshot's drawn lines, with the page brightness read inside the learner's window (not the whole monitor round it).
    private static InkMap? InkOf(ToolTurn turn, CapturedFrame? frame)
    {
        if (frame?.Raw is not { } raw) return null;
        var src = frame.Geometry.SourcePhysical;
        PxRect? sample = null;
        if (turn.State.TryGetValue(AskKey, out var a) && a is ScreenAsk { Foreground: not 0 } ask && Win32.WindowBounds(ask.Foreground) is { } w && src.Width > 0)
            sample = new PxRect((w.X - src.X) * raw.Width / src.Width, (w.Y - src.Y) * raw.Height / src.Height, w.Width * raw.Width / src.Width, w.Height * raw.Height / src.Height);
        return new InkMap(raw.Bgra, raw.Width, raw.Height, sample);
    }

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

    // Free space for a card: the width asked for, then narrower and taller, until it fits on empty ground. Shared by notes and diagrams.
    private static async Task<(NoteRenderer.Card Card, FreeSpace.Placement Place, double Scale)> PlaceCardAsync(
        ToolTurn turn, ScreenIndex screen, PxRect monitor, Func<double, double, NoteRenderer.Card> render, double widthDip, PxRect? near, Side side, CancellationToken ct)
    {
        var scale = Win32.Monitors().FirstOrDefault(m => m.BoundsPhysical == monitor)?.DpiScale ?? 1.0;
        var space = await Task.Run(() => FreeSpaceOf(turn, screen, monitor), ct).ConfigureAwait(false);
        await OnUi(() => { foreach (var r in AppServices.Overlays.OwnWindowRects()) space.Occupy(r, 16); return true; }).ConfigureAwait(false);
        (NoteRenderer.Card Card, FreeSpace.Placement Place)? chosen = null;
        foreach (var w in new[] { widthDip, widthDip * 0.82, widthDip * 0.68 })
        {
            var width = Math.Min(w, monitor.Width / scale * 0.45);
            var card = await OnUi(() => render(width, scale)).ConfigureAwait(false);
            var placed = space.Place((card.SizeDip.Width * scale, card.SizeDip.Height * scale), near, side);
            if (chosen is null || placed.BusyFraction < chosen.Value.Place.BusyFraction) chosen = (card, placed);
            if (!placed.Covers) break;
        }
        return (chosen!.Value.Card, chosen.Value.Place, scale);
    }

    /// <summary>
    /// A diagram of the problem on free space: 3D vectors, lines and planes, the complex plane, a cube or a tesseract. Vernier
    /// works out the numbers a 3D problem asks for (shortest distance between lines, point to plane, angle, cross product).
    /// </summary>
    private sealed class OverlayDiagram : ITool
    {
        private static readonly string[] Measures = ["none", "skew_lines", "point_plane", "angle", "cross_product"];

        public ToolDefinition Definition { get; } = new("overlay_diagram",
            "Draws a diagram of the problem on the learner's screen, on free space: seeing it is how problems about space, vectors, complex numbers and higher dimensions are understood. " +
            "kind space3d: points, vectors, lines, planes, segments in 3D (x right, y in, z up; 2D problems use z = 0). kind complex: numbers and circles on the Argand plane. kind hypercube: a cube or a tesseract (4D) for explaining dimensions above three by analogy. " +
            "Set measure and Vernier computes and draws the answer for you: skew_lines (the first two lines: shortest distance and the common perpendicular), point_plane (the first point and plane), angle (the first two vectors), cross_product (the first two vectors). Use the numbers it returns. " +
            "kind forces: a free-body diagram: each force {type: force, a: [size, direction in degrees from +x anticlockwise; 270 = down], label}; scene slope (a block; angle = the incline's slope in degrees, 0 = level), fluid (a ball in water: weight, drag, upthrust) or stacked (two blocks on level ground; on: 0 = bottom, 1 = top, so a friction pair is one force on each); bodies = names bottom first. Vernier adds the net force on each body (and its components along and across a slope). " +
            "Items: point {a}, vector {a: from, b: to}, line {a: point, b: direction, range: [t from, t to]}, plane {a: point, b: normal, range: [half size]}, segment {a, b}; complex: number {a: [re, im]}, circle {a: centre, b: [radius]}. label is a few characters (TeX is turned into symbols).",
            Schema(new JsonObject
            {
                ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("space3d", "complex", "hypercube", "forces") },
                ["title"] = Str("A short heading, e.g. \"Shortest distance between skew lines\".", 80),
                ["items"] = new JsonObject
                {
                    ["type"] = "array", ["maxItems"] = DiagramBuilder.MaxItems,
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object", ["required"] = new JsonArray("type", "a"), ["additionalProperties"] = false,
                        ["properties"] = new JsonObject
                        {
                            ["type"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("point", "vector", "line", "plane", "segment", "number", "circle", "force") },
                            ["a"] = new JsonObject { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 3, ["items"] = new JsonObject { ["type"] = "number" } },
                            ["b"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 3, ["items"] = new JsonObject { ["type"] = "number" } },
                            ["label"] = Str("A few characters, e.g. L₁, u×v, z.", 28),
                            ["tone"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("main", "result", "warn", "soft"), ["description"] = "main (blue, the subject), result (green), warn (red), soft (grey, helpers)." },
                            ["range"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 2, ["items"] = new JsonObject { ["type"] = "number" } },
                            ["on"] = new JsonObject { ["type"] = "integer", ["enum"] = new JsonArray(0, 1), ["description"] = "forces, scene stacked: the body the force acts on, 0 = bottom, 1 = top." },
                        },
                    },
                },
                ["measure"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. Measures.Select(m => (JsonNode)m)]) },
                ["note"] = Str("One short line under the diagram (words; TeX is turned into symbols).", 120),
                ["dim"] = new JsonObject { ["type"] = "integer", ["enum"] = new JsonArray(3, 4), ["description"] = "hypercube: 3 for a cube, 4 for a tesseract." },
                ["angle"] = new JsonObject { ["type"] = "number", ["description"] = "forces: the slope of the incline in degrees (0 = level). hypercube: rotation in the x–w plane; default 30." },
                ["scene"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("slope", "fluid", "stacked"), ["description"] = "forces: slope (default), fluid or stacked." },
                ["bodies"] = new JsonObject { ["type"] = "array", ["maxItems"] = 2, ["items"] = Str("A body's name, e.g. 5.0 kg.", 16), ["description"] = "forces: names of the bodies, bottom first." },
                ["near"] = Target(),
                ["side"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("auto", "right", "below", "left", "above") },
            }, "kind"),
            ToolRisk.Annotate);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var screen = Screen(turn);
            var monitor = MonitorOf(turn, screen);
            var kind = (args["kind"]?.GetValue<string>() ?? "space3d") switch { "complex" => DiagramKind.Complex, "hypercube" => DiagramKind.Hypercube, "forces" => DiagramKind.Forces, _ => DiagramKind.Space3D };
            var items = new List<DiagramItem>();
            var number = 0;
            foreach (var node in (args["items"] as JsonArray) ?? [])
            {
                number++;
                var o = (JsonObject)node!;
                double[]? Arr(string key) => (o[key] as JsonArray)?.Select(v => v?.GetValue<double>() ?? double.NaN).ToArray();
                var item = new DiagramItem(o["type"]?.GetValue<string>() ?? "", Arr("a") ?? [], Arr("b"), DiagramBuilder.Text(o["label"]?.GetValue<string>()), o["tone"]?.GetValue<string>(), Arr("range"), o["on"]?.GetValue<int>() ?? 0);
                if (kind != DiagramKind.Hypercube && DiagramBuilder.Problem(kind, item) is { } why) throw new ToolException($"Item {number} ({item.Type}): {why}.");
                items.Add(item);
            }
            var caption = new List<string>();
            var report = new StringBuilder();
            var measure = args["measure"]?.GetValue<string>() ?? "none";
            var scene = args["scene"]?.GetValue<string>() switch { "fluid" => ForceScene.Fluid, "stacked" => ForceScene.Stacked, _ => ForceScene.Slope };
            var bodies = (args["bodies"] as JsonArray)?.Select(b => DiagramBuilder.Text(b?.GetValue<string>()) ?? "").ToList() ?? [];
            if (kind == DiagramKind.Space3D && measure != "none")
            {
                DiagramItem[] Of(string type, int count) => items.Where(i => i.Type == type).Take(count).ToArray();
                static string P(V3 v) => $"({DiagramBuilder.Number(Math.Round(v.X, 3))}, {DiagramBuilder.Number(Math.Round(v.Y, 3))}, {DiagramBuilder.Number(Math.Round(v.Z, 3))})";
                switch (measure)
                {
                    case "skew_lines":
                    {
                        var l = Of("line", 2);
                        if (l.Length < 2) throw new ToolException("skew_lines needs two items of type line.");
                        var (d, a, b, skew) = Geometry3D.LineLine(V3.From(l[0].A), V3.From(l[0].B), V3.From(l[1].A), V3.From(l[1].B));
                        items.Add(new DiagramItem("segment", [a.X, a.Y, a.Z], [b.X, b.Y, b.Z], $"d = {DiagramBuilder.Number(Math.Round(d, 3))}", "result"));
                        caption.Add($"{(skew ? "Shortest distance" : "The lines are parallel; distance")} d = {DiagramBuilder.Number(Math.Round(d, 3))}");
                        report.AppendLine($"{(skew ? "Skew lines" : "Parallel lines")}: shortest distance {Math.Round(d, 4)}; the common perpendicular runs from {P(a)} on the first line to {P(b)} on the second.");
                        break;
                    }
                    case "point_plane":
                    {
                        var pt = Of("point", 1); var pl = Of("plane", 1);
                        if (pt.Length < 1 || pl.Length < 1) throw new ToolException("point_plane needs one point and one plane.");
                        var (d, foot) = Geometry3D.PointPlane(V3.From(pt[0].A), V3.From(pl[0].A), V3.From(pl[0].B));
                        items.Add(new DiagramItem("segment", pt[0].A, [foot.X, foot.Y, foot.Z], $"d = {DiagramBuilder.Number(Math.Round(d, 3))}", "result"));
                        caption.Add($"Distance from the point to the plane d = {DiagramBuilder.Number(Math.Round(d, 3))}");
                        report.AppendLine($"Point to plane: distance {Math.Round(d, 4)}; the perpendicular lands at {P(foot)}.");
                        break;
                    }
                    case "angle":
                    {
                        var v = Of("vector", 2);
                        if (v.Length < 2) throw new ToolException("angle needs two items of type vector.");
                        var angle = Geometry3D.AngleDegrees(V3.From(v[0].B) - V3.From(v[0].A), V3.From(v[1].B) - V3.From(v[1].A));
                        caption.Add($"Angle between the vectors θ = {DiagramBuilder.Number(Math.Round(angle, 2))}°");
                        report.AppendLine($"Angle between the vectors: {Math.Round(angle, 3)}°.");
                        break;
                    }
                    case "cross_product":
                    {
                        var v = Of("vector", 2);
                        if (v.Length < 2) throw new ToolException("cross_product needs two items of type vector.");
                        var c = V3.Cross(V3.From(v[0].B) - V3.From(v[0].A), V3.From(v[1].B) - V3.From(v[1].A));
                        items.Add(new DiagramItem("vector", [0, 0, 0], [c.X, c.Y, c.Z], "u×v", "result"));
                        caption.Add($"u × v = {P(c)}, perpendicular to both");
                        report.AppendLine($"Cross product {P(c)}, length {Math.Round(c.Length, 4)} (the area of the parallelogram the two vectors span).");
                        break;
                    }
                }
            }
            if (kind == DiagramKind.Forces)
            {
                if (items.Count == 0) throw new ToolException("A free-body diagram needs at least one force: {\"type\":\"force\",\"a\":[size, direction in degrees from +x, anticlockwise],\"label\":\"W = 14.7 N\"}.");
                var slope = scene == ForceScene.Slope ? args["angle"]?.GetValue<double>() ?? 0 : 0;
                static string N(double v) => DiagramBuilder.Number(Math.Round(v, 3));
                var bodyCount = scene == ForceScene.Stacked ? 2 : 1;
                if (scene != ForceScene.Stacked && items.Any(i => i.On != 0)) throw new ToolException("on is only for scene stacked (two blocks); leave it out for one body.");
                foreach (var body in Enumerable.Range(0, bodyCount))
                {
                    var on = items.Where(i => i.On == body).ToList();
                    var name = bodyCount == 1 ? "" : (bodies.Count > body && bodies[body].Length > 0 ? bodies[body] : body == 0 ? "bottom block" : "top block") + ": ";
                    if (on.Count == 0) { caption.Add($"{name}no forces given."); continue; }
                    var r = Geometry3D.Resultant(on.Select(i => (i.A[0], i.A[1])), slope);
                    var balanced = r.Size <= 1e-6 * on.Max(i => i.A[0]); // relative: forces of 10⁻⁵ N are as real as forces of 10 N
                    if (!balanced) items.Add(new DiagramItem("force", [r.Size, r.Direction], null, $"ΣF = {N(r.Size)}", "result", On: body));
                    var net = balanced ? "the forces balance: ΣF = 0 (equilibrium)." : $"net force ΣF = {N(r.Size)} at {N(r.Direction)}° (components {N(r.Fx)}, {N(r.Fy)})";
                    caption.Add(name.Length == 0 ? char.ToUpperInvariant(net[0]) + net[1..] : name + net);
                    if (Math.Abs(slope) > 0.5) caption.Add($"Along the slope (up): {N(r.Along)}; across it (out of the surface): {N(r.Across)}");
                    report.AppendLine($"{name}Net force {Math.Round(r.Size, 4)} at {Math.Round(r.Direction, 2)}° from +x (Fx {Math.Round(r.Fx, 4)}, Fy {Math.Round(r.Fy, 4)})" +
                                      (Math.Abs(slope) > 0.5 ? $"; along the slope (up) {Math.Round(r.Along, 4)}, across it {Math.Round(r.Across, 4)}." : "."));
                }
            }
            if (kind == DiagramKind.Hypercube)
                caption.Add(args["dim"]?.GetValue<int>() == 4
                    ? "A tesseract: 16 corners, 32 edges, 8 cubic cells. Green edges run along the 4th axis, joining the inner cube to the outer one, the way a cube drawn flat joins a small square to a large one."
                    : "A cube: 8 corners, 12 edges, 6 square faces.");
            if (DiagramBuilder.Text(args["note"]?.GetValue<string>()) is { Length: > 0 } note) caption.Add(note);

            var spec = new DiagramSpec(kind, DiagramBuilder.Text(args["title"]?.GetValue<string>()) is { } t ? t : null, items, [.. caption.Take(DiagramBuilder.MaxCaption)],
                args["dim"]?.GetValue<int>() == 4 ? 4 : 3, args["angle"]?.GetValue<double>() ?? (kind == DiagramKind.Forces ? 0 : 30), scene, bodies);
            PxRect? near = null;
            ResolvedTarget? nearTarget = null;
            if (args["near"] is JsonObject nearSpec) { nearTarget = Resolve(screen, nearSpec); near = nearTarget.Bounds; }
            near ??= AppServices.Overlays.InkBounds;
            var side = (args["side"]?.GetValue<string>() ?? "auto") switch { "right" => Side.Right, "below" => Side.Below, "left" => Side.Left, "above" => Side.Above, _ => Side.Auto };
            if (nearTarget is not null) await RefuseIfChangedAsync(turn, [nearTarget.Bounds], ct).ConfigureAwait(false);

            var (card, place, scale) = await PlaceCardAsync(turn, screen, monitor, (w, s) => DiagramRenderer.Render(spec, w, s), 440, near, side, ct).ConfigureAwait(false);
            var mark = new Annotation
            {
                Kind = AnnotationKind.Diagram, Points = [new PxPoint(place.Rect.X, place.Rect.Y), new PxPoint(place.Rect.X + card.SizeDip.Width * scale, place.Rect.Y + card.SizeDip.Height * scale)],
                Diagram = spec, Color = AnnotationColors.Focus, Meaning = "diagram", TargetDescription = nearTarget?.Item?.Text, TargetRegion = near, Confidence = 1,
                Source = "tutor", Lifetime = TimeSpan.FromMinutes(10), StrokeWidth = 0,
            };
            DiagramRenderer.Remember(mark.Id, card, scale);
            var marks = (ConcurrentDictionary<string, Guid>)turn.State.GetOrAdd(MarksKey, _ => new ConcurrentDictionary<string, Guid>());
            var id = MarkIds(turn).Next('d');
            marks[id] = mark.Id;
            await OnUi(() =>
            {
                AppServices.Annotations.Add(mark);
                AppServices.Overlays.WatchTutorMarks(monitor);
                return true;
            }).ConfigureAwait(false);
            return ToolResult.Ok($"Drew diagram {id} ({items.Count} items) {place.Where}. {report}" + (place.Covers ? "It covers a little of the screen; there was no room." : "") + " Remove it with overlay_clear. Explain what the learner can see in it.");
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
            var (final, place, scale) = await PlaceCardAsync(turn, screen, monitor, (w, sc) => NoteRenderer.Render(content, w, sc), widthDip, near, side, ct).ConfigureAwait(false);
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
            if (guids is not null) await OnUi(() => { AppServices.Overlays.Forget(guids); return true; }).ConfigureAwait(false);
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
