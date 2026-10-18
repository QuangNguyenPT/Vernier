using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Vernier.Core.Providers;
using Vernier.Core.Settings;

namespace Vernier.App.Views;

public partial class SettingsPage : UserControl, IRefreshable
{
    private static readonly ProviderSettings NoChecker = new() { Id = "", DisplayName = "None — code checks only" };
    private bool _loading;

    private sealed record AccountRow(string Id, string Name, string State, string Detail, bool SignedIn, bool CanAct, string ActionLabel)
    {
        public string ActionName => $"{ActionLabel} ({Name})";
        public string UseName => $"Use {Name} for tutoring";
    }

    public SettingsPage() => InitializeComponent();

    // ---------- accounts ----------

    private async Task ShowAccountsAsync()
    {
        var statuses = await Task.WhenAll(CliAccounts.All.Select(a => CliAccounts.StatusAsync(a, CancellationToken.None)));
        Accounts.ItemsSource = statuses.Select(s => new AccountRow(
            s.Account.Id, s.Account.Name,
            s.SignedIn ? "signed in" : s.Installed ? "signed out" : "not installed",
            $"{s.Account.Service}. {s.Detail}" + (s.SignedIn && S.ActiveProviderId == s.Account.ProviderId ? " In use for tutoring." : ""),
            s.SignedIn, s.Installed, s.SignedIn ? "Sign in again" : "Sign in…")).ToList();
    }

    private void OnAccountsRefresh(object sender, RoutedEventArgs e) => _ = ShowAccountsAsync();

    // Opens a visible terminal running the vendor's own sign-in command. Everything after that is between the user, the tool and the browser.
    private void OnAccountAction(object sender, RoutedEventArgs e)
    {
        var account = CliAccounts.All.First(a => a.Id == (string)((Button)sender).Tag);
        if (CliAccounts.LoginCommand(account) is not { } command) return;
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = true };
            start.ArgumentList.Add("/k");
            start.ArgumentList.Add(command.File);
            foreach (var arg in command.Args) start.ArgumentList.Add(arg);
            System.Diagnostics.Process.Start(start);
            AppServices.Log.Info($"Opened the {account.Name} sign-in in a terminal.");
            DataStatus.Text = "";
            MessageBox.Show(Window.GetWindow(this)!, $"A terminal has opened for the {account.Name} sign-in. Follow it in your browser, then come back and press Check again.", "Vernier", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(Window.GetWindow(this)!, "The sign-in terminal could not be opened: " + ex.Message, "Vernier", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnAccountUse(object sender, RoutedEventArgs e)
    {
        var account = CliAccounts.All.First(a => a.Id == (string)((Button)sender).Tag);
        S.ActiveProviderId = account.ProviderId;
        AppServices.SaveSettings();
        AppServices.Session.NewConversation();
        Refresh();
    }

    private static AppSettings S => AppServices.Settings;
    private ProviderSettings? Current => ProviderBox.SelectedItem as ProviderSettings;

    public void Refresh()
    {
        _loading = true;
        ProviderBox.ItemsSource = S.Providers;
        ProviderBox.SelectedItem = S.ActiveProvider ?? S.Providers.FirstOrDefault();
        CheckerBox.ItemsSource = new[] { NoChecker }.Concat(S.Providers).ToList();
        CheckerBox.SelectedItem = S.Providers.FirstOrDefault(p => p.Id == S.VerifierProviderId) ?? NoChecker;

        AllowCapture.IsChecked = S.AllowScreenCapture;
        AllowMic.IsChecked = S.AllowMicrophone;
        AllowActivity.IsChecked = S.AllowActivityContext;
        AllowResearch.IsChecked = S.AllowWebResearch;
        ReadScreen.IsChecked = S.ReadScreenForContext;
        Proactive.IsChecked = S.ProactiveSuggestions;
        RetentionText.Text = S.ContextRetentionDays.ToString(CultureInfo.InvariantCulture);
        ExcludedText.Text = string.Join(", ", S.ContextExcludedApps);
        AllowNotes.IsChecked = S.AllowSendNotesToRemote;
        AllowControl.IsChecked = S.AllowComputerControl;
        GoogleClientIdBox.Text = S.GoogleClientId ?? "";
        SyncGoogle();
        EdgeText.Text = S.MaxImageEdge.ToString(CultureInfo.InvariantCulture);
        RetainText.Text = S.RetainSessionsDays.ToString(CultureInfo.InvariantCulture);

        LevelText.Text = S.Learner.Level;
        GoalsText.Text = S.Learner.Goals;
        PaceText.Text = S.Learner.Pace;
        AssessText.Text = S.Learner.Assessment;

        RateSlider.Value = S.SpeechRate;
        RateText.Text = $"{S.SpeechRate:0.00}×";
        IntervalsText.Text = string.Join(", ", S.ReviewIntervalsDays);
        if (ThemePicker.Children.Count == 0)
            foreach (var (id, name) in AppServices.Themes)
            {
                var option = new RadioButton { Content = name.ToUpperInvariant(), Tag = id, GroupName = "theme", Style = (Style)FindResource("SegmentRadio") };
                AutomationProperties.SetName(option, name + " theme");
                option.Click += OnTheme;
                ThemePicker.Children.Add(option);
            }
        foreach (RadioButton option in ThemePicker.Children) option.IsChecked = (string)option.Tag == S.Theme;

        DataPath.Text = $"Everything is stored on this computer in {AppServices.DataDir}. A backup of the learning database is made at each start (the last 7 are kept).";
        RemoveSample.IsEnabled = AppServices.Store.HasSampleData;
        AutoRouteBox.IsChecked = S.AutoRoute;
        BuildRoutes();
        var locals = S.Providers.Where(p => p.IsLocal).ToList();
        ContextModelBox.ItemsSource = new[] { CodeOnly }.Concat(locals.Select(p => p.DisplayName + (string.IsNullOrWhiteSpace(p.Model) ? " (choose a model above first)" : " · " + p.Model))).ToList();
        var current = locals.FindIndex(p => p.Id == S.ContextModelProviderId);
        ContextModelBox.SelectedIndex = current < 0 ? 0 : current + 1;
        _loading = false;
        ShowProvider();
        _ = ShowAccountsAsync();
    }

    // ---------- models per task ----------

    private const string DefaultChoice = "Default model (above)";

    private void BuildRoutes()
    {
        RoutesPanel.Children.Clear();
        RoutesPanel.IsEnabled = S.AutoRoute;
        var options = ModelRouter.Options(S);
        foreach (var task in Enum.GetValues<TaskKind>())
        {
            var route = S.Routes.FirstOrDefault(r => r.Task == task);
            var row = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            var label = new TextBlock { Text = char.ToUpperInvariant(ModelRouter.Name(task)[0]) + ModelRouter.Name(task)[1..], Style = (Style)FindResource("Soft"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            var model = new ComboBox { ItemsSource = new[] { DefaultChoice }.Concat(options.Select(o => o.Label)).ToList(), Margin = new Thickness(0, 0, 8, 0) };
            AutomationProperties.SetName(model, "Model for " + ModelRouter.Name(task));
            model.SelectedItem = route is null ? DefaultChoice
                : options.FirstOrDefault(o => o.Provider.Id == route.ProviderId && (o.Provider.Model ?? "") == (route.Model ?? S.Providers.FirstOrDefault(p => p.Id == route.ProviderId)?.Model ?? "")).Label ?? DefaultChoice;
            var effort = new ComboBox { ItemsSource = ModelRouter.Efforts.Select(e => e.Length == 0 ? "default effort" : e).ToList() };
            AutomationProperties.SetName(effort, "Effort for " + ModelRouter.Name(task));
            effort.SelectedItem = string.IsNullOrEmpty(route?.Effort) ? "default effort" : route!.Effort;
            void Changed()
            {
                if (_loading) return;
                S.Routes.RemoveAll(r => r.Task == task);
                if (model.SelectedItem is string chosen && chosen != DefaultChoice && options.FirstOrDefault(o => o.Label == chosen) is { Provider: { } p })
                    S.Routes.Add(new ModelRoute { Task = task, ProviderId = p.Id, Model = p.Model, Effort = effort.SelectedItem is string e && e != "default effort" ? e : null });
                AppServices.SaveSettings();
                AppServices.Session.DiscardPrewarm();
            }
            model.SelectionChanged += (_, _) => Changed();
            effort.SelectionChanged += (_, _) => Changed();
            Grid.SetColumn(model, 1);
            Grid.SetColumn(effort, 2);
            row.Children.Add(label);
            row.Children.Add(model);
            row.Children.Add(effort);
            RoutesPanel.Children.Add(row);
        }
    }

    private void OnContextNumbers(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (int.TryParse(RetentionText.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)) S.ContextRetentionDays = Math.Clamp(days, 1, 365);
        S.ContextExcludedApps = [.. ExcludedText.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(a => a.Replace(".exe", "", StringComparison.OrdinalIgnoreCase))];
        AppServices.SaveSettings();
    }

    private void Forget(DateTimeOffset since, string what)
    {
        AppServices.Context?.Engine.ForgetSince(since);
        DataStatus.Text = what + " was forgotten";
        AppServices.Log.Info("Context forgotten: " + what);
    }

    private void OnForgetHour(object sender, RoutedEventArgs e) => Forget(DateTimeOffset.UtcNow.AddHours(-1), "the last hour of context");
    private void OnForgetToday(object sender, RoutedEventArgs e) => Forget(new DateTimeOffset(DateTime.Today), "today's context");
    private void OnForgetAll(object sender, RoutedEventArgs e) => Forget(DateTimeOffset.MinValue, "all context");

    private const string CodeOnly = "Built by code only (no model)";

    private void OnContextModel(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var locals = S.Providers.Where(p => p.IsLocal).ToList();
        S.ContextModelProviderId = ContextModelBox.SelectedIndex > 0 ? locals[ContextModelBox.SelectedIndex - 1].Id : null;
        AppServices.SaveSettings();
    }

    private void OnRouting(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        S.AutoRoute = AutoRouteBox.IsChecked == true;
        RoutesPanel.IsEnabled = S.AutoRoute;
        AppServices.SaveSettings();
        AppServices.Session.DiscardPrewarm();
    }

    // ---------- provider ----------

    private void ShowProvider()
    {
        if (Current is not { } p) return;
        _loading = true;
        var isCli = p.Kind is ProviderKinds.ClaudeCode or ProviderKinds.CodexCli;
        ModelText.Text = p.Model;
        ModelBox.Visibility = Visibility.Collapsed;
        EndpointText.Text = p.BaseUrl ?? "";
        EndpointLabel.Visibility = EndpointText.Visibility = isCli || p.Kind == ProviderKinds.Anthropic && string.IsNullOrEmpty(p.BaseUrl) ? Visibility.Collapsed : Visibility.Visible;
        KeyLabel.Visibility = KeyRow.Visibility = KeyStatus.Visibility = p.ApiKeyName is null ? Visibility.Collapsed : Visibility.Visible;
        KeyBox.Clear();
        VisionCheck.IsChecked = p.SupportsVision;
        VisionCheck.Visibility = p.Kind is ProviderKinds.OpenAiCompatible or ProviderKinds.Gemini ? Visibility.Visible : Visibility.Collapsed;
        if (p.ApiKeyName is { } name)
            KeyStatus.Text = $"key {name}: {AppServices.Secrets.Source(name)} · stored in windows credential manager, never in a file";
        try { ProviderNotes.Text = AppServices.Providers.Create(p).Capabilities.Notes + (isCli ? " Leave the model empty to use the tool's own default." : ""); }
        catch (ProviderException e) { ProviderNotes.Text = e.Message; }
        TestResult.Text = "";
        _loading = false;
    }

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        AppServices.Session.DiscardPrewarm(); // the next question may need a different model, prompt or tools
        if (_loading || Current is not { } p) return;
        // Switching provider is always an explicit choice made here; nothing else in the app changes it.
        S.ActiveProviderId = p.Id;
        AppServices.SaveSettings();
        AppServices.Session.NewConversation();
        ShowProvider();
    }

    private void OnProviderFieldChanged(object sender, RoutedEventArgs e)
    {
        AppServices.Session.DiscardPrewarm(); // the next question may need a different model, prompt or tools
        if (_loading || Current is not { } p) return;
        p.Model = ModelText.Text.Trim();
        if (EndpointText.Visibility == Visibility.Visible) p.BaseUrl = EndpointText.Text.Trim();
        p.SupportsVision = p.Kind is ProviderKinds.Anthropic or ProviderKinds.ClaudeCode or ProviderKinds.CodexCli || VisionCheck.IsChecked == true;
        AppServices.SaveSettings();
        ShowProvider();
    }

    private void OnCheckerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        S.VerifierProviderId = CheckerBox.SelectedItem is ProviderSettings { Id.Length: > 0 } p ? p.Id : null;
        AppServices.SaveSettings();
    }

    private void OnSaveKey(object sender, RoutedEventArgs e)
    {
        if (Current?.ApiKeyName is not { } name || KeyBox.Password.Length == 0) return;
        try
        {
            AppServices.Secrets.Set(name, KeyBox.Password.Trim());
            AppServices.Log.Info($"API key {name} saved to Windows Credential Manager.");
        }
        catch (InvalidOperationException ex) { TestResult.Text = ex.Message; }
        ShowProvider();
    }

    private void OnRemoveKey(object sender, RoutedEventArgs e)
    {
        if (Current?.ApiKeyName is not { } name) return;
        AppServices.Secrets.Delete(name);
        ShowProvider();
    }

    private async void OnTest(object sender, RoutedEventArgs e)
    {
        if (Current is not { } p) return;
        OnProviderFieldChanged(sender, e);
        TestButton.IsEnabled = false;
        TestResult.Foreground = (Brush)FindResource("InkSoft");
        TestResult.Text = "Sending one short request…";
        try
        {
            var provider = AppServices.Providers.Create(p);
            var (ok, message) = await Task.Run(() => ChatRunner.TestAsync(provider, CancellationToken.None));
            TestResult.Foreground = (Brush)FindResource(ok ? "Ok" : "Err");
            TestResult.Text = message;
            AppServices.Log.Info($"Connection test for {p.Id}: {(ok ? "ok" : "failed")}");
        }
        catch (ProviderException ex)
        {
            TestResult.Foreground = (Brush)FindResource("Err");
            TestResult.Text = ex.Message;
        }
        finally { TestButton.IsEnabled = true; }
    }

    private async void OnListModels(object sender, RoutedEventArgs e)
    {
        if (Current is not { } p) return;
        OnProviderFieldChanged(sender, e);
        try
        {
            var models = await AppServices.Providers.Create(p).ListModelsAsync(CancellationToken.None);
            if (models.Count == 0) { TestResult.Text = "The service returned no models."; return; }
            _loading = true;
            ModelBox.ItemsSource = models;
            ModelBox.Visibility = Visibility.Visible;
            _loading = false;
            TestResult.Foreground = (Brush)FindResource("InkSoft");
            TestResult.Text = $"{models.Count} model(s) offered. Pick one from the list.";
        }
        catch (ProviderException ex)
        {
            TestResult.Foreground = (Brush)FindResource("Err");
            TestResult.Text = $"{ex.Kind}: {ex.Message}";
        }
        catch (OperationCanceledException) { }
    }

    private void OnModelPicked(object sender, SelectionChangedEventArgs e)
    {
        AppServices.Session.DiscardPrewarm(); // the next question may need a different model, prompt or tools
        if (_loading || ModelBox.SelectedItem is not string model || Current is not { } p) return;
        p.Model = model.StartsWith('(') ? "" : model;
        ModelText.Text = p.Model;
        AppServices.SaveSettings();
    }

    // ---------- privacy and numbers ----------

    private void OnPrivacy(object sender, RoutedEventArgs e)
    {
        AppServices.Session.DiscardPrewarm(); // the next question may need a different model, prompt or tools
        if (_loading) return;
        if (AllowControl.IsChecked == true && !S.AllowComputerControl &&
            !ConfirmDialog.Ask(Window.GetWindow(this), "Allow assisted input?",
                "In Assist mode the tutor may then propose mouse clicks and key presses inside the window you have targeted. " +
                "Positions are estimated from a screenshot and can be wrong, so every plan is listed and numbered on screen and nothing is sent until you approve it.\n\n" +
                "Use it for low-stakes steps in software you can undo in. Do not approve steps you do not understand.", "Allow"))
            AllowControl.IsChecked = false;

        S.AllowScreenCapture = AllowCapture.IsChecked == true;
        S.AllowMicrophone = AllowMic.IsChecked == true;
        S.AllowActivityContext = AllowActivity.IsChecked == true;
        S.AllowWebResearch = AllowResearch.IsChecked == true;
        S.ReadScreenForContext = ReadScreen.IsChecked == true;
        S.ProactiveSuggestions = Proactive.IsChecked == true;
        S.AllowSendNotesToRemote = AllowNotes.IsChecked == true;
        S.AllowComputerControl = AllowControl.IsChecked == true;
        AppServices.SaveSettings();
        if (!S.AllowMicrophone) AppServices.SpeechIn?.Cancel();
        AppServices.Session.ClearPendingActions();
    }

    private void OnNumbers(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (int.TryParse(EdgeText.Text, out var edge)) S.MaxImageEdge = Math.Clamp(edge, 480, 2576);
        if (int.TryParse(RetainText.Text, out var retain)) S.RetainSessionsDays = Math.Clamp(retain, 0, 3650);
        var intervals = IntervalsText.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var d) ? Math.Clamp(d, 1, 365) : 0).Where(d => d > 0).Take(6).ToArray();
        if (intervals.Length > 0) S.ReviewIntervalsDays = intervals;
        AppServices.SaveSettings();
        Refresh();
    }

    private void OnLearner(object sender, RoutedEventArgs e)
    {
        AppServices.Session.DiscardPrewarm(); // the next question may need a different model, prompt or tools
        if (_loading) return;
        S.Learner.Level = LevelText.Text.Trim();
        S.Learner.Goals = GoalsText.Text.Trim();
        S.Learner.Pace = PaceText.Text.Trim();
        S.Learner.Assessment = AssessText.Text.Trim();
        AppServices.SaveSettings();
    }

    private void OnRate(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || RateText is null) return;
        S.SpeechRate = Math.Round(e.NewValue, 2);
        RateText.Text = $"{S.SpeechRate:0.00}×";
        AppServices.SaveSettings();
    }

    private void OnTryVoice(object sender, RoutedEventArgs e) =>
        AppServices.SpeechOut?.Speak("Force equals mass times acceleration. This is how fast I will read answers.", S.SpeechRate);

    private void OnTheme(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string id }) return;
        S.Theme = id;
        AppServices.SaveSettings();
        AppServices.ApplyTheme(S.Theme);
    }

    // ---------- connectors ----------

    private void SyncGoogle()
    {
        var connected = AppServices.Google.IsConnected;
        GoogleConnectButton.Content = connected ? "Disconnect" : "Connect Google Docs…";
        GoogleClientIdBox.IsEnabled = GoogleSecretBox.IsEnabled = !connected;
        GoogleStatus.Text = connected ? "connected" + (S.GoogleNotesDocId is null ? "" : " · Vernier notes exists") : "not connected";
    }

    private async void OnGoogleConnect(object sender, RoutedEventArgs e)
    {
        var google = AppServices.Google;
        GoogleConnectButton.IsEnabled = false;
        try
        {
            if (google.IsConnected)
            {
                await google.DisconnectAsync();
                S.GoogleNotesDocId = null; // a new sign-in cannot open the old document
            }
            else
            {
                var clientId = GoogleClientIdBox.Text.Trim();
                if (clientId.Length == 0) { GoogleStatus.Text = "enter your OAuth client id first"; return; }
                if (GoogleSecretBox.Password.Length > 0) AppServices.Secrets.Set(Connectors.GoogleConnector.ClientSecretName, GoogleSecretBox.Password);
                GoogleSecretBox.Clear();
                S.GoogleClientId = clientId;
                GoogleStatus.Text = "finish signing in in your browser…";
                await google.ConnectAsync(clientId, CancellationToken.None);
                AppServices.Log.Info("Google Docs connected.");
            }
            AppServices.SaveSettings();
        }
        catch (Exception ex) when (ex is Vernier.Core.Connectors.ConnectorException or System.Net.Http.HttpRequestException or System.Net.HttpListenerException or InvalidOperationException)
        {
            AppServices.Log.Warn("Google Docs connection failed: " + ex.GetType().Name);
            MessageBox.Show(Window.GetWindow(this)!, ex.Message, "Vernier", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            GoogleConnectButton.IsEnabled = true;
            SyncGoogle();
        }
    }

    // ---------- data ----------

    private void OnExport(object sender, RoutedEventArgs e) => SaveTo("Vernier learning record|*.json", "vernier-learning.json", AppServices.Store.Export);
    private void OnExportCsv(object sender, RoutedEventArgs e) => SaveTo("CSV|*.csv", "vernier-cards.csv", AppServices.Store.ExportCardsCsv);

    private void SaveTo(string filter, string name, Func<string> content)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = name };
        if (dialog.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, content());
            DataStatus.Text = "exported to " + dialog.FileName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DataStatus.Text = ex.Message; }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Vernier learning record|*.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 32 * 1024 * 1024) { DataStatus.Text = "that file is too large to be a learning record"; return; }
            var (concepts, cards) = AppServices.Store.Import(File.ReadAllText(dialog.FileName));
            DataStatus.Text = $"imported {concepts} concept(s) and {cards} new card(s); existing records were kept";
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { DataStatus.Text = ex.Message; }
    }

    private void OnBackup(object sender, RoutedEventArgs e)
    {
        try { DataStatus.Text = "backup written to " + AppServices.Store.Backup(); }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException) { DataStatus.Text = ex.Message; }
    }

    private void OnRemoveSample(object sender, RoutedEventArgs e)
    {
        AppServices.Store.RemoveSampleData();
        DataStatus.Text = "sample data removed";
        Refresh();
    }

    private void OnErase(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Ask(Window.GetWindow(this), "Erase your whole learning record?",
                "This permanently deletes every session, concept, card, formula, mistake and review on this computer. " +
                "Backups in the backups folder are deleted as well. Settings and saved API keys are kept.\n\nExport first if you might want it back. This cannot be undone.",
                "Erase everything", danger: true)) return;
        AppServices.Session.NewConversation();
        AppServices.Store.DeleteEverything();
        AppServices.Context?.Engine.ForgetSince(DateTimeOffset.MinValue); // the in-memory context too
        foreach (var copy in Directory.GetFiles(Path.GetDirectoryName(AppServices.Store.Path)!, Path.GetFileName(AppServices.Store.Path) + ".before-v*"))
            try { File.Delete(copy); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppServices.Log.Error("Could not delete " + Path.GetFileName(copy), ex); }
        try { if (Directory.Exists(AppServices.Store.BackupDirectory)) Directory.Delete(AppServices.Store.BackupDirectory, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppServices.Log.Error("Could not delete backups", ex); }
        DataStatus.Text = "everything was erased";
        Refresh();
    }
}
