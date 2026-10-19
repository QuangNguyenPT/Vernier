using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vernier.App;
using Vernier.App.Platform;
using Vernier.Core.Geometry;
using Vernier.Core.Annotations;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>
/// A tutor mark follows its content on a real scrolling window, with no question asked again: it moves when the page scrolls,
/// leaves the screen when its content scrolls out of the window, and comes back onto it when the content scrolls back.
/// </summary>
[Collection("desktop")]
public class FollowTests(ITestOutputHelper output)
{
    // A tall page of distinctive blocks (like text and diagrams), so every part of it can be recognised again.
    private static BitmapSource Page(int w, int h)
    {
        var rng = new Random(7);
        var px = new byte[w * h * 4];
        for (var by = 0; by < h; by += 12)
            for (var bx = 0; bx < w; bx += 12)
            {
                var v = (byte)rng.Next(0, 256);
                for (var y = by; y < Math.Min(h, by + 12); y++)
                    for (var x = bx; x < Math.Min(w, bx + 12); x++) { var k = (y * w + x) * 4; px[k] = px[k + 1] = px[k + 2] = v; px[k + 3] = 255; }
            }
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
    }

    private static async Task<T> Until<T>(Func<T> read, Func<T, bool> ok, int ms = 6000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        var value = read();
        while (!ok(value) && DateTime.UtcNow < until) { await Task.Delay(100); value = read(); }
        return value;
    }

    [Fact]
    public void A_mark_moves_with_a_scroll_leaves_with_its_content_and_comes_back_with_it() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = new Image { Source = Page(400, 2400), Stretch = Stretch.None } };
        var page = WindowsRealApiTests.Show(scroll, 420, 360, "Vernier scroll test");
        page.Left = 160; page.Top = 140;
        try
        {
            AppServices.Overlays.CanSample = () => true;
            AppServices.Overlays.Start();
            await Task.Delay(300);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(page).Handle;
            var b = Vernier.App.Platform.Win32.WindowBounds(hwnd)!.Value;
            var scale = b.Width / page.ActualWidth;
            AppServices.Overlays.Track(hwnd);
            var mark = new Annotation { Kind = AnnotationKind.Circle, Source = "tutor", Lifetime = TimeSpan.FromMinutes(2), Points = [new(b.X + 80, b.Y + 140), new(b.X + 220, b.Y + 190)] };
            AppServices.Annotations.Add(mark);
            AppServices.Overlays.WatchTutorMarks(Vernier.App.Platform.Win32.Monitors().First(m => m.BoundsPhysical.Contains(mark.Points[0])).BoundsPhysical);
            await Task.Delay(1500); // settles, takes its anchor and baseline

            Annotation? Now() => AppServices.Annotations.Items.FirstOrDefault(a => a.Id == mark.Id);
            // 1. A short scroll: the mark moves up with its content.
            scroll.ScrollToVerticalOffset(60);
            var expected = mark.Points[0].Y - 60 * scale;
            var moved = await Until(Now, a => a is not null && Math.Abs(a.Points[0].Y - expected) < 6);
            output.WriteLine($"after 60 DIP: {moved?.Points[0]} (expected y {expected:0})");
            Assert.NotNull(moved);
            Assert.InRange(moved!.Points[0].Y, expected - 6, expected + 6);

            // 2. Scrolled far: its content left the window, so the mark leaves the screen too (kept, not forgotten).
            scroll.ScrollToVerticalOffset(900);
            var gone = await Until(Now, a => a is null);
            Assert.Null(gone);

            // 3. Back to the top: the mark is put back on its content, without anyone asking again.
            scroll.ScrollToVerticalOffset(0);
            var back = await Until(Now, a => a is not null && Math.Abs(a.Points[0].Y - mark.Points[0].Y) < 6);
            output.WriteLine($"back: {back?.Points[0]} (expected y {mark.Points[0].Y:0})");
            Assert.NotNull(back);
            Assert.InRange(back!.Points[0].Y, mark.Points[0].Y - 6, mark.Points[0].Y + 6);

            // 4. Removed on purpose while parked (overlay_clear by id): it does not come back with its content.
            scroll.ScrollToVerticalOffset(900);
            Assert.Null(await Until(Now, a => a is null));
            AppServices.Overlays.Forget([mark.Id]);
            scroll.ScrollToVerticalOffset(0);
            await Task.Delay(2500);
            Assert.Null(Now());
        }
        finally { page.Close(); AppServices.Shutdown(); }
    });

    [Fact]
    public void A_real_mouse_drag_on_the_grip_moves_the_card_and_nothing_else() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        AppServices.Initialize(dir.Path);
        var saved = new Vernier.App.Platform.Win32.POINT();
        Vernier.App.Platform.Win32.GetCursorPos(out saved);
        try
        {
            AppServices.Overlays.Start();
            var area = Vernier.App.Platform.Win32.Monitors().First(m => m.IsPrimary).BoundsPhysical;
            var (x, y) = (area.X + 300, area.Y + 260);
            var card = new Annotation
            {
                Kind = AnnotationKind.Note, Points = [new(x, y), new(x + 400, y + 240)], Source = "tutor", Lifetime = TimeSpan.FromMinutes(1),
                Note = new NoteContent("Drag me", [new NoteLine(NoteLineKind.Step, "a line")], true),
            };
            AppServices.Annotations.Add(card);
            await Task.Delay(900); // the buttons fade in (a layered window lets clicks through while it is still transparent)
            var window = System.Windows.Application.Current.Windows.OfType<NoteDismissWindow>().Single(w => w.NoteId == card.Id);
            var b = Vernier.App.Platform.Win32.WindowBounds(new System.Windows.Interop.WindowInteropHelper(window).Handle)!.Value;
            var grip = new PxPoint(b.X + b.Height / 2, b.Y + b.Height / 2); // the left button
            var desktop = Vernier.App.Platform.Win32.VirtualDesktop();
            void Mouse(PxPoint at, uint flags)
            {
                var (ax, ay) = SendInputDriver.ToAbsolute(at, desktop);
                var input = new Vernier.App.Platform.Win32.INPUT { type = 0, u = { mi = new Vernier.App.Platform.Win32.MOUSEINPUT { dx = ax, dy = ay, dwFlags = flags | 0x0001 | 0x8000 | 0x4000 } } };
                Vernier.App.Platform.Win32.SendInput(1, [input], System.Runtime.InteropServices.Marshal.SizeOf<Vernier.App.Platform.Win32.INPUT>());
            }
            // Real input goes wherever the pointer is. If the card's buttons are not the window under it (the learner is using a
            // full-screen app in front, say), send nothing at all: a click there would land in their app.
            var under = Vernier.App.Platform.Win32.GetAncestor(Vernier.App.Platform.Win32.WindowFromPoint(new Vernier.App.Platform.Win32.POINT { X = (int)grip.X, Y = (int)grip.Y }), Vernier.App.Platform.Win32.GA_ROOT);
            if (under != new System.Windows.Interop.WindowInteropHelper(window).Handle)
            {
                output.WriteLine($"INCONCLUSIVE: another window ('{Vernier.App.Platform.Win32.WindowTitle(under)}') is over the card's buttons, so no input was sent.");
                return;
            }
            Mouse(grip, 0);
            await Task.Delay(120);
            Mouse(grip, 0x0002); // left down
            await Task.Delay(80);
            for (var i = 1; i <= 10; i++) { Mouse(new PxPoint(grip.X + 15 * i, grip.Y + 6 * i), 0); await Task.Delay(30); }
            Mouse(new PxPoint(grip.X + 150, grip.Y + 60), 0x0004); // left up
            await Task.Delay(200);
            var after = AppServices.Annotations.Items.Single(a => a.Id == card.Id);
            output.WriteLine($"card moved to {after.Points[0]} from ({x}, {y})");
            // The card follows the pointer (150, 60); a few pixels either way when the real mouse is also nudged during the run.
            Assert.InRange(after.Points[0].X - x, 135, 165);
            Assert.InRange(after.Points[0].Y - y, 45, 75);
        }
        finally
        {
            var (sx, sy) = SendInputDriver.ToAbsolute(new PxPoint(saved.X, saved.Y), Vernier.App.Platform.Win32.VirtualDesktop());
            Vernier.App.Platform.Win32.SendInput(1, [new Vernier.App.Platform.Win32.INPUT { type = 0, u = { mi = new Vernier.App.Platform.Win32.MOUSEINPUT { dx = sx, dy = sy, dwFlags = 0x0001 | 0x8000 | 0x4000 } } }], System.Runtime.InteropServices.Marshal.SizeOf<Vernier.App.Platform.Win32.INPUT>());
            AppServices.Shutdown();
        }
    });
}
