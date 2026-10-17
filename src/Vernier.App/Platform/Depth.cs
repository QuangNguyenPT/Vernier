using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Vernier.App.Platform;

/// <summary>
/// Windows 11 depth for this app's own windows, from the compositor rather than drawn in WPF:
/// rounded corners, the system shadow, a dark or light caption, a border in the theme's rule
/// colour and, for the ask bar, an acrylic backdrop under a translucent theme tint. Each attribute
/// is asked for separately; where Windows says no (Windows 10, a remote session, transparency
/// effects off) the window simply stays square and opaque.
/// </summary>
public static class Depth
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34, DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2, DWMSBT_TRANSIENTWINDOW = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS margins);

    public readonly record struct Result(bool Rounded, bool Acrylic);

    private static readonly Dictionary<Window, bool> Applied = [];
    private static readonly Dictionary<Window, Result> Results = [];

    /// <summary>Applies depth once the window has a handle, and again whenever the theme changes.</summary>
    public static void Attach(Window window, bool acrylic)
    {
        Applied[window] = acrylic;
        window.Closed += (_, _) => { Applied.Remove(window); Results.Remove(window); };
        if (new WindowInteropHelper(window).Handle != 0) Apply(window, acrylic);
        else window.SourceInitialized += (_, _) => Apply(window, acrylic);
    }

    /// <summary>After a theme change: caption colour, border and tint follow the new theme.</summary>
    public static void Refresh()
    {
        foreach (var (window, acrylic) in Applied.ToArray()) Apply(window, acrylic);
    }

    /// <summary>What Windows granted the window the last time depth was applied.</summary>
    public static Result Of(Window window) => Results.GetValueOrDefault(window);

    private static void Apply(Window window, bool acrylic)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0) return;
        var dark = window.TryFindResource("IsDark") is true ? 1 : 0;
        Set(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, dark);
        var rounded = Set(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        if (window.TryFindResource("RuleStrong") is SolidColorBrush rule)
            Set(hwnd, DWMWA_BORDER_COLOR, rule.Color.R | rule.Color.G << 8 | rule.Color.B << 16);

        var glass = false;
        if (acrylic && !SystemParameters.HighContrast && Set(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_TRANSIENTWINDOW))
        {
            // The backdrop shows through only where WPF leaves the surface transparent.
            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            glass = DwmExtendFrameIntoClientArea(hwnd, ref margins) == 0;
            if (glass && HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
        }
        else if (acrylic)
        {
            // A borderless window gets the system shadow from a one-pixel frame.
            var margins = new MARGINS { Bottom = 1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        if (acrylic) window.SetResourceReference(Window.BackgroundProperty, glass ? "BgGlass" : "Bg");
        Results[window] = new Result(rounded, glass);
    }

    private static bool Set(nint hwnd, int attribute, int value) => DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0;
}
