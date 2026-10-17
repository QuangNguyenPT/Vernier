using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;

namespace Vernier.App.Views;

/// <summary>The workspace conversation: the full transcript and typed questions. Screen questions come from the ask bar and show up here too.</summary>
public partial class TutorPanel : UserControl
{
    private TutorSession Session => AppServices.Session;
    private bool _syncing;

    public TutorPanel()
    {
        InitializeComponent();
        Items.ItemsSource = Session.Transcript;
        Loaded += (_, _) =>
        {
            Session.Changed += Sync;
            Session.AnswerUpdated += OnAnswerUpdated;
            Session.AnswerCompleted += OnAnswerCompleted;
            Sync();
        };
        Unloaded += (_, _) =>
        {
            Session.Changed -= Sync;
            Session.AnswerUpdated -= OnAnswerUpdated;
            Session.AnswerCompleted -= OnAnswerCompleted;
        };
    }

    public void FocusInput() => Input.Focus();

    private void Sync()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(Sync)); return; }
        _syncing = true;
        foreach (var rb in Modes.Children.OfType<RadioButton>())
            rb.IsChecked = (string)rb.Tag == Session.Mode.ToString();

        Scale.Current = Session.Stage;
        Scale.Busy = Session.IsBusy;
        Empty.Visibility = Session.Transcript.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AskKey.Text = MainWindow.AskHotkey;
        PenKey.Text = MainWindow.PenHotkey;
        Send.Content = Session.IsBusy ? "Stop" : "Ask";
        StatusText.Text = Session.Status;

        var pending = Session.PendingActions;
        ActionsBar.Visibility = pending.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ActionsText.Text = $"The tutor proposes {pending.Count} input step(s) in the window you were using. Nothing happens until you review and approve them.";

        var settings = AppServices.Settings;
        SpeakToggle.IsChecked = settings.SpeakAnswers;
        SpeakToggle.IsEnabled = AppServices.SpeechOut is not null;
        SpeechPause.IsEnabled = SpeechStop.IsEnabled = AppServices.SpeechOut?.IsSpeaking == true;
        Talk.IsEnabled = settings.AllowMicrophone && !Session.IsBusy;
        Talk.ToolTip = settings.AllowMicrophone
            ? "Hold while speaking; release to put the words in the box. Local Windows speech recognition; audio is not saved."
            : "The microphone is off. Turn it on in Settings › Privacy.";
        _syncing = false;
    }

    private void OnAnswerUpdated(TranscriptItem _) => Scroll.ScrollToEnd();

    private void OnAnswerCompleted(string text)
    {
        if (AppServices.Settings.SpeakAnswers && AppServices.SpeechOut is { } voice)
            voice.Speak(Vernier.Core.Agents.AnswerText.ForSpeech(text), AppServices.Settings.SpeechRate); // formulas read as words, not TeX
        Sync();
    }

    private void OnMode(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        Session.Mode = Enum.Parse<TutorMode>((string)((RadioButton)sender).Tag);
        Sync();
    }

    private void OnNew(object sender, RoutedEventArgs e) => Session.NewConversation();

    private async void OnSend(object sender, RoutedEventArgs e)
    {
        if (Session.IsBusy) { Session.Cancel(); return; }
        var question = Input.Text;
        if (string.IsNullOrWhiteSpace(question)) return;
        AppServices.SpeechOut?.Stop(); // a new question interrupts the spoken answer
        Input.Clear();
        await Session.AskAsync(question);
        Input.Focus();
    }

    private void OnInputKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            OnSend(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            if (Session.IsBusy) Session.Cancel(); else AppServices.SpeechOut?.Stop();
            e.Handled = true;
        }
    }

    // ---------- assisted input ----------

    private void OnDiscardActions(object sender, RoutedEventArgs e) => Session.ClearPendingActions();

    private async void OnRunActions(object sender, RoutedEventArgs e) => await AssistedInput.ReviewAndRunAsync(Window.GetWindow(this));

    // ---------- voice ----------

    private void OnTalkDown(object sender, MouseButtonEventArgs e) => StartListening();
    private void OnTalkUp(object sender, MouseButtonEventArgs e) => StopListening();
    private void OnTalkKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Space && !e.IsRepeat) { StartListening(); e.Handled = true; } }
    private void OnTalkKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Space) { StopListening(); e.Handled = true; } }

    private void StartListening()
    {
        if (!AppServices.Settings.AllowMicrophone || AppServices.SpeechIn is not { } mic || mic.IsListening) return;
        AppServices.SpeechOut?.Stop();
        try
        {
            mic.Start();
            Talk.Content = "● Listening…";
            Talk.Foreground = (Brush)FindResource("Err");
            StatusText.Text = "Microphone on. Release to stop.";
        }
        catch (InvalidOperationException e)
        {
            StatusText.Text = e.Message;
            AppServices.Log.Warn(e.Message);
        }
    }

    private async void StopListening()
    {
        if (AppServices.SpeechIn is not { IsListening: true } mic) return;
        var heard = await mic.StopAsync();
        Talk.Content = "Hold to talk";
        Talk.ClearValue(ForegroundProperty);
        if (heard.Length == 0) { StatusText.Text = "Nothing was recognised. Try again, or type."; return; }
        Input.Text = (Input.Text.Trim() + " " + heard).Trim();
        Input.CaretIndex = Input.Text.Length;
        Input.Focus();
        StatusText.Text = "Microphone off. Check the text, then press Enter.";
    }

    private void OnSpeakToggle(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.SpeakAnswers = SpeakToggle.IsChecked == true;
        AppServices.SaveSettings();
        if (!AppServices.Settings.SpeakAnswers) AppServices.SpeechOut?.Stop();
        else if (Session.LastTurn is { } turn) AppServices.SpeechOut?.Speak(Vernier.Core.Agents.AnswerText.ForSpeech(turn.Output.Text), AppServices.Settings.SpeechRate);
        Sync();
    }

    private bool _speechPaused;

    private void OnSpeechPause(object sender, RoutedEventArgs e)
    {
        if (_speechPaused) AppServices.SpeechOut?.Resume(); else AppServices.SpeechOut?.Pause();
        _speechPaused = !_speechPaused;
        SpeechPause.Content = _speechPaused ? "Resume voice" : "Pause voice";
    }

    private void OnSpeechStop(object sender, RoutedEventArgs e)
    {
        AppServices.SpeechOut?.Stop();
        _speechPaused = false;
        SpeechPause.Content = "Pause voice";
        Sync();
    }
}

/// <summary>
/// The pipeline drawn as a ruled scale: one tick per stage, the active one marked in vermilion.
/// It tells the learner what the tutor is doing without a spinner.
/// </summary>
public sealed class StageScale : FrameworkElement
{
    private static readonly (PipelineStage Stage, string Label)[] Stages =
    [
        (PipelineStage.Observe, "OBSERVE"), (PipelineStage.Interpret, "INTERPRET"), (PipelineStage.Teach, "TEACH"), (PipelineStage.Verify, "VERIFY"),
        (PipelineStage.Correct, "CORRECT"), (PipelineStage.Confirm, "CONFIRM"), (PipelineStage.Present, "PRESENT"), (PipelineStage.UpdateMastery, "MASTERY"),
    ];
    private static readonly Typeface Face = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private PipelineStage? _current;
    private bool _busy;

    public PipelineStage? Current { get => _current; set { _current = value; InvalidateVisual(); } }
    public bool Busy { get => _busy; set { _busy = value; InvalidateVisual(); } }

    protected override void OnRender(DrawingContext dc)
    {
        var rule = (Brush)FindResource("Rule");
        var faint = (Brush)FindResource("InkFaint");
        var ink = (Brush)FindResource("Ink");
        var accent = (Brush)FindResource("Accent");
        var w = ActualWidth;
        if (w < 40) return;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var showLabels = w >= 520;
        var step = w / Stages.Length;

        dc.DrawLine(new Pen(rule, 1), new Point(0, 0.5), new Point(w, 0.5));
        // Minor graduations between stages, like the fixed scale of a caliper.
        for (var x = 0.0; x < w; x += step / 5) dc.DrawLine(new Pen(rule, 1), new Point(Math.Round(x) + 0.5, 0), new Point(Math.Round(x) + 0.5, 4));

        for (var i = 0; i < Stages.Length; i++)
        {
            var x = Math.Round(i * step) + 0.5;
            var active = _busy && Stages[i].Stage == _current;
            dc.DrawLine(new Pen(active ? accent : faint, active ? 2 : 1), new Point(x, 0), new Point(x, active ? 14 : 9));
            if (!showLabels && !active) continue;
            var text = new FormattedText(Stages[i].Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 9.5, active ? ink : faint, dpi);
            dc.DrawText(text, new Point(Math.Min(x + 4, w - text.Width), 14));
        }
    }
}

/// <summary>A plain approval dialog: what will happen, in full, and two unambiguous buttons.</summary>
public static class ConfirmDialog
{
    public static bool Ask(Window? owner, string title, string body, string okLabel, bool mono = false, bool danger = false)
    {
        var (dialog, ok, cancel) = Build(owner, title, body, okLabel, mono, danger);
        ok.Click += (_, _) => dialog.DialogResult = true;
        return dialog.ShowDialog() == true;
    }

    /// <summary>
    /// The same question as a card that stays on top without blocking Vernier (Stop still works), and closes by itself as a
    /// "no" when <paramref name="ct"/> is cancelled, so a timed-out request never leaves a live button behind.
    /// </summary>
    /// <remarks>
    /// The card does not take focus and has no default button, so a keystroke the learner was typing elsewhere can never
    /// answer it, and its buttons only respond after half a second. Result: true = allow, false = cancel or closed,
    /// null = Esc (stop the tutor altogether).
    /// </remarks>
    public static Task<bool?> AskAsync(string title, string body, string okLabel, bool danger, CancellationToken ct)
    {
        var (dialog, ok, cancel) = Build(null, title, body, okLabel, mono: false, danger);
        dialog.Topmost = true;
        dialog.ShowActivated = false;
        ok.IsDefault = cancel.IsDefault = false;
        cancel.IsCancel = false; // Esc is handled below: it means "stop", not just "no to this one"
        ok.IsEnabled = cancel.IsEnabled = false;
        var arm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        arm.Tick += (_, _) => { arm.Stop(); ok.IsEnabled = cancel.IsEnabled = true; };
        var answer = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ok.Click += (_, _) => { answer.TrySetResult(true); dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            e.Handled = true;
            answer.TrySetResult(null);
            dialog.Close();
        };
        dialog.Closed += (_, _) => { arm.Stop(); answer.TrySetResult(false); };
        var closeOnCancel = ct.Register(() => dialog.Dispatcher.BeginInvoke(dialog.Close));
        _ = answer.Task.ContinueWith(_ => closeOnCancel.Dispose(), TaskScheduler.Default);
        dialog.Show();
        arm.Start();
        return answer.Task;
    }

    private static (Window Dialog, Button Ok, Button Cancel) Build(Window? owner, string title, string body, string okLabel, bool mono, bool danger)
    {
        var text = new TextBox
        {
            Text = body, IsReadOnly = true, TextWrapping = mono ? TextWrapping.NoWrap : TextWrapping.Wrap, BorderThickness = new Thickness(0),
            Background = Brushes.Transparent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = mono ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            FontFamily = (FontFamily)Application.Current.FindResource(mono ? "MonoFont" : "UiFont"), FontSize = mono ? 12 : 13.5, IsTabStop = false,
        };
        var ok = new Button { Content = okLabel, IsDefault = !danger, MinWidth = 110, Margin = new Thickness(8, 0, 0, 0), Style = (Style)Application.Current.FindResource(danger ? "Danger" : "Primary") };
        var cancel = new Button { Content = "Cancel", IsCancel = true, IsDefault = danger, MinWidth = 90 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        var heading = new TextBlock { Text = title, Style = (Style)Application.Current.FindResource("Heading") };
        var panel = new DockPanel { Margin = new Thickness(22) };
        DockPanel.SetDock(heading, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(heading);
        panel.Children.Add(buttons);
        panel.Children.Add(text);

        var dialog = new Window
        {
            Title = "Vernier", Content = panel, Owner = owner is { IsVisible: true } ? owner : null,
            WindowStartupLocation = owner is { IsVisible: true } ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Width = mono ? 760 : 520, MaxHeight = 640, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.CanResize, ShowInTaskbar = false,
            Background = (Brush)Application.Current.FindResource("Bg"),
        };
        AppServices.TrackWindow(dialog, "Dialog");
        return (dialog, ok, cancel);
    }
}
