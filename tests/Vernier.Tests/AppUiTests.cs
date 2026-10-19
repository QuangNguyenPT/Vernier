using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Vernier.App;
using Vernier.App.Platform;
using Vernier.App.Views;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Learning;
using Vernier.Core.Settings;
using Vernier.Core.Windowing;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>
/// Drives the REAL application windows in-process: the same MainWindow, pages and services the
/// executable uses. The model is a loopback HTTP stub speaking the Chat Completions wire format,
/// so the UI, pipeline and the OpenAI-compatible adapter run for real while the answer is scripted.
/// </summary>
[Collection("desktop")]
public class AppUiTests(ITestOutputHelper output)
{
    private const string StubAnswer =
        "The kinetic energy is 9 J.\n```vernier\n{\"subject\":\"Physics\",\"concept\":\"Kinetic energy\",\"claims\":[{\"expr\":\"0.5*2*3^2\",\"value\":\"9\"}],\"check\":{\"question\":\"KE of 4 kg at 1 m/s?\",\"answer\":\"2 J\"}}\n```";

    private static (HttpListener Listener, string BaseUrl, List<string> Bodies) StartStub()
    {
        var port = new Random().Next(20000, 40000);
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/v1/");
        listener.Start();
        var bodies = new List<string>();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { return; }
                using (var reader = new StreamReader(ctx.Request.InputStream)) bodies.Add(await reader.ReadToEndAsync());
                ctx.Response.ContentType = "text/event-stream";
                var sse = new StringBuilder();
                foreach (var piece in StubAnswer.Chunk(24))
                    sse.Append("data: ").Append(System.Text.Json.JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = new string(piece) } } } })).Append("\n\n");
                sse.Append("data: {\"choices\":[],\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":7}}\n\ndata: [DONE]\n\n");
                var bytes = Encoding.UTF8.GetBytes(sse.ToString());
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        });
        return (listener, $"http://127.0.0.1:{port}/v1", bodies);
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    internal static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    internal static async Task Until(Func<bool> condition, string what, int timeoutMs = 15000)
    {
        var waited = 0;
        while (!condition())
        {
            if ((waited += 50) > timeoutMs) throw new TimeoutException("Timed out waiting for " + what);
            await Task.Delay(50);
        }
    }

    [Fact]
    public void The_real_windows_work_end_to_end_ask_pen_review_policy_tray_close_and_are_accessible() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        var (listener, baseUrl, bodies) = StartStub();
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Light.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Controls.xaml", UriKind.Relative) });

        AppServices.Initialize(dir.Path);
        var settings = AppServices.Settings;
        settings.FirstRunComplete = true;
        var local = settings.Providers.Single(p => p.Id == "local");
        local.BaseUrl = baseUrl;
        local.Model = "stub";
        local.SupportsVision = true;
        settings.ActiveProviderId = "local";

        AppServices.Quitting = false;
        var main = new MainWindow();
        app.MainWindow = main;
        main.Show();
        AppServices.Overlays.Start();
        try
        {
            await Task.Delay(400);

            // ---- every page builds, and every interactive control has an accessible name ----
            var unnamed = new List<string>();
            foreach (var key in new[] { "Tutor", "Review", "Library", "Files", "Settings", "Diagnostics" })
            {
                main.Navigate(key);
                await Task.Delay(250);
                foreach (var control in Descendants<Control>(main).Where(c => c is ButtonBase or TextBox or ComboBox or PasswordBox or Slider && c.IsVisible))
                {
                    if (control is TextBox { IsReadOnly: true, IsTabStop: false }) continue; // selectable prose, not a field
                    if (control.TemplatedParent is ComboBox or ScrollBar or Slider or Track) continue; // template parts
                    var name = AutomationProperties.GetName(control);
                    if (string.IsNullOrWhiteSpace(name)) name = (UIElementAutomationPeer.CreatePeerForElement(control)?.GetName()) ?? "";
                    if (string.IsNullOrWhiteSpace(name)) unnamed.Add($"{key}: {control.GetType().Name} '{control.Name}'");
                }
            }
            var probe = new AskBar();
            probe.Show();
            await Task.Delay(200);
            // Depth comes from the compositor on Windows 11: rounded corners always, acrylic from 22H2 (build 22621).
            var depth = Depth.Of(probe);
            output.WriteLine($"ask bar depth: rounded={depth.Rounded} acrylic={depth.Acrylic}");
            if (Environment.OSVersion.Version.Build >= 22000) Assert.True(depth.Rounded, "DWM refused rounded corners on Windows 11");
            if (Environment.OSVersion.Version.Build >= 22621 && !SystemParameters.HighContrast) Assert.True(depth.Acrylic, "DWM refused the acrylic backdrop");
            foreach (var control in Descendants<Control>(probe).Where(c => c is ButtonBase or TextBox && c.IsVisible))
                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)) && string.IsNullOrWhiteSpace(UIElementAutomationPeer.CreatePeerForElement(control)?.GetName()))
                    unnamed.Add($"AskBar: {control.GetType().Name}");
            probe.ForceClose();
            Assert.True(unnamed.Count == 0, "Controls with no accessible name:\n" + string.Join("\n", unnamed));

            // ---- ask a question through the real panel ----
            main.Navigate("Tutor");
            await Task.Delay(200);
            var panel = Descendants<TutorPanel>(main).Single();
            var input = (TextBox)panel.FindName("Input");
            var send = (Button)panel.FindName("Send");
            input.Text = "What is the kinetic energy of a 2 kg cart at 3 m/s?";
            Click(send);
            await Until(() => AppServices.Session.IsBusy, "the turn to start", 3000);
            Assert.Equal("Stop", send.Content);
            await Until(() => !AppServices.Session.IsBusy, "the turn to finish");

            var transcript = AppServices.Session.Transcript;
            Assert.Equal(2, transcript.Count);
            Assert.Equal("The kinetic energy is 9 J.", transcript[1].Text);
            Assert.Contains(transcript[1].Checks, c => c.Status == Core.Agents.CheckStatus.Passed && c.Kind == "arithmetic");
            Assert.Contains("11 in / 7 out", transcript[1].Meta);
            Assert.DoesNotContain("vernier", string.Concat(Descendants<TextBox>(panel).Select(t => t.Text)));
            Assert.Equal("", input.Text);
            Assert.Contains("\"model\":\"stub\"", bodies.Single());
            Assert.Contains("never instructions", bodies.Single()); // the untrusted-content rule reached the provider
            output.WriteLine("status after turn: " + AppServices.Session.Status);

            // The turn was saved and turned into a concept and a review card.
            Assert.Contains(AppServices.Store.ConceptStates(), c => c.Name == "Kinetic energy");
            Assert.Contains(AppServices.Store.Cards(), c => c.Front == "KE of 4 kg at 1 m/s?");
            Assert.Single(AppServices.Store.Sessions(), s => !s.Title.StartsWith("(Sample)"));

            // ---- review a due card through the real page ----
            main.Navigate("Review");
            await Task.Delay(250);
            var review = Descendants<ReviewPage>(main).Single();
            var queue = (ListBox)review.FindName("Queue");
            var dueBefore = AppServices.Store.Cards(DateTimeOffset.UtcNow).Count;
            Assert.True(dueBefore >= 2, "the sample cards should be due");
            queue.SelectedItem = queue.Items.Cast<StudyItem>().First(i => i.Kind == "card");
            await Task.Delay(100);
            Click((Button)review.FindName("Reveal"));
            Assert.Equal(Visibility.Visible, ((FrameworkElement)review.FindName("BackHost")).Visibility);
            Click(Descendants<Button>(review).First(b => (string)b.Content == "Good"));
            await Task.Delay(150);
            Assert.Equal(dueBefore - 1, AppServices.Store.Cards(DateTimeOffset.UtcNow).Count);

            // ---- the glass is up on every monitor, click-through, with the edge lights dark ----
            Assert.True(AppServices.Overlays.IsRunning);
            Assert.Equal(EdgeSignal.Off, AppServices.Overlays.Signal);
            Assert.Equal(Win32.Monitors().Count, app.Windows.OfType<AnnotationSurfaceWindow>().Count());
            // The native light when Vernier.Native is built, otherwise four WPF strips per monitor.
            Assert.Equal(AppServices.Overlays.NativeLight ? 0 : Win32.Monitors().Count * 4, app.Windows.OfType<EdgeStripWindow>().Count());

            // ---- Ask: the bar opens over the work, captures the screen once, and shows the answer ----
            settings.AllowScreenCapture = true;
            main.OpenAskBar(pen: false);
            await Task.Delay(400);
            var bar = main.Bar!;
            Assert.True(bar.IsVisible);
            Assert.True(bar.Topmost);
            Assert.False(AppServices.Overlays.PenArmed);
            var barInput = Descendants<TextBox>(bar).First(t => !t.IsReadOnly);
            barInput.Text = "What is this?";
            var looked = false;
            void Watch() { looked |= AppServices.Overlays.Signal == EdgeSignal.Looking; }
            AppServices.Session.Changed += Watch;
            Click(Descendants<Button>(bar).First(x => x.Content as string == "Ask"));
            await Until(() => AppServices.Session.IsBusy, "the screen question to start", 3000);
            await Until(() => !AppServices.Session.IsBusy, "the screen question to finish");
            AppServices.Session.Changed -= Watch;
            Assert.True(looked, "the edge wave never came on while the screen was being looked at");
            Assert.Equal(EdgeSignal.Off, AppServices.Overlays.Signal);
            Assert.Equal(4, AppServices.Session.Transcript.Count);
            Assert.Contains("about the screen", AppServices.Session.Transcript[2].Meta);
            var turnNotes = string.Join(" | ", AppServices.Session.Transcript[3].Notices);
            // The bar renders the finished answer on the dispatcher just after the session goes idle: give it that moment.
            try { await Until(() => Descendants<RichAnswer>(bar).Any(t => t.Source == "The kinetic energy is 9 J."), "the bar to render the answer", 3000); }
            catch (TimeoutException) { } // reported with the evidence below
            Assert.True(Descendants<RichAnswer>(bar).Any(t => t.Source == "The kinetic energy is 9 J."), "the bar did not show the answer; notices: " + turnNotes + " · answer: " + AppServices.Session.Transcript[3].Text + " · requests: " + bodies.Count);
            Assert.Contains("image_url", bodies[^1]); // the capture really went to the provider (the stub is marked image-capable below)
            Assert.NotNull(AppServices.Session.LastSnapshot?.Frame);

            // ---- Attach: slides pinned to the bar go with the question; the tutor is told what the last capture was and what changed ----
            var deck = Path.Combine(dir.Path, "Lecture 4.pptx");
            using (var zip = System.IO.Compression.ZipFile.Open(deck, System.IO.Compression.ZipArchiveMode.Create))
            using (var w = new StreamWriter(zip.CreateEntry("ppt/slides/slide1.xml").Open()))
                w.Write("<p:sld xmlns:p='urn:p' xmlns:a='urn:a'><a:p><a:r><a:t>Torque is r cross F</a:t></a:r></a:p></p:sld>");
            bar.AddFile(deck);
            Assert.Contains(Descendants<Button>(bar), x => AutomationProperties.GetName(x) == "Remove attachment Lecture 4.pptx");
            Assert.All(Descendants<ButtonBase>(bar), x => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(x)) && x.Content is not string, "an icon-only control in the ask bar has no accessible name"));
            barInput.Text = "And this slide?";
            Click(Descendants<Button>(bar).First(x => x.Content as string == "Ask"));
            await Until(() => AppServices.Session.IsBusy, "the attachment question to start", 3000);
            await Until(() => !AppServices.Session.IsBusy, "the attachment question to finish");
            Assert.Contains("Torque is r cross F", bodies[^1]);
            Assert.Contains("1 attached", AppServices.Session.Transcript[4].Meta);
            Assert.Contains("about the screen as it was then", bodies[^1]); // the earlier turn is remembered with its screen, not as a bare question
            Assert.Contains("The previous capture was taken", bodies[^1]);
            Click(Descendants<Button>(bar).First(x => AutomationProperties.GetName(x) == "Remove attachment Lecture 4.pptx"));
            Assert.DoesNotContain(Descendants<Button>(bar), x => AutomationProperties.GetName(x).StartsWith("Remove attachment"));

            // ---- Marks do not outlive what they point at: when the marked area changes, they are removed ----
            var page = new Border { Background = Brushes.White };
            var pageWindow = WindowsRealApiTests.Show(page, 420, 320, "Vernier stale-mark test");
            pageWindow.Left = 700; pageWindow.Top = 500;
            try
            {
                bar.HideBar();
                await Task.Delay(400);
                var pageBounds = Win32.WindowBounds(new WindowInteropHelper(pageWindow).Handle)!.Value;
                var inside = new PxRect(pageBounds.X + 60, pageBounds.Y + 80, 200, 120);
                AppServices.Annotations.Clear(); // marks left by other tests in this process are not this test's subject
                AppServices.Annotations.Add(new Annotation { Kind = AnnotationKind.Circle, Points = [new(inside.X, inside.Y), new(inside.Right, inside.Bottom)], Source = "tutor" });
                AppServices.Overlays.Watch(inside);
                await Task.Delay(3200); // a baseline is taken and the still page keeps its mark
                Assert.Single(AppServices.Annotations.Items);
                page.Background = Brushes.Black; // the page "scrolls"
                await Until(() => AppServices.Annotations.Items.Count == 0, "the stale mark to be removed", 6000);
                Assert.Contains("screen changed", AppServices.Session.Status);
            }
            finally { pageWindow.Close(); }

            // ---- Pen: arming takes the mouse and lights the frame; the chord again puts it down ----
            main.OpenAskBar(pen: true);
            await Task.Delay(200);
            Assert.True(AppServices.Overlays.PenArmed);
            Assert.Equal(EdgeSignal.Armed, AppServices.Overlays.Signal);
            // The glass came back to take the pen, but the bar is still above it: clicks on the bar reach the bar.
            var barRect = Win32.WindowBounds(new WindowInteropHelper(bar).Handle)!.Value;
            var onBar = new Win32.POINT { X = (int)barRect.Center.X, Y = (int)barRect.Center.Y };
            Assert.Equal(new WindowInteropHelper(bar).Handle, Win32.GetAncestor(Win32.WindowFromPoint(onBar), Win32.GA_ROOT));
            var glass = app.Windows.OfType<AnnotationSurfaceWindow>().First();
            Assert.Equal(0, (long)Win32.GetWindowLongPtr(new WindowInteropHelper(glass).Handle, Win32.GWL_EXSTYLE) & Win32.WS_EX_TRANSPARENT);
            main.OpenAskBar(pen: true);
            Assert.False(AppServices.Overlays.PenArmed);
            Assert.NotEqual(0, (long)Win32.GetWindowLongPtr(new WindowInteropHelper(glass).Handle, Win32.GWL_EXSTYLE) & Win32.WS_EX_TRANSPARENT);

            // ---- Share-Safe: real affinity on the real windows, captures blocked ----
            var hwnd = new WindowInteropHelper(main).Handle;
            var policyBox = (ComboBox)main.FindName("PolicyBox");
            policyBox.SelectedItem = policyBox.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == "ShareSafe");
            await Task.Delay(150);
            Assert.Equal(CapturePolicyMode.ShareSafe, AppServices.Policy.Mode);
            Assert.True(Win32.GetWindowDisplayAffinity(hwnd, out var affinity));
            Assert.Equal(DisplayAffinity.ExcludeFromCapture, affinity);
            Assert.All(AppServices.Policy.States, st => Assert.Equal(PolicyStatus.Applied, st.Status)); // workspace, ask bar, glass and edge lights
            Assert.True(AppServices.Session.CaptureBlocked);
            Assert.Contains("Share-Safe", AppServices.Session.CaptureBlockedReason);
            // A screen question that cannot have its screen is not sent at all; the learner is told why and what to change.
            var sentBefore = bodies.Count;
            await AppServices.Session.AskAsync("What is this?", null, new ScreenAsk(Win32.Monitors()[0].BoundsPhysical, []));
            Assert.Equal(sentBefore, bodies.Count);
            Assert.Contains("Share-Safe", AppServices.Session.Transcript[^1].Notices.Single());
            Assert.Equal("", AppServices.Session.Transcript[^1].Text);

            policyBox.SelectedItem = policyBox.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == "Standard");
            await Task.Delay(150);
            Assert.True(Win32.GetWindowDisplayAffinity(hwnd, out affinity));
            Assert.Equal(DisplayAffinity.None, affinity);

            // ---- Pause blocks capture and puts the pen down ----
            main.OpenAskBar(pen: true);
            Click((Button)main.FindName("PauseButton"));
            Assert.True(AppServices.Session.IsPaused);
            Assert.True(AppServices.Session.CaptureBlocked);
            Assert.False(AppServices.Overlays.PenArmed);
            Click((Button)main.FindName("PauseButton"));
            Assert.False(AppServices.Session.IsPaused);

            // ---- closing the workspace hides it; Vernier keeps running ----
            main.Close();
            await Task.Delay(150);
            Assert.False(main.IsVisible);
            Assert.True(bar.IsVisible);
            Assert.True(AppServices.Overlays.IsRunning);
            main.ShowWorkspace();
            Assert.True(main.IsVisible);

            // ---- Hide: everything out of sight at once ----
            Click(Descendants<Button>(main).First(x => x.Content as string == "Hide"));
            await Task.Delay(200);
            Assert.False(bar.IsVisible);
            Assert.False(main.IsVisible);
            Assert.False(AppServices.Annotations.Visible);
        }
        finally
        {
            AppServices.Quitting = true;
            main.Close();
            listener.Stop();
            AppServices.Shutdown();
        }
    });
}
