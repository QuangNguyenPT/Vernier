using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vernier.Core.Agents;
using Vernier.Core.Geometry;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Vernier.App.Platform;

public abstract record CaptureTarget
{
    public sealed record None : CaptureTarget;
    public sealed record Region(PxRect Physical) : CaptureTarget;
    public sealed record Window(nint Handle, string Title) : CaptureTarget;
    /// <summary>A whole monitor. <paramref name="Foreground"/> is the application in front, used for its text and its name.</summary>
    public sealed record Screen(PxRect Monitor, nint Foreground) : CaptureTarget;
}

/// <summary>GDI screen capture in physical pixels. Callers are responsible for consent; this class only copies pixels.</summary>
public sealed class ScreenCaptureService
{
    /// <summary>Copies a rectangle of the virtual desktop. Windows with capture exclusion are left out by the OS.</summary>
    public static BitmapSource Grab(PxRect physical)
    {
        int x = (int)Math.Round(physical.X), y = (int)Math.Round(physical.Y);
        int w = Math.Max(1, (int)Math.Round(physical.Width)), h = Math.Max(1, (int)Math.Round(physical.Height));
        var screen = Win32.GetDC(0);
        var mem = Win32.CreateCompatibleDC(screen);
        var bmp = Win32.CreateCompatibleBitmap(screen, w, h);
        var old = Win32.SelectObject(mem, bmp);
        try
        {
            if (!Win32.BitBlt(mem, 0, 0, w, h, screen, x, y, Win32.SRCCOPY | Win32.CAPTUREBLT))
                throw new InvalidOperationException("The screen could not be copied (BitBlt failed). A secure desktop or a locked session blocks capture.");
            var source = Imaging.CreateBitmapSourceFromHBitmap(bmp, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            Win32.SelectObject(mem, old);
            Win32.DeleteObject(bmp);
            Win32.DeleteDC(mem);
            Win32.ReleaseDC(0, screen);
        }
    }

    /// <param name="ink">Strokes the learner drew, in physical pixels. They are painted into the image so the model sees them as the learner did.</param>
    public CapturedFrame? Capture(CaptureTarget target, int maxEdge, IReadOnlyList<IReadOnlyList<PxPoint>>? ink = null)
    {
        PxRect? area = target switch
        {
            CaptureTarget.Region r => r.Physical,
            CaptureTarget.Window w => Win32.WindowBounds(w.Handle),
            CaptureTarget.Screen sc => sc.Monitor,
            _ => null,
        };
        if (area is not { } rect || rect.IsEmpty) return null;
        rect = rect.Intersect(Win32.VirtualDesktop());
        if (rect.Width < 8 || rect.Height < 8) return null;

        var clean = Grab(rect);
        var strokes = (ink ?? []).Where(st => st.Count > 1).ToList();
        var full = strokes.Count > 0 ? PaintInk(clean, rect, strokes) : clean;
        var (iw, ih) = DpiMath.FitWithin(full.PixelWidth, full.PixelHeight, maxEdge);
        var scaled = Scale(full, iw, ih);

        nint hwnd = target switch { CaptureTarget.Window win => win.Handle, CaptureTarget.Screen sc => sc.Foreground, _ => 0 };
        string? process = null;
        if (hwnd != 0)
        {
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            try { process = Process.GetProcessById((int)pid).ProcessName; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { }
        }
        // The geometry records the size actually encoded, so model coordinates map back exactly.
        var geometry = new CaptureGeometry(rect, scaled.PixelWidth, scaled.PixelHeight);
        var frame = new CapturedFrame(Png(scaled), "image/png", geometry, Hash(clean),
            hwnd != 0 ? Win32.WindowTitle(hwnd) : null, process, hwnd, DateTimeOffset.UtcNow) { Thumb = Gray(clean, 256, 160), Raw = Raw(clean) };
        if (hwnd != 0 && Win32.WindowBounds(hwnd) is { } wb && !wb.Intersect(rect).IsEmpty)
        {
            var w = wb.Intersect(rect);
            var tl = geometry.PhysicalToImage(new PxPoint(w.X, w.Y));
            frame = frame with { WindowBox = new PxRect(tl.X, tl.Y, w.Width / geometry.ScaleX, w.Height / geometry.ScaleY) };
        }
        if (strokes.Count == 0) return frame;

        // The marked area, padded so its surroundings are visible, cropped from the un-inked capture at full resolution.
        var pts = strokes.SelectMany(st => st).ToList();
        double l = pts.Min(p => p.X), t = pts.Min(p => p.Y), r2 = pts.Max(p => p.X), b2 = pts.Max(p => p.Y);
        var pad = Math.Max(48, 0.15 * Math.Max(r2 - l, b2 - t));
        var marked = new PxRect(l - pad, t - pad, r2 - l + 2 * pad, b2 - t + 2 * pad).Intersect(rect);
        if (marked.Width < 8 || marked.Height < 8) return frame;
        var crop = new CroppedBitmap(clean, new Int32Rect((int)(marked.X - rect.X), (int)(marked.Y - rect.Y),
            Math.Min((int)marked.Width, clean.PixelWidth - (int)(marked.X - rect.X)), Math.Min((int)marked.Height, clean.PixelHeight - (int)(marked.Y - rect.Y))));
        var (cw, ch) = DpiMath.FitWithin(crop.PixelWidth, crop.PixelHeight, maxEdge);
        var topLeft = geometry.PhysicalToImage(new PxPoint(marked.X, marked.Y));
        return frame with
        {
            MarkedRegion = new PxRect(topLeft.X, topLeft.Y, marked.Width / geometry.ScaleX, marked.Height / geometry.ScaleY),
            // A crop covering most of the screen adds nothing the first image does not already show.
            MarkedDetail = marked.Width * marked.Height < 0.5 * rect.Width * rect.Height ? Png(Scale(crop, cw, ch)) : null,
        };
    }

    private static BitmapSource Scale(BitmapSource source, int width, int height) =>
        width == source.PixelWidth ? source : new TransformedBitmap(source, new ScaleTransform((double)width / source.PixelWidth, (double)height / source.PixelHeight));

    private static BitmapSource PaintInk(BitmapSource screen, PxRect origin, IReadOnlyList<IReadOnlyList<PxPoint>> strokes)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(screen, new Rect(0, 0, screen.PixelWidth, screen.PixelHeight));
            var halo = new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 9) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(Vernier.Core.Annotations.AnnotationColors.Learner)), 5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            foreach (var p in new[] { halo, pen })
                foreach (var stroke in strokes)
                    for (var i = 1; i < stroke.Count; i++)
                        dc.DrawLine(p, new Point(stroke[i - 1].X - origin.X, stroke[i - 1].Y - origin.Y), new Point(stroke[i].X - origin.X, stroke[i].Y - origin.Y));
        }
        var target = new RenderTargetBitmap(screen.PixelWidth, screen.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    /// <summary>A small grayscale picture of a screen area (size x size bytes), for comparing "then" with "now".</summary>
    public static byte[] Thumbnail(PxRect physical, int size) => Gray(Grab(physical), size, size);

    /// <summary>
    /// Whether an area of the screen looks substantially different now from the same area in a
    /// capture's pixels. Coarse on purpose: the tutor's own marks or a blinking caret do not count;
    /// a scroll, a zoom or a new video frame does.
    /// </summary>
    public static bool HasChanged(CapturedFrame frame, PxRect physical)
    {
        if (frame.Raw is not { } raw) return false;
        var source = frame.Geometry.SourcePhysical;
        var area = physical.Intersect(source);
        if (area.Width < 8 || area.Height < 8) return false;
        var rx = raw.Width / source.Width;
        var ry = raw.Height / source.Height;
        var then = BitmapSource.Create(raw.Width, raw.Height, 96, 96, PixelFormats.Bgra32, null, raw.Bgra, raw.Width * 4);
        var crop = new CroppedBitmap(then, Clip(new Int32Rect((int)((area.X - source.X) * rx), (int)((area.Y - source.Y) * ry),
            Math.Max(1, (int)(area.Width * rx)), Math.Max(1, (int)(area.Height * ry))), raw.Width, raw.Height));
        byte[] now;
        try { now = Thumbnail(area, 32); }
        catch (InvalidOperationException) { return false; } // capture refused: cannot tell, so do not block the mark
        return ScreenDiff.Fraction(Gray(crop, 32, 32), now, 32) > 0.35;
    }

    private static Int32Rect Clip(Int32Rect r, int width, int height)
    {
        var x = Math.Clamp(r.X, 0, width - 1);
        var y = Math.Clamp(r.Y, 0, height - 1);
        return new Int32Rect(x, y, Math.Clamp(r.Width, 1, width - x), Math.Clamp(r.Height, 1, height - y));
    }

    /// <summary>The capture's own pixels, BGRA, for OCR at full resolution.</summary>
    public static RawImage Raw(BitmapSource source)
    {
        var bgra = source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Bgr32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = bgra.PixelWidth * 4;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        return new RawImage(pixels, bgra.PixelWidth, bgra.PixelHeight);
    }

    private static byte[] Gray(BitmapSource source, int width, int height)
    {
        var gray = new FormatConvertedBitmap(new TransformedBitmap(source, new ScaleTransform((double)width / source.PixelWidth, (double)height / source.PixelHeight)), PixelFormats.Gray8, null, 0);
        var buffer = new byte[gray.PixelWidth * gray.PixelHeight];
        gray.CopyPixels(buffer, gray.PixelWidth, 0);
        return buffer;
    }

    /// <summary>Decodes a picture the learner attached and re-encodes it as a PNG no larger than <paramref name="maxEdge"/>.</summary>
    public static byte[] NormalizePicture(byte[] data, int maxEdge)
    {
        using var ms = new MemoryStream(data);
        var frame = System.Windows.Media.Imaging.BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var (w, h) = DpiMath.FitWithin(frame.PixelWidth, frame.PixelHeight, maxEdge);
        return Png(Scale(frame, w, h));
    }

    public static byte[] Png(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    public static ulong Hash(BitmapSource source)
    {
        var small = new TransformedBitmap(source, new ScaleTransform(9.0 / source.PixelWidth, 8.0 / source.PixelHeight));
        var gray = new FormatConvertedBitmap(small, PixelFormats.Gray8, null, 0);
        var w = Math.Min(9, gray.PixelWidth);
        var h = Math.Min(8, gray.PixelHeight);
        var buffer = new byte[gray.PixelWidth * gray.PixelHeight];
        gray.CopyPixels(buffer, gray.PixelWidth, 0);
        var thumb = new byte[72];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                thumb[y * 9 + x] = buffer[y * gray.PixelWidth + x];
        return PerceptualHash.DHash(thumb);
    }

    public static (byte R, byte G, byte B) PixelAt(BitmapSource source, int x, int y)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var px = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return (px[2], px[1], px[0]);
    }
}

/// <summary>
/// Reads text from the target window's accessibility tree. Preferred over OCR: exact characters,
/// no recognition error. Only used for whole-window targets; password fields are skipped.
/// </summary>
public sealed class UiaTextExtractor : ITextExtractor
{
    private const int MaxNodes = 500;
    private const int MaxChars = 12000;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    public string Provenance => "accessibility";

    public Task<IReadOnlyList<ExtractedText>> ExtractAsync(CapturedFrame frame, CancellationToken ct)
    {
        if (frame.WindowHandle == 0) return Task.FromResult<IReadOnlyList<ExtractedText>>([]);
        return Task.Run<IReadOnlyList<ExtractedText>>(() =>
        {
            var results = new List<ExtractedText>();
            var seen = new HashSet<string>();
            var clock = Stopwatch.StartNew();
            var total = 0;
            var nodes = 0;
            AutomationElement root;
            try { root = AutomationElement.FromHandle((nint)frame.WindowHandle); }
            catch (Exception e) when (e is ElementNotAvailableException or ArgumentException or System.Runtime.InteropServices.COMException) { return results; }

            var pending = new Stack<AutomationElement>();
            pending.Push(root);
            while (pending.Count > 0 && nodes++ < MaxNodes && total < MaxChars && clock.Elapsed < Budget)
            {
                ct.ThrowIfCancellationRequested();
                var el = pending.Pop();
                try
                {
                    if (el.Current.IsPassword) continue;
                    string? text = null;
                    if (el.TryGetCurrentPattern(TextPattern.Pattern, out var tp))
                        text = ((TextPattern)tp).DocumentRange.GetText(4000);
                    else if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp))
                        text = ((ValuePattern)vp).Current.Value;
                    if (string.IsNullOrWhiteSpace(text)) text = el.Current.Name;
                    text = text?.Trim();
                    if (!string.IsNullOrEmpty(text) && text.Length > 1 && seen.Add(text))
                    {
                        results.Add(new ExtractedText(text, null, 0.95, Provenance));
                        total += text.Length;
                    }
                    var child = TreeWalker.ControlViewWalker.GetLastChild(el);
                    while (child is not null)
                    {
                        pending.Push(child);
                        child = TreeWalker.ControlViewWalker.GetPreviousSibling(child);
                    }
                }
                catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
            }
            return results;
        }, ct);
    }
}

/// <summary>Windows built-in OCR (Windows.Media.Ocr). Runs locally. The engine reports no per-word confidence.</summary>
public sealed class OcrTextExtractor : ITextExtractor
{
    /// <summary>The engine gives no confidence figure, so a fixed, deliberately modest one is reported.</summary>
    public const double AssumedConfidence = 0.7;

    public string Provenance => "ocr";

    public async Task<IReadOnlyList<ExtractedText>> ExtractAsync(CapturedFrame frame, CancellationToken ct)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException("No Windows OCR language pack is installed.");
        // Full resolution when the engine accepts it: small text and subscripts survive. Boxes are reported in screenshot pixels either way.
        SoftwareBitmap bitmap;
        double toImage;
        if (frame.Raw is { } raw && raw.Width <= OcrEngine.MaxImageDimension && raw.Height <= OcrEngine.MaxImageDimension)
        {
            bitmap = SoftwareBitmap.CreateCopyFromBuffer(raw.Bgra.AsBuffer(), BitmapPixelFormat.Bgra8, raw.Width, raw.Height, BitmapAlphaMode.Premultiplied);
            toImage = (double)frame.Geometry.ImageWidth / raw.Width;
        }
        else
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(frame.ImageBytes.AsBuffer()).AsTask(ct);
            stream.Seek(0);
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream).AsTask(ct);
            if (decoder.PixelWidth > OcrEngine.MaxImageDimension || decoder.PixelHeight > OcrEngine.MaxImageDimension) return [];
            bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied).AsTask(ct);
            toImage = 1;
        }
        OcrResult result;
        using (bitmap) result = await engine.RecognizeAsync(bitmap).AsTask(ct);

        PxRect Box(Windows.Foundation.Rect r) => new(r.X * toImage, r.Y * toImage, r.Width * toImage, r.Height * toImage);
        var lines = new List<ExtractedText>();
        foreach (var line in result.Lines)
        {
            if (line.Words.Count == 0) continue;
            double l = line.Words.Min(w => w.BoundingRect.Left), t = line.Words.Min(w => w.BoundingRect.Top);
            double r = line.Words.Max(w => w.BoundingRect.Right), b = line.Words.Max(w => w.BoundingRect.Bottom);
            lines.Add(new ExtractedText(line.Text, Box(new Windows.Foundation.Rect(l, t, r - l, b - t)), AssumedConfidence, Provenance)
            {
                Words = [.. line.Words.Select(w => new ExtractedText(w.Text, Box(w.BoundingRect), AssumedConfidence, Provenance))],
            });
        }
        return lines;
    }
}

/// <summary>
/// Named, visible controls of the window in front, with their boxes, from the accessibility
/// tree in one cached query. They let the tutor point at "the Extrude button" exactly.
/// Password fields and off-screen elements are left out.
/// </summary>
public sealed class UiaElementExtractor : ITextExtractor
{
    private const int MaxElements = 300;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1.5);

    public string Provenance => "accessibility";
    public bool ProvidesText => false;

    public async Task<IReadOnlyList<ExtractedText>> ExtractAsync(CapturedFrame frame, CancellationToken ct)
    {
        if (frame.WindowHandle == 0) return [];
        // UIA cannot be cancelled mid-query; a slow tree (a huge web page) is abandoned rather than waited for.
        var work = Task.Run(() => Collect(frame), CancellationToken.None);
        try { return await work.WaitAsync(Budget, ct).ConfigureAwait(false); }
        catch (TimeoutException) { return []; }
    }

    private static IReadOnlyList<ExtractedText> Collect(CapturedFrame frame)
    {
        var results = new List<ExtractedText>();
        try
        {
            var root = AutomationElement.FromHandle((nint)frame.WindowHandle);
            var cache = new CacheRequest { TreeScope = TreeScope.Element, AutomationElementMode = AutomationElementMode.None };
            cache.Add(AutomationElement.NameProperty);
            cache.Add(AutomationElement.BoundingRectangleProperty);
            cache.Add(AutomationElement.ControlTypeProperty);
            cache.Add(AutomationElement.IsPasswordProperty);
            AutomationElementCollection all;
            using (cache.Activate())
                all = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsOffscreenProperty, false));
            var source = frame.Geometry.SourcePhysical;
            foreach (AutomationElement el in all)
            {
                if (results.Count >= MaxElements) break;
                var name = (el.Cached.Name ?? "").Trim();
                if (name.Length is 0 or > 120 || el.Cached.IsPassword) continue;
                var r = el.Cached.BoundingRectangle;
                if (r.IsEmpty || r.Width < 2 || r.Height < 2) continue;
                var physical = new PxRect(r.X, r.Y, r.Width, r.Height).Intersect(source);
                if (physical.IsEmpty) continue;
                var tl = frame.Geometry.PhysicalToImage(new PxPoint(physical.X, physical.Y));
                var role = el.Cached.ControlType?.ProgrammaticName.Replace("ControlType.", "").ToLowerInvariant();
                results.Add(new ExtractedText(name, new PxRect(tl.X, tl.Y, physical.Width / frame.Geometry.ScaleX, physical.Height / frame.Geometry.ScaleY), 0.97, "accessibility") { Role = role });
            }
        }
        catch (Exception e) when (e is ElementNotAvailableException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        return results;
    }
}

public sealed record CaptureExclusionResult(string Path, string Expected, string Observed, bool? Excluded);

/// <summary>
/// Real-OS check: shows a window filled with a marker colour, captures the screen there with and
/// without WDA_EXCLUDEFROMCAPTURE, and reports what each capture path actually returned.
/// Must run on an STA thread with a dispatcher.
/// </summary>
public static class CaptureExclusionSelfTest
{
    private static readonly Color Marker = Color.FromRgb(255, 0, 255);

    public static async Task<IReadOnlyList<CaptureExclusionResult>> RunAsync()
    {
        var results = new List<CaptureExclusionResult>();
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false, ShowActivated = false,
            Width = 160, Height = 120, Left = 40, Top = 40, Background = new SolidColorBrush(Marker), Title = "Vernier capture self-test",
        };
        window.Show();
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            await Task.Delay(400);
            var bounds = Win32.WindowBounds(hwnd) ?? throw new InvalidOperationException("The test window has no bounds.");
            var probe = new PxRect(bounds.X + bounds.Width / 2 - 4, bounds.Y + bounds.Height / 2 - 4, 8, 8);

            var before = IsMarker(ScreenCaptureService.Grab(probe));
            results.Add(new("GDI BitBlt of the screen, standard window", "marker colour visible", before ? "marker colour visible" : "marker colour NOT visible (test window was covered)", null));

            if (!Win32.SetWindowDisplayAffinity(hwnd, Vernier.Core.Windowing.DisplayAffinity.ExcludeFromCapture))
            {
                results.Add(new("SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)", "call succeeds", $"failed, Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}", false));
                return results;
            }
            Win32.GetWindowDisplayAffinity(hwnd, out var effective);
            results.Add(new("GetWindowDisplayAffinity read-back", "0x11", $"0x{effective:X2}", effective == 0x11));
            await Task.Delay(400); // let the compositor present a frame with the new policy

            var after = IsMarker(ScreenCaptureService.Grab(probe));
            results.Add(new("GDI BitBlt of the screen, exclusion requested", "marker colour absent", after ? "marker colour STILL visible" : "marker colour absent",
                before ? !after : null));

            results.Add(new("PrintWindow(PW_RENDERFULLCONTENT) of the window itself", "informational", PrintWindowShowsMarker(hwnd, bounds) ? "marker colour returned" : "marker colour not returned", null));
        }
        finally
        {
            window.Close();
        }
        return results;
    }

    private static bool IsMarker(BitmapSource s)
    {
        var (r, g, b) = ScreenCaptureService.PixelAt(s, s.PixelWidth / 2, s.PixelHeight / 2);
        return r > 240 && g < 15 && b > 240;
    }

    private static bool PrintWindowShowsMarker(nint hwnd, PxRect bounds)
    {
        int w = (int)bounds.Width, h = (int)bounds.Height;
        var screen = Win32.GetDC(0);
        var mem = Win32.CreateCompatibleDC(screen);
        var bmp = Win32.CreateCompatibleBitmap(screen, w, h);
        var old = Win32.SelectObject(mem, bmp);
        try
        {
            if (!Win32.PrintWindow(hwnd, mem, Win32.PW_RENDERFULLCONTENT)) return false;
            var source = Imaging.CreateBitmapSourceFromHBitmap(bmp, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var (r, g, b) = ScreenCaptureService.PixelAt(source, w / 2, h / 2);
            return r > 240 && g < 15 && b > 240;
        }
        finally
        {
            Win32.SelectObject(mem, old);
            Win32.DeleteObject(bmp);
            Win32.DeleteDC(mem);
            Win32.ReleaseDC(0, screen);
        }
    }
}
