using System.Text.Json.Nodes;
using System.Windows;
using Vernier.App;
using Vernier.App.Agent;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Tools;
using Xunit;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>
/// The drawing tools end to end through the real executor and the real annotation store, on a synthetic worksheet (no capture, no
/// window is shown): a batch of marks in one call, and a note card that lands on empty space beside the problem.
/// </summary>
[Collection("desktop")]
public class ToolOverlayTests(ITestOutputHelper output)
{
    private static ExtractedText Line(string text, double x, double y) => new(text, new PxRect(x, y, text.Length * 8, 16), 0.9, "ocr");

    [Fact]
    public void Several_marks_go_in_one_call_a_bad_target_does_not_stop_the_rest_and_a_note_lands_on_free_space() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Dark.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Controls.xaml", UriKind.Relative) });
        AppServices.Initialize(dir.Path);
        try
        {
            var monitor = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary);
            var area = monitor.BoundsPhysical;
            // The screenshot is half the screen's size. The problem fills the left half; the right half is empty.
            var geo = new CaptureGeometry(area, (int)(area.Width / 2), (int)(area.Height / 2));
            var screen = new ScreenIndex("o1", geo,
                [Line("A stationary mass of 1 kg is pulled", 20, 30), Line("What is the speed of the mass at the top", 20, 300), Line("Find the acceleration", 20, 500)], DateTimeOffset.UtcNow);
            var (w, h) = (geo.ImageWidth, geo.ImageHeight);
            var bgra = new byte[w * h * 4];
            Array.Fill(bgra, (byte)255);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w / 2; x++)
                {
                    var v = (x / 2 + y / 2) % 2 == 0 ? (byte)15 : (byte)245; // fine detail, like text and diagrams
                    var i = (y * w + x) * 4;
                    bgra[i] = bgra[i + 1] = bgra[i + 2] = v;
                }
            var frame = new CapturedFrame([], "image/png", geo, 0, "Worksheet", "chrome", 0, DateTimeOffset.UtcNow) { Raw = new RawImage(bgra, w, h) };
            var noRaw = frame with { Raw = null }; // marks check the live screen for change; a synthetic frame has nothing to compare with

            var turn = new ToolTurn(ScreenTools.Names, CancellationToken.None);
            turn.State[ScreenTools.ScreenKey] = screen;
            turn.State[ScreenTools.FrameKey] = noRaw;
            turn.State[ScreenTools.FullFrameKey] = frame;
            turn.State[ScreenTools.AskKey] = new ScreenAsk(area, [], 0);
            JsonNode? Json(string text) => JsonNode.Parse(text);

            // 1. Three marks in one call: two resolve, one names text that is not on the screen.
            var batch = await AppServices.Tools.RunAsync("overlay_marks", Json("""
                {"marks":[{"kind":"circle","target":{"text":"stationary mass"}},{"kind":"underline","target":{"text":"speed of the mass"},"meaning":"focus"},{"kind":"circle","target":{"text":"Bernoulli"}}]}
                """), turn, CancellationToken.None);
            output.WriteLine(batch.Text);
            Assert.False(batch.IsError, batch.Text);
            Assert.Contains("Drew 2 of 3 marks", batch.Text);
            Assert.Contains("3. not drawn", batch.Text);
            Assert.Equal(2, AppServices.Annotations.Items.Count(a => a.Source == "tutor"));

            // 2. A note card: no coordinates given, the free right half is found from the pixels.
            var written = await AppServices.Tools.RunAsync("overlay_write", Json("""
                {"title":"Step 1: Newton's second law","lines":[
                  {"kind":"step","text":"Forces along the slope","math":"6 - mg\\sin\\theta = ma"},
                  {"kind":"step","text":"The slope rises 1 m over 3 m","math":"\\sin\\theta = \\frac{1}{3}"},
                  {"kind":"result","text":"Acceleration","math":"a = 2.7\\,\\mathrm{m/s^{2}}"}]}
                """), turn, CancellationToken.None);
            output.WriteLine(written.Text);
            Assert.False(written.IsError, written.Text);
            Assert.Contains("on free space", written.Text);
            Assert.DoesNotContain("covers a little", written.Text);
            var note = Assert.Single(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Note);
            Assert.Equal(3, note.Note!.Lines.Count);
            var rect = new PxRect(note.Points[0].X, note.Points[0].Y, note.Points[1].X - note.Points[0].X, note.Points[1].Y - note.Points[0].Y);
            Assert.True(rect.X >= area.X + area.Width / 2 - 30, $"the card at {rect} sits over the problem (left half ends at {area.X + area.Width / 2})");
            Assert.True(area.Contains(new PxPoint(rect.Right, rect.Bottom)), "and it is on the screen");

            // 2b. An area on a graph: the model reads the axes, Vernier shades the shapes and computes the areas.
            var graph = await AppServices.Tools.RunAsync("graph_area", Json("""
                {"x_ticks":[{"px":700,"value":0},{"px":1100,"value":10}],"y_ticks":[{"px":600,"value":0},{"px":200,"value":20}],"x_unit":"s","y_unit":"m/s","area_unit":"m",
                 "shapes":[{"name":"A1","vertices":[[0,0],[4,12],[4,0]]},{"name":"A2","vertices":[[4,0],[4,12],[9,12],[9,0]]}]}
                """), turn, CancellationToken.None);
            output.WriteLine(graph.Text);
            Assert.False(graph.IsError, graph.Text);
            Assert.Contains("A1: corners (0, 0) (4, 12) (4, 0) → area 24 m", graph.Text);
            Assert.Contains("Total ≈ 84 m", graph.Text);
            var shaded = AppServices.Annotations.Items.Where(a => a.Kind == AnnotationKind.Region).ToList();
            Assert.Equal(2, shaded.Count);
            Assert.Equal("A1 ≈ 24 m", shaded[0].Label);
            Assert.Equal(geo.ImageToPhysical(new PxPoint(860, 360)), shaded[0].Points[1]); // (4 s, 12 m/s) on the screen
            var refused = await AppServices.Tools.RunAsync("graph_area", Json("""
                {"x_ticks":[{"px":700,"value":0},{"px":705,"value":10}],"y_ticks":[{"px":600,"value":0},{"px":200,"value":20}],"shapes":[{"vertices":[[0,0],[1,1],[1,0]]}]}
                """), turn, CancellationToken.None);
            Assert.True(refused.IsError);
            Assert.Contains("apart", refused.Text);
            AppServices.Annotations.RemoveWhere(a => a.Kind == AnnotationKind.Region);

            // 2c. A diagram of a 3D problem: Vernier works the answer out, draws the common perpendicular and places the card on free space.
            var dia = await AppServices.Tools.RunAsync("overlay_diagram", Json("""
                {"kind":"space3d","title":"Shortest distance between skew lines","measure":"skew_lines",
                 "items":[{"type":"line","a":[0,0,0],"b":[1,0,0],"label":"L1","tone":"main"},{"type":"line","a":[0,0,1],"b":[0,1,0],"label":"L2","tone":"main"}]}
                """), turn, CancellationToken.None);
            output.WriteLine(dia.Text);
            Assert.False(dia.IsError, dia.Text);
            Assert.Contains("Skew lines: shortest distance 1", dia.Text);
            var diagram = Assert.Single(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Diagram);
            Assert.Contains(diagram.Diagram!.Items, i => i.Type == "segment" && i.Label == "d = 1");
            Assert.True(diagram.Points[0].X >= area.X + area.Width / 2 - 30, "the diagram sits on the free right half, not over the problem");
            var badDia = await AppServices.Tools.RunAsync("overlay_diagram", Json("""{"kind":"complex","items":[{"type":"plane","a":[0,0],"b":[0,0,1]}]}"""), turn, CancellationToken.None);
            Assert.True(badDia.IsError);
            Assert.Contains("cannot be drawn", badDia.Text);
            AppServices.Annotations.RemoveWhere(a => a.Kind == AnnotationKind.Diagram);

            // 3. Bad input is refused with something the model can act on, and drawing nothing.
            var empty = await AppServices.Tools.RunAsync("overlay_write", Json("""{"lines":[{"kind":"step"}]}"""), turn, CancellationToken.None);
            Assert.True(empty.IsError);
            Assert.Contains("neither text nor math", empty.Text);
            var unknown = await AppServices.Tools.RunAsync("overlay_marks", Json("""{"marks":[{"kind":"circle","target":{"text":"Bernoulli"}}]}"""), turn, CancellationToken.None);
            Assert.True(unknown.IsError);
            Assert.Contains("Nothing was drawn", unknown.Text);

            // 4. Clearing takes the tutor's marks and the card away.
            var cleared = await AppServices.Tools.RunAsync("overlay_clear", Json("{}"), turn, CancellationToken.None);
            Assert.Contains("Removed 3 mark(s)", cleared.Text);
            Assert.Empty(AppServices.Annotations.Items);
        }
        finally { AppServices.Shutdown(); }
    });

    [Fact]
    public void A_note_card_has_a_move_grip_and_a_dismiss_button_in_its_corner_dragging_moves_only_it_and_dismiss_removes_only_it() => WindowsRealApiTests.OnAppSta(() =>
    {
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        try
        {
            AppServices.Overlays.Start();
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var (x, y) = (area.X + 200, area.Y + 200);
            var card = new Annotation
            {
                Kind = AnnotationKind.Note, Points = [new(x, y), new(x + 400, y + 240)], Source = "tutor", Lifetime = TimeSpan.FromMinutes(1),
                Note = new NoteContent("Check", [new NoteLine(NoteLineKind.Step, "a line")], true),
            };
            var circle = new Annotation { Kind = AnnotationKind.Circle, Points = [new(x - 150, y), new(x - 50, y + 40)], Source = "tutor", Lifetime = TimeSpan.FromMinutes(1) };
            AppServices.Annotations.Add(circle);
            AppServices.Annotations.Add(card);

            var button = Assert.Single(Application.Current.Windows.OfType<NoteDismissWindow>());
            Assert.Equal(card.Id, button.NoteId);
            var at = Vernier.App.Platform.Win32.WindowBounds(new System.Windows.Interop.WindowInteropHelper(button).Handle)!.Value;
            Assert.True(at.Right <= x + 400 && at.Right > x + 400 - 40 && at.Y >= y && at.Y < y + 40, $"the button at {at} is not in the card's top-right corner");
            Assert.DoesNotContain(at, AppServices.Overlays.OwnWindowRects()); // part of the overlay, not a window a card must avoid

            var buttons = ((System.Windows.Controls.Panel)button.Content).Children.OfType<System.Windows.Controls.Button>().ToList();
            Assert.Equal(["Move note", "Dismiss note"], buttons.Select(System.Windows.Automation.AutomationProperties.GetName));

            // The learner drags the card by its grip: it moves by exactly that much, as the learner put it, and the buttons go with it.
            AppServices.Overlays.MoveCard(card.Id, 120, -40);
            DispatcherPump();
            var dragged = AppServices.Annotations.Items.Single(a => a.Id == card.Id);
            Assert.Equal(new PxPoint(x + 120, y - 40), dragged.Points[0]);
            var moved = Vernier.App.Platform.Win32.WindowBounds(new System.Windows.Interop.WindowInteropHelper(button).Handle)!.Value;
            Assert.Equal(at.X + 120, moved.X, 1);
            Assert.Equal(circle.Points[0], AppServices.Annotations.Items.Single(a => a.Id == circle.Id).Points[0]); // nothing else moved

            var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(buttons[1]);
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
            DispatcherPump();
            Assert.Equal(circle.Id, Assert.Single(AppServices.Annotations.Items).Id); // the other mark stays
            Assert.Empty(Application.Current.Windows.OfType<NoteDismissWindow>());
        }
        finally { AppServices.Shutdown(); }
        return Task.CompletedTask;
    });

    [Fact]
    public void The_tutors_own_shading_is_not_mistaken_for_the_screen_changing() => WindowsRealApiTests.OnAppSta(async () =>
    {
        // Regression: after graph_area shaded a graph, the next mark there was refused as "the screen has changed",
        // because the check captured Vernier's own glass too, and the tutor gave up saying the graph was wrong.
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        var page = WindowsRealApiTests.Show(new System.Windows.Controls.Border { Background = System.Windows.Media.Brushes.White }, 480, 360, "Vernier change-check test");
        try
        {
            AppServices.Overlays.Start();
            await Task.Delay(400);
            var b = Vernier.App.Platform.Win32.WindowBounds(new System.Windows.Interop.WindowInteropHelper(page).Handle)!.Value;
            var area = new PxRect(b.X + 60, b.Y + 80, 300, 200);
            var shot = Vernier.App.Platform.ScreenCaptureService.Raw(Vernier.App.Platform.ScreenCaptureService.Grab(area));
            var frame = new CapturedFrame([], "image/png", new CaptureGeometry(area, shot.Width, shot.Height), 0, "", "", 0, DateTimeOffset.UtcNow) { Raw = shot };
            // The tutor shades most of that area (as graph_area does).
            AppServices.Annotations.Add(new Annotation
            {
                Kind = AnnotationKind.Region, Source = "tutor", Color = AnnotationColors.Focus, StrokeWidth = 2.5, Lifetime = TimeSpan.FromMinutes(1),
                Points = [new(area.X + 10, area.Y + 10), new(area.Right - 10, area.Y + 10), new(area.Right - 10, area.Bottom - 10), new(area.X + 10, area.Bottom - 10)],
            });
            await Task.Delay(800);
            output.WriteLine($"old check (own glass included): {Vernier.App.Platform.ScreenCaptureService.HasChanged(frame, area)}");
            Assert.False(await ScreenTools.ChangedSinceCaptureAsync(frame, [area]), "Vernier's own shading counted as the page changing");
            page.Background = System.Windows.Media.Brushes.Black; // and a real change is still seen
            ((System.Windows.Controls.Border)page.Content).Background = System.Windows.Media.Brushes.Black;
            await Task.Delay(300);
            Assert.True(await ScreenTools.ChangedSinceCaptureAsync(frame, [area]), "a real change was missed");
        }
        finally { page.Close(); AppServices.Shutdown(); }
    });

    [Fact]
    public void Everything_the_tutor_drew_for_a_window_moves_with_it_cards_beside_it_included() => WindowsRealApiTests.OnAppSta(async () =>
    {
        // Regression: a note or diagram card sits on free space, often beside the window rather than inside it; when the
        // learner moved the window, marks inside it moved but the card stayed behind.
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        var page = WindowsRealApiTests.Show(new System.Windows.Controls.Border { Background = System.Windows.Media.Brushes.White }, 420, 300, "Vernier follow test");
        page.Left = 150; page.Top = 150;
        try
        {
            AppServices.Overlays.Start();
            await Task.Delay(300);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(page).Handle;
            var b = Vernier.App.Platform.Win32.WindowBounds(hwnd)!.Value;
            AppServices.Overlays.Track(hwnd);
            var inside = new Annotation { Kind = AnnotationKind.Circle, Source = "tutor", Lifetime = TimeSpan.FromMinutes(1), Points = [new(b.X + 40, b.Y + 60), new(b.X + 140, b.Y + 100)] };
            var card = new Annotation
            {
                Kind = AnnotationKind.Note, Source = "tutor", Lifetime = TimeSpan.FromMinutes(1), Note = new NoteContent("Step", [new NoteLine(NoteLineKind.Step, "beside the window")], false),
                Points = [new(b.Right + 30, b.Y), new(b.Right + 330, b.Y + 160)],
            };
            AppServices.Annotations.Add(inside, card);
            await Task.Delay(200);
            page.Left += 120; page.Top += 60;
            var moved = Vernier.App.Platform.Win32.WindowBounds(hwnd)!.Value;
            var (dx, dy) = (moved.X - b.X, moved.Y - b.Y);
            await Task.Delay(1200); // the overlay checks the window every 400 ms
            var after = AppServices.Annotations.Items.ToDictionary(a => a.Id);
            Assert.Equal(inside.Points[0].X + dx, after[inside.Id].Points[0].X, 1);
            Assert.Equal(card.Points[0].X + dx, after[card.Id].Points[0].X, 1);
            Assert.Equal(card.Points[0].Y + dy, after[card.Id].Points[0].Y, 1);
        }
        finally { page.Close(); AppServices.Shutdown(); }
    });

    private static void DispatcherPump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void The_guide_names_every_tool_the_tutor_is_offered_and_the_skill_is_the_same_text()
    {
        var guide = OperatorGuide.Text;
        string[] tools = [.. ScreenTools.Names, .. ContextTools.Names, .. ComputerTools.Names, .. NoteTools.Names, .. GoogleTools.ReadNames, .. GoogleTools.WriteNames, .. WebTools.Names];
        foreach (var name in tools) Assert.Contains(name, guide);
        Assert.Contains("never with code", guide);

        var skill = File.ReadAllText(Path.Combine(RepoRoot(), ".claude", "skills", "vernier-operator", "SKILL.md")).ReplaceLineEndings("\n");
        var parts = skill.Split("---\n", 3);
        Assert.StartsWith("name: vernier-operator", parts[1]);
        Assert.Equal(guide, parts[2].Trim()); // one source of truth: the skill cannot drift from what the app tells the tutor
        Assert.Contains(guide, new MasteryTutor().BuildSystem(new Vernier.Core.Settings.LearnerProfile(), LearningSummary.None, false, tools: true));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vernier.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Vernier.sln was not found above the test folder.");
    }
}

/// <summary>The learner's ink is a question in progress, not a mark: it leaves the screen by itself and is gone once it is sent.</summary>
[Collection("desktop")]
public class InkPolicyTests
{
    private static PxPoint[] Stroke(double x) => [new(x, 100), new(x + 40, 140), new(x + 80, 100), new(x + 120, 140)];

    [Fact]
    public void Ink_is_kept_for_the_question_shown_only_briefly_and_removed_when_it_is_sent_or_taken_back() => WindowsRealApiTests.OnAppSta(() =>
    {
        var store = new AnnotationStore();
        var overlays = new AnnotationOverlayManager(store); // not started: the managed fallback shows ink as marks that expire by themselves
        overlays.OnStroke(Stroke(100));
        overlays.OnStroke(Stroke(400));
        Assert.Equal(2, overlays.Ink.Count);
        Assert.True(overlays.HasInk);
        var shown = store.Items.Where(a => a.Source == "user").ToList();
        Assert.Equal(2, shown.Count);
        Assert.All(shown, a => Assert.True(a.Lifetime <= TimeSpan.FromSeconds(2), "ink must not stay on the screen")); // gone in about 1.75 s
        Assert.NotNull(overlays.InkBounds);

        // Undo takes back the last stroke only, and the visible copy with it.
        Assert.True(overlays.UndoInk());
        Assert.Single(overlays.Ink);
        Assert.Single(store.Items, a => a.Source == "user");

        // The tutor's marks are not the learner's: sending the question removes the ink and keeps the marks.
        store.Add(new Annotation { Kind = AnnotationKind.Circle, Points = [new(10, 10), new(60, 60)], Source = "tutor", Lifetime = TimeSpan.FromMinutes(10) });
        overlays.ConsumeInk();
        Assert.Empty(overlays.Ink);
        Assert.False(overlays.HasInk);
        Assert.Equal("tutor", Assert.Single(store.Items).Source);

        // Clear marks (ink part): nothing left to take back is not an error.
        Assert.False(overlays.UndoInk());
        overlays.ClearInk();
        store.PruneExpired(DateTimeOffset.UtcNow.AddMinutes(1)); // the strokes' own lifetime is over
        return Task.CompletedTask;
    });
}

/// <summary>LIVE: a real model (Claude Code, this machine's sign-in) drives the drawing tools from the operator guide alone. VERNIER_LIVE=1 only.</summary>
[Collection("desktop")]
public class LiveOperatorTests(ITestOutputHelper output)
{
    private static ExtractedText Line(string text, double x, double y) => new(text, new PxRect(x, y, text.Length * 8, 16), 0.9, "ocr");

    [Fact]
    public void A_real_model_marks_in_one_batch_and_writes_the_working_in_one_note_card() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Dark.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Controls.xaml", UriKind.Relative) });
        AppServices.Initialize(dir.Path);
        try
        {
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var geo = new CaptureGeometry(area, (int)(area.Width / 2), (int)(area.Height / 2));
            var screen = new ScreenIndex("o1", geo,
            [
                Line("A stationary mass of 1 kg is pulled along a frictionless 3 m inclined plane by a constant force of 6 N.", 20, 30),
                Line("At the top of the plane the mass has been displaced 1 m vertically.", 20, 52),
                Line("What is the speed of the mass at the top of the incline?", 20, 330),
                Line("A. 3 m/s    B. 4 m/s    C. 6 m/s    D. 18 m/s", 20, 380),
            ], DateTimeOffset.UtcNow);
            var turn = new ToolTurn(ScreenTools.Names, CancellationToken.None);
            turn.State[ScreenTools.ScreenKey] = screen;
            turn.State[ScreenTools.AskKey] = new ScreenAsk(area, [], 0);
            using var grant = AppServices.Mcp!.Open(turn);
            var defs = AppServices.Tools.Registry.Definitions.Where(d => ScreenTools.Names.Contains(d.Name)).ToList();
            var session = new Vernier.Core.Providers.ToolSession(defs, turn, AppServices.Tools, AppServices.Mcp);
            var provider = new Vernier.Core.Providers.ClaudeCodeProvider(new Vernier.Core.Settings.ProviderSettings { Id = "claude-code", DisplayName = "Claude Code", Kind = Vernier.Core.Settings.ProviderKinds.ClaudeCode }, dir.Path);
            var system = new MasteryTutor().BuildSystem(new Vernier.Core.Settings.LearnerProfile(), LearningSummary.None, false, tools: true);
            var question = "The learner pressed Ask on this worksheet and wants it solved with the working written next to the problem, and the quantities the question depends on circled. " +
                           "The screen list (ids and text of what Vernier located):\n" + screen.Describe() + "\nGo.";
            var request = new Vernier.Core.Providers.ChatRequest(system, [Vernier.Core.Providers.ChatMessage.User(question)], Tools: session);
            var result = await Vernier.Core.Providers.ChatRunner.CompleteAsync(provider, request, TimeSpan.FromSeconds(240), CancellationToken.None);

            foreach (var e in turn.Events) output.WriteLine($"{e.Tool} ({e.Took.TotalMilliseconds:0} ms) error={e.IsError}: {e.Outcome}");
            output.WriteLine("answer: " + result.Text.ReplaceLineEndings(" ")[..Math.Min(300, result.Text.Length)]);
            var drawing = turn.Events.Where(e => e.Tool.StartsWith("overlay_", StringComparison.Ordinal)).ToList();
            Assert.Contains(drawing, e => e.Tool == "overlay_write" && !e.IsError);
            Assert.True(drawing.Count(e => e.Tool == "overlay_write") <= 2, "one card per idea, not a stream of them");
            Assert.True(drawing.Count(e => e.Tool == "overlay_mark") <= 1, "several marks belong in one overlay_marks call");
            Assert.True(drawing.Count <= 5, $"{drawing.Count} drawing calls; the guide asks for about three");
            var note = Assert.Single(AppServices.Annotations.Items.Where(a => a.Kind == AnnotationKind.Note).Take(2).ToList().Count == 1 ? AppServices.Annotations.Items.Where(a => a.Kind == AnnotationKind.Note) : AppServices.Annotations.Items.Where(a => a.Kind == AnnotationKind.Note).Take(1));
            Assert.True(note.Note!.Lines.Count >= 3, "a worked solution has steps");
            Assert.Contains(note.Note.Lines, l => l.Math is { Length: > 0 });
        }
        finally { AppServices.Shutdown(); }
    });

    // One live turn with the real Claude Code CLI and Vernier's tools over MCP; returns the turn for its events and state.
    private async Task<(ToolTurn Turn, string Answer)> LiveTurnAsync(string dataDir, ScreenIndex screen, PxRect area, string[] tools, string question, CapturedFrame? frame = null)
    {
        var turn = new ToolTurn(tools, CancellationToken.None);
        turn.State[ScreenTools.ScreenKey] = screen;
        if (frame is not null) turn.State[ScreenTools.FullFrameKey] = frame;
        turn.State[ScreenTools.AskKey] = new ScreenAsk(area, [], 0);
        using var grant = AppServices.Mcp!.Open(turn);
        var defs = AppServices.Tools.Registry.Definitions.Where(d => tools.Contains(d.Name)).ToList();
        var session = new Vernier.Core.Providers.ToolSession(defs, turn, AppServices.Tools, AppServices.Mcp);
        var provider = new Vernier.Core.Providers.ClaudeCodeProvider(new Vernier.Core.Settings.ProviderSettings { Id = "claude-code", DisplayName = "Claude Code", Kind = Vernier.Core.Settings.ProviderKinds.ClaudeCode, Model = Environment.GetEnvironmentVariable("VERNIER_LIVE_MODEL") ?? "" }, dataDir); // e.g. haiku: the small model the learner uses
        var system = new MasteryTutor().BuildSystem(new Vernier.Core.Settings.LearnerProfile(), LearningSummary.None, false, tools: true);
        var request = new Vernier.Core.Providers.ChatRequest(system, [Vernier.Core.Providers.ChatMessage.User(question)], Tools: session);
        var result = await Vernier.Core.Providers.ChatRunner.CompleteAsync(provider, request, TimeSpan.FromSeconds(300), CancellationToken.None);
        foreach (var e in turn.Events) output.WriteLine($"{e.Tool} ({e.Took.TotalMilliseconds:0} ms) error={e.IsError}: {e.Outcome.ReplaceLineEndings(" ")[..Math.Min(400, e.Outcome.Length)]}");
        output.WriteLine("answer: " + result.Text.ReplaceLineEndings(" "));
        return (turn, result.Text);
    }

    [Fact]
    public void A_real_model_researches_unfamiliar_software_on_the_real_web_before_explaining() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        try
        {
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var screen = new ScreenIndex("o1", new CaptureGeometry(area, (int)(area.Width / 2), (int)(area.Height / 2)),
                [Line("Blender 4.2 — Properties › Modifiers › Bevel", 900, 40), Line("Amount 0.1 m   Segments 1   Limit Method: Angle 30°", 900, 70), Line("Width Type: Offset", 900, 100)], DateTimeOffset.UtcNow);
            var demand = Vernier.Core.Research.ResearchPolicy.Decide(AppProfiles.Match("blender", null), "blender", "What do Segments and Limit Method do here?");
            Assert.True(demand.Required);
            var (turn, answer) = await LiveTurnAsync(dir.Path, screen, area, [.. ScreenTools.Names, .. WebTools.Names],
                "The learner is working in Blender (3D).\n" +
                $"Research: REQUIRED for this answer, because {demand.Reason}. Call web_research first and read at least 3 working sources, then explain in your own words for this learner.\n" +
                "Screen list:\n" + screen.Describe() + "\nQuestion: What do Segments and Limit Method do on this Bevel modifier, and what should I change to get a rounder edge?");
            var ledger = turn.State.TryGetValue(Vernier.Core.Research.ResearchLedger.Key, out var l) ? (Vernier.Core.Research.ResearchLedger)l : null;
            output.WriteLine($"working sources: {ledger?.Working ?? 0}: {string.Join(" ", ledger?.Urls ?? [])}");
            Assert.True((ledger?.Working ?? 0) >= Vernier.Core.Research.ResearchPolicy.MinSources, "fewer than three working sources were read");
            Assert.Contains("Segments", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Sources", answer, StringComparison.OrdinalIgnoreCase);
        }
        finally { AppServices.Shutdown(); }
    });

    [Fact]
    public void A_real_model_shades_the_area_under_a_velocity_time_graph_and_uses_the_computed_number() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        try
        {
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            // A v–t graph described by its tick labels (screenshot pixels) and the plotted line, as OCR would locate them.
            var screen = new ScreenIndex("o1", new CaptureGeometry(area, (int)(area.Width / 2), (int)(area.Height / 2)),
            [
                Line("v (m/s)", 60, 80), Line("20", 70, 100), Line("10", 70, 300), Line("0", 80, 500),
                Line("0", 100, 515), Line("5", 350, 515), Line("10", 600, 515), Line("t (s)", 620, 515),
                // Short items: the screen list shortens long text, as it does for real OCR lines.
                Line("Line: (0 s, 0) to (4 s, 12 m/s)", 100, 550),
                Line("Flat at 12 m/s from 4 s to 9 s", 100, 570),
                Line("Falls to 0 m/s at 10 s", 100, 590),
                Line("Find the displacement in 10 s.", 100, 610),
            ], DateTimeOffset.UtcNow);
            var (turn, answer) = await LiveTurnAsync(dir.Path, screen, area, ScreenTools.Names,
                "Screen list (each item's box is in screenshot pixels; the graph's axes are labelled by the tick text):\n" + screen.Describe() + "\nThe learner asks for the full answer: find the displacement of the car in the first 10 s, and show on the graph how you got it.");
            var graph = turn.Events.Where(e => e.Tool == "graph_area" && !e.IsError).ToList();
            Assert.NotEmpty(graph);
            Assert.Contains(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Region);
            // ½·4·12 + 5·12 + ½·1·12 = 24 + 60 + 6 = 90 m, computed by Vernier, and the answer gives that number.
            Assert.Contains(graph, e => e.Outcome.Contains("Total ≈ 90"));
            Assert.Contains("90", answer);
        }
        finally { AppServices.Shutdown(); }
    });

    [Fact]
    public void A_real_model_is_made_to_draw_a_3D_diagram_and_uses_the_distance_vernier_computed() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        try
        {
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var screen = new ScreenIndex("o1", new CaptureGeometry(area, (int)(area.Width / 2), (int)(area.Height / 2)),
                [Line("1 Find the shortest distance between the skew lines", 20, 30), Line("x = 1 - t, y = 1 + t, z = 3 - t", 40, 60), Line("x = 2 + s, y = 1 - 2s, z = s", 40, 90)], DateTimeOffset.UtcNow);
            var question = "Find the shortest distance between the skew lines on the worksheet.";
            var demand = DimensionPolicy.Decide(question, screen.Describe());
            Assert.True(demand.Required);
            var (turn, answer) = await LiveTurnAsync(dir.Path, screen, area, ScreenTools.Names,
                $"Diagram: REQUIRED for this answer, because {demand.Reason}. Call overlay_diagram first (space3d, complex or hypercube; set measure so Vernier works the numbers out), then explain around what the learner sees.\n" +
                "Screen list:\n" + screen.Describe() + "\nQuestion: " + question);
            var drawn = turn.Events.Where(e => e.Tool == "overlay_diagram" && !e.IsError).ToList();
            Assert.NotEmpty(drawn);
            Assert.Contains(drawn, e => e.Outcome.Contains("shortest distance 2.8284"));
            Assert.Contains(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Diagram);
            Assert.Matches(@"2\.83|2\.828|2\sqrt\{?2|2\s?√\s?2", answer);
        }
        finally { AppServices.Shutdown(); }
    });

    // What the production prompt carries for this question: the REQUIRED line and the code-made plan.
    private static string Production(string question, string? screenText)
    {
        var demand = DimensionPolicy.Decide(question, screenText);
        var plan = TurnPlan.Build(demand, Vernier.Core.Research.ResearchDemand.None, null, question, false, true);
        return $"Drawing: REQUIRED for this answer, because {demand.Reason}. Call {demand.Tool}{(demand.Tool == "overlay_diagram" ? $" (kind {demand.Kind})" : "")} before you explain; an answer without it is sent back.\n{plan}\n";
    }

    [Fact]
    public void A_model_draws_the_forces_on_a_ramp_and_uses_the_net_force_vernier_computed() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        try
        {
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var screen = new ScreenIndex("o1", new CaptureGeometry(area, (int)(area.Width / 2), (int)(area.Height / 2)),
                [Line("A 1.5 kg box is pulled up a ramp inclined at 30 degrees by a string with tension 26 N parallel to the ramp.", 20, 30),
                 Line("Friction of 2.1 N acts down the ramp. Take g = 9.8 m/s2.", 20, 52), Line("Find the net force on the box.", 20, 74)], DateTimeOffset.UtcNow);
            var question = "Find the net force on the box.";
            var (turn, answer) = await LiveTurnAsync(dir.Path, screen, area, ScreenTools.Names, Production(question, screen.Describe()) + "Screen list:\n" + screen.Describe() + "\nQuestion: " + question);
            var drawn = turn.Events.Where(e => e.Tool == "overlay_diagram" && !e.IsError).ToList();
            Assert.NotEmpty(drawn);
            Assert.Contains(AppServices.Annotations.Items, a => a.Kind == AnnotationKind.Diagram && a.Diagram!.Kind == Vernier.Core.Diagrams.DiagramKind.Forces);
            // T − mg sin30 − F = 26 − 7.35 − 2.1 = 16.55 N up the slope.
            Assert.Contains(drawn, e => e.Outcome.Contains("16.55"));
            Assert.Matches(@"16\.5|16\.6", answer);
        }
        finally { AppServices.Shutdown(); }
    });

    [Fact]
    public void A_model_shades_the_real_curve_with_only_the_x_range_and_vernier_reads_the_axes() => WindowsRealApiTests.OnAppSta(async () =>
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        try
        {
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var geo = new CaptureGeometry(area, (int)(area.Width / 2), (int)(area.Height / 2));
            int w = geo.ImageWidth, h = geo.ImageHeight;
            // A real picture of a curved v–t graph: v = 12(1 − e^(−t/3)), t 0..10 s over x 200..1000 px, v 0..20 m/s over y 600..200 px.
            var bgra = new byte[w * h * 4];
            Array.Fill(bgra, (byte)255);
            void Dot(int x, int y) { if ((uint)x < w && (uint)y < h) { var k = (y * w + x) * 4; bgra[k] = bgra[k + 1] = bgra[k + 2] = 20; } }
            for (var x = 200; x <= 1000; x++) { Dot(x, 600); Dot(x, 601); }
            for (var y = 200; y <= 600; y++) { Dot(200, y); Dot(201, y); }
            for (var x = 200; x <= 1000; x++)
            {
                var t = (x - 200) / 80.0;
                var y = (int)Math.Round(600 - 12 * (1 - Math.Exp(-t / 3)) * 20);
                for (var d = -1; d <= 1; d++) Dot(x, y + d);
            }
            var frame = new CapturedFrame([], "image/png", geo, 0, "Graph", "acrord32", 0, DateTimeOffset.UtcNow) { Raw = new RawImage(bgra, w, h) };
            List<ExtractedText> items = [Line("Velocity-time graph of a car. Find the displacement in the first 10 s.", 200, 140), Line("v / m s-1", 120, 180), Line("t / s", 1010, 610)];
            for (var i = 0; i <= 10; i += 2) items.Add(new ExtractedText(i.ToString(), new PxRect(200 + i * 80 - 6, 612, 12, 14), 0.9, "ocr"));
            for (var i = 0; i <= 20; i += 5) items.Add(new ExtractedText(i.ToString(), new PxRect(170, 600 - i * 20 - 7, 14, 14), 0.9, "ocr"));
            var screen = new ScreenIndex("o1", geo, items, DateTimeOffset.UtcNow);
            var question = "Find the displacement of the car in the first 10 s from the velocity-time graph.";
            var (turn, answer) = await LiveTurnAsync(dir.Path, screen, area, ScreenTools.Names, Production(question, screen.Describe()) + "Screen list:\n" + screen.Describe() + "\nQuestion: " + question, frame);
            var shaded = turn.Events.Where(e => e.Tool == "graph_area" && !e.IsError).ToList();
            Assert.NotEmpty(shaded);
            // ∫0..10 12(1 − e^(−t/3)) dt = 85.3 m; the traced region must be within a few percent of it.
            var m = System.Text.RegularExpressions.Regex.Match(shaded[^1].Outcome, @"area (\d+(?:\.\d+)?)");
            Assert.True(m.Success, shaded[^1].Outcome);
            Assert.InRange(double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 82, 88.5);
            Assert.Matches(@"8[4-7]", answer);
        }
        finally { AppServices.Shutdown(); }
    });
}
