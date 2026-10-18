using System.Runtime.InteropServices;
using Vernier.Core.Geometry;

namespace Vernier.App.Platform;

/// <summary>
/// Vernier.Native.dll (C++, built by scripts/build-native.ps1). Optional: when the DLL is missing or
/// a call fails, callers use the managed path. <see cref="Available"/> is checked once.
/// </summary>
public static partial class Native
{
    private const string Dll = "Vernier.Native.dll";

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FrameInfo
    {
        public int Width, Height, DirtyCount;
        public Rect DirtyBounds;
        public double DirtyFraction;
        public int Accumulated;
        public long PresentQpc;
    }

    public const int Ok = 0, Timeout = 1, AccessLost = -2, Flat = 2;

    /// <summary>The ABI this build speaks (VN_ABI in vernier_native.h). A DLL reporting any other number is not used.</summary>
    public const int Abi = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct MatchResult { public int X, Y; public double Score, Second; }

    [LibraryImport(Dll)] private static partial int vn_version();
    [LibraryImport(Dll)] public static partial int vn_dup_open(int left, int top, out nint handle);
    [LibraryImport(Dll)] public static partial int vn_dup_next(nint handle, int timeoutMs, out FrameInfo info);
    [LibraryImport(Dll)] public static unsafe partial int vn_dup_copy(nint handle, Rect region, byte* dst, int stride);
    [LibraryImport(Dll)] public static unsafe partial int vn_dup_thumb(nint handle, Rect region, int w, int h, byte* gray);
    [LibraryImport(Dll)] public static partial void vn_dup_close(nint handle);
    [LibraryImport(Dll)] private static unsafe partial int vn_png_encode(byte* bgra, int width, int height, int stride, int maxEdge, out nint png, out int length, out int outWidth, out int outHeight);
    [LibraryImport(Dll)] private static partial void vn_free(nint p);
    [LibraryImport(Dll)] public static partial int vn_glow_create(Rect monitor, int thicknessPx, uint argb, out nint handle);
    [LibraryImport(Dll)] public static partial int vn_glow_set(nint handle, int mode, int reducedMotion);
    [LibraryImport(Dll)] public static partial nint vn_glow_hwnd(nint handle);
    [LibraryImport(Dll)] public static partial long vn_glow_frames(nint handle);
    [LibraryImport(Dll)] public static partial void vn_glow_destroy(nint handle);
    [LibraryImport(Dll)] public static partial int vn_ink_create(Rect monitor, uint argb, int widthPx, nint callback, nint context, out nint handle);
    [LibraryImport(Dll)] public static partial int vn_ink_start(nint handle, int watchButton);
    [LibraryImport(Dll)] public static partial int vn_ink_stop(nint handle);
    [LibraryImport(Dll)] public static unsafe partial int vn_ink_feed(nint handle, int* xy, int count);
    [LibraryImport(Dll)] public static partial int vn_ink_clear(nint handle);
    [LibraryImport(Dll)] public static partial int vn_ink_timing(nint handle, int holdMs, int fadeMs);
    [LibraryImport(Dll)] public static partial nint vn_ink_hwnd(nint handle);
    [LibraryImport(Dll)] public static partial long vn_ink_segments(nint handle);
    [LibraryImport(Dll)] public static partial int vn_ink_error(nint handle);
    [LibraryImport(Dll)] public static partial void vn_ink_destroy(nint handle);
    [LibraryImport(Dll)] public static unsafe partial int vn_match(byte* image, int iw, int ih, byte* patch, int pw, int ph, Rect search, out MatchResult result);

    private static readonly Lazy<bool> Loaded = new(() =>
    {
        try { return (Version = vn_version()) == Abi; } // an older or newer DLL beside the exe is ignored, not half-used
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    });

    /// <summary>The native module is present and loads. False means: use the managed paths.</summary>
    public static bool Available => Loaded.Value;

    /// <summary>What the DLL reported (0 when it did not load): diagnostics and the smoke test.</summary>
    public static int Version { get; private set; }

    public static Rect From(PxRect r) => new() { Left = (int)Math.Round(r.X), Top = (int)Math.Round(r.Y), Right = (int)Math.Round(r.Right), Bottom = (int)Math.Round(r.Bottom) };

    /// <summary>The result code of the last native call that failed (diagnostics).</summary>
    public static int LastError { get; private set; }

    /// <summary>WIC Fant/cubic downscale + PNG encode of BGRA pixels. Null when the native module cannot do it.</summary>
    public static unsafe (byte[] Png, int Width, int Height)? EncodePng(byte[] bgra, int width, int height, int maxEdge)
    {
        if (!Available) return null;
        fixed (byte* p = bgra)
        {
            LastError = vn_png_encode(p, width, height, width * 4, maxEdge, out var png, out var length, out var w, out var h);
            if (LastError != Ok) return null;
            try
            {
                var bytes = new byte[length];
                Marshal.Copy(png, bytes, 0, length);
                return (bytes, w, h);
            }
            finally { vn_free(png); }
        }
    }
}

/// <summary>
/// DXGI Desktop Duplication of one monitor: the OS reports which rectangles changed in each frame,
/// and the frame stays on the GPU until a region or thumbnail is asked for. Recreates itself after
/// access is lost (mode change, secure desktop). Not thread-safe across callers; one owner.
/// </summary>
public sealed class DuplicationSession : IDisposable
{
    private nint _handle;
    private readonly PxRect _monitor;

    private DuplicationSession(PxRect monitor, nint handle) { _monitor = monitor; _handle = handle; }

    public PxRect Monitor => _monitor;
    /// <summary>At least one frame has arrived, so Copy and Thumb have something to read.</summary>
    public bool HasFrame { get; private set; }

    /// <summary>Null when duplication is unavailable here (no native module, remote session, unsupported driver).</summary>
    public static DuplicationSession? TryOpen(PxRect monitor)
    {
        if (!Native.Available) return null;
        return Native.vn_dup_open((int)monitor.X, (int)monitor.Y, out var h) == Native.Ok ? new DuplicationSession(monitor, h) : null;
    }

    /// <summary>
    /// Waits for the next frame with new pixels. Returns the frame info, or null when nothing changed within the timeout.
    /// Throws <see cref="InvalidOperationException"/> when duplication is lost and cannot be reopened.
    /// </summary>
    public Native.FrameInfo? Next(int timeoutMs)
    {
        if (_handle == 0 && !Reopen()) throw new InvalidOperationException("Desktop duplication is not available right now.");
        var r = Native.vn_dup_next(_handle, timeoutMs, out var info);
        if (r == Native.Ok) { HasFrame = true; return info; }
        if (r == Native.Timeout) return null;
        if (r == Native.AccessLost && Reopen()) return null; // the next frame after reopening carries the whole screen
        throw new InvalidOperationException($"Desktop duplication failed (0x{r:X8}).");
    }

    private bool Reopen()
    {
        if (_handle != 0) { Native.vn_dup_close(_handle); _handle = 0; }
        HasFrame = false;
        if (Native.vn_dup_open((int)_monitor.X, (int)_monitor.Y, out var h) != Native.Ok) return false;
        _handle = h;
        return true;
    }

    /// <summary>BGRA pixels of a physical-pixel region of the latest frame. Null if no frame has arrived yet.</summary>
    public unsafe byte[]? Copy(PxRect physical)
    {
        var r = Native.From(new PxRect(physical.X - _monitor.X, physical.Y - _monitor.Y, physical.Width, physical.Height));
        var w = r.Right - r.Left;
        var h = r.Bottom - r.Top;
        if (_handle == 0 || w <= 0 || h <= 0) return null;
        var buffer = new byte[w * h * 4];
        fixed (byte* p = buffer)
            return Native.vn_dup_copy(_handle, r, p, w * 4) == Native.Ok ? buffer : null;
    }

    /// <summary>Grayscale thumbnail (w*h bytes) of the latest frame, or of one physical region of it.</summary>
    public unsafe byte[]? Thumb(int w, int h, PxRect? physical = null)
    {
        if (_handle == 0) return null;
        var r = physical is { } p ? Native.From(new PxRect(p.X - _monitor.X, p.Y - _monitor.Y, p.Width, p.Height)) : default;
        var gray = new byte[w * h];
        fixed (byte* g = gray)
            return Native.vn_dup_thumb(_handle, r, w, h, g) == Native.Ok ? gray : null;
    }

    public void Dispose()
    {
        if (_handle != 0) Native.vn_dup_close(_handle);
        _handle = 0;
    }
}

/// <summary>The native capture light for one monitor: composited by DWM, animated on its own thread.</summary>
public sealed class NativeGlow : IDisposable
{
    private nint _handle;

    private NativeGlow(nint handle) => _handle = handle;

    public nint Hwnd => _handle == 0 ? 0 : Native.vn_glow_hwnd(_handle);
    public long Frames => _handle == 0 ? 0 : Native.vn_glow_frames(_handle);

    public static NativeGlow? TryCreate(PxRect monitor, int thicknessPx, uint argb)
    {
        if (!Native.Available) return null;
        return Native.vn_glow_create(Native.From(monitor), thicknessPx, argb, out var h) == Native.Ok ? new NativeGlow(h) : null;
    }

    /// <param name="mode">0 off, 1 steady, 2 travelling wave.</param>
    public void Set(int mode, bool reducedMotion)
    {
        if (_handle != 0) Native.vn_glow_set(_handle, mode, reducedMotion ? 1 : 0);
    }

    public void Dispose()
    {
        if (_handle != 0) Native.vn_glow_destroy(_handle);
        _handle = 0;
    }
}

/// <summary>
/// The pen's ink, drawn by the native module on its own thread straight into DWM: it follows the cursor without waiting for
/// WPF, and fades away by itself after the pen lifts. <see cref="StrokeEnded"/> reports the finished stroke (on the ink thread).
/// </summary>
public sealed class NativeInk : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StrokeCallback(nint xy, int count, nint context);

    private nint _handle;
    private readonly StrokeCallback _callback; // kept alive: native code holds its function pointer

    private NativeInk() => _callback = OnStroke;

    public event Action<PxPoint[]>? StrokeEnded;
    public nint Hwnd => _handle == 0 ? 0 : Native.vn_ink_hwnd(_handle);
    public long Segments => _handle == 0 ? 0 : Native.vn_ink_segments(_handle);
    /// <summary>The last HRESULT a native draw call returned (0 if none).</summary>
    public int LastError => _handle == 0 ? 0 : Native.vn_ink_error(_handle);

    public static NativeInk? TryCreate(PxRect monitor, uint argb, int widthPx)
    {
        if (!Native.Available) return null;
        var ink = new NativeInk();
        var pointer = Marshal.GetFunctionPointerForDelegate(ink._callback);
        if (Native.vn_ink_create(Native.From(monitor), argb, widthPx, pointer, 0, out var h) != Native.Ok) return null;
        ink._handle = h;
        return ink;
    }

    private void OnStroke(nint xy, int count, nint context)
    {
        var raw = new int[count * 2];
        Marshal.Copy(xy, raw, 0, raw.Length);
        var points = new PxPoint[count];
        for (var i = 0; i < count; i++) points[i] = new PxPoint(raw[2 * i], raw[2 * i + 1]);
        StrokeEnded?.Invoke(points);
    }

    /// <param name="watchButton">True for a mouse (the stroke ends when the button is released); false for pen and touch, ended with <see cref="Stop"/>.</param>
    public void Start(bool watchButton) { if (_handle != 0) Native.vn_ink_start(_handle, watchButton ? 1 : 0); }
    public void Stop() { if (_handle != 0) Native.vn_ink_stop(_handle); }

    /// <summary>Draws a stroke from the given points, as if the cursor had made it (tests and replays).</summary>
    public unsafe void Feed(IReadOnlyList<PxPoint> points)
    {
        if (_handle == 0 || points.Count < 2) return;
        var raw = new int[points.Count * 2];
        for (var i = 0; i < points.Count; i++) { raw[2 * i] = (int)points[i].X; raw[2 * i + 1] = (int)points[i].Y; }
        fixed (int* p = raw) Native.vn_ink_feed(_handle, p, points.Count);
    }
    public void Clear() { if (_handle != 0) Native.vn_ink_clear(_handle); }
    public void SetTiming(int holdMs, int fadeMs) { if (_handle != 0) Native.vn_ink_timing(_handle, holdMs, fadeMs); }

    public void Dispose()
    {
        if (_handle != 0) Native.vn_ink_destroy(_handle);
        _handle = 0;
    }
}

/// <summary>The native patch matcher (match.cpp); the managed <see cref="PatchMatcher"/> when the module is not there.</summary>
public sealed class NativePatchMatcher : IPatchMatcher
{
    public static IPatchMatcher Best => Native.Available ? new NativePatchMatcher() : PatchMatcher.Instance;

    public unsafe PatchMatch? Match(GrayImage image, GrayImage patch, int left, int top, int right, int bottom)
    {
        fixed (byte* i = image.Pixels)
        fixed (byte* p = patch.Pixels)
        {
            var code = Native.vn_match(i, image.Width, image.Height, p, patch.Width, patch.Height,
                new Native.Rect { Left = left, Top = top, Right = right, Bottom = bottom }, out var r);
            return code switch
            {
                Native.Ok => new PatchMatch(r.X, r.Y, r.Score, r.Second, false),
                Native.Flat => new PatchMatch(0, 0, 0, 0, true),
                _ => null,
            };
        }
    }
}
