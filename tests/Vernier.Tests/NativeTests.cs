using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Vernier.App.Platform;
using Vernier.Core.Geometry;
using Vernier.Core.Windowing;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>
/// The native module against the real desktop. Each test states the claim that justifies having
/// native code for it; if the module is not built, the tests say so and pass (the app falls back).
/// </summary>
[Collection("desktop")]
public class NativeTests(ITestOutputHelper output)
{
    private static (byte R, byte G, byte B) Pixel(PxPoint at) => ScreenCaptureService.PixelAt(ScreenCaptureService.Grab(new PxRect(at.X, at.Y, 1, 1)), 0, 0);

    // Frames keep coming while anything on screen moves (a playing video), so draining is bounded in time.
    private static void Drain(DuplicationSession dup)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 700 && dup.Next(60) is not null) { }
    }

    // Duplication delivers a frame only when something on screen changes (a still screen times out, seen on the
    // author's machine), so the test makes a change. The app falls back to GDI until the first frame exists.
    private static async Task FirstFrame(DuplicationSession dup)
    {
        var nudge = WindowsRealApiTests.Show(new Border { Background = Brushes.Gray }, 120, 80, "Vernier duplication nudge");
        try
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 3000 && !dup.HasFrame) { await Task.Delay(30); dup.Next(50); }
        }
        finally { nudge.Close(); }
    }

    private bool Skip()
    {
        if (Native.Available) return false;
        output.WriteLine("Vernier.Native.dll is not built here (scripts/build-native.ps1); the managed paths are used.");
        return true;
    }

    [Fact]
    public void Edge_light_is_click_through_per_pixel_transparent_moves_and_keeps_animating_while_the_ui_thread_is_blocked() => WindowsRealApiTests.OnSta(async () =>
    {
        if (Skip()) return;
        var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
        var page = WindowsRealApiTests.Show(new Border { Background = Brushes.White }, 600, 300, "Vernier glow test page");
        page.Left = 300; page.Top = 300;
        using var glow = NativeGlow.TryCreate(monitor, 16, 0xFF6CC4FF);
        Assert.NotNull(glow);
        try
        {
            await Task.Delay(400);
            var probe = new PxPoint(monitor.X + monitor.Width / 2, monitor.Y + 2);
            var before = Pixel(probe);
            glow!.Set(2, reducedMotion: false);
            await Task.Delay(600);

            // Click-through: the window under the light's own pixels is not the light.
            var hwnd = glow.Hwnd;
            Assert.NotEqual(0, hwnd);
            Assert.True(Win32.IsWindowVisible(hwnd));
            Assert.NotEqual(hwnd, Win32.GetAncestor(Win32.WindowFromPoint(new Win32.POINT { X = (int)probe.X, Y = (int)probe.Y }), Win32.GA_ROOT));

            // Per-pixel transparency: the page in the middle of the screen shows through untouched.
            var pageBounds = Win32.WindowBounds(new WindowInteropHelper(page).Handle)!.Value;
            var middle = Pixel(pageBounds.Center);
            Assert.True(middle is { R: > 245, G: > 245, B: > 245 }, $"the page under the light's window is tinted: {middle}");

            // It travels: the brightness profile along the top edge is different 250 ms later.
            byte[] Row()
            {
                var row = ScreenCaptureService.Grab(new PxRect(monitor.X, monitor.Y + 1, monitor.Width, 1));
                var bytes = new byte[(int)monitor.Width];
                for (var x = 0; x < bytes.Length; x++) bytes[x] = ScreenCaptureService.PixelAt(row, x, 0).B;
                return bytes;
            }
            var a = Row();
            await Task.Delay(250);
            var b = Row();
            var moved = a.Zip(b).Count(p => Math.Abs(p.First - p.Second) > 12);
            output.WriteLine($"top-edge pixels whose blue changed by >12 levels in 250 ms: {moved} of {a.Length}; edge pixel before {before}, during {Pixel(probe)}");
            Assert.True(moved > a.Length / 20, "the light did not move");

            // Its own thread: the UI thread is blocked for 2 s and it keeps presenting frames.
            var f0 = glow.Frames;
            Thread.Sleep(2000);
            var fps = (glow.Frames - f0) / 2.0;
            output.WriteLine($"frames per second while the UI thread was blocked: {fps:0}");
            Assert.True(fps >= 40, $"only {fps:0} fps while the UI thread was blocked");

            // It can be left out of screen captures like Vernier's other windows.
            Assert.True(Win32.SetWindowDisplayAffinity(hwnd, DisplayAffinity.ExcludeFromCapture));
            await Task.Delay(300);
            var excluded = Pixel(probe);
            output.WriteLine($"edge pixel with capture exclusion: {excluded} (without the light: {before})");
            Assert.True(Math.Abs(excluded.B - before.B) < 30, "the light still appears in a GDI capture after exclusion");

            glow.Set(0, false);
            await Task.Delay(300);
            Assert.False(Win32.IsWindowVisible(hwnd));
        }
        finally { page.Close(); }
    });

    [Fact]
    public void Duplication_reports_where_the_screen_changed_and_matches_gdi_pixels() => WindowsRealApiTests.OnSta(async () =>
    {
        if (Skip()) return;
        var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
        using var dup = DuplicationSession.TryOpen(monitor);
        if (dup is null) { output.WriteLine("Desktop duplication is not available in this session (remote/unsupported); GDI is used."); return; }
        await FirstFrame(dup);
        if (!dup.HasFrame) { output.WriteLine("INCONCLUSIVE: no duplication frame arrived (the display may be asleep, which stops composition). GDI capture is the fallback."); return; }
        Drain(dup);

        var swatch = new Border { Background = new SolidColorBrush(Color.FromRgb(255, 0, 255)) };
        var window = WindowsRealApiTests.Show(swatch, 300, 200, "Vernier duplication test");
        window.Left = 400; window.Top = 350;
        try
        {
            var bounds = Win32.WindowBounds(new WindowInteropHelper(window).Handle)!.Value;
            Native.FrameInfo? changed = null;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 3000 && changed is null)
            {
                await Task.Delay(30);
                if (dup.Next(50) is { } f && f.DirtyCount > 0 && f.DirtyBounds.Right > bounds.X - monitor.X && f.DirtyBounds.Left < bounds.Right - monitor.X) changed = f;
            }
            Assert.NotNull(changed);
            output.WriteLine($"dirty: {changed!.Value.DirtyCount} rects, bounds ({changed.Value.DirtyBounds.Left},{changed.Value.DirtyBounds.Top})-({changed.Value.DirtyBounds.Right},{changed.Value.DirtyBounds.Bottom}), {changed.Value.DirtyFraction:P1} of the screen");
            Drain(dup); // let the window finish appearing
            // A change inside a window that is already showing: the rectangles should be about that window, not the screen.
            swatch.Background = new SolidColorBrush(Color.FromRgb(250, 0, 250));
            Native.FrameInfo? repaint = null;
            clock.Restart();
            while (clock.ElapsedMilliseconds < 2000 && repaint is null)
            {
                await Task.Delay(20);
                if (dup.Next(50) is { DirtyCount: > 0 } f) repaint = f;
            }
            Assert.NotNull(repaint);
            output.WriteLine($"repaint inside the window: {repaint!.Value.DirtyCount} rects, bounds ({repaint.Value.DirtyBounds.Left},{repaint.Value.DirtyBounds.Top})-({repaint.Value.DirtyBounds.Right},{repaint.Value.DirtyBounds.Bottom}), {repaint.Value.DirtyFraction:P1} of the screen");
            swatch.Background = new SolidColorBrush(Color.FromRgb(255, 0, 255));
            await Task.Delay(100);
            Drain(dup);

            var inside = new PxRect(bounds.Center.X - 10, bounds.Center.Y - 10, 20, 20);
            var pixels = dup.Copy(inside)!;
            Assert.Equal(20 * 20 * 4, pixels.Length);
            Assert.True(pixels[2] > 240 && pixels[1] < 15 && pixels[0] > 240, $"duplicated pixel is {pixels[2]},{pixels[1]},{pixels[0]}, not the swatch");
            var thumb = dup.Thumb(8, 8, inside)!;
            Assert.All(thumb, v => Assert.InRange(v, 100, 120)); // magenta luma ≈ (255*77+0*150+255*29)/256 = 105

            // Capture exclusion holds for duplication too: Vernier's own windows can be left out of what it reads.
            Assert.True(Win32.SetWindowDisplayAffinity(new WindowInteropHelper(window).Handle, DisplayAffinity.ExcludeFromCapture));
            await Task.Delay(200);
            Drain(dup);
            var after = dup.Copy(inside)!;
            output.WriteLine($"excluded window in the duplicated frame: {after[2]},{after[1]},{after[0]}");
            Assert.False(after[2] > 240 && after[1] < 15 && after[0] > 240, "an excluded window still appears in desktop duplication");
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Png_encoding_scales_and_round_trips()
    {
        if (Skip()) return;
        var bgra = new byte[64 * 32 * 4];
        for (var i = 0; i < bgra.Length; i += 4) { bgra[i] = 255; bgra[i + 3] = 255; } // blue
        var png = Native.EncodePng(bgra, 64, 32, 16);
        Assert.True(png is not null, $"native encode failed: 0x{Native.LastError:X8}");
        Assert.Equal((16, 8), (png!.Value.Width, png.Value.Height));
        var decoded = System.Windows.Media.Imaging.BitmapDecoder.Create(new MemoryStream(png.Value.Png), System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(16, decoded.PixelWidth);
        Assert.Equal((0, 0, 255), ((int, int, int))ScreenCaptureService.PixelAt(decoded, 8, 4));
    }

    [Fact]
    public void Native_capture_and_encode_versus_the_gdi_and_wpf_path() => WindowsRealApiTests.OnSta(async () =>
    {
        if (Skip()) return;
        await Task.Yield();
        var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
        using var dup = DuplicationSession.TryOpen(monitor);
        if (dup is null) { output.WriteLine("duplication unavailable here"); return; }
        await FirstFrame(dup);
        if (!dup.HasFrame) { output.WriteLine("INCONCLUSIVE: no duplication frame arrived (display asleep?)"); return; }
        var service = new ScreenCaptureService();
        var gdi = new List<double>();
        var nat = new List<double>();
        for (var i = 0; i < 6; i++)
        {
            var c = Stopwatch.StartNew();
            service.Capture(new CaptureTarget.Screen(monitor, 0), 1568);
            gdi.Add(c.Elapsed.TotalMilliseconds);
            c.Restart();
            var raw = dup.Copy(monitor)!;
            var png = Native.EncodePng(raw, (int)monitor.Width, (int)monitor.Height, 1568);
            nat.Add(c.Elapsed.TotalMilliseconds);
            Assert.True(png is not null, $"native encode failed: 0x{Native.LastError:X8}");
            Assert.Equal(1568, Math.Max(png!.Value.Width, png.Value.Height));
        }
        gdi.Sort(); nat.Sort();
        output.WriteLine($"full-monitor capture to a downscaled PNG: GDI + WPF median {gdi[3]:0.0} ms; duplication + WIC median {nat[3]:0.0} ms (6 runs each)");
    });

    [Fact]
    public void Ink_is_drawn_by_the_native_layer_while_the_ui_thread_is_blocked_reports_the_stroke_and_fades_away_by_itself() => WindowsRealApiTests.OnSta(async () =>
    {
        if (Skip()) return;
        var monitor = Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
        using var ink = NativeInk.TryCreate(monitor, 0xFFFFD84D, 4);
        Assert.NotNull(ink);
        ink!.SetTiming(700, 300);
        PxPoint[]? reported = null;
        ink.StrokeEnded += p => reported = p;
        try
        {
            // A diagonal in the top-left corner (a brief mark, away from the middle of the screen).
            var from = new PxPoint(monitor.X + 60, monitor.Y + 60);
            var line = Enumerable.Range(0, 41).Select(i => new PxPoint(from.X + i * 5, from.Y + i * 3)).ToArray();
            var mid = new PxPoint(from.X + 100, from.Y + 60);
            var before = Pixel(mid);
            var clock = Stopwatch.StartNew();
            ink.Feed(line);
            Thread.Sleep(5); // the UI thread is busy from here on: the ink must not need it
            double visibleAfter = -1;
            while (clock.ElapsedMilliseconds < 600)
            {
                if (Pixel(mid) is { R: > 200, G: > 170, B: < 140 }) { visibleAfter = clock.Elapsed.TotalMilliseconds; break; }
                Thread.Sleep(2);
            }
            output.WriteLine($"stroke of 40 segments visible after {visibleAfter:0} ms (screen pixel {before} before, {Pixel(mid)} now), with the UI thread blocked");
            output.WriteLine($"diagnostics: segments {ink.Segments}, window visible {Win32.IsWindowVisible(ink.Hwnd)}, bounds {Win32.WindowBounds(ink.Hwnd)}, reported {reported?.Length}, last native error 0x{ink.LastError:X8}");
            Assert.True(visibleAfter >= 0, "the stroke never appeared");
            Assert.True(visibleAfter < 250, $"the stroke took {visibleAfter:0} ms to appear");
            Assert.NotEqual(0, ink.Hwnd);
            Assert.Equal(40, ink.Segments);

            Thread.Sleep(100);
            Assert.NotNull(reported);                      // the finished stroke came back, for sending with the question
            Assert.Equal(line.Length, reported!.Length);
            Assert.Equal(line[0], reported[0]);

            // It leaves by itself: held for 0.7 s, faded over 0.3 s.
            var gone = false;
            for (var i = 0; i < 40 && !gone; i++)
            {
                await Task.Delay(50);
                gone = Pixel(mid) is { } px && Math.Abs(px.R - before.R) < 40 && Math.Abs(px.B - before.B) < 40;
            }
            Assert.True(gone, "the ink did not fade away");
            for (var i = 0; i < 20 && Win32.IsWindowVisible(ink.Hwnd); i++) await Task.Delay(50);
            Assert.False(Win32.IsWindowVisible(ink.Hwnd)); // and the layer hides, so it costs nothing while idle

            // Clearing is immediate.
            ink.Feed(line);
            await Task.Delay(150);
            ink.Clear();
            for (var i = 0; i < 10 && Win32.IsWindowVisible(ink.Hwnd); i++) await Task.Delay(30);
            Assert.False(Win32.IsWindowVisible(ink.Hwnd));
        }
        finally { ink.Clear(); }
    });

    [Fact]
    public void Benchmark_scaling_and_encoding_a_screen_wpf_against_wic()
    {
        if (Environment.GetEnvironmentVariable("VERNIER_BENCH") != "1" || !Native.Available) { output.WriteLine("skipped: set VERNIER_BENCH=1 with the native module built"); return; }
        // A screen-like frame: flat panels, hairlines and dense small text-sized noise, so PNG has real work to do.
        const int w = 2560, h = 1600;
        var bgra = new byte[w * h * 4];
        var rng = new Random(7);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                var panel = (x / 320 + y / 200) % 2 == 0 ? (byte)245 : (byte)30;
                var ink = y % 22 < 12 && x % 640 < 560 && rng.Next(3) == 0;
                bgra[i] = bgra[i + 1] = bgra[i + 2] = ink ? (byte)(255 - panel) : panel;
                bgra[i + 3] = 255;
            }
        double Median(Func<int> run)
        {
            var times = new List<double>();
            for (var r = 0; r < 9; r++)
            {
                var c = Stopwatch.StartNew();
                Assert.True(run() > 0);
                times.Add(c.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            return times[4];
        }
        int wpfBytes = 0, wicBytes = 0;
        var wpf = Median(() =>
        {
            var source = System.Windows.Media.Imaging.BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4);
            var scaled = new System.Windows.Media.Imaging.TransformedBitmap(source, new ScaleTransform(1568.0 / w, 980.0 / h));
            return wpfBytes = ScreenCaptureService.Png(scaled).Length;
        });
        var wic = Median(() => wicBytes = Native.EncodePng(bgra, w, h, 1568)!.Value.Png.Length);
        output.WriteLine($"2560x1600 BGRA to a 1568 px PNG: WPF median {wpf:0.0} ms ({wpfBytes / 1024} KB); WIC median {wic:0.0} ms ({wicBytes / 1024} KB); 9 runs each");
    }
}
