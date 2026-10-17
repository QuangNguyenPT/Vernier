using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vernier.App.Platform;
using Vernier.Core.Providers;
using Vernier.Core.Settings;

namespace Vernier.App.Views;

/// <summary>
/// The workspace: full conversation, review, library, notes, settings. It is not where studying
/// happens; that is the glass and the ask bar. Closing it only hides it: Vernier stays in the
/// notification area with its hotkeys live.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<string, FrameworkElement> _pages = [];
    private AskBar? _bar;
    private bool _syncing, _toldAboutTray;

    public HotkeyManager? Hotkeys { get; private set; }

    /// <summary>The chords that actually got registered (another application may own the first choice).</summary>
    public static string AskHotkey { get; private set; } = "Ctrl+Alt+Space";
    public static string PenHotkey { get; private set; } = "Ctrl+Alt+D";

    public MainWindow()
    {
        InitializeComponent();
        AppServices.TrackWindow(this, "Workspace");
        AppServices.Session.Changed += Sync;
        AppServices.Policy.Changed += Sync;
        Loaded += (_, _) =>
        {
            Navigate("Tutor");
            if (AppServices.StartupNotices.Count > 0) ShowNotice(string.Join("  ", AppServices.StartupNotices));
            Sync();
        };
        Closing += (_, e) =>
        {
            if (AppServices.Quitting) return;
            // Close means "get out of my way", not "stop": the tutor is needed while the learner works elsewhere.
            e.Cancel = true;
            Hide();
            if (_toldAboutTray) return;
            _toldAboutTray = true;
            AppServices.Tray?.Notify("Vernier is still here", $"{AskHotkey} asks about your screen, {PenHotkey} is the pen. Right-click this icon to quit.");
        };
        Closed += (_, _) =>
        {
            Hotkeys?.Dispose();
            _bar?.ForceClose();
            AppServices.Session.Changed -= Sync;
            AppServices.Policy.Changed -= Sync;
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Hotkeys = new HotkeyManager(new WindowInteropHelper(this).Handle);
        const uint ctrlAlt = Win32.MOD_CONTROL | Win32.MOD_ALT;
        if (Hotkeys.Register("Ctrl+Alt+Space (ask)", ctrlAlt, 0x20, () => OpenAskBar(pen: false)) < 0)
            AskHotkey = Hotkeys.Register("Ctrl+Alt+A (ask)", ctrlAlt, 0x41, () => OpenAskBar(pen: false)) < 0 ? "the tray icon" : "Ctrl+Alt+A";
        if (Hotkeys.Register("Ctrl+Alt+D (pen)", ctrlAlt, 0x44, () => OpenAskBar(pen: true)) < 0)
            PenHotkey = Hotkeys.Register("Ctrl+Alt+S (pen)", ctrlAlt, 0x53, () => OpenAskBar(pen: true)) < 0 ? "the PEN button" : "Ctrl+Alt+S";
        Hotkeys.Register("Ctrl+Alt+P (pause)", ctrlAlt, 0x50, TogglePause);
        Hotkeys.Register("Ctrl+Alt+H (hide)", ctrlAlt, 0x48, HideAll);
        if (Hotkeys.Unavailable.Count > 0)
            AppServices.Log.Warn("Hotkeys already used by another application: " + string.Join(", ", Hotkeys.Unavailable));
    }

    // ---------- the two tools ----------

    /// <summary>Ask (pen false) or Pen (pen true), from a hotkey, the tray, or the header. Pressing the pen chord again puts the pen down.</summary>
    public void OpenAskBar(bool pen)
    {
        if (AppServices.Session.IsPaused) AppServices.Session.Resume();
        _bar ??= new AskBar();
        if (pen && _bar.IsVisible && AppServices.Overlays.PenArmed) { AppServices.Overlays.ArmPen(false); return; }
        _bar.Open(pen);
    }

    public AskBar? Bar => _bar;

    public void ShowWorkspace()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    // ---------- navigation ----------

    private void OnNavigate(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Navigate((string)((RadioButton)sender).Tag);
    }

    public void Navigate(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
            _pages[key] = page = key switch
            {
                "Tutor" => new TutorPanel(),
                "Review" => new ReviewPage(),
                "Library" => new LibraryPage(),
                "Files" => new FilesPage(),
                "Settings" => new SettingsPage(),
                _ => new DiagnosticsPage(),
            };
        (page as IRefreshable)?.Refresh();
        PageHost.Content = page;
        foreach (var rb in Rail.Children.OfType<RadioButton>())
            if ((string)rb.Tag == key && rb.IsChecked != true) rb.IsChecked = true;
        Sync();
    }

    /// <summary>Opens the conversation with a question ready to send (used by Review and Library).</summary>
    public void AskTutor(string question)
    {
        Navigate("Tutor");
        var panel = (TutorPanel)_pages["Tutor"];
        panel.Input.Text = question;
        panel.FocusInput();
    }

    public void VisitAllPages()
    {
        foreach (var key in new[] { "Tutor", "Review", "Library", "Files", "Settings", "Diagnostics" })
        {
            Navigate(key);
            UpdateLayout();
        }
    }

    public void ShowNotice(string text)
    {
        NoticeText.Text = text;
        NoticeBar.Visibility = Visibility.Visible;
    }

    private void OnDismissNotice(object sender, RoutedEventArgs e) => NoticeBar.Visibility = Visibility.Collapsed;

    // ---------- always-visible state ----------

    private void Sync()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(Sync)); return; }
        _syncing = true;
        var session = AppServices.Session;
        var settings = AppServices.Settings;

        Lamp.Fill = (Brush)FindResource(session.IsLooking ? "Accent" : "Rule");
        ObserveText.Text = session.IsPaused ? "paused · nothing captured, recorded or sent"
            : session.IsLooking ? "looking at your screen now"
            : !settings.AllowScreenCapture ? "screen: never looked at until you allow it"
            : session.CaptureBlocked ? session.CaptureBlockedReason.ToLowerInvariant()
            : "screen: looked at only when you ask or mark";
        ObserveText.Foreground = (Brush)FindResource(session.IsLooking ? "AccentText" : "InkFaint");

        PauseButton.Content = session.IsPaused ? "Resume" : "Pause";
        PauseButton.Style = (Style)FindResource(session.IsPaused ? "Primary" : typeof(Button));

        foreach (ComboBoxItem item in PolicyBox.Items)
            if ((string)item.Tag == settings.CapturePolicy.ToString()) PolicyBox.SelectedItem = item;
        PolicyText.Text = AppServices.Policy.Summary;
        PolicyText.Foreground = (Brush)FindResource(settings.CapturePolicy == CapturePolicyMode.Standard ? "InkFaint" : "AccentText");

        var provider = settings.ActiveProvider;
        ProviderText.Text = provider is null ? "no provider selected"
            : $"{provider.DisplayName}{(string.IsNullOrWhiteSpace(provider.Model) ? "" : " · " + provider.Model)}{(provider.IsLocal ? " · local" : " · remote")}";

        var due = SafeDueCount();
        ReviewNav.Content = due > 0 ? $"Review  ·  {due}" : "Review";
        GoalText.Text = string.IsNullOrWhiteSpace(settings.Learner.Goals) ? "" : "goal: " + settings.Learner.Goals;
        _syncing = false;
    }

    private static int SafeDueCount()
    {
        try { return AppServices.Store.StudyQueue(DateTimeOffset.UtcNow).Count; }
        catch (Exception e) when (e is Microsoft.Data.Sqlite.SqliteException or ObjectDisposedException or InvalidOperationException) { return 0; }
    }

    private void OnPolicyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || PolicyBox.SelectedItem is not ComboBoxItem item) return;
        var mode = Enum.Parse<CapturePolicyMode>((string)item.Tag);
        AppServices.Settings.CapturePolicy = mode;
        AppServices.SaveSettings();
        AppServices.Policy.SetMode(mode);
        if (mode == CapturePolicyMode.ShareSafe) AppServices.Session.EnterShareSafe();
        AppServices.Log.Info($"Capture policy set to {mode}. {AppServices.Policy.Summary}");
        Sync();
    }

    private void OnPause(object sender, RoutedEventArgs e) => TogglePause();

    public void TogglePause()
    {
        if (AppServices.Session.IsPaused) AppServices.Session.Resume(); else AppServices.Session.Pause();
    }

    private void OnHideAll(object sender, RoutedEventArgs e) => HideAll();

    /// <summary>The immediate hide control: marks off, ask bar gone, workspace hidden to the notification area.</summary>
    public void HideAll()
    {
        AppServices.Overlays.ArmPen(false);
        AppServices.Annotations.SetVisible(false);
        _bar?.HideBar();
        Hide();
        Sync();
    }

    private void OnOpenAsk(object sender, RoutedEventArgs e)
    {
        Hide(); // the workspace is not what the learner wants to ask about
        OpenAskBar(pen: false);
    }

    // ---------- design review ----------

    /// <summary>Renders each page in both themes to PNG files, using whatever (sample) data the store holds.</summary>
    public async Task SaveReviewScreenshotsAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var (theme, _) in AppServices.Themes)
        {
            AppServices.ApplyTheme(theme);
            foreach (var key in new[] { "Tutor", "Review", "Library", "Files", "Settings", "Diagnostics" })
            {
                Navigate(key);
                await Task.Delay(350);
                Save(this, Path.Combine(directory, $"{theme}-{key.ToLowerInvariant()}.png"));
            }
        }
        Navigate("Tutor");
        var bar = new AskBar();
        bar.Open(pen: false);
        bar.ShowForReview(new TranscriptItem
        {
            Role = "assistant",
            Meta = "sample answer",
            Text = """
                ## Kinetic energy
                The energy of a moving body is \( E_k = \tfrac{1}{2} m v^{2} \), so doubling the speed gives four times the energy.
                \[ E_k = \frac{1}{2}\,(2\,\mathrm{kg})\,(3\,\mathrm{m/s})^{2} = 9\,\mathrm{J} \]
                1. Square the speed first: 3^2 = 9.
                2. Then halve the product with the mass.

                Which step would you check first?
                """,
        });
        foreach (var (theme, _) in AppServices.Themes)
        {
            AppServices.ApplyTheme(theme);
            await Task.Delay(400);
            Save(bar, Path.Combine(directory, $"{theme}-ask-bar.png"));
        }
        AppServices.ApplyTheme("dark");
        var sample = Path.Combine(Path.GetTempPath(), "Lecture 4 - torque.txt");
        File.WriteAllText(sample, "Torque is r x F.");
        bar.AddFile(sample);
        File.Delete(sample);
        await Task.Delay(300);
        Save(bar, Path.Combine(directory, "dark-ask-bar-attached.png"));
        bar.ForceClose();

        static void Save(Window window, string file)
        {
            var root = (FrameworkElement)window.Content;
            var dpi = VisualTreeHelper.GetDpi(root);
            var bitmap = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            var backdrop = new DrawingVisual();
            using (var dc = backdrop.RenderOpen())
            {
                dc.DrawRectangle((Brush)window.FindResource("Bg"), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            }
            bitmap.Render(backdrop);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(file);
            encoder.Save(stream);
        }
    }
}

public interface IRefreshable
{
    void Refresh();
}

/// <summary>First launch: how Vernier is used, what it does with your screen and voice, and the choices that are off until you turn them on.</summary>
public sealed class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        Title = "Welcome to Vernier";
        Width = 620;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Bg");
        AppServices.TrackWindow(this, "First-run setup");

        var settings = AppServices.Settings;
        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
        TextBlock Text(string text, string style, Thickness? margin = null) => new() { Text = text, Style = (Style)FindResource(style), Margin = margin ?? new Thickness(0) };

        panel.Children.Add(Text("A tutor that stays out of your way", "Title"));
        panel.Children.Add(Text(
            $"Keep studying in whatever you use. Press {MainWindow.AskHotkey} to ask about your screen, or {MainWindow.PenHotkey} and circle the thing you do not understand. " +
            "Vernier answers and marks the screen in blue. A light-blue wave round the screen edge shows whenever it is looking.", "Soft", new Thickness(0, 0, 0, 18)));

        panel.Children.Add(Text("who answers", "Readout", new Thickness(0, 0, 0, 4)));
        var providers = new ComboBox { DisplayMemberPath = nameof(ProviderSettings.DisplayName), ItemsSource = settings.Providers, SelectedItem = settings.ActiveProvider };
        System.Windows.Automation.AutomationProperties.SetName(providers, "Provider");
        panel.Children.Add(providers);
        var providerNote = Text("Checking which accounts are signed in…", "Soft", new Thickness(0, 6, 0, 16));
        providerNote.FontSize = 12;
        panel.Children.Add(providerNote);

        panel.Children.Add(Text("permissions · all off by default, change any time in settings", "Readout", new Thickness(0, 0, 0, 4)));
        var capture = new CheckBox { Content = "Let Vernier look at my screen when I ask or mark something", IsChecked = settings.AllowScreenCapture };
        var mic = new CheckBox { Content = "Allow the microphone while I hold the talk button (local recognition, audio not saved)", IsChecked = settings.AllowMicrophone };
        var sample = new CheckBox { Content = "Keep the sample subject so I can try Review straight away (clearly labelled as sample)", IsChecked = true };
        panel.Children.Add(capture);
        panel.Children.Add(mic);
        panel.Children.Add(sample);

        panel.Children.Add(Text("context · on unless you turn it off", "Readout", new Thickness(0, 14, 0, 4)));
        var keep = new CheckBox { Content = "Keep context of my work: which apps, pages and videos, for how long, what I rewound (on this computer)", IsChecked = settings.AllowActivityContext };
        var read = new CheckBox { Content = "Also read the text of the window in front when it changes (local, memory only, needs the screen permission above)", IsChecked = settings.ReadScreenForContext };
        panel.Children.Add(keep);
        panel.Children.Add(read);

        panel.Children.Add(Text(
            "With a remote provider, your question, the capture and the text read from it are sent to that provider. " +
            "Your learning record stays in a local database. Sign-ins are handled by each vendor's own tool; Vernier never sees a password.",
            "Soft", new Thickness(0, 14, 0, 0)));

        var start = new Button { Content = "Start", Style = (Style)FindResource("Primary"), IsDefault = true, MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        start.Click += (_, _) =>
        {
            if (providers.SelectedItem is ProviderSettings chosen) settings.ActiveProviderId = chosen.Id;
            settings.AllowScreenCapture = capture.IsChecked == true;
            settings.AllowMicrophone = mic.IsChecked == true;
            settings.AllowActivityContext = keep.IsChecked == true;
            settings.ReadScreenForContext = read.IsChecked == true;
            settings.ContextNoticeShown = true;
            settings.FirstRunComplete = true;
            if (sample.IsChecked != true) AppServices.Store.RemoveSampleData();
            AppServices.SaveSettings();
            Close();
        };
        panel.Children.Add(start);
        Content = panel;

        Loaded += async (_, _) =>
        {
            var statuses = await Task.WhenAll(CliAccounts.All.Select(a => CliAccounts.StatusAsync(a, CancellationToken.None)));
            var signedIn = statuses.Where(s => s.SignedIn).ToList();
            providerNote.Text = signedIn.Count > 0
                ? $"Signed in on this computer: {string.Join(", ", signedIn.Select(s => s.Account.Name))}. No API key needed for those. Others can be added in Settings."
                : "No Claude or ChatGPT account is signed in yet. Sign in under Settings › Accounts, add an API key, or use a local model.";
            // Prefer an account that is actually signed in over one that is not.
            if (signedIn.Count > 0 && signedIn.All(s => s.Account.ProviderId != settings.ActiveProviderId))
                providers.SelectedItem = settings.Providers.FirstOrDefault(p => p.Id == signedIn[0].Account.ProviderId);
        };
    }
}
