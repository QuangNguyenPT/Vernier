using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vernier.App.Platform;
using Vernier.Core.Geometry;

namespace Vernier.App;

/// <summary>
/// The one part of a note card that takes the mouse: two small round buttons in its top-right corner, a grip that moves the
/// card while it is dragged and a × that removes it. The glass has to stay click-through everywhere (or it would take clicks
/// meant for the work under it), so the buttons are a tiny window of their own, topmost, never activated, and excluded from
/// Vernier's own captures like every other.
/// </summary>
public sealed class NoteDismissWindow : Window
{
    public const double SizeDip = 24, InsetDip = 9, GapDip = 4;
    /// <summary>The window's width: the grip and the ×, side by side.</summary>
    public const double WidthDip = 2 * SizeDip + GapDip;

    // The note card is always dark, so the button is too, whatever the theme.
    private static readonly ControlTemplate Round = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Grid Background="Transparent">
            <Ellipse x:Name="Disc" Fill="#E6131E27" Stroke="#5A45596A" StrokeThickness="1"/>
            <TextBlock x:Name="Glyph" Text="&#xE711;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="9" Foreground="#B2C0CB"
                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Disc" Property="Fill" Value="#FF1F2E3A"/>
              <Setter TargetName="Disc" Property="Stroke" Value="#FF6CC4FF"/>
              <Setter TargetName="Glyph" Property="Foreground" Value="#FFFFFFFF"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Disc" Property="Fill" Value="#FF2A3B48"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    public Guid NoteId { get; }
    private PxRect _card;
    private double _scale = 1;

    private Win32.POINT? _dragFrom;

    /// <param name="move">Moves the card by (dx, dy) physical pixels while the grip is dragged.</param>
    public NoteDismissWindow(Guid noteId, Action<Guid> dismiss, Action<Guid, double, double> move)
    {
        NoteId = noteId;
        Title = "Vernier note";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = WidthDip;
        Height = SizeDip;
        var button = new Button { Template = Round, Focusable = false, ToolTip = "Dismiss this note", Width = SizeDip, Height = SizeDip };
        AutomationProperties.SetName(button, "Dismiss note");
        button.Click += (_, _) => dismiss(NoteId);
        // The grip: press and drag to put the card somewhere else (the cursor is followed in physical pixels, so it works across monitors and scales).
        var grip = new Button { Template = Round, Focusable = false, ToolTip = "Drag to move this note", Width = SizeDip, Height = SizeDip, Margin = new Thickness(0, 0, GapDip, 0), Cursor = System.Windows.Input.Cursors.SizeAll, Content = "\uE7C2" };
        AutomationProperties.SetName(grip, "Move note");
        grip.Loaded += (_, _) => { if (grip.Template.FindName("Glyph", grip) is TextBlock g) g.Text = "\uE7C2"; };
        grip.PreviewMouseLeftButtonDown += (_, e) => { Win32.GetCursorPos(out var p); _dragFrom = p; grip.CaptureMouse(); e.Handled = true; };
        void Follow()
        {
            if (_dragFrom is not { } from) return;
            Win32.GetCursorPos(out var p);
            if (p.X == from.X && p.Y == from.Y) return;
            _dragFrom = p;
            move(NoteId, p.X - from.X, p.Y - from.Y);
        }
        grip.PreviewMouseMove += (_, _) => Follow();
        // The last moves of a quick drag can arrive after the button is up: the card catches up with the pointer on release.
        grip.PreviewMouseLeftButtonUp += (_, e) => { Follow(); _dragFrom = null; grip.ReleaseMouseCapture(); e.Handled = true; };
        grip.LostMouseCapture += (_, _) => _dragFrom = null;
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { grip, button } };
        // It arrives with the card's wipe, not ahead of it.
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 240 : 1)) { BeginTime = TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 180 : 0) });
    }

    private nint Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var ex = (long)Win32.GetWindowLongPtr(Handle, Win32.GWL_EXSTYLE) | Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLongPtr(Handle, Win32.GWL_EXSTYLE, (nint)ex);
        Apply();
    }

    /// <summary>Puts the buttons in the card's top-right corner (physical pixels; <paramref name="scale"/> is that monitor's DPI scale).</summary>
    public void Place(PxRect card, double scale)
    {
        (_card, _scale) = (card, scale);
        Apply();
    }

    private void Apply()
    {
        if (Handle == 0) return;
        var size = (int)Math.Round(SizeDip * _scale);
        var width = (int)Math.Round(WidthDip * _scale);
        var inset = (int)Math.Round(InsetDip * _scale);
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, (int)_card.Right - inset - width, (int)_card.Y + inset, width, size, Win32.SWP_NOACTIVATE);
    }
}
