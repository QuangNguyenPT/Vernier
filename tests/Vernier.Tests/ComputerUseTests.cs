using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Vernier.App;
using Vernier.App.Agent;
using Vernier.App.Platform;
using Vernier.App.Views;
using Vernier.Core.Geometry;
using Vernier.Core.Tools;
using Xunit.Abstractions;
using static Vernier.Tests.AppUiTests;

namespace Vernier.Tests;

/// <summary>
/// The tutor's hands on real windows: Notepad gets text and a save through the real approval card
/// and SendInput (checked on disk, not on screen), a denial runs nothing, and a terminal is refused
/// before the learner is even asked.
/// </summary>
[Collection("desktop")]
public class ComputerUseTests(ITestOutputHelper output)
{
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint msg, nint w, nint l);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, nint l);
    private delegate bool EnumProc(nint hwnd, nint l);

    private static nint FindTopWindow(Func<nint, bool> match)
    {
        nint found = 0;
        EnumWindows((h, _) => { if (Win32.IsWindowVisible(h) && match(h)) { found = h; return false; } return true; }, 0);
        return found;
    }

    private static async Task<nint> WaitForWindow(Func<nint, bool> match, string what)
    {
        nint hwnd = 0;
        await Until(() => (hwnd = FindTopWindow(match)) != 0, what, 10000);
        return hwnd;
    }

    private static Window? ApprovalCard() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.Topmost && Descendants<Button>(w).Any(b => b.Content as string == "Allow once"));

    [Fact]
    public void Hands_type_and_save_in_Notepad_after_approval_refuse_a_terminal_and_run_nothing_when_denied() => WindowsRealApiTests.OnAppSta(async () =>
    {
        Assert.False(ComputerTools.IsElevated((uint)Environment.ProcessId), "the tests must run unelevated");
        Assert.True(ComputerTools.IsElevated(4), "the System process must count as not operable");

        using var dir = new TempDir();
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Dark.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vernier;component/Themes/Controls.xaml", UriKind.Relative) });
        AppServices.Initialize(dir.Path);
        AppServices.Settings.FirstRunComplete = true;
        AppServices.Settings.AllowScreenCapture = true;
        AppServices.Quitting = false;
        var main = new MainWindow();
        app.MainWindow = main;
        main.Show();

        // Notepad opens files as tabs of a window already open; closing that window at the end would close the learner's own tabs.
        Assert.True(FindTopWindow(h => Win32.WindowTitle(h).EndsWith("Notepad", StringComparison.OrdinalIgnoreCase)) == 0, "Close Notepad before running this test.");
        var file = Path.Combine(dir.Path, "hands-" + Guid.NewGuid().ToString("N")[..6] + ".txt");
        File.WriteAllText(file, "");
        Process.Start(new ProcessStartInfo("notepad.exe", '"' + file + '"') { UseShellExecute = true })?.Dispose();
        nint notepad = 0, console = 0;
        try
        {
            notepad = await WaitForWindow(h => Win32.WindowTitle(h).Contains(Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase), "Notepad to open the file");
            await Task.Delay(800); // the editor takes focus once the file is loaded
            Assert.Null(ComputerTools.Refusal(notepad));
            var fg = Win32.GetAncestor(Win32.GetForegroundWindow(), Win32.GA_ROOT);
            Win32.GetWindowThreadProcessId(fg, out var fgPid);
            output.WriteLine($"foreground before the steps: '{Win32.WindowTitle(fg)}' pid {fgPid} (this process {Environment.ProcessId}); notepad is {(fg == notepad ? "in front" : "not in front")}");

            async Task<ToolTurn> Turn(nint window)
            {
                var monitor = Win32.Monitors().First(m => Win32.WindowBounds(window) is { } b && m.BoundsPhysical.Contains(b.Center)).BoundsPhysical;
                var turn = new ToolTurn(ComputerTools.Names, CancellationToken.None);
                var ask = new ScreenAsk(monitor, [], window);
                turn.State[ScreenTools.AskKey] = ask;
                var index = await AppServices.Session.ObserveForToolAsync(ask, monitor, ScreenTools.Ids(turn), CancellationToken.None);
                turn.State[ScreenTools.ScreenKey] = index.Screen;
                turn.State[ScreenTools.FrameKey] = index.Frame!;
                return turn;
            }

            async Task<ToolResult> Act(ToolTurn turn, JsonObject args, bool allow)
            {
                var run = AppServices.Tools.RunAsync("computer_act", args, turn, CancellationToken.None);
                await Until(() => ApprovalCard() is not null || run.IsCompleted, "the approval card", 8000);
                if (ApprovalCard() is { } card)
                {
                    var body = string.Concat(Descendants<TextBox>(card).Select(t => t.Text));
                    output.WriteLine("approval: " + body.Replace('\n', ' '));
                    Assert.Contains(Path.GetFileNameWithoutExtension(file), body, StringComparison.OrdinalIgnoreCase); // names the target window
                    var button = Descendants<Button>(card).First(b => b.Content as string == (allow ? "Allow once" : "Cancel"));
                    Assert.False(button.IsEnabled || button.IsDefault, "the card must not answer to a stray keystroke"); // armed only after a moment, never by Enter
                    await Until(() => button.IsEnabled, "the card's buttons to arm", 2000);
                    Click(button);
                }
                var result = await run;
                output.WriteLine("result: " + result.Text);
                return result;
            }

            // When the learner is working in another application, Windows will not hand focus over. The tutor must then refuse,
            // never type into whatever happens to be in front; the positive path below is only meaningful when focus can be taken.
            if (!Win32.BringToFront(notepad))
            {
                AppServices.Settings.AllowComputerControl = true;
                var refused = await Act(await Turn(notepad), new JsonObject { ["action"] = "type", ["text"] = "must not be typed", ["why"] = "focus check" }, allow: true);
                Assert.True(refused.IsError);
                Assert.Contains("front", refused.Text);
                Assert.Equal("", File.ReadAllText(file));
                output.WriteLine("INCONCLUSIVE for the typing path: Windows kept another application in front (the user was working). The refusal was verified; nothing was typed anywhere.");
                return;
            }

            // Hands off by default: refused before any approval.
            AppServices.Settings.AllowComputerControl = false;
            var off = await AppServices.Tools.RunAsync("computer_act", new JsonObject { ["action"] = "type", ["text"] = "x", ["why"] = "test" }, await Turn(notepad), CancellationToken.None);
            Assert.True(off.IsError);
            Assert.Contains("Assisted input is off", off.Text);
            AppServices.Settings.AllowComputerControl = true;

            // Denied: nothing typed.
            var denied = await Act(await Turn(notepad), new JsonObject { ["action"] = "type", ["text"] = "should not appear", ["why"] = "denial check" }, allow: false);
            Assert.True(denied.IsError);
            Assert.Equal(ToolExecutor.DenialText, denied.Text);

            // Allowed: type, then save with ctrl+s; each step re-observes and reports the effect.
            var typed = await Act(await Turn(notepad), new JsonObject { ["action"] = "type", ["text"] = "hello from vernier", ["why"] = "type the greeting" }, allow: true);
            Assert.False(typed.IsError, typed.Text);
            Assert.Contains("changed", typed.Text);
            var saved = await Act(await Turn(notepad), new JsonObject { ["action"] = "keys", ["keys"] = "ctrl+s", ["why"] = "save the file" }, allow: true);
            Assert.False(saved.IsError, saved.Text);
            await Until(() => File.ReadAllText(file).Contains("hello from vernier"), "the typed text to be saved to disk", 5000);
            Assert.DoesNotContain("should not appear", File.ReadAllText(file));

            // A blocked chord never reaches the learner.
            var blocked = await AppServices.Tools.RunAsync("computer_act", new JsonObject { ["action"] = "keys", ["keys"] = "win+r", ["why"] = "open run" }, await Turn(notepad), CancellationToken.None);
            Assert.True(blocked.IsError);
            Assert.Null(ApprovalCard());

            // A terminal: refused before asking.
            Process.Start(new ProcessStartInfo("cmd.exe", "/k title vernier-hands-terminal") { UseShellExecute = true })?.Dispose();
            console = await WaitForWindow(h => Win32.WindowTitle(h).Contains("vernier-hands-terminal", StringComparison.OrdinalIgnoreCase), "the terminal window");
            Assert.Contains("terminal", ComputerTools.Refusal(console));
            var terminal = await AppServices.Tools.RunAsync("computer_act", new JsonObject { ["action"] = "type", ["text"] = "dir", ["why"] = "list files" }, await Turn(console), CancellationToken.None);
            Assert.True(terminal.IsError);
            Assert.Contains("terminal", terminal.Text);
            Assert.Null(ApprovalCard());
        }
        finally
        {
            const uint WM_CLOSE = 0x0010;
            if (notepad != 0) PostMessage(notepad, WM_CLOSE, 0, 0); // saved, so it closes without asking
            if (console != 0) PostMessage(console, WM_CLOSE, 0, 0);
            AppServices.Quitting = true;
            main.Close();
            AppServices.Shutdown();
        }
    });
}
