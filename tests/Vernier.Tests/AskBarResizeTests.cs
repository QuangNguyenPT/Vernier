using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Vernier.App;
using Vernier.App.Views;

namespace Vernier.Tests;

[Collection("desktop")]
public class AskBarResizeTests
{
    [Fact]
    public void The_answer_grip_resizes_within_bounds_saves_the_size_and_resets_on_double_click() => WindowsRealApiTests.OnAppSta(async () =>
    {
        using var dir = new TempDir();
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Light.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Controls.xaml", UriKind.Relative) });
        AppServices.Initialize(dir.Path);
        AppServices.Quitting = false;
        var bar = new AskBar();
        try
        {
            bar.Show();
            await Task.Delay(200);
            var grip = AppUiTests.Descendants<Thumb>(bar).Single();
            Assert.Equal("Resize answer", AutomationProperties.GetName(grip));
            Assert.Equal(Cursors.SizeNWSE, grip.Cursor);
            var scroll = AppUiTests.Descendants<System.Windows.Controls.ScrollViewer>(bar).First(s => s.MaxHeight == 420);
            double w0 = bar.Width, h0 = scroll.MaxHeight;

            void Drag(double dx, double dy)
            {
                grip.RaiseEvent(new DragDeltaEventArgs(dx, dy) { RoutedEvent = Thumb.DragDeltaEvent });
                grip.RaiseEvent(new DragCompletedEventArgs(dx, dy, false) { RoutedEvent = Thumb.DragCompletedEvent });
            }

            Drag(120, 80);
            Assert.Equal(w0 + 120, bar.Width);
            Assert.Equal(h0 + 80, scroll.MaxHeight);
            Assert.Equal(bar.Width, AppServices.Settings.AskBarWidth);
            Assert.Equal(scroll.MaxHeight, AppServices.Settings.AskBarAnswerHeight);

            Drag(-5000, -5000); // never below the default
            Assert.Equal(w0, bar.Width);
            Assert.Equal(h0, scroll.MaxHeight);

            Drag(100000, 100000); // never beyond the screen
            Assert.True(bar.Width < SystemParameters.VirtualScreenWidth);
            Assert.True(scroll.MaxHeight < SystemParameters.VirtualScreenHeight);

            Drag(0, 0);
            grip.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = System.Windows.Controls.Control.MouseDoubleClickEvent, Source = grip });
            Assert.Equal(w0, bar.Width);
            Assert.Equal(h0, scroll.MaxHeight);
            Assert.Null(AppServices.Settings.AskBarWidth);
            Assert.Null(AppServices.Settings.AskBarAnswerHeight);
        }
        finally
        {
            bar.ForceClose();
            AppServices.Quitting = true;
            AppServices.Shutdown();
        }
    });
}
