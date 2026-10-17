using System.Diagnostics;
using System.Windows;
using Vernier.App;
using Vernier.App.Platform;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>
/// Measures the overlay on the real desktop: how long a mark takes to reach the screen, how far
/// a mark takes to draw when the glass already holds ink, and what the glass costs while idle. Opt-in (VERNIER_BENCH=1)
/// because it takes over the screen for a minute. Numbers are printed, and recorded in docs/VERIFICATION_REPORT.md.
/// </summary>
[Collection("desktop")]
public class OverlayBenchmarks(ITestOutputHelper output)
{
    private static bool Enabled => Environment.GetEnvironmentVariable("VERNIER_BENCH") == "1";

    private static bool IsColour(PxPoint at, Func<byte, byte, byte, bool> match)
    {
        var shot = ScreenCaptureService.Grab(new PxRect(at.X - 1, at.Y - 1, 3, 3));
        var (r, g, b) = ScreenCaptureService.PixelAt(shot, 1, 1);
        return match(r, g, b);
    }

    // Polls from a worker thread so the probe never competes with the UI thread it is measuring.
    private static Task<double> UntilSeen(Stopwatch clock, PxPoint at, Func<byte, byte, byte, bool> match, int timeoutMs = 2000) => Task.Run(() =>
    {
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            if (IsColour(at, match)) return clock.Elapsed.TotalMilliseconds;
            Thread.Sleep(1);
        }
        return double.NaN;
    });

    private static string Stats(List<double> ms)
    {
        var ok = ms.Where(m => !double.IsNaN(m)).OrderBy(m => m).ToList();
        if (ok.Count == 0) return "never seen";
        double P(double q) => ok[Math.Min(ok.Count - 1, (int)Math.Floor(q * ok.Count))];
        return $"median {P(0.5):0.0} ms, p95 {P(0.95):0.0} ms, max {ok[^1]:0.0} ms, missed {ms.Count - ok.Count}/{ms.Count}";
    }


    [Fact]
    public void Overlay_latency_and_cost() => WindowsRealApiTests.OnSta(async () =>
    {
        if (!Enabled) return;
        var monitor = Win32.Monitors().First(m => m.IsPrimary);
        var page = WindowsRealApiTests.Show(new System.Windows.Controls.Border { Background = System.Windows.Media.Brushes.White }, 900, 700, "Vernier bench page");
        var store = new AnnotationStore();
        var glass = new AnnotationSurfaceWindow(monitor, store);
        glass.Show();
        try
        {
            await Task.Delay(500);
            var b = Win32.WindowBounds(new System.Windows.Interop.WindowInteropHelper(page).Handle)!.Value;
            var origin = new PxPoint(b.X + 100, b.Y + 150);

            // 1. A mark: from "draw this" to its pixels on screen. Every frame redraws the whole glass, so the
            //    cost grows with what is already there; measured on an empty glass and on one holding about
            //    ten minutes of use (twenty long pen strokes and ten labelled tutor marks).
            // Source "bench" draws without the entrance fade, so this is pure render latency.
            async Task<string> MarkRun(string label, string source = "bench")
            {
                var marks = new List<double>();
                for (var i = 0; i < 15; i++)
                {
                    var probe = new Annotation { Kind = AnnotationKind.Highlight, Points = [origin, new(origin.X + 300, origin.Y + 120)], Color = "#FF00FF", StrokeWidth = 6, Source = source };
                    var clock = Stopwatch.StartNew();
                    store.Add(probe);
                    glass.Redraw(); // what AnnotationOverlayManager does on every store change
                    marks.Add(await UntilSeen(clock, new PxPoint(origin.X + 150, origin.Y), (r, g, bl) => r > 200 && g < 80 && bl > 200));
                    store.Remove(probe.Id);
                    glass.Redraw();
                    await Task.Delay(150);
                }
                return $"mark → visible, {label}: " + Stats(marks);
            }
            output.WriteLine(await MarkRun("empty glass"));
            output.WriteLine(await MarkRun("empty glass, tutor mark fully faded in", "tutor"));
            for (var s = 0; s < 20; s++)
                store.Add(new Annotation { Kind = AnnotationKind.Freehand, Color = AnnotationColors.Learner, StrokeWidth = 4, Source = "user",
                    // Simplified on arrival exactly as the overlay does with a finished pen stroke.
                    Points = PolylineMath.Simplify(Enumerable.Range(0, 1500).Select(k => new PxPoint(b.X + 50 + k * 0.5, b.Y + 330 + s * 12 + Math.Sin(k / 20.0) * 5)).ToArray(), 0.75) });
            for (var m = 0; m < 10; m++)
                store.Add(new Annotation { Kind = AnnotationKind.Circle, Color = "#2F9BE6", Label = "mark " + m, Source = "tutor",
                    Points = [new(b.X + 450 + m * 20, b.Y + 30), new(b.X + 500 + m * 20, b.Y + 70)] });
            output.WriteLine(await MarkRun("30k ink points already on the glass"));
            store.Clear();
            glass.Redraw();

            // 3. What the glass costs: CPU while idle with marks on screen.
            store.Add(new Annotation { Kind = AnnotationKind.Circle, Points = [origin, new(origin.X + 200, origin.Y + 100)], Color = "#2F9BE6", Label = "F = ma", Source = "tutor" });
            await Task.Delay(300);
            var p = Process.GetCurrentProcess();
            var cpu0 = p.TotalProcessorTime;
            var wall = Stopwatch.StartNew();
            await Task.Delay(2000);
            p.Refresh();
            output.WriteLine($"idle with marks: {(p.TotalProcessorTime - cpu0).TotalMilliseconds / wall.Elapsed.TotalMilliseconds * 100 / Environment.ProcessorCount:0.00}% of the machine");

            // 4. One full-monitor capture as the ask path does it (on the UI thread today).
            var capture = new ScreenCaptureService();
            var times = new List<double>();
            for (var i = 0; i < 5; i++)
            {
                var clock = Stopwatch.StartNew();
                capture.Capture(new CaptureTarget.Screen(monitor.BoundsPhysical, 0), 1568);
                times.Add(clock.Elapsed.TotalMilliseconds);
            }
            output.WriteLine("full-monitor capture (blocks its thread): " + Stats(times));
        }
        finally
        {
            glass.Close();
            page.Close();
        }
    });
}
