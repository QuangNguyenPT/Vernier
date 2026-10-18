using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Vernier.App.Platform;
using Vernier.Core.Knowledge;
using Vernier.Core.Providers;
using Windows.Media.Ocr;

namespace Vernier.App.Views;

public partial class DiagnosticsPage : UserControl, IRefreshable
{
    private sealed record Integration(string Name, bool Available, string Detail, string Missing = "unavailable");

    public DiagnosticsPage()
    {
        InitializeComponent();
        AppServices.Policy.Changed += () => Dispatcher.BeginInvoke(new Action(ShowPolicy));
    }

    public void Refresh()
    {
        var monitors = Win32.Monitors();
        Environment.Text =
            $"Vernier {typeof(DiagnosticsPage).Assembly.GetName().Version?.ToString(3)} · Windows build {System.Environment.OSVersion.Version.Build} · {RuntimeInformation.FrameworkDescription} · {RuntimeInformation.ProcessArchitecture}\n" +
            $"Displays: {string.Join("  |  ", monitors.Select(m => $"{m.BoundsPhysical.Width:0}×{m.BoundsPhysical.Height:0} at ({m.BoundsPhysical.X:0},{m.BoundsPhysical.Y:0}) scale {m.DpiScale:0.00}{(m.IsPrimary ? " primary" : "")}"))}\n" +
            $"Learning database: schema v{AppServices.Store.SchemaVersion} · {AppServices.Store.Path}";
        ShowPolicy();
        LogText.Text = string.Join(System.Environment.NewLine, AppServices.Log.Snapshot());
        LogText.ScrollToEnd();
        _ = ShowIntegrationsAsync();
    }

    private void ShowPolicy()
    {
        PolicySummary.Text = $"Mode: {AppServices.Policy.Mode}. {AppServices.Policy.Summary}";
        PolicyStates.ItemsSource = AppServices.Policy.States;
    }

    private async Task ShowIntegrationsAsync()
    {
        var accounts = await Task.WhenAll(CliAccounts.All.Select(a => CliAccounts.StatusAsync(a, CancellationToken.None)));
        var graphify = GraphifySource.FindCli();
        var ocr = OcrEngine.TryCreateFromUserProfileLanguages();
        var hotkeys = (Window.GetWindow(this) as MainWindow)?.Hotkeys?.Unavailable ?? [];
        List<Integration> rows =
        [
            .. accounts.Select(a => new Integration(a.Account.Name, a.SignedIn, $"{(a.Path is null ? "" : a.Path + ". ")}{a.Detail}", a.Installed ? "signed out" : "not installed")),
            new("Native module (C++)", Vernier.App.Platform.Native.Available, Vernier.App.Platform.Native.Available
                ? $"Vernier.Native.dll ABI {Vernier.App.Platform.Native.Version} in use: GPU screen capture, image encoding, the capture light, the pen ink and mark matching run in C++."
                : Vernier.App.Platform.Native.Version != 0
                    ? $"Vernier.Native.dll reports ABI {Vernier.App.Platform.Native.Version}, this build needs {Vernier.App.Platform.Native.Abi}: not used. Everything runs on the slower managed fallbacks."
                    : "Vernier.Native.dll is not beside Vernier.exe: everything runs on the slower managed fallbacks.", "fallback"),
            new("Graphify CLI", graphify is not null, graphify is not null
                ? $"{graphify}. Vernier reads graphify-out/graph.json and can run 'graphify update'."
                : "Not on PATH. Optional: the knowledge graph falls back to the links between your notes."),
            new("Windows OCR", ocr is not null, ocr is not null
                ? $"Recogniser language: {ocr.RecognizerLanguage.DisplayName}. Runs locally; reports no confidence, so Vernier assumes a modest one."
                : "No OCR language pack is installed. Screen text will come from the accessibility tree only, or not at all for region captures."),
            new("Speech output", AppServices.SpeechOut is not null, AppServices.SpeechOut is not null ? "Windows voice (SAPI), local." : "No Windows voice is available."),
            new("Speech input", AppServices.Settings.AllowMicrophone, AppServices.Settings.AllowMicrophone
                ? "Windows dictation (SAPI), local, push-to-talk. The recogniser is only started while the talk button is held."
                : "The microphone permission is off (Settings › Privacy).", "off"),
            new("Global hotkeys", hotkeys.Count == 0, hotkeys.Count == 0
                ? $"Ask {MainWindow.AskHotkey}, pen {MainWindow.PenHotkey}, pause Ctrl+Alt+P, hide Ctrl+Alt+H."
                : $"Taken by another application: {string.Join(", ", hotkeys)}. In use instead: ask {MainWindow.AskHotkey}, pen {MainWindow.PenHotkey}.", "partly"),
        ];
        Integrations.ItemsSource = rows;
    }

    private void OnRecheck(object sender, RoutedEventArgs e) => _ = ShowIntegrationsAsync();

    private void OnReapply(object sender, RoutedEventArgs e) => AppServices.Policy.Reapply();

    private async void OnSelfTest(object sender, RoutedEventArgs e)
    {
        SelfTest.IsEnabled = false;
        try
        {
            var results = await CaptureExclusionSelfTest.RunAsync();
            SelfTestResults.ItemsSource = results;
            foreach (var r in results)
                AppServices.Log.Info($"Capture self-test · {r.Path} · expected {r.Expected} · observed {r.Observed}");
        }
        catch (InvalidOperationException ex)
        {
            SelfTestResults.ItemsSource = new[] { new CaptureExclusionResult("Self-test could not run", "", ex.Message, null) };
        }
        finally
        {
            SelfTest.IsEnabled = true;
            LogText.Text = string.Join(System.Environment.NewLine, AppServices.Log.Snapshot());
        }
    }

    private void OnCopyLog(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Environment.Text + System.Environment.NewLine + LogText.Text); }
        catch (COMException) { } // the clipboard is briefly held by another process
    }
}
