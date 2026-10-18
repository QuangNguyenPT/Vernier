using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Vernier.App.Platform;

/// <summary>
/// The notification-area icon Vernier lives in. Closing the workspace does not end the app: the
/// tutor stays resident here so the hotkeys keep working while the learner studies.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int WM_APP_TRAY = 0x8000 + 41;
    private const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B;
    private const int NIN_BALLOONHIDE = 0x0403, NIN_BALLOONTIMEOUT = 0x0404, NIN_BALLOONUSERCLICK = 0x0405;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_INFO = 0x10;
    private const uint MF_STRING = 0, MF_SEPARATOR = 0x800, MF_CHECKED = 8, MF_GRAYED = 1;
    private const uint TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nint id, string? text);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint inst, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);

    // The notification area wants the small icon; the large one is not needed.
    private static nint Keep(nint small, nint large)
    {
        if (large != 0) DestroyIcon(large);
        return small;
    }
    [DllImport("user32.dll")] private static extern nint LoadIcon(nint inst, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);

    public sealed record Item(string Text, Action? Invoke, bool Checked = false, bool Enabled = true)
    {
        public static readonly Item Separator = new("", null);
    }

    private readonly HwndSource _source;
    private readonly nint _icon;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private NOTIFYICONDATA _data;
    private bool _added;

    /// <summary>Left click.</summary>
    public Action? Activated { get; set; }
    /// <summary>The learner clicked the notification.</summary>
    public Action? NotificationClicked { get; set; }
    /// <summary>The notification closed without a click (dismissed or timed out).</summary>
    public Action? NotificationDismissed { get; set; }
    /// <summary>Builds the right-click menu each time it opens, so it always shows current state.</summary>
    public Func<IReadOnlyList<Item>>? Menu { get; set; }

    /// <param name="iconSource">An .ico file, or an executable whose own icon is used (the published app is one file, so its icon lives in Vernier.exe).</param>
    public TrayIcon(string iconSource, string tooltip)
    {
        // A message-only style window: never shown, exists to receive the icon's callbacks.
        _source = new HwndSource(new HwndSourceParameters("Vernier tray") { Width = 0, Height = 0, WindowStyle = 0 });
        _source.AddHook(Hook);
        _icon = iconSource.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? (ExtractIconEx(iconSource, 0, out var large, out var small, 1) > 0 ? Keep(small, large) : 0)
            : LoadImage(0, iconSource, 1 /* IMAGE_ICON */, 0, 0, 0x10 /* LR_LOADFROMFILE */ | 0x40 /* LR_DEFAULTSIZE */);
        HasOwnIcon = _icon != 0;
        if (_icon == 0) _icon = LoadIcon(0, 32512 /* IDI_APPLICATION: a plain icon is better than an invisible one */);
        _data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _source.Handle, uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP, uCallbackMessage = WM_APP_TRAY, hIcon = _icon,
            szTip = tooltip, szInfo = "", szInfoTitle = "",
        };
        _added = Shell_NotifyIcon(NIM_ADD, ref _data);
    }

    public bool IsVisible => _added;
    /// <summary>Vernier's own icon was found (false: the plain Windows application icon stands in).</summary>
    public bool HasOwnIcon { get; }

    public void SetTooltip(string tooltip)
    {
        _data.szTip = tooltip.Length > 120 ? tooltip[..120] : tooltip;
        _data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        Shell_NotifyIcon(NIM_MODIFY, ref _data);
    }

    /// <summary>A notification from the tray icon: "still running here", or a suggestion the learner can click.</summary>
    public void Notify(string title, string text)
    {
        _data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_INFO;
        _data.szInfoTitle = title;
        _data.szInfo = text.Length > 250 ? text[..250] : text;
        Shell_NotifyIcon(NIM_MODIFY, ref _data);
        _data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    }

    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == _taskbarCreated)
        {
            // Explorer restarted: the icon is gone and must be added again.
            _data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            _added = Shell_NotifyIcon(NIM_ADD, ref _data);
        }
        else if (msg == WM_APP_TRAY)
        {
            var what = (int)lParam & 0xFFFF;
            if (what == WM_LBUTTONUP) Activated?.Invoke();
            else if (what == NIN_BALLOONUSERCLICK) NotificationClicked?.Invoke();
            else if (what is NIN_BALLOONTIMEOUT or NIN_BALLOONHIDE) NotificationDismissed?.Invoke();
            else if (what is WM_RBUTTONUP or WM_CONTEXTMENU) ShowMenu();
            handled = true;
        }
        return 0;
    }

    private void ShowMenu()
    {
        var items = Menu?.Invoke();
        if (items is null || items.Count == 0) return;
        var menu = CreatePopupMenu();
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Invoke is null && item.Text.Length == 0) AppendMenu(menu, MF_SEPARATOR, 0, null);
                else AppendMenu(menu, MF_STRING | (item.Checked ? MF_CHECKED : 0) | (item.Enabled && item.Invoke is not null ? 0 : MF_GRAYED), i + 1, item.Text);
            }
            Win32.GetCursorPos(out var p);
            Win32.SetForegroundWindow(_source.Handle); // required, or the menu will not dismiss when clicking elsewhere
            var chosen = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, p.X, p.Y, _source.Handle, 0);
            if (chosen > 0) items[chosen - 1].Invoke?.Invoke();
        }
        finally { DestroyMenu(menu); }
    }

    public void Dispose()
    {
        if (_added) Shell_NotifyIcon(NIM_DELETE, ref _data);
        _added = false;
        if (_icon != 0) DestroyIcon(_icon);
        _source.RemoveHook(Hook);
        _source.Dispose();
    }
}
