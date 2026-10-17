using System.Runtime.InteropServices;
using System.Text;
using Vernier.Core.Geometry;
using Vernier.Core.Security;
using Vernier.Core.Windowing;

namespace Vernier.App.Platform;

internal static class Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly PxRect ToPx() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    public delegate bool MonitorEnumProc(nint hMonitor, nint hdc, ref RECT rect, nint data);

    // --- window policy (documented: learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) ---
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowDisplayAffinity(nint hWnd, out uint pdwAffinity);

    // --- windows ---
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hWnd);
    // Out-of-context WinEvent hooks: the OS posts focus and title changes to this thread's message loop; nothing is injected anywhere.
    public delegate void WinEventProc(nint hook, uint ev, nint hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc proc, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(nint hook);
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_OBJECT_NAMECHANGE = 0x800C, WINEVENT_OUTOFCONTEXT = 0x0000, WINEVENT_SKIPOWNPROCESS = 0x0002;
    // Unlike GetWindowText, this never sends a message to the window, so a hung application cannot block us.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int InternalGetWindowText(nint hWnd, StringBuilder text, int max);
    [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public uint cbSize, dwTime; }
    [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    public static string SafeTitle(nint hwnd)
    {
        var sb = new StringBuilder(512);
        InternalGetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static TimeSpan SinceLastInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime)) : TimeSpan.Zero;
    }
    [DllImport("user32.dll")] public static extern bool IsIconic(nint hWnd);
    [DllImport("user32.dll")] public static extern nint GetAncestor(nint hWnd, uint flags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(nint hWnd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern nint WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hWnd, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(nint hWnd, StringBuilder text, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint hwnd, int attr, out RECT value, int size);

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_CHILD = 0x40000000;
    public const long WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
    public const uint GA_ROOT = 2;
    public const uint SWP_NOACTIVATE = 0x10, SWP_NOZORDER = 0x4, SWP_SHOWWINDOW = 0x40, SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1;
    public static readonly nint HWND_TOPMOST = -1;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // --- monitors ---
    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc proc, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO info);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint hMonitor, int type, out uint dpiX, out uint dpiY);

    // --- GDI capture ---
    [DllImport("user32.dll")] public static extern nint GetDC(nint hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(nint hWnd, nint hdc);
    [DllImport("gdi32.dll")] public static extern nint CreateCompatibleDC(nint hdc);
    [DllImport("gdi32.dll")] public static extern nint CreateCompatibleBitmap(nint hdc, int w, int h);
    [DllImport("gdi32.dll")] public static extern nint SelectObject(nint hdc, nint obj);
    [DllImport("gdi32.dll")] public static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(nint hdc);
    [DllImport("user32.dll")] public static extern bool PrintWindow(nint hWnd, nint hdc, uint flags);
    public const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000, PW_RENDERFULLCONTENT = 2;

    // --- hotkeys ---
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint hWnd, int id);
    public const int WM_HOTKEY = 0x0312, WM_DISPLAYCHANGE = 0x007E, WM_DPICHANGED = 0x02E0;
    public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;

    // --- input ---
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public nint dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }

    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    public const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    public const uint MOUSEEVENTF_MOVE = 1, MOUSEEVENTF_LEFTDOWN = 2, MOUSEEVENTF_LEFTUP = 4, MOUSEEVENTF_RIGHTDOWN = 8, MOUSEEVENTF_RIGHTUP = 0x10,
        MOUSEEVENTF_WHEEL = 0x800, MOUSEEVENTF_VIRTUALDESK = 0x4000, MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;

    // --- credentials ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREDENTIAL
    {
        public uint Flags, Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist, AttributeCount;
        public nint Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredWriteW(ref CREDENTIAL credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredReadW(string target, uint type, uint flags, out nint credential);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] public static extern void CredFree(nint buffer);

    public static string WindowTitle(nint hwnd)
    {
        var sb = new StringBuilder(256);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Brings a window to the front and says whether it is there. Windows only lets the foreground process hand focus on, so the
    /// calling thread briefly shares the foreground window's input queue (no keys are sent), and the result is checked rather than assumed.
    /// </summary>
    public static bool BringToFront(nint hwnd)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (GetAncestor(GetForegroundWindow(), GA_ROOT) == hwnd) return true;
            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var me = GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != me && AttachThreadInput(me, foregroundThread, true);
            try
            {
                SetForegroundWindow(hwnd);
                BringWindowToTop(hwnd);
            }
            finally { if (attached) AttachThreadInput(me, foregroundThread, false); }
            Thread.Sleep(80 * (attempt + 1));
        }
        return GetAncestor(GetForegroundWindow(), GA_ROOT) == hwnd;
    }

    /// <summary>Visible bounds in physical pixels (excludes the invisible resize border GetWindowRect includes).</summary>
    public static PxRect? WindowBounds(nint hwnd)
    {
        if (!IsWindow(hwnd) || IsIconic(hwnd)) return null;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, Marshal.SizeOf<RECT>()) == 0) return r.ToPx();
        return GetWindowRect(hwnd, out r) ? r.ToPx() : null;
    }

    public static IReadOnlyList<MonitorInfo> Monitors()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(0, 0, (nint h, nint _, ref RECT _, nint _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(h, ref info)) return true;
            var scale = GetDpiForMonitor(h, 0, out var dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
            list.Add(new MonitorInfo(info.rcMonitor.ToPx(), scale, (info.dwFlags & 1) != 0));
            return true;
        }, 0);
        return list;
    }

    public static PxRect VirtualDesktop() => new(
        GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));
}

/// <summary>The real user32 behind <see cref="INativeWindowApi"/>.</summary>
public sealed class NativeWindowApi : INativeWindowApi
{
    public int OsBuild => Environment.OSVersion.Version.Build;
    public uint CurrentProcessId => (uint)Environment.ProcessId;
    public bool IsWindow(nint hwnd) => Win32.IsWindow(hwnd);

    public bool IsTopLevel(nint hwnd) =>
        (Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE) & Win32.WS_CHILD) == 0 && Win32.GetAncestor(hwnd, Win32.GA_ROOT) == hwnd;

    public uint GetWindowProcessId(nint hwnd)
    {
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    public bool SetWindowDisplayAffinity(nint hwnd, uint affinity, out int lastError)
    {
        var ok = Win32.SetWindowDisplayAffinity(hwnd, affinity);
        lastError = ok ? 0 : Marshal.GetLastWin32Error();
        return ok;
    }

    public bool GetWindowDisplayAffinity(nint hwnd, out uint affinity, out int lastError)
    {
        var ok = Win32.GetWindowDisplayAffinity(hwnd, out affinity);
        lastError = ok ? 0 : Marshal.GetLastWin32Error();
        return ok;
    }
}

/// <summary>API keys in Windows Credential Manager (generic credentials, current user, local machine).</summary>
public sealed class CredentialManagerSecretStore : ISecretStore
{
    private const uint CRED_TYPE_GENERIC = 1, CRED_PERSIST_LOCAL_MACHINE = 2;
    private static string Target(string name) => "Vernier:" + name;

    public string? Get(string name)
    {
        if (!Win32.CredReadW(Target(name), CRED_TYPE_GENERIC, 0, out var ptr)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<Win32.CREDENTIAL>(ptr);
            if (cred.CredentialBlob == 0 || cred.CredentialBlobSize == 0) return null;
            return Marshal.PtrToStringUni(cred.CredentialBlob, (int)cred.CredentialBlobSize / 2);
        }
        finally { Win32.CredFree(ptr); }
    }

    public void Set(string name, string value)
    {
        var bytes = Encoding.Unicode.GetBytes(value);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new Win32.CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = Target(name),
                Comment = "Vernier AI provider key",
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = Environment.UserName,
            };
            if (!Win32.CredWriteW(ref cred, 0))
                throw new InvalidOperationException($"Windows Credential Manager refused the key (Win32 error {Marshal.GetLastWin32Error()}).");
        }
        finally
        {
            Array.Clear(bytes);
            Marshal.FreeHGlobal(blob);
        }
    }

    public void Delete(string name) => Win32.CredDeleteW(Target(name), CRED_TYPE_GENERIC, 0);
}
