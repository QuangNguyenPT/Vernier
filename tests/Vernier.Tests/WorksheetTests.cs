using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vernier.App;
using Vernier.App.Agent;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Diagrams;
using Vernier.Core.Geometry;
using Vernier.Core.Tools;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>
/// Two real worksheet screenshots (Fixtures/Worksheets) read with the real Windows OCR and drawn on through the real tools:
/// the area under a v–t curve that stops at 0.8 s on an axis running to 1.0 s, and a line sketched on an empty Ff–F grid
/// whose only labels are 0 and Fmax.
/// </summary>
[Collection("desktop")]
public class WorksheetTests(ITestOutputHelper output)
{
    internal sealed record Sheet(string Name, ToolTurn Turn, ScreenIndex Screen, CaptureGeometry Geometry, RawImage Raw);

    internal static async Task<Sheet> LoadAsync(string name, string[] tools)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Worksheets", name);
        var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var bmp = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        int w = bmp.PixelWidth, h = bmp.PixelHeight;
        var bgra = new byte[w * h * 4];
        bmp.CopyPixels(bgra, w * 4, 0);
        var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
        var geo = new CaptureGeometry(new PxRect(monitor.X, monitor.Y, w, h), w, h);
        var raw = new RawImage(bgra, w, h);
        var frame = new CapturedFrame([], "image/png", geo, 0, "Worksheet", "chrome", 0, DateTimeOffset.UtcNow) { Raw = raw };
        var text = await new OcrTextExtractor().ExtractAsync(frame, CancellationToken.None);
        var screen = new ScreenIndex("o1", geo, text, DateTimeOffset.UtcNow);
        var turn = new ToolTurn(tools, CancellationToken.None);
        turn.State[ScreenTools.ScreenKey] = screen;
        turn.State[ScreenTools.FrameKey] = frame with { Raw = null };
        turn.State[ScreenTools.FullFrameKey] = frame;
        turn.State[ScreenTools.AskKey] = new ScreenAsk(geo.SourcePhysical, [], 0);
        return new Sheet(name, turn, screen, geo, raw);
    }

    /// <summary>With VERNIER_SHOT_DIR set, the worksheet with the tutor's marks drawn over it (outlines only), to look at.</summary>
    internal static void Shot(Sheet sheet, string name)
    {
        if (Environment.GetEnvironmentVariable("VERNIER_SHOT_DIR") is not { Length: > 0 } outDir) return;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(BitmapSource.Create(sheet.Raw.Width, sheet.Raw.Height, 96, 96, PixelFormats.Bgra32, null, sheet.Raw.Bgra, sheet.Raw.Width * 4), new Rect(0, 0, sheet.Raw.Width, sheet.Raw.Height));
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), 3);
            foreach (var a in AppServices.Annotations.Items.Where(a => a.Source == "tutor"))
            {
                var pts = a.Points.Select(sheet.Geometry.PhysicalToImage).Select(p => new Point(p.X, p.Y)).ToList();
                if (a.Kind is AnnotationKind.Note or AnnotationKind.Diagram) { dc.DrawRectangle(null, new Pen(Brushes.DarkOrange, 2), new Rect(pts[0], pts[1])); continue; }
                for (var i = 1; i < pts.Count; i++) dc.DrawLine(pen, pts[i - 1], pts[i]);
                if (a.Kind == AnnotationKind.Region && pts.Count > 2) dc.DrawLine(pen, pts[^1], pts[0]);
            }
        }
        var bmp = new RenderTargetBitmap(sheet.Raw.Width, sheet.Raw.Height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        Directory.CreateDirectory(outDir);
        using var f = File.Create(Path.Combine(outDir, name));
        enc.Save(f);
    }

    /// <summary>Starts the app's services on <paramref name="dir"/>; disposing removes every mark again, so the next test starts with a clear screen.</summary>
    internal static IDisposable Start(TempDir dir)
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Dark.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Controls.xaml", UriKind.Relative) });
        AppServices.Initialize(dir.Path);
        AppServices.Annotations.RemoveWhere(_ => true);
        return new Cleanup();
    }

    private sealed class Cleanup : IDisposable
    {
        public void Dispose() => AppServices.Annotations.RemoveWhere(_ => true);
    }

    [Fact]
    public void The_area_under_the_sphere_velocity_curve_to_0_8_s_is_shaded_on_the_real_curve() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        using var clean = Start(dir);
        var sheet = await LoadAsync("sphere-vt-graph.png", ScreenTools.Names);
        var result = await AppServices.Tools.RunAsync("graph_area", JsonNode.Parse("""{"trace":{"x_from":0,"x_to":0.8},"x_unit":"s","y_unit":"m/s","area_unit":"m"}"""), sheet.Turn, CancellationToken.None);
        output.WriteLine(result.Text);
        Assert.False(result.IsError, result.Text);
        var region = Assert.Single(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Region);
        var label = region.Label!;
        var area = double.Parse(System.Text.RegularExpressions.Regex.Match(label, @"≈\s*([\d.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(area, 0.255, 0.29); // 0.4 m/s terminal speed, reached by about 0.4 s
        // The region spans the 0 s tick (x ≈ 340 px) to the 0.8 s tick (x ≈ 806 px) in the screenshot, not on to 1.0 s.
        var xs = region.Points.Select(p => sheet.Geometry.PhysicalToImage(p).X).ToList();
        Assert.InRange(xs.Min(), 332, 352);
        Assert.InRange(xs.Max(), 794, 816);
        Shot(sheet, "worksheet-sphere-area.png");
    });

    [Fact]
    public void A_line_is_sketched_on_the_empty_friction_grid_from_the_origin_to_the_Fmax_tick() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        using var clean = Start(dir);
        var sheet = await LoadAsync("blocks-friction-grid.png", ScreenTools.Names);
        // Ff on the bottom block = (5/7)F up to Fmax = 13.7 N, where Ff = μ·m·g = 9.81 N. The model reads the Fmax tick 6 px off.
        var result = await AppServices.Tools.RunAsync("graph_sketch", JsonNode.Parse("""{"points":[[0,0],[13.7,9.81]],"x_ticks":[{"px":794,"value":13.7}],"label":"Ff = (5/7)F"}"""), sheet.Turn, CancellationToken.None);
        output.WriteLine(result.Text);
        Assert.False(result.IsError, result.Text);
        var line = Assert.Single(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Path && a.Source == "tutor");
        var p = line.Points.Select(sheet.Geometry.PhysicalToImage).ToList();
        // The axes cross at about (356, 918); the Fmax tick mark is at about x = 800.
        Assert.InRange(p[0].X, 352, 360);
        Assert.InRange(p[0].Y, 914, 922);
        Assert.InRange(p[^1].X, 796, 806);
        Assert.InRange(p[^1].Y, 600, 720); // no values on the Ff axis: scaled to fit, inside the grid
        Assert.Contains("moved onto the tick mark", result.Text);
        Shot(sheet, "worksheet-friction-sketch.png");
    });

    [Fact]
    public void The_setups_are_drawn_as_force_diagrams_with_the_net_force_on_each_body_worked_out_by_code() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        using var clean = Start(dir);
        var sphere = await LoadAsync("sphere-vt-graph.png", ScreenTools.Names);
        var fluid = await AppServices.Tools.RunAsync("overlay_diagram", JsonNode.Parse("""
            {"kind":"forces","scene":"fluid","title":"Sphere at terminal velocity","bodies":["sphere"],
             "items":[{"type":"force","a":[1.38e-5,270],"label":"W"},{"type":"force","a":[4.6e-6,90],"label":"D"},{"type":"force","a":[9.2e-6,90],"label":"U"}]}
            """), sphere.Turn, CancellationToken.None);
        output.WriteLine(fluid.Text);
        Assert.False(fluid.IsError, fluid.Text);
        Assert.Equal(ForceScene.Fluid, Assert.Single(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Diagram).Diagram!.Scene);
        Assert.Contains("Net force 0 ", fluid.Text); // W = D + U at terminal speed

        AppServices.Annotations.RemoveWhere(_ => true);
        var blocks = await LoadAsync("blocks-friction-grid.png", ScreenTools.Names);
        var stacked = await AppServices.Tools.RunAsync("overlay_diagram", JsonNode.Parse("""
            {"kind":"forces","scene":"stacked","title":"Blocks at F = Fmax","bodies":["5.0 kg","2.0 kg"],
             "items":[{"type":"force","a":[13.73,0],"label":"F","on":1},{"type":"force","a":[9.81,180],"label":"f","on":1},{"type":"force","a":[9.81,0],"label":"f","on":0}]}
            """), blocks.Turn, CancellationToken.None);
        output.WriteLine(stacked.Text);
        Assert.False(stacked.IsError, stacked.Text);
        Assert.Contains("2.0 kg: Net force 3.92", stacked.Text);   // a = 3.92 / 2 = 1.96 m/s² …
        Assert.Contains("5.0 kg: Net force 9.81", stacked.Text);   // … = 9.81 / 5: both blocks accelerate together
        var spec = Assert.Single(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Diagram).Diagram!;
        Assert.Equal(2, spec.Items.Count(i => i.Tone == "result"));
        var refused = await AppServices.Tools.RunAsync("overlay_diagram", JsonNode.Parse("""{"kind":"forces","items":[{"type":"force","a":[1,0],"on":1}]}"""), blocks.Turn, CancellationToken.None);
        Assert.True(refused.IsError);
        Assert.Contains("only for scene stacked", refused.Text);
    });

    [Fact]
    public void Without_a_tick_the_sketch_ends_at_the_last_tick_mark_of_the_empty_grid() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        using var clean = Start(dir);
        var sheet = await LoadAsync("blocks-friction-grid.png", ScreenTools.Names);
        var result = await AppServices.Tools.RunAsync("graph_sketch", JsonNode.Parse("""{"points":[[0,0],[13.7,9.81]]}"""), sheet.Turn, CancellationToken.None);
        output.WriteLine(result.Text);
        Assert.False(result.IsError, result.Text);
        var p = Assert.Single(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Path).Points.Select(sheet.Geometry.PhysicalToImage).ToList();
        Assert.InRange(p[^1].X, 796, 806); // the Fmax tick
    });

    // ---- LIVE: a real model (VERNIER_LIVE=1; VERNIER_LIVE_MODEL=haiku for the small model) with the real screenshot attached ----

    private async Task<(ToolTurn Turn, string Answer)> LiveAsync(Sheet sheet, string question)
    {
        var demand = DimensionPolicy.Decide(question, string.Join("\n", sheet.Screen.Items.Where(i => i.Kind == ScreenItemKind.Line).Select(i => i.Text)));
        var plan = TurnPlan.Build(demand, Vernier.Core.Research.ResearchDemand.None, null, question, false, true);
        var prompt = $"Drawing: REQUIRED for this answer, because {demand.Reason}. Call {demand.Tool} before you explain; an answer without it is sent back." +
                     (demand.Also is { } also ? $" Also draw {also.Tool} (kind {also.Kind}, scene {also.Scene}): {also.Reason}." : "") + $"\n{plan}\n" +
                     $"A screenshot is attached. It is {sheet.Geometry.ImageWidth}x{sheet.Geometry.ImageHeight} pixels.\nThings Vernier located on this screen:\n{sheet.Screen.Describe()}\nQuestion: {question}";
        using var grant = AppServices.Mcp!.Open(sheet.Turn);
        var defs = AppServices.Tools.Registry.Definitions.Where(d => ScreenTools.Names.Contains(d.Name)).ToList();
        var session = new Vernier.Core.Providers.ToolSession(defs, sheet.Turn, AppServices.Tools, AppServices.Mcp);
        var provider = new Vernier.Core.Providers.ClaudeCodeProvider(new Vernier.Core.Settings.ProviderSettings { Id = "claude-code", DisplayName = "Claude Code", Kind = Vernier.Core.Settings.ProviderKinds.ClaudeCode, Model = Environment.GetEnvironmentVariable("VERNIER_LIVE_MODEL") ?? "" }, AppContext.BaseDirectory);
        var system = new MasteryTutor().BuildSystem(new Vernier.Core.Settings.LearnerProfile(), LearningSummary.None, false, tools: true);
        var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Worksheets", sheet.Name));
        var request = new Vernier.Core.Providers.ChatRequest(system, [Vernier.Core.Providers.ChatMessage.User(prompt, new Vernier.Core.Providers.ImageInput(png, "image/png"))], Tools: session);
        var result = await Vernier.Core.Providers.ChatRunner.CompleteAsync(provider, request, TimeSpan.FromSeconds(300), CancellationToken.None);
        foreach (var e in sheet.Turn.Events) output.WriteLine($"{e.Tool} ({e.Took.TotalMilliseconds:0} ms) error={e.IsError}: {e.Outcome.ReplaceLineEndings(" ")[..Math.Min(500, e.Outcome.Length)]}");
        output.WriteLine("answer: " + result.Text.ReplaceLineEndings(" "));
        return (sheet.Turn, result.Text);
    }

    [Fact]
    public void LIVE_the_sphere_worksheet_is_shaded_to_0_8_s_drawn_as_forces_in_water_and_answered_with_the_computed_depth() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        using var clean = Start(dir);
        try
        {
            var sheet = await LoadAsync("sphere-vt-graph.png", ScreenTools.Names);
            var (turn, answer) = await LiveAsync(sheet, "Help me with (a) and (c): explain the shape of the graph and estimate the depth of the water.");
            var area = turn.Events.Where(e => e.Tool == "graph_area" && !e.IsError).ToList();
            Assert.NotEmpty(area);
            var region = AppServices.Annotations.Items.Last(a => a.Kind == AnnotationKind.Region);
            var xs = region.Points.Select(p => sheet.Geometry.PhysicalToImage(p).X).ToList();
            Assert.InRange(xs.Max(), 790, 820);                                   // stops at 0.8 s, not at the end of the axis
            Assert.Contains(turn.Events, e => e.Tool == "overlay_diagram" && !e.IsError);
            Assert.Matches(@"0\.2[5-9]|2[5-9]\s?cm", answer);                    // depth ≈ 0.27 m
            Shot(sheet, "live-sphere.png");
        }
        finally { AppServices.Shutdown(); }
    });

    [Fact]
    public void LIVE_the_friction_worksheet_gets_its_line_sketched_to_Fmax_the_blocks_drawn_and_Fmax_computed() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        using var clean = Start(dir);
        try
        {
            var sheet = await LoadAsync("blocks-friction-grid.png", ScreenTools.Names);
            var (turn, answer) = await LiveAsync(sheet, "Show me (b)(i) and (ii): work out Fmax and sketch the friction graph on the grid.");
            Assert.Contains(turn.Events, e => e.Tool == "graph_sketch" && !e.IsError);
            var line = AppServices.Annotations.Items.Last(a => a.Kind == AnnotationKind.Path && a.Source == "tutor").Points.Select(sheet.Geometry.PhysicalToImage).ToList();
            Assert.InRange(line[0].X, 350, 364);                                  // from the origin …
            Assert.InRange(line[0].Y, 910, 924);
            Assert.InRange(line[^1].X, 790, 810);                                 // … to the Fmax tick
            Assert.True(line[^1].Y < line[0].Y - 40, "the line rises");
            Assert.Contains(turn.Events, e => e.Tool == "overlay_diagram" && !e.IsError);
            Assert.Matches(@"13\.7|13\.73", answer);                               // Fmax = μmg(m+M)/M = 13.7 N
            Shot(sheet, "live-friction.png");
        }
        finally { AppServices.Shutdown(); }
    });

    [Fact]
    public void Every_tool_argument_described_as_optional_is_optional()
    {
        foreach (var tool in ScreenTools.All())
            Check(tool.Definition.Name, tool.Definition.InputSchema, "");

        static void Check(string tool, JsonObject schema, string path)
        {
            var required = (schema["required"] as JsonArray ?? []).Select(r => r!.GetValue<string>()).ToHashSet();
            foreach (var (name, node) in schema["properties"] as JsonObject ?? [])
            {
                if (node is not JsonObject prop) continue;
                if (required.Contains(name) && prop["description"]?.GetValue<string>() is { } d && d.StartsWith("Optional", StringComparison.OrdinalIgnoreCase))
                    Assert.Fail($"{tool}: {path}{name} is required but described as optional");
                Check(tool, prop, $"{path}{name}.");
                if (prop["items"] is JsonObject items) Check(tool, items, $"{path}{name}[].");
            }
        }
    }
}
