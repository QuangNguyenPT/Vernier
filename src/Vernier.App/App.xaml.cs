using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Vernier.App.Agent;
using Vernier.App.Platform;
using Vernier.App.Views;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Learning;
using Vernier.Core.Providers;
using Vernier.Core.Security;
using Vernier.Core.Settings;
using Vernier.Core.Tools;
using Vernier.Core.Windowing;

namespace Vernier.App;

/// <summary>Composition root. Services are created once here and handed to the views.</summary>
public static class AppServices
{
    public static string DataDir { get; private set; } = "";
    public static AppSettings Settings { get; private set; } = new();
    public static SettingsStore SettingsStore { get; private set; } = null!;
    public static DiagnosticLog Log { get; private set; } = new();
    public static LayeredSecretStore Secrets { get; private set; } = null!;
    public static Connectors.GoogleConnector Google { get; private set; } = null!;
    public static ProviderFactory Providers { get; private set; } = null!;
    public static LearningStore Store { get; private set; } = null!;
    public static IWindowCapturePolicyService Policy { get; private set; } = null!;
    public static AnnotationStore Annotations { get; } = new();
    public static TutorSession Session { get; private set; } = null!;
    public static ISpeechOutput? SpeechOut { get; private set; }
    public static ISpeechInput? SpeechIn { get; private set; }
    public static AnnotationOverlayManager Overlays { get; private set; } = null!;
    /// <summary>The UI thread's dispatcher; tools running on other threads draw through it.</summary>
    public static Dispatcher Ui { get; private set; } = Dispatcher.CurrentDispatcher;
    public static ToolExecutor Tools { get; private set; } = null!;
    /// <summary>Loopback MCP server for CLI providers. Null if it could not start (tools are then offered to native providers only).</summary>
    public static McpServer? Mcp { get; private set; }
    public static Context.ContextHub? Context { get; private set; }
    /// <summary>Which model served which kind of task, with timings and tokens (local, no content).</summary>
    public static RouteLog? Routes { get; private set; }
    public static List<string> StartupNotices { get; } = [];
    public static TrayIcon? Tray { get; set; }
    /// <summary>True only once the user chose Quit: until then, closing a window hides it.</summary>
    public static bool Quitting { get; set; }

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan }; // per-call timeouts are enforced by ChatRunner

    private static bool _initialized;

    public static void Initialize(string? dataDirOverride = null)
    {
        // Starting again without stopping first (tests do) would leave the old overlay's windows on the screen, over the new ones.
        Shutdown();
        DataDir = dataDirOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vernier");
        Directory.CreateDirectory(DataDir);
        Log = new DiagnosticLog(Path.Combine(DataDir, "logs", "vernier.log"));
        SettingsStore = new SettingsStore(Path.Combine(DataDir, "settings.json"));
        Settings = SettingsStore.Load();
        if (SettingsStore.LoadWarning is { } warning) StartupNotices.Add(warning);
        if (!string.IsNullOrWhiteSpace(Settings.VaultPath))
        {
            // A note transaction cut short by a crash or power loss is undone, so the vault is never left half-written.
            try { if (new Vernier.Core.Knowledge.ObsidianVault(Settings.VaultPath).Recover()) StartupNotices.Add("Saving notes was interrupted last time; the vault files involved were put back as they were."); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or PathGuardException or FormatException)
            {
                Log.Error("Vault recovery failed", e);
                StartupNotices.Add("Saving notes was interrupted last time and Vernier could not put the files back: " + e.Message + " Check the Vernier folder in your vault.");
            }
        }

        Secrets = new LayeredSecretStore(new CredentialManagerSecretStore());
        var temp = Path.Combine(DataDir, "tmp");
        // Screenshots handed to the Claude Code CLI live here briefly; clear anything a crash left behind.
        try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        Directory.CreateDirectory(temp);
        Providers = new ProviderFactory(Secrets, Http, temp);
        Google = new Connectors.GoogleConnector(Secrets, Http);

        Store = new LearningStore(Path.Combine(DataDir, "learning.db"));
        if (Store.RecoveryNotice is { } recovery) StartupNotices.Add(recovery);
        if (!Settings.FirstRunComplete && Store.IsEmpty) Store.SeedSampleData(DateTimeOffset.UtcNow);
        if (Settings.RetainSessionsDays > 0) Store.PurgeSessionsOlderThan(Settings.RetainSessionsDays, DateTimeOffset.UtcNow);
        try { Store.Backup(); }
        catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException) { Log.Error("Backup failed", e); }

        Policy = new WindowCapturePolicyService(new NativeWindowApi());
        Policy.SetMode(Settings.CapturePolicy);

        Ui = Dispatcher.CurrentDispatcher;
        Routes = new RouteLog(Path.Combine(DataDir, "logs", "routes.jsonl"));
        var registry = new ToolRegistry();
        foreach (var tool in ScreenTools.All()) registry.Add(tool);
        foreach (var tool in ContextTools.All()) registry.Add(tool);
        foreach (var tool in ComputerTools.All()) registry.Add(tool);
        foreach (var tool in NoteTools.All()) registry.Add(tool);
        foreach (var tool in GoogleTools.All()) registry.Add(tool);
        var web = new Vernier.Core.Research.SafeWeb();
        foreach (var tool in WebTools.All(new Vernier.Core.Research.WebResearcher(new Vernier.Core.Research.DuckDuckGoSearch(web), web))) registry.Add(tool);
        Tools = new ToolExecutor(registry, new UiApprover(), ToolGate, m => Log.Warn(m));
        try { Mcp = new McpServer(Tools, m => Log.Warn(m)); }
        catch (System.Net.HttpListenerException e) { Log.Error("The local tool server could not start; Claude Code answers will have no tools", e); }

        var observer = new Observer([new UiaTextExtractor(), new OcrTextExtractor(), new UiaElementExtractor()], Settings.ChangeThresholdBits);
        var pipeline = new TutorPipeline(new MasteryTutor(), new Verifier(), Store, Settings, Log);
        Session = new TutorSession(Settings, Providers, pipeline, Store, new ScreenCaptureService(), observer, Annotations, Policy, Log);
        Overlays = new AnnotationOverlayManager(Annotations);
        // The edge wave follows the session: on from the moment a capture starts until its answer is finished.
        Session.Changed += () => Overlays.SetLooking(Session.IsLooking);
        Overlays.CanSample = () => !Session.CaptureBlocked;
        var engine = new Vernier.Core.Context.ContextEngine(new Vernier.Core.Context.ContextOptions { Retention = TimeSpan.FromDays(Math.Max(1, Settings.ContextRetentionDays)) },
            new Vernier.Core.Context.LearningEpisodeStore(Store));
        // Metadata context stops in Pause and Share-Safe; reading screen text also needs screen capture allowed.
        Context = new Context.ContextHub(engine,
            allowed: () => Settings.AllowActivityContext && !Session.IsPaused && Policy.Mode != CapturePolicyMode.ShareSafe,
            mayRead: () => Settings.AllowActivityContext && Settings.ReadScreenForContext && !Session.CaptureBlocked);
        Context.ReadingChanged += reading => OnUi(() => Overlays.SetWatching(reading));
        _ = new Context.EpisodeSummariser(engine); // only acts when a local context model is chosen in Settings
        Overlays.MarksWentStale += Session.NoteMarksWentStale;
        Overlays.MarksRefit += Session.NoteMarksRefit;

        try { SpeechOut = new SapiSpeechOutput(); }
        catch (Exception e) { Log.Warn("Speech output unavailable: " + e.Message); }
        SpeechIn = new SapiSpeechInput(); // the engine is only created when the learner first holds the talk button
        Log.Info($"Started. Windows build {Environment.OSVersion.Version.Build}, schema v{Store.SchemaVersion}, provider {Settings.ActiveProviderId}.");
        _initialized = true;
    }

    // Settings are checked again on every call, so turning capture off or entering Share-Safe mid-answer stops the next look.
    private static string? ToolGate(ToolDefinition tool) =>
        tool.Name == "look" && Session.CaptureBlocked ? Session.CaptureBlockedReason + " The screen was not captured."
        : tool.Name is "get_context" or "media_control" or "video_info" && !Settings.AllowActivityContext ? "The learner has not switched on activity context (Settings › Privacy), so Vernier does not track windows or media."
        : tool.Name.StartsWith("gdocs_", StringComparison.Ordinal) && !Google.IsConnected ? "Google Docs is not connected (Settings › Connectors)."
        : (tool.Name.StartsWith("notes_", StringComparison.Ordinal) || tool.Name == "gdocs_read") && !Session.TurnProviderIsLocal && !Settings.AllowSendNotesToRemote ? "Sending notes to this provider is not allowed (Settings › Privacy), so they stay on this computer."
        : tool.Name.StartsWith("notes_", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(Settings.VaultPath) ? "No Obsidian vault is connected (Notes & files), so there are no notes to use."
        : tool.Name is "computer_act" or "write_to_app" && !Settings.AllowComputerControl ? "Assisted input is off (Settings › Assisted input), so the tutor cannot operate the computer. Give the learner the steps to do themselves."
        : tool.Name == "computer_act" && Session.CaptureBlocked ? Session.CaptureBlockedReason + " Without the screen the tutor cannot see where to act."
        : null;

    public static void SaveSettings()
    {
        try { SettingsStore.Save(Settings); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Error("Could not save settings", e); }
    }

    public static void OnUi(Action action) => Ui.BeginInvoke(DispatcherPriority.Normal, action);

    /// <summary>For streamed text: below input and rendering, so the pen and animations stay smooth while an answer arrives.</summary>
    public static void OnUiBackground(Action action) => Ui.BeginInvoke(DispatcherPriority.Background, action);

    /// <summary>Registers a window for the capture policy as soon as it has a handle, and unregisters it on close.</summary>
    public static void TrackWindow(Window window, string name)
    {
        nint registered = 0;
        void Register()
        {
            registered = new WindowInteropHelper(window).Handle;
            if (registered != 0) Policy.Register(registered, name);
        }
        if (new WindowInteropHelper(window).Handle != 0) Register();
        else window.SourceInitialized += (_, _) => Register();
        // The handle is gone by the time Closed fires, so the one captured at registration is used.
        window.Closed += (_, _) => { if (registered != 0) Policy.Unregister(registered); };
    }

    /// <summary>
    /// Runs a capture with every Vernier window temporarily excluded, so the tutor never reads its
    /// own interface back. The user's chosen policy is restored afterwards.
    /// </summary>
    public static async Task<T> WithSelfExcludedAsync<T>(Func<T> capture)
    {
        var alreadyExcluded = Policy.Mode != CapturePolicyMode.Standard;
        var handles = Policy.States.Select(s => s.Hwnd).ToList();
        if (!alreadyExcluded)
        {
            foreach (var h in handles) Win32.SetWindowDisplayAffinity(h, DisplayAffinity.ExcludeFromCapture);
            await Task.Delay(80); // one or two compositor frames
        }
        // GDI capture, ink painting, scaling and PNG encoding run on their own STA thread, never the UI thread.
        try { return await StaThread.RunAsync(capture); }
        finally
        {
            if (!alreadyExcluded) Policy.Reapply();
        }
    }

    /// <summary>Theme ids as stored in settings, with their display names. "dark" and "light" keep their old ids.</summary>
    public static readonly (string Id, string Name)[] Themes = [("dark", "Night"), ("aurora", "Aurora"), ("nebula", "Nebula"), ("glacier", "Glacier"), ("light", "Paper")];

    public static void ApplyTheme(string theme)
    {
        var file = theme switch { "light" => "Light", "aurora" => "Aurora", "nebula" => "Nebula", "glacier" => "Glacier", _ => "Dark" };
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = new Uri($"/Vernier;component/Themes/{file}.xaml", UriKind.Relative) };
        Depth.Refresh();
    }

    public static void Shutdown()
    {
        // Once per start: services are disposed here and must not be disposed (or used) a second time.
        if (!_initialized) return;
        _initialized = false;
        Tray?.Dispose();
        Tray = null;
        Session?.Pause();
        Overlays?.Dispose();
        Context?.Dispose();
        ClaudeCodeProvider.DiscardWarm();
        Mcp?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        Mcp = null;
        (SpeechOut as IDisposable)?.Dispose();
        (SpeechIn as IDisposable)?.Dispose();
        Store?.Dispose();
    }
}

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            AppServices.Log.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show("Vernier hit an unexpected error and will try to continue.\n\n" + SecretRedactor.Redact(args.Exception.Message),
                "Vernier", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        // --data <dir> keeps tests and screenshots away from the real profile.
        var dataAt = Array.IndexOf(e.Args, "--data");
        try
        {
            AppServices.Initialize(dataAt >= 0 && dataAt + 1 < e.Args.Length ? e.Args[dataAt + 1] : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show("Vernier could not start: " + ex.Message, "Vernier", MessageBoxButton.OK, MessageBoxImage.Error);
            AppServices.Quitting = true;
            Shutdown(2);
            return;
        }
        AppServices.ApplyTheme(AppServices.Settings.Theme);

        ShutdownMode = ShutdownMode.OnExplicitShutdown; // the app lives in the notification area, not in a window
        var main = new MainWindow();
        Depth.Attach(main, acrylic: false); // caption follows the theme
        MainWindow = main;
        main.Show();
        AppServices.Overlays.Start(); // the glass goes up once and stays
        // The first formula layout loads WpfMath's fonts (~300 ms); do it while idle, not inside the tutor's first overlay_write.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
            NoteRenderer.Render(new Vernier.Core.Annotations.NoteContent("", [new(Vernier.Core.Annotations.NoteLineKind.Step, "warm", @"v=\frac{1}{2}at^{2}")], true), 200, 1));
        StartTray(main);

        var shots = Array.IndexOf(e.Args, "--screenshots");
        if (shots >= 0 && shots + 1 < e.Args.Length)
        {
            _ = main.SaveReviewScreenshotsAsync(e.Args[shots + 1]).ContinueWith(_ => Dispatcher.Invoke(Quit));
            return;
        }
        if (e.Args.Contains("--smoke"))
        {
            // Packaging smoke test: the real window is up and every page has been constructed; exit cleanly.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                main.VisitAllPages(); main.OpenAskBar(pen: true); main.Bar?.HideBar();
                var problem = await SmokeCheckAsync();
                AppServices.Log.Info(problem is null ? $"Smoke: native module (ABI {Native.Version}, loaded={Native.Available}), MCP server and tools all answered." : "Smoke FAILED: " + problem);
                AppServices.Quitting = true;
                Shutdown(problem is null ? 0 : 3);
            };
            timer.Start();
            return;
        }
        if (!AppServices.Settings.FirstRunComplete) new FirstRunWindow { Owner = main }.ShowDialog();
        else if (!AppServices.Settings.ContextNoticeShown)
        {
            // Upgrading from a build without the context engine: an explicit choice, never a silent default.
            var keep = ConfirmDialog.Ask(main, "Vernier now keeps context of your work",
                "So the tutor knows which video, page or problem you mean, Vernier keeps a record on this computer of the apps, pages and videos you use, for how long, and where you rewound. " +
                "With the screen permission on, it also reads the text of the window in front when it changes; that text stays in memory and is never saved.\n\n" +
                "Private browser windows and password managers are skipped. Pause and Share-Safe stop it. A small light at the top of the screen shows while it reads. " +
                "It is sent only with a question you ask, and the ask bar shows what goes with it. You can forget the last hour, today or everything in Settings › Privacy.",
                "Keep context on");
            AppServices.Settings.AllowActivityContext = keep;
            AppServices.Settings.ContextNoticeShown = true;
            AppServices.SaveSettings();
        }
        if (!AppServices.Settings.WebResearchNoticeShown)
        {
            // A new kind of outbound request: an explicit choice, never a silent default.
            var research = ConfirmDialog.Ask(main, "The tutor now researches on the web",
                "So it does not guess how software works or what a page claims, the tutor searches the web and reads several pages before it explains a web page, an app such as Blender, " +
                "or anything it is not sure of. The search words are written by the tutor from your question and screen, with names, emails and numbers taken out; the searches go to DuckDuckGo " +
                "and the pages are fetched from this computer. Each search is shown while it runs. Pause and Share-Safe stop it, and you can turn it off in Settings › Privacy.",
                "Keep research on");
            AppServices.Settings.AllowWebResearch = research;
            AppServices.Settings.WebResearchNoticeShown = true;
            AppServices.SaveSettings();
        }
    }

    /// <summary>The published app's own self-check: the native module loads when it was shipped, and the MCP server answers a real client.</summary>
    private static async Task<string?> SmokeCheckAsync()
    {
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "Vernier.Native.dll")) && !Native.Available) // linked next to the exe (Vernier.App.csproj)
            return $"Vernier.Native.dll is in the build but is not in use (reports ABI {Native.Version}, this build needs {Native.Abi}; last error {Native.LastError:X8}).";
        if (AppServices.Mcp is not { } mcp) return "the MCP server did not start.";
        var turn = new ToolTurn(ScreenTools.Names, CancellationToken.None);
        using var grant = mcp.Open(turn);
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        async Task<JsonNode?> Call(string json)
        {
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, mcp.Url) { Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", turn.Token);
            using var response = await http.SendAsync(request);
            return JsonNode.Parse(await response.Content.ReadAsStringAsync());
        }
        try
        {
            var init = await Call("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}""");
            if (init?["result"]?["serverInfo"]?["name"]?.GetValue<string>() != "vernier") return "the MCP server did not answer initialize.";
            var list = await Call("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
            var names = list?["result"]?["tools"]?.AsArray().Select(t => t?["name"]?.GetValue<string>()).ToList() ?? [];
            return names.Contains("overlay_marks") && names.Contains("overlay_write") && names.Contains("look") ? null : "the MCP server did not list Vernier's tools (" + string.Join(", ", names) + ").";
        }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return "the MCP server could not be reached: " + e.GetType().Name;
        }
    }

    private void StartTray(MainWindow main)
    {
        var tray = AppServices.Tray = new TrayIcon(Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Vernier.exe"), "Vernier · tutor on your screen");
        tray.Activated = main.ShowWorkspace;
        if (AppServices.Context is { } hub) _ = new Context.Proactive(hub, tray, question => { main.OpenAskBar(pen: false); main.Bar?.Prefill(question); });
        tray.Menu = () =>
        [
            new($"Ask about my screen\t{Views.MainWindow.AskHotkey}", () => main.OpenAskBar(pen: false)),
            new($"Pen: mark something\t{Views.MainWindow.PenHotkey}", () => main.OpenAskBar(pen: true)),
            TrayIcon.Item.Separator,
            new("Clear marks", AppServices.Annotations.Clear, Enabled: AppServices.Annotations.Items.Count > 0),
            new("Pause\tCtrl+Alt+P", main.TogglePause, Checked: AppServices.Session.IsPaused),
            new("Hide everything\tCtrl+Alt+H", main.HideAll),
            TrayIcon.Item.Separator,
            new("Open workspace", main.ShowWorkspace),
            new("Quit Vernier", Quit),
        ];
    }

    private void Quit()
    {
        AppServices.Quitting = true;
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppServices.Shutdown();
        base.OnExit(e);
    }
}
