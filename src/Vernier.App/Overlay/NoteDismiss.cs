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
/// The one part of a note card that takes the mouse: a small round × in its top-right corner that removes the card.
/// The glass has to stay click-through everywhere (or it would take clicks meant for the work under it), so the button
/// is a tiny window of its own, topmost, never activated, and excluded from Vernier's own captures like every other.
/// </summary>
public sealed class NoteDismissWindow : Window
{
    public const double SizeDip = 24, InsetDip = 9;

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

    public NoteDismissWindow(Guid noteId, Action<Guid> dismiss)
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
        Width = Height = SizeDip;
        var button = new Button { Template = Round, Focusable = false, ToolTip = "Dismiss this note" };
        AutomationProperties.SetName(button, "Dismiss note");
        button.Click += (_, _) => dismiss(NoteId);
        Content = button;
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

    /// <summary>Puts the button in the card's top-right corner (physical pixels; <paramref name="scale"/> is that monitor's DPI scale).</summary>
    public void Place(PxRect card, double scale)
    {
        (_card, _scale) = (card, scale);
        Apply();
    }

    private void Apply()
    {
        if (Handle == 0) return;
        var size = (int)Math.Round(SizeDip * _scale);
        var inset = (int)Math.Round(InsetDip * _scale);
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, (int)_card.Right - inset - size, (int)_card.Y + inset, size, size, Win32.SWP_NOACTIVATE);
    }
}
