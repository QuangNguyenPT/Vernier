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
    public void A_note_card_has_its_own_dismiss_button_in_its_corner_and_it_removes_only_that_card() => WindowsRealApiTests.OnAppSta(() =>
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

            var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer((System.Windows.Controls.Button)button.Content);
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
            DispatcherPump();
            Assert.Equal(circle.Id, Assert.Single(AppServices.Annotations.Items).Id); // the other mark stays
            Assert.Empty(Application.Current.Windows.OfType<NoteDismissWindow>());
        }
        finally { AppServices.Shutdown(); }
        return Task.CompletedTask;
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
        string[] tools = [.. ScreenTools.Names, .. ContextTools.Names, .. ComputerTools.Names, .. NoteTools.Names, .. GoogleTools.ReadNames, .. GoogleTools.WriteNames];
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
}
