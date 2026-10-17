using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Vernier.App;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Settings;
using Vernier.Core.Windowing;
using Xunit.Abstractions;

namespace Vernier.Tests;

[CollectionDefinition("desktop", DisableParallelization = true)]
public class DesktopCollection;

/// <summary>
/// REAL Windows APIs on the interactive desktop: real windows, real user32, real GDI capture,
/// real OCR and accessibility. Nothing here is mocked. They need a logged-in desktop session.
/// </summary>
[Collection("desktop")]
public class WindowsRealApiTests(ITestOutputHelper output)
{
    /// <summary>Runs the body on an STA thread with a live dispatcher, as WPF requires.</summary>
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);

    internal static void OnSta(Func<Task> body)
    {
        // Desktop tests read real pixels: a display that went to sleep stops composition and every capture fails.
        // ES_DISPLAY_REQUIRED wakes it and keeps it on while the test runs (no input is sent).
        SetThreadExecutionState(0x80000000u | 0x00000002u); // ES_CONTINUOUS | ES_DISPLAY_REQUIRED
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
            {
                try { await body(); }
                catch (Exception e) { failure = e; }
                finally { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    // WPF allows one Application per process, bound to the thread that made it; every test that needs it shares this thread.
    private static readonly Lazy<Dispatcher> AppDispatcher = new(() =>
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ready.Set();
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Vernier test app" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    });

    /// <summary>Runs the body on the thread that owns the test process's WPF Application.</summary>
    internal static void OnAppSta(Func<Task> body)
    {
        SetThreadExecutionState(0x80000000u | 0x00000002u);
        AppDispatcher.Value.InvokeAsync(body).Task.Unwrap().GetAwaiter().GetResult();
    }

    internal static Window Show(UIElement content, double width = 520, double height = 220, string title = "Vernier test window")
    {
        var w = new Window
        {
            Title = title, Content = content, Width = width, Height = height, Left = 60, Top = 60, Topmost = true,
            WindowStyle = WindowStyle.ToolWindow, ShowActivated = true, Background = Brushes.White,
        };
        w.Show();
        return w;
    }

    [Fact]
    public void Capture_exclusion_really_removes_the_window_from_a_gdi_screen_capture() => OnSta(async () =>
    {
        var results = await CaptureExclusionSelfTest.RunAsync();
        output.WriteLine($"Windows build {Environment.OSVersion.Version.Build}");
        foreach (var r in results) output.WriteLine($"{r.Path} | expected: {r.Expected} | observed: {r.Observed}");

        Assert.Contains(results, r => r.Path.StartsWith("GDI BitBlt of the screen, standard") && r.Observed == "marker colour visible");
        Assert.True(results.Single(r => r.Path.StartsWith("GetWindowDisplayAffinity")).Excluded);
        Assert.True(results.Single(r => r.Path.StartsWith("GDI BitBlt of the screen, exclusion")).Excluded, "the excluded window still appeared in a BitBlt screen capture");
    });

    [Fact]
    public void Policy_service_applies_reads_back_and_clears_on_real_windows_and_refuses_foreign_ones() => OnSta(async () =>
    {
        var service = new WindowCapturePolicyService(new NativeWindowApi());
        var a = Show(new TextBlock { Text = "A" }, 200, 120);
        var b = Show(new TextBlock { Text = "B" }, 200, 120);
        try
        {
            nint ha = new WindowInteropHelper(a).Handle, hb = new WindowInteropHelper(b).Handle;
            service.Register(ha, "A");
            service.SetMode(CapturePolicyMode.RequestExclusion);
            service.Register(hb, "B (created after the mode was set)");
            await Task.Delay(100);

            Assert.All(service.States, s => Assert.Equal(PolicyStatus.Applied, s.Status));
            Assert.True(Win32.GetWindowDisplayAffinity(hb, out var affinity));
            Assert.Equal(DisplayAffinity.ExcludeFromCapture, affinity);

            service.SetMode(CapturePolicyMode.Standard);
            Assert.True(Win32.GetWindowDisplayAffinity(ha, out affinity));
            Assert.Equal(DisplayAffinity.None, affinity);

            // Another process's window (the shell's): must be refused, and must not be touched.
            var foreign = Win32.GetAncestor(Win32.WindowFromPoint(new Win32.POINT { X = 2, Y = (int)Win32.VirtualDesktop().Bottom - 2 }), Win32.GA_ROOT);
            Win32.GetWindowThreadProcessId(foreign, out var pid);
            if (foreign != 0 && pid != Environment.ProcessId)
            {
                service.SetMode(CapturePolicyMode.RequestExclusion);
                service.Register(foreign, "foreign");
                var state = service.States.Single(s => s.Hwnd == foreign);
                Assert.Equal(PolicyStatus.Failed, state.Status);
                Assert.Contains("another process", state.Detail);
            }

            // A closed window reports a failure rather than pretending.
            b.Close();
            service.Reapply();
            Assert.Equal(PolicyStatus.Failed, service.States.Single(s => s.Hwnd == hb).Status);
        }
        finally { a.Close(); }
    });

    [Fact]
    public void Screen_capture_maps_image_pixels_back_to_the_right_screen_pixels() => OnSta(async () =>
    {
        // A red square at a known offset inside a white window.
        var canvas = new Canvas { Background = Brushes.White };
        var square = new System.Windows.Shapes.Rectangle { Width = 60, Height = 60, Fill = Brushes.Red };
        Canvas.SetLeft(square, 200);
        Canvas.SetTop(square, 60);
        canvas.Children.Add(square);
        var window = Show(canvas, 600, 300);
        try
        {
            await Task.Delay(500);
            var hwnd = new WindowInteropHelper(window).Handle;
            var frame = new ScreenCaptureService().Capture(new CaptureTarget.Window(hwnd, "t"), maxEdge: 300)!; // forces a downscale
            Assert.True(frame.Geometry.ImageWidth <= 300);
            Assert.True(frame.Geometry.ScaleX > 1);
            Assert.Equal(Win32.WindowBounds(hwnd), frame.Geometry.SourcePhysical);

            // Find the red square in the downscaled image, map its centre to the screen, and check the real screen pixel there.
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(new MemoryStream(frame.ImageBytes), System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            var image = decoder.Frames[0];
            double sx = 0, sy = 0;
            var n = 0;
            for (var y = 0; y < image.PixelHeight; y += 2)
                for (var x = 0; x < image.PixelWidth; x += 2)
                {
                    var (r, g, b) = ScreenCaptureService.PixelAt(image, x, y);
                    if (r > 200 && g < 60 && b < 60) { sx += x; sy += y; n++; }
                }
            Assert.True(n > 10, "the red square was not found in the capture");
            var onScreen = frame.Geometry.ImageToPhysical(new PxPoint(sx / n, sy / n));
            var (pr, pg, pb) = ScreenCaptureService.PixelAt(ScreenCaptureService.Grab(new PxRect(onScreen.X, onScreen.Y, 1, 1)), 0, 0);
            Assert.True(pr > 200 && pg < 60 && pb < 60, $"mapped point ({onScreen.X:0},{onScreen.Y:0}) is not on the red square");
            output.WriteLine($"capture {frame.Geometry.SourcePhysical.Width}x{frame.Geometry.SourcePhysical.Height} -> image {frame.Geometry.ImageWidth}x{frame.Geometry.ImageHeight}; square centre mapped to ({onScreen.X:0},{onScreen.Y:0}); quantisation ±{frame.Geometry.QuantizationUncertaintyPx:0.0}px");
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Accessibility_and_ocr_both_read_real_window_text_and_the_observer_prefers_accessibility() => OnSta(async () =>
    {
        var text = new TextBlock { Text = "A 2 kg cart moves at 3 m/s. Find its kinetic energy.", FontSize = 26, Margin = new Thickness(20), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Black };
        var window = Show(text, 700, 200);
        try
        {
            await Task.Delay(600);
            var frame = new ScreenCaptureService().Capture(new CaptureTarget.Window(new WindowInteropHelper(window).Handle, "t"), 1600)!;

            var uia = await new UiaTextExtractor().ExtractAsync(frame, default);
            Assert.Contains(uia, t => t.Text.Contains("kinetic energy"));

            var ocr = await new OcrTextExtractor().ExtractAsync(frame, default);
            var ocrText = string.Join(" ", ocr.Select(t => t.Text));
            output.WriteLine("OCR read: " + ocrText);
            Assert.Contains("kinetic", ocrText, StringComparison.OrdinalIgnoreCase);
            Assert.All(ocr, t => Assert.True(frame.Geometry.ImageBounds.Contains(t.BoundsImage!.Value)));

            var snapshot = await new Observer([new UiaTextExtractor(), new OcrTextExtractor()]).ObserveAsync(frame, default);
            Assert.Equal("accessibility", snapshot.TextProvenance);
            Assert.True(snapshot.Confidence > 0.9);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Glass_is_click_through_until_the_pen_is_armed_and_covers_its_monitor_in_physical_pixels() => OnSta(async () =>
    {
        var monitor = Win32.Monitors().First(m => m.IsPrimary);
        var store = new AnnotationStore();
        var surface = new AnnotationSurfaceWindow(monitor, store);
        surface.Show();
        try
        {
            await Task.Delay(200);
            var hwnd = new WindowInteropHelper(surface).Handle;
            long Ex() => (long)Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);

            Assert.NotEqual(0, Ex() & Win32.WS_EX_TRANSPARENT);
            Assert.NotEqual(0, Ex() & Win32.WS_EX_NOACTIVATE);
            Assert.NotEqual(0, Ex() & Win32.WS_EX_TOOLWINDOW);
            Win32.GetWindowRect(hwnd, out var rect);
            Assert.Equal(monitor.BoundsPhysical, rect.ToPx());

            // Click-through for real: the window under a point on the surface is not the surface.
            var probe = new Win32.POINT { X = (int)monitor.BoundsPhysical.Center.X, Y = (int)monitor.BoundsPhysical.Center.Y };
            Assert.NotEqual(hwnd, Win32.GetAncestor(Win32.WindowFromPoint(probe), Win32.GA_ROOT));

            surface.SetPenArmed(true);
            Assert.Equal(0, Ex() & Win32.WS_EX_TRANSPARENT);
            surface.SetPenArmed(false);
            Assert.NotEqual(0, Ex() & Win32.WS_EX_TRANSPARENT);

            // A mark really appears on screen at its physical coordinates.
            var at = new PxRect(monitor.BoundsPhysical.X + 300, monitor.BoundsPhysical.Y + 300, 120, 80);
            store.Add(new Annotation { Kind = AnnotationKind.Highlight, Points = [new(at.X, at.Y), new(at.Right, at.Bottom)], Color = "#FF00FF", StrokeWidth = 6 });
            surface.Redraw();
            await Task.Delay(400);
            var edge = ScreenCaptureService.Grab(new PxRect(at.X - 2, at.Y + at.Height / 2, 5, 1));
            Assert.Contains(Enumerable.Range(0, edge.PixelWidth), x => ScreenCaptureService.PixelAt(edge, x, 0) is { R: > 200, G: < 90, B: > 200 });
        }
        finally { surface.Close(); }
    });

    [Fact]
    public void Pen_ink_is_painted_into_the_capture_and_the_marked_area_is_reported_and_enlarged() => OnSta(async () =>
    {
        var text = new TextBlock { Text = "torque = r x F", FontSize = 30, Margin = new Thickness(30), Foreground = Brushes.Black };
        var window = Show(text, 700, 300);
        try
        {
            await Task.Delay(500);
            var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var win = Win32.WindowBounds(new WindowInteropHelper(window).Handle)!.Value;
            // A box drawn round the middle of the window, as a learner would circle something.
            var box = new PxRect(win.X + 40, win.Y + 60, 300, 120);
            PxPoint[] stroke = [new(box.X, box.Y), new(box.Right, box.Y), new(box.Right, box.Bottom), new(box.X, box.Bottom), new(box.X, box.Y)];

            var frame = new ScreenCaptureService().Capture(new CaptureTarget.Screen(monitor, new WindowInteropHelper(window).Handle), 1280, [stroke])!;

            Assert.Equal(monitor, frame.Geometry.SourcePhysical);
            Assert.NotNull(frame.MarkedRegion);
            Assert.NotNull(frame.MarkedDetail);
            Assert.Equal("Vernier.Tests".Length > 0, frame.ProcessName is not null); // the app in front is identified
            // The reported region, mapped back to the screen, contains what was drawn.
            var back = frame.Geometry.ImageToPhysical(frame.MarkedRegion!.Value);
            Assert.True(back.Contains(new PxPoint(box.X, box.Y)) && back.Contains(new PxPoint(box.Right, box.Bottom)));

            // The yellow ink is in the image the model receives, where the learner drew it.
            var image = System.Windows.Media.Imaging.BitmapDecoder.Create(new MemoryStream(frame.ImageBytes), System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
            var onImage = frame.Geometry.PhysicalToImage(new PxPoint(box.X + box.Width / 2, box.Y));
            var found = false;
            for (var dy = -3; dy <= 3 && !found; dy++)
            {
                var (r, g, b) = ScreenCaptureService.PixelAt(image, (int)onImage.X, Math.Clamp((int)onImage.Y + dy, 0, image.PixelHeight - 1));
                found = r > 200 && g > 170 && b < 110;
            }
            Assert.True(found, "no yellow ink at the top edge of the drawn box");

            // The enlarged detail is the un-inked content at higher scale than the downscaled screenshot.
            var detail = System.Windows.Media.Imaging.BitmapDecoder.Create(new MemoryStream(frame.MarkedDetail!), System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
            Assert.True(detail.PixelWidth > frame.MarkedRegion.Value.Width, "the detail image is not larger than the same area in the screenshot");
            output.WriteLine($"screen {monitor.Width}x{monitor.Height} -> {frame.Geometry.ImageWidth}x{frame.Geometry.ImageHeight}; marked {frame.MarkedRegion.Value.Width:0}x{frame.MarkedRegion.Value.Height:0} in image; detail {detail.PixelWidth}x{detail.PixelHeight}");
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Edge_light_sits_on_the_screen_edge_is_click_through_and_shows_only_while_signalled() => OnSta(async () =>
    {
        var monitor = Win32.Monitors().First(m => m.IsPrimary);
        var strip = new EdgeStripWindow(monitor, System.Windows.Controls.Dock.Top);
        strip.Show();
        try
        {
            await Task.Delay(200);
            var hwnd = new WindowInteropHelper(strip).Handle;
            Win32.GetWindowRect(hwnd, out var rect);
            var b = rect.ToPx();
            Assert.Equal(monitor.BoundsPhysical.X, b.X);
            Assert.Equal(monitor.BoundsPhysical.Y, b.Y);
            Assert.Equal(monitor.BoundsPhysical.Width, b.Width);
            Assert.InRange(b.Height, 8, 40);
            Assert.NotEqual(0, (long)Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE) & Win32.WS_EX_TRANSPARENT);
            Assert.Equal(0, strip.Opacity);

            // Looking: the top edge of the screen really turns light blue.
            strip.Show(EdgeSignal.Looking);
            await Task.Delay(600);
            var probe = ScreenCaptureService.Grab(new PxRect(monitor.BoundsPhysical.X + monitor.BoundsPhysical.Width / 2 - 200, monitor.BoundsPhysical.Y, 400, 1));
            var blue = Enumerable.Range(0, probe.PixelWidth).Count(x => ScreenCaptureService.PixelAt(probe, x, 0) is var (r, g, bl) && bl > 150 && bl > r + 25);
            output.WriteLine($"blue-tinted pixels along the top edge while looking: {blue} of {probe.PixelWidth}");
            Assert.True(blue > probe.PixelWidth / 4, "the top edge did not turn blue");

            strip.Show(EdgeSignal.Off);
            await Task.Delay(400);
            Assert.Equal(0, strip.Opacity, 2);
        }
        finally { strip.Close(); }
    });

    [Fact]
    public void Tray_icon_is_added_to_the_notification_area_and_removed() => OnSta(() =>
    {
        var ico = Path.Combine(AppContext.BaseDirectory, "Assets", "vernier.ico");
        Assert.True(File.Exists(ico), "the icon file is not next to the executable");
        using (var tray = new TrayIcon(ico, "Vernier test"))
        {
            Assert.True(tray.IsVisible);
            tray.SetTooltip("Vernier test, updated");
        }
        return Task.CompletedTask;
    });

    [Fact]
    public void Credential_manager_round_trips_a_key_and_deletes_it()
    {
        var store = new CredentialManagerSecretStore();
        var name = "VERNIER_TEST_" + Guid.NewGuid().ToString("N");
        try
        {
            Assert.Null(store.Get(name));
            store.Set(name, "not-a-real-key-äß-123");
            Assert.Equal("not-a-real-key-äß-123", store.Get(name));
        }
        finally { store.Delete(name); }
        Assert.Null(store.Get(name));
    }

    [Fact]
    public void Input_coordinates_and_key_chords_are_computed_correctly()
    {
        var desktop = new PxRect(-1920, 0, 3840, 1080); // a monitor to the left of the primary
        Assert.Equal((0, 0), SendInputDriver.ToAbsolute(new PxPoint(-1920, 0), desktop));
        Assert.Equal((65535, 65535), SendInputDriver.ToAbsolute(new PxPoint(1919, 1079), desktop));
        var (x, _) = SendInputDriver.ToAbsolute(new PxPoint(0, 0), desktop);
        Assert.InRange(x, 32760, 32790);

        Assert.Equal([0x11, 0x53], SendInputDriver.ParseChord("Ctrl+S"));
        Assert.Equal([0x11, 0x10, 0x5A], SendInputDriver.ParseChord("control + shift + z"));
        Assert.Equal([0x7B], SendInputDriver.ParseChord("F12"));
        Assert.Equal([0x09], SendInputDriver.ParseChord("tab"));
        Assert.Throws<InvalidOperationException>(() => SendInputDriver.ParseChord("ctrl+nonsense"));
    }

    [Fact]
    public void Input_driver_refuses_when_the_target_window_is_not_in_front() => OnSta(async () =>
    {
        var window = Show(new TextBlock { Text = "target" }, 200, 120);
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var other = Show(new TextBlock { Text = "in front" }, 200, 120);
            other.Activate();
            await Task.Delay(300);
            if (Win32.GetAncestor(Win32.GetForegroundWindow(), Win32.GA_ROOT) == hwnd) { other.Close(); return; } // could not arrange focus; nothing to assert
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new SendInputDriver(hwnd).ExecuteAsync(new ComputerAction(ActionType.Type, null, "x", null, 0, ""), default));
            Assert.Contains("no longer in front", e.Message);
            other.Close();
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Speech_output_starts_and_can_be_interrupted()
    {
        SapiSpeechOutput voice;
        try { voice = new SapiSpeechOutput(); }
        catch (Exception e) { output.WriteLine("No audio output device/voice here; not verified: " + e.Message); return; }
        using (voice)
        {
            voice.Speak("This sentence is long enough that it will still be playing when it is stopped.", 2.0);
            Thread.Sleep(250);
            Assert.True(voice.IsSpeaking);
            voice.Pause();
            voice.Resume();
            voice.Stop();
            Thread.Sleep(250);
            Assert.False(voice.IsSpeaking);
        }
    }
}

/// <summary>LIVE end to end: real window → real capture → real text extraction → real model → verifier → mapped annotations. VERNIER_LIVE=1 only.</summary>
[Collection("desktop")]
public class ContextHubRealTests(ITestOutputHelper output)
{
    [Fact]
    public void The_media_session_and_the_window_in_front_are_read_from_windows() => WindowsRealApiTests.OnSta(async () =>
    {
        var engine = new Vernier.Core.Context.ContextEngine(new Vernier.Core.Context.ContextOptions { MinimumDwell = TimeSpan.FromSeconds(1) });
        using var hub = new Vernier.App.Context.ContextHub(engine, () => true, () => false);
        // Whatever is playing on this machine right now, if anything: recorded, not asserted.
        var media = await hub.ReadMediaAsync();
        output.WriteLine(media is null ? "media session: nothing playing" : $"media session: \"{media.Title}\" in {media.App}, position {media.Position}, length {media.Duration}, playing {media.Playing}");
        var window = WindowsRealApiTests.Show(new TextBlock { Text = "context test" }, 300, 120, "Vernier context test");
        try
        {
            await Task.Delay(3500); // focus hook + timer samples past the dwell time
            output.WriteLine(engine.Select("", DateTimeOffset.UtcNow).Render());
            // Windows may refuse to bring a test window to the front; then whatever is in front forms the episode.
            Assert.NotNull(engine.Current);
        }
        finally { window.Close(); }
    });
}

[Collection("desktop")]
public class LiveScreenTutoringTests(ITestOutputHelper output)
{
    [Fact]
    public void Starting_the_tutor_while_the_learner_types_shortens_the_wait_for_the_first_words()
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        WindowsRealApiTests.OnSta(async () =>
        {
            using var dir = new TempDir();
            AppServices.Initialize(dir.Path);
            AppServices.Settings.AllowScreenCapture = true;
            AppServices.Settings.ActiveProviderId = "claude-code";
            var window = WindowsRealApiTests.Show(new TextBlock { Text = "Ohm's law: V = I R", FontSize = 30, Margin = new Thickness(24), Foreground = Brushes.Black }, 600, 200, "Circuits");
            try
            {
                await Task.Delay(600);
                var hwnd = new WindowInteropHelper(window).Handle;
                var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
                var session = AppServices.Session;
                async Task<double> FirstWords(string question)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    double first = double.NaN, teach = double.NaN;
                    void Seen(TranscriptItem item) { if (double.IsNaN(first) && item.Text.Length > 0) first = clock.Elapsed.TotalSeconds; }
                    void Stage() { if (double.IsNaN(teach) && session.Stage == PipelineStage.Teach) teach = clock.Elapsed.TotalSeconds; }
                    session.AnswerUpdated += Seen;
                    session.Changed += Stage;
                    await session.AskAsync(question, null, new ScreenAsk(monitor, [], hwnd));
                    session.AnswerUpdated -= Seen;
                    session.Changed -= Stage;
                    output.WriteLine($"  capture + reading the screen: {teach:0.0} s; model to first words: {first - teach:0.0} s");
                    Assert.True(session.LastTurn is not null, string.Join(" | ", session.Transcript.SelectMany(t => t.Notices)));
                    return first;
                }
                var cold = await FirstWords("In five words, what law is on my screen?");
                session.Prewarm();          // the bar opened
                await Task.Delay(8000);     // the learner types
                var warm = await FirstWords("In five words, what does V stand for there?");
                output.WriteLine($"Enter → first words: cold start {cold:0.0} s, started while typing {warm:0.0} s (warm CLI used: {Vernier.Core.Providers.ClaudeCodeProvider.LastRunWasWarm})");
                Assert.True(warm < cold, $"the prewarmed turn was not faster ({warm:0.0} s vs {cold:0.0} s)");
            }
            finally
            {
                window.Close();
                AppServices.Shutdown();
            }
        });
    }

    [Fact]
    public void A_changed_screen_is_followed_without_apology_and_a_circle_lands_on_its_target()
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        WindowsRealApiTests.OnSta(async () =>
        {
            using var dir = new TempDir();
            AppServices.Initialize(dir.Path);
            AppServices.Settings.AllowScreenCapture = true;
            AppServices.Settings.ActiveProviderId = "claude-code";
            var square = new Border { Width = 110, Height = 110, Background = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(330, 20, 0, 0) };
            var text = new TextBlock { Text = "torque = r x F", FontSize = 30, Margin = new Thickness(24), Foreground = Brushes.Black };
            var window = WindowsRealApiTests.Show(new StackPanel { Children = { text, square } }, 640, 330, "Lecture 4");
            try
            {
                await Task.Delay(600);
                var hwnd = new WindowInteropHelper(window).Handle;
                var session = AppServices.Session;
                session.Mode = TutorMode.Annotate;
                var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;

                AppServices.Annotations.Clear();
                var asked = DateTimeOffset.UtcNow;
                await session.AskAsync("Circle the red square on my screen. Use one circle and nothing else.", null, new ScreenAsk(monitor, [], hwnd));
                var first = session.LastTurn;
                Assert.True(first is not null, "no turn: " + session.Status + " | " + string.Join(" | ", session.Transcript.SelectMany(t => t.Notices)));
                var target = new PxRect(square.PointToScreen(new Point(0, 0)).X, square.PointToScreen(new Point(0, 0)).Y, square.PointToScreen(new Point(110, 110)).X - square.PointToScreen(new Point(0, 0)).X, square.PointToScreen(new Point(110, 110)).Y - square.PointToScreen(new Point(0, 0)).Y);
                foreach (var e in session.ToolEvents) output.WriteLine($"  {(e.At - asked).TotalSeconds:0.0}s {e.Tool} {e.Arguments} → {(e.IsError ? "ERROR " : "")}{e.Outcome}");
                output.WriteLine("answer: " + first.Output.Text);
                // The mark arrived as a tool call, not in a block at the end of the answer.
                var circle = Assert.Single(AppServices.Annotations.Items, a => a is { Source: "tutor", Kind: AnnotationKind.Circle });
                var markEvent = session.ToolEvents.First(e => e.Tool == "overlay_mark" && !e.IsError);
                output.WriteLine($"Enter → first mark drawn: {(markEvent.At - asked).TotalSeconds:0.0} s; tool calls: {string.Join(", ", session.ToolEvents.Select(e => e.Tool + (e.IsError ? "(error)" : "")))}");
                var centre = new PxPoint((circle.Points[0].X + circle.Points[1].X) / 2, (circle.Points[0].Y + circle.Points[1].Y) / 2);
                var error = Math.Sqrt(Math.Pow(centre.X - target.Center.X, 2) + Math.Pow(centre.Y - target.Center.Y, 2));
                output.WriteLine($"target {target}; circle box {circle.Points[0]}..{circle.Points[1]}; centre error {error:0} px; confidence {circle.Confidence:0.00}");
                Assert.True(target.Contains(centre), $"the circle's centre is {error:0} px from the square's centre, outside the square");

                // The screen changes completely, as when a video moves on. The next answer must be about the new screen, without retracting the first.
                text.Text = "d/dx of x^2 = 2x";
                square.Visibility = Visibility.Collapsed;
                await Task.Delay(500);
                session.Mode = TutorMode.Ask;
                // Without the learner's say-so the attachment is not sent to a remote provider, and neither is the question.
                await session.AskAsync("What does my screen show now? One sentence.", null, new ScreenAsk(monitor, [], hwnd), [new Attachment("notes.txt", "x")]);
                Assert.Same(first, session.LastTurn);
                Assert.Contains("not sent", session.Transcript[^1].Notices.Single());
                session.AttachmentsToRemoteAllowed = true;
                await session.AskAsync("What does my screen show now? One sentence.", null, new ScreenAsk(monitor, [], hwnd),
                    [new Attachment("notes.txt", "The power rule: d/dx of x^n = n x^(n-1).")]);
                var second = session.LastTurn!.Output.Text;
                output.WriteLine(second);
                output.WriteLine("since last: " + session.LastSnapshot?.SinceLast);
                // One changed line in a small window on a large screen is still seen as a change, so the old reading of the screen is not reused.
                Assert.Contains("part of the picture has changed", session.LastSnapshot!.SinceLast);
                Assert.Contains("d/dx", session.LastSnapshot.JoinedText); // OCR reads x^2 as "xA2"; what matters is that the new screen is what is read
                Assert.DoesNotContain("torque", session.LastSnapshot.JoinedText);
                Assert.NotSame(first, session.LastTurn);
                Assert.Matches("(?i)deriv|2x|power rule", second);
                Assert.DoesNotMatch("(?i)sorry|apolog|i was wrong|my (earlier|previous) (answer|assumption|reading)|correction:", second);
            }
            finally
            {
                window.Close();
                AppServices.Shutdown();
            }
        });
    }

    [Fact]
    public void A_pen_mark_on_a_real_window_is_explained_and_annotated_inside_it()
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        WindowsRealApiTests.OnSta(async () =>
        {
            using var dir = new TempDir();
            AppServices.Initialize(dir.Path);
            AppServices.Settings.AllowScreenCapture = true;
            AppServices.Settings.ActiveProviderId = "claude-code";
            var problem = new TextBlock { Text = "A 2 kg cart moves at 3 m/s.\nFind its kinetic energy.", FontSize = 30, Margin = new Thickness(24), Foreground = Brushes.Black };
            var window = WindowsRealApiTests.Show(problem, 640, 260, "Physics homework");
            try
            {
                await Task.Delay(600);
                var hwnd = new WindowInteropHelper(window).Handle;
                var session = AppServices.Session;
                var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
                var bounds = Win32.WindowBounds(hwnd)!.Value;
                // The learner circles the problem with the pen and says nothing: the mark is the question.
                var box = new PxRect(bounds.X + 10, bounds.Y + 40, bounds.Width - 20, bounds.Height - 60);
                PxPoint[] ink = [new(box.X, box.Y), new(box.Right, box.Y), new(box.Right, box.Bottom), new(box.X, box.Bottom), new(box.X, box.Y)];

                AppServices.Annotations.Clear();
                var asked = DateTimeOffset.UtcNow;
                await session.AskAsync("", null, new ScreenAsk(monitor, [ink], hwnd));
                var marks = AppServices.Annotations.Items.Where(a => a.Source == "tutor").ToList();

                var turn = session.LastTurn;
                Assert.True(turn is not null, "no turn: " + session.Status + " | " + string.Join(" | ", session.Transcript.SelectMany(t => t.Notices)));
                output.WriteLine(turn.Output.Text);
                foreach (var c in turn.Verification.Checks) output.WriteLine($"[{c.Status}] {c.Detail}");
                output.WriteLine($"stages: {string.Join(" > ", turn.StagesRun)}; tutor marks: {marks.Count}; text source: {session.LastSnapshot?.TextProvenance}; marked: {session.LastSnapshot?.Frame?.MarkedRegion}");
                foreach (var e in session.ToolEvents) output.WriteLine($"  {(e.At - asked).TotalSeconds:0.0}s {e.Tool} {e.Arguments} → {(e.IsError ? "ERROR " : "")}{e.Outcome}");

                Assert.Equal(TutorSession.DefaultInkQuestion, session.Transcript[0].Text);
                Assert.Contains("2 kg", session.LastSnapshot!.JoinedText);
                Assert.NotNull(session.LastSnapshot.Frame!.MarkedRegion);
                Assert.Contains(PipelineStage.Observe, turn.StagesRun);
                Assert.Contains("kinetic", turn.Output.Text, StringComparison.OrdinalIgnoreCase);
                Assert.False(turn.Verification.HasFailures, turn.Verification.Summary);
                Assert.NotEmpty(marks);
                // Placed by name: at least one mark sits on text Vernier found, not on a guessed box.
                Assert.Contains(marks, m => m.Confidence >= 0.5);
                // The tutor's marks land on the window the learner circled, not somewhere else on the screen.
                Assert.All(marks, a => Assert.All(a.Points, p => Assert.True(bounds.Contains(p), $"annotation point ({p.X:0},{p.Y:0}) is outside the marked window")));
            }
            finally
            {
                window.Close();
                AppServices.Shutdown();
            }
        });
    }
}
