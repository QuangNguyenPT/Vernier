using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Geometry;
using Vernier.Core.Knowledge;

namespace Vernier.App.Views;

/// <summary>
/// The ask bar: the only interactive thing Vernier puts over the learner's work. One line to type
/// in, the pen, and the answer underneath. It appears on a hotkey, sits beside what was marked,
/// and leaves on Esc. The stripe on its left is yellow while it waits for you and blue while the
/// tutor is working.
/// </summary>
public sealed class AskBar : Window
{
    private readonly TextBox _input = new();
    private readonly TextBlock _hint = new(), _status = new(), _meta = new();
    private readonly RichAnswer _answer = new();
    private readonly StackPanel _notes = new();
    private readonly Border _answerHost = new();
    private readonly Rectangle _stripe = new() { Width = 3 };
    private readonly Border _frame = new();
    private readonly ToggleButton _pen = new();
    private readonly Button _send = new(), _undo = new(), _clear = new(), _attach = new(), _steps = new(), _model = new();
    private readonly WrapPanel _chips = new() { Margin = new Thickness(12, 8, 10, 0), Visibility = Visibility.Collapsed };
    // What Vernier knows will go with the question: visible before sending, and earlier work can be left out.
    private readonly TextBlock _context = new() { Margin = new Thickness(16, 0, 8, 6), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _contextToggle = new();
    private readonly DockPanel _contextRow = new() { Visibility = Visibility.Collapsed };
    private readonly List<Attachment> _attachments = [];
    private const int MaxAttachments = 8;
    // Windows' own icon font: the same pen and paperclip the rest of the system uses.
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string PenGlyph = "\uE70F", ClipGlyph = "\uE723";
    private PxRect _monitor;
    private nint _subject; // the application that was in front when the bar was called up
    private bool _closing, _answerIsMine;

    private static TutorSession Session => AppServices.Session;

    public AskBar()
    {
        Title = "Vernier ask bar";
        Width = 580;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "Bg");
        AppServices.TrackWindow(this, "Ask bar");

        Style Res(string key) => (Style)FindResource(key);

        // --- input row ---
        _pen.Content = Glyph(PenGlyph);
        _pen.Style = Res("Segment");
        _pen.ToolTip = $"Draw on the screen to mark what you mean ({MainWindow.PenHotkey})";
        _pen.Click += (_, _) => { AppServices.Overlays.ArmPen(_pen.IsChecked == true); _input.Focus(); };
        AutomationProperties.SetName(_pen, "Pen");

        _attach.Content = Glyph(ClipGlyph);
        _attach.Style = Res("Quiet");
        _attach.Padding = new Thickness(8, 6, 8, 6);
        _attach.ToolTip = "Attach a picture, slides (.pptx), a Word document or a text file. You can also paste a picture or drop files here.";
        _attach.Click += (_, _) => PickFiles();
        AutomationProperties.SetName(_attach, "Attach a file");

        _input.BorderThickness = new Thickness(0);
        _input.Background = Brushes.Transparent;
        _input.FontSize = 15;
        _input.Padding = new Thickness(4, 8, 4, 8);
        _input.VerticalContentAlignment = VerticalAlignment.Center;
        _input.PreviewKeyDown += OnKey;
        _input.TextChanged += (_, _) => _hint.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _input.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ControlTemplate TargetType='TextBox' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/></ControlTemplate>");
        AutomationProperties.SetName(_input, "Question about your screen");

        _hint.Style = Res("Soft");
        _hint.FontSize = 15;
        _hint.IsHitTestVisible = false;
        _hint.Margin = new Thickness(10, 0, 0, 0);
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.TextWrapping = TextWrapping.NoWrap;
        _hint.TextTrimming = TextTrimming.CharacterEllipsis;
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "InkFaint");

        _send.Style = Res("Primary");
        _send.MinWidth = 76;
        _send.Margin = new Thickness(8, 8, 10, 8);
        _send.Click += (_, _) => { if (Session.IsBusy) Session.Cancel(); else _ = SendAsync(); };
        AutomationProperties.SetName(_send, "Ask about the screen");

        var inputGrid = new Grid();
        inputGrid.Children.Add(_input);
        inputGrid.Children.Add(_hint);
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_pen, Dock.Left);
        DockPanel.SetDock(_attach, Dock.Left);
        DockPanel.SetDock(_send, Dock.Right);
        _pen.Margin = new Thickness(6, 0, 0, 0);
        _attach.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(_pen);
        row.Children.Add(_attach);
        row.Children.Add(_send);
        row.Children.Add(inputGrid);

        // --- answer ---
        _meta.Style = Res("Readout");
        _meta.Margin = new Thickness(0, 0, 0, 5);

        var answerStack = new StackPanel { Margin = new Thickness(16, 12, 14, 12) };
        answerStack.Children.Add(_meta);
        answerStack.Children.Add(_answer);
        answerStack.Children.Add(_notes);
        _answerHost.Child = new ScrollViewer { Content = answerStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 420, Focusable = false };
        _answerHost.BorderThickness = new Thickness(0, 1, 0, 0);
        _answerHost.SetResourceReference(Border.BorderBrushProperty, "Rule");
        _answerHost.Visibility = Visibility.Collapsed;

        // --- footer ---
        _status.Style = Res("Readout");
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.TextWrapping = TextWrapping.NoWrap;
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        _status.Margin = new Thickness(16, 0, 8, 0);
        var footerButtons = new StackPanel { Orientation = Orientation.Horizontal };
        void Quiet(Button b, string text, string tip, Action act)
        {
            b.Content = text;
            b.ToolTip = tip;
            b.Style = Res("Quiet");
            b.FontSize = 11.5;
            b.Click += (_, _) => act();
            footerButtons.Children.Add(b);
        }
        Quiet(_steps, "Review steps…", "See the steps the tutor proposes to do in your application, and approve or refuse them", () => _ = AssistedInput.ReviewAndRunAsync(this));
        _steps.SetResourceReference(ForegroundProperty, "Accent");
        Quiet(_undo, "Undo mark", "Remove the last mark", () => AppServices.Annotations.Undo());
        Quiet(_clear, "Clear marks", "Remove every mark from the screen", () => AppServices.Annotations.Clear());
        Quiet(new Button(), "Workspace", "Open the full conversation, review and settings", () => { (Application.Current.MainWindow as MainWindow)?.ShowWorkspace(); });
        Quiet(new Button(), "Close", "Hide the bar (Esc). Vernier stays in the notification area.", HideBar);
        // Which model answers, and a way to change it for the questions that follow.
        _model.Style = Res("Quiet");
        _model.FontSize = 11.5;
        _model.Margin = new Thickness(8, 0, 0, 0);
        _model.Click += (_, _) => ShowModelMenu();
        AutomationProperties.SetName(_model, "Choose the model");
        var footer = new DockPanel { Height = 30 };
        DockPanel.SetDock(footerButtons, Dock.Right);
        DockPanel.SetDock(_model, Dock.Left);
        footer.Children.Add(footerButtons);
        footer.Children.Add(_model);
        footer.Children.Add(_status);
        var footerHost = new Border { Child = footer, BorderThickness = new Thickness(0, 1, 0, 0) };
        footerHost.SetResourceReference(Border.BorderBrushProperty, "Rule");
        footerHost.SetResourceReference(Border.BackgroundProperty, "Surface");

        _context.Style = Res("Readout");
        _contextToggle.Style = Res("Quiet");
        _contextToggle.FontSize = 11;
        _contextToggle.Margin = new Thickness(0, 0, 8, 4);
        _contextToggle.Click += (_, _) => { Session.IncludeEarlierContext = !Session.IncludeEarlierContext; Sync(); };
        AutomationProperties.SetName(_contextToggle, "Include or leave out earlier work");
        DockPanel.SetDock(_contextToggle, Dock.Right);
        _contextRow.Children.Add(_contextToggle);
        _contextRow.Children.Add(_context);
        _input.TextChanged += (_, _) => SyncContext();

        var column = new StackPanel();
        column.Children.Add(_chips);
        column.Children.Add(row);
        column.Children.Add(_contextRow);
        column.Children.Add(_answerHost);
        column.Children.Add(footerHost);
        var layout = new DockPanel();
        DockPanel.SetDock(_stripe, Dock.Left);
        layout.Children.Add(_stripe);
        layout.Children.Add(column);
        _frame.Child = layout;
        _frame.BorderThickness = new Thickness(1);
        _frame.SetResourceReference(Border.BorderBrushProperty, "RuleStrong");
        Content = _frame;
        // Depth from the compositor: acrylic behind a theme tint, rounded corners, the system shadow. Where Windows
        // draws the rounded outline, WPF keeps only a lit top edge; otherwise the square hairline frame stays.
        Depth.Attach(this, acrylic: true);
        SourceInitialized += (_, _) =>
        {
            if (!Depth.Of(this).Rounded) return;
            _frame.BorderThickness = new Thickness(0, 1, 0, 0);
            _frame.SetResourceReference(Border.BorderBrushProperty, "Sheen");
        };

        // Drag by any empty part of the bar.
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is not TextBox && e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        SizeChanged += (_, _) => KeepOnScreen();
        Session.Changed += Sync;
        Session.AnswerUpdated += OnAnswer;
        AppServices.Overlays.PenChanged += Sync;
        AppServices.Annotations.Changed += Sync;
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach (var f in files) AddFile(f); };
        Closing += (_, e) => { if (!_closing) { e.Cancel = true; HideBar(); } };
        Closed += (_, _) =>
        {
            Session.Changed -= Sync;
            Session.AnswerUpdated -= OnAnswer;
            AppServices.Overlays.PenChanged -= Sync;
            AppServices.Annotations.Changed -= Sync;
        };
        Sync();
    }

    public void ForceClose()
    {
        _closing = true;
        Close();
    }

    /// <summary>Opens the bar on the monitor the pointer is on. With <paramref name="pen"/> the pen is armed straight away.</summary>
    public void Open(bool pen)
    {
        var front = Win32.GetAncestor(Win32.GetForegroundWindow(), Win32.GA_ROOT);
        Win32.GetWindowThreadProcessId(front, out var pid);
        if (pid != Environment.ProcessId) _subject = front;
        Win32.GetCursorPos(out var cursor);
        var monitors = Win32.Monitors();
        _monitor = (DpiMath.MonitorFor(new PxPoint(cursor.X, cursor.Y), monitors) ?? monitors[0]).BoundsPhysical;
        if (!_answerIsMine || !IsVisible) { _answerHost.Visibility = Visibility.Collapsed; _answerIsMine = false; }
        if (!IsVisible) Enter();
        Show();
        Place(null);
        AppServices.Overlays.ArmPen(pen);
        Activate();
        _input.Focus();
        Sync();
        Session.Prewarm(); // the tutor starts up while the learner types
    }

    // The bar rises 8 px into place as it fades in; with Windows animations off it only fades.
    private void Enter()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _frame.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        if (!SystemParameters.ClientAreaAnimation) return;
        var rise = new TranslateTransform(0, 8);
        _frame.RenderTransform = rise;
        rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
    }

    /// <summary>Puts a question in the box for the learner to edit or send; it is not sent.</summary>
    public void Prefill(string question)
    {
        _input.Text = question;
        _input.CaretIndex = question.Length;
        _input.Focus();
    }

    public void HideBar()
    {
        AppServices.Overlays.ArmPen(false);
        if (!Session.IsBusy) Session.DiscardPrewarm();
        Hide();
    }

    private void Place(PxRect? beside)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0 || Win32.WindowBounds(hwnd) is not { } self) return;
        // Beside what was marked when there is ink; otherwise top centre, where the answer can grow downwards.
        var at = beside is { } target
            ? OverlayPlacement.Beside(target, self.Width, Math.Max(self.Height, 260), _monitor, gap: 24)
            : new PxPoint(_monitor.X + (_monitor.Width - self.Width) / 2, _monitor.Y + _monitor.Height * 0.08);
        Win32.SetWindowPos(hwnd, 0, (int)at.X, (int)at.Y, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    private void KeepOnScreen()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0 || !IsVisible || _monitor.IsEmpty || Win32.WindowBounds(hwnd) is not { } self) return;
        var at = OverlayPlacement.Clamp(self, _monitor);
        if (at.X != self.X || at.Y != self.Y)
            Win32.SetWindowPos(hwnd, 0, (int)at.X, (int)at.Y, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    private void Sync()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(Sync)); return; }
        var overlays = AppServices.Overlays;
        var hasInk = overlays.Ink.Count > 0;
        _pen.IsChecked = overlays.PenArmed;
        _stripe.SetResourceReference(Shape.FillProperty, Session.IsBusy ? "Accent" : "You");
        _send.Content = Session.IsBusy ? "Stop" : "Ask";
        _hint.Text = Session.IsBusy ? "Working…"
            : hasInk ? "Type your question about what you marked, then Enter"
            : overlays.PenArmed ? "Circle what you mean, then ask here"
            : _attachments.Count > 0 ? "Ask about what you attached"
            : "Ask about your screen";
        _attach.IsEnabled = !Session.IsBusy;
        SyncContext();
        _model.IsEnabled = !Session.IsBusy;
        _model.Content = ModelLabel();
        _model.ToolTip = "Model for your questions: " + (Session.ModelOverride is { } o ? TutorSession.Describe(o) : AppServices.Settings.AutoRoute ? "chosen by task (Settings › Models)" : TutorSession.Describe(AppServices.Settings.ActiveProvider!)) + ". Click to change.";
        _steps.Visibility = Session.PendingActions.Count > 0 && !Session.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        _input.IsEnabled = !Session.IsBusy;
        _undo.IsEnabled = AppServices.Annotations.CanUndo;
        _clear.IsEnabled = AppServices.Annotations.Items.Count > 0;
        _status.Text = Session.IsBusy ? StageText(Session.Stage)
            : Session.CaptureBlocked && AppServices.Settings.AllowScreenCapture ? Session.CaptureBlockedReason
            : _notice ?? (_answerIsMine ? Session.Status
            : $"{MainWindow.AskHotkey} ask · {MainWindow.PenHotkey} pen · esc close");
    }

    private static string StageText(PipelineStage? stage) => stage switch
    {
        PipelineStage.Observe => "looking at the screen",
        PipelineStage.Interpret => "reading what is there",
        PipelineStage.Teach => "answering",
        PipelineStage.Verify or PipelineStage.Confirm => "checking the answer",
        PipelineStage.Correct => "fixing a mistake the check found",
        _ => "finishing",
    };

    private void SyncContext()
    {
        if (!AppServices.Settings.AllowActivityContext || AppServices.Context is not { } hub) { _contextRow.Visibility = Visibility.Collapsed; return; }
        var selection = hub.Engine.Select(_input.Text, DateTimeOffset.UtcNow);
        if (!Session.IncludeEarlierContext) selection = selection.WithoutEarlier();
        var chip = selection.Chip();
        _contextRow.Visibility = chip.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _context.Text = "context · " + chip + (Session.IncludeEarlierContext ? "" : " · earlier work left out");
        _context.ToolTip = "Goes with your question so the tutor knows what you mean. Kept on this computer; Settings › Privacy to forget it.";
        _contextToggle.Content = Session.IncludeEarlierContext ? "Leave out earlier" : "Include earlier";
    }

    private static string ModelLabel()
    {
        var s = AppServices.Settings;
        var p = Session.ModelOverride;
        if (p is null && s.AutoRoute) return "by task ▾";
        p ??= s.ActiveProvider;
        if (p is null) return "no model ▾";
        var name = string.IsNullOrWhiteSpace(p.Model) ? p.Kind == Vernier.Core.Settings.ProviderKinds.ClaudeCode ? "Claude" : p.DisplayName.Split(' ')[0] : p.Model;
        return name + (string.IsNullOrWhiteSpace(p.Effort) ? "" : " · " + p.Effort) + " ▾";
    }

    private void ShowModelMenu()
    {
        var menu = new ContextMenu { PlacementTarget = _model, Placement = PlacementMode.Top };
        MenuItem Item(string header, bool chosen, Action pick)
        {
            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = chosen };
            item.Click += (_, _) => { pick(); Session.DiscardPrewarm(); Session.Prewarm(); Sync(); };
            AutomationProperties.SetName(item, header);
            return item;
        }
        var settings = AppServices.Settings;
        var current = Session.ModelOverride;
        menu.Items.Add(Item(settings.AutoRoute ? "Chosen by task (Settings › Models)" : $"Default: {TutorSession.Describe(settings.ActiveProvider!)}", current is null, () => Session.ModelOverride = null));
        menu.Items.Add(new Separator());
        foreach (var (label, provider) in Vernier.Core.Providers.ModelRouter.Options(settings))
            menu.Items.Add(Item(label, current is not null && current.Id == provider.Id && current.Model == provider.Model,
                () => Session.ModelOverride = provider.With(null, current?.Id == provider.Id ? current.Effort : null)));
        var basis = current ?? settings.ActiveProvider;
        if (basis is not null && Vernier.Core.Providers.ModelRouter.SupportsEffort(basis))
        {
            var effort = new MenuItem { Header = "Effort" };
            AutomationProperties.SetName(effort, "Effort");
            foreach (var level in Vernier.Core.Providers.ModelRouter.Efforts)
                effort.Items.Add(Item(level.Length == 0 ? "model default" : level, (basis.Effort ?? "") == level, () =>
                {
                    var copy = basis.With(null, null);
                    copy.Effort = level.Length == 0 ? null : level;
                    Session.ModelOverride = copy;
                }));
            menu.Items.Add(new Separator());
            menu.Items.Add(effort);
        }
        menu.IsOpen = true;
    }

    private static TextBlock Glyph(string glyph) => new() { Text = glyph, FontFamily = IconFont, FontSize = 17 };

    // ---------- attachments ----------

    private string? _notice; // one line about the last attach attempt, shown in the footer until the next action

    private void PickFiles()
    {
        AppServices.Overlays.ArmPen(false);
        var all = string.Join(";", AttachmentReader.ImageExtensions.Concat(AttachmentReader.DocumentExtensions).Select(e => "*" + e));
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Attach to your question", Multiselect = true,
            Filter = $"Pictures, slides, documents|{all}|Pictures|{string.Join(";", AttachmentReader.ImageExtensions.Select(e => "*" + e))}|All files|*.*",
        };
        if (dialog.ShowDialog(this) == true)
            foreach (var file in dialog.FileNames) AddFile(file);
        _input.Focus();
    }

    public void AddFile(string path)
    {
        try
        {
            if (AttachmentReader.IsImage(path))
            {
                var info = new System.IO.FileInfo(path);
                if (info.Length > AttachmentReader.MaxFileBytes) throw new AttachmentException($"'{info.Name}' is too large to attach.");
                AddPicture(info.Name, System.IO.File.ReadAllBytes(path));
            }
            else
            {
                var doc = AttachmentReader.Read(path);
                if (doc.Text.Length == 0 && doc.Pictures.Count == 0) throw new AttachmentException($"No text or pictures were found in '{doc.Name}'.");
                if (doc.Text.Length > 0) Add(new Attachment(doc.Name, doc.Text));
                foreach (var (name, data) in doc.Pictures) AddPicture($"{doc.Name} › {name}", data);
            }
        }
        catch (Exception e) when (e is AttachmentException or System.IO.IOException or UnauthorizedAccessException or NotSupportedException or System.IO.FileFormatException or ArgumentException)
        {
            // NotSupportedException / FileFormatException: Windows has no decoder for that picture.
            _notice = e is AttachmentException ? e.Message : $"'{System.IO.Path.GetFileName(path)}' could not be read: {e.Message}";
        }
        Sync();
    }

    private void AddPicture(string name, byte[] data) =>
        Add(new Attachment(name, null, ScreenCaptureService.NormalizePicture(data, AppServices.Settings.MaxImageEdge)));

    private void Add(Attachment attachment)
    {
        if (_attachments.Count >= MaxAttachments) { _notice = $"Up to {MaxAttachments} attachments per question."; return; }
        _notice = null;
        _attachments.Add(attachment);
        RenderChips();
    }

    // Attachments stay pinned to the bar, and go with every question, until removed.
    private void RenderChips()
    {
        _chips.Children.Clear();
        foreach (var a in _attachments)
        {
            var name = new TextBlock { Text = a.Name, Style = (Style)FindResource("Readout"), MaxWidth = 190, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var remove = new Button { Content = "×", Style = (Style)FindResource("Quiet"), Padding = new Thickness(6, 0, 4, 1), ToolTip = "Remove " + a.Name };
            AutomationProperties.SetName(remove, "Remove attachment " + a.Name);
            remove.Click += (_, _) => { _attachments.Remove(a); _notice = null; RenderChips(); Sync(); };
            var glyph = Glyph(a.Image is null ? "\uE8A5" : "\uEB9F"); // document / picture
            glyph.FontSize = 12;
            glyph.Margin = new Thickness(0, 0, 6, 0);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "You");
            var chip = new Border
            {
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { glyph, name, remove } },
                BorderThickness = new Thickness(1), Padding = new Thickness(8, 2, 0, 2), Margin = new Thickness(0, 0, 6, 4),
                ToolTip = a.Text is { } t ? $"{a.Name}: {t.Length:N0} characters of text" : a.Name,
            };
            chip.SetResourceReference(Border.BorderBrushProperty, "Rule");
            chip.SetResourceReference(Border.BackgroundProperty, "Surface");
            _chips.Children.Add(chip);
        }
        _chips.Visibility = _attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool PasteAttachment()
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                foreach (var f in Clipboard.GetFileDropList()) if (f is not null) AddFile(f);
                return true;
            }
            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
            {
                AddPicture($"Pasted picture {DateTime.Now:HH.mm.ss}", ScreenCaptureService.Png(image));
                Sync();
                return true;
            }
        }
        catch (System.Runtime.InteropServices.ExternalException) { _notice = "The clipboard is in use by another application. Try pasting again."; Sync(); }
        return false;
    }

    /// <summary>Design review only: shows an answer as if the tutor had just given it.</summary>
    internal void ShowForReview(TranscriptItem item)
    {
        _answerIsMine = true;
        OnAnswer(item);
    }

    private void OnAnswer(TranscriptItem item)
    {
        if (!_answerIsMine || item.IsUser) return;
        _answerHost.Visibility = Visibility.Visible;
        _meta.Text = item.Meta;
        _answer.Source = item.Text;
        _answer.Visibility = item.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _notes.Children.Clear();
        foreach (var note in item.Notices) AddNote(note, "Warn");
        foreach (var check in item.Checks.Where(c => c.Status == CheckStatus.Failed)) AddNote("Check failed: " + check.Detail, "Err");
        var passed = item.Checks.Count(c => c.Status == CheckStatus.Passed);
        if (passed > 0) AddNote($"{passed} check(s) passed: numbers and marks were re-checked by code. Details in the workspace.", "Ok");
    }

    private void AddNote(string text, string brush)
    {
        var block = new TextBlock { Text = text, Style = (Style)FindResource("Body"), FontSize = 12, Margin = new Thickness(0, 6, 0, 0) };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        _notes.Children.Add(block);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            _ = SendAsync();
        }
        else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && PasteAttachment())
        {
            e.Handled = true; // a picture or files went to the attachments; text still pastes into the box
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (Session.IsBusy) Session.Cancel();
            else if (AppServices.Overlays.PenArmed) AppServices.Overlays.ArmPen(false);
            else HideBar();
        }
    }

    private async Task SendAsync()
    {
        if (Session.IsBusy) return;
        var settings = AppServices.Settings;
        if (!settings.AllowScreenCapture)
        {
            AppServices.Overlays.ArmPen(false);
            if (!ConfirmDialog.Ask(this, "Let Vernier look at your screen?",
                    "When you press Ask or finish a pen mark, Vernier captures the monitor you are on, once, to answer that question. " +
                    "A light-blue wave runs round the screen edge for as long as it is looking.\n\n" +
                    "With a remote provider, the capture and the text read from it are sent to that provider. Captures are kept in memory for the answer and are not saved to disk by Vernier.\n\n" +
                    "You can turn this off again in Settings › Privacy.", "Allow")) return;
            settings.AllowScreenCapture = true;
            AppServices.SaveSettings();
        }

        if (_attachments.Count > 0 && settings.ActiveProvider is { } provider && !settings.AllowSendNotesToRemote && !Session.AttachmentsToRemoteAllowed
            && !AppServices.Providers.Create(provider).Capabilities.IsLocal)
        {
            if (!ConfirmDialog.Ask(this, $"Send your attachments to {provider.DisplayName}?",
                    "The files pinned to the ask bar (" + string.Join(", ", _attachments.Select(a => a.Name)) + $") will be sent to {provider.DisplayName} with this question, and with later questions while they stay pinned.\n\n" +
                    "Vernier asks once each time it is started.", "Send")) return;
            Session.AttachmentsToRemoteAllowed = true;
        }

        _notice = null;
        var overlays = AppServices.Overlays;
        var ink = overlays.Ink;
        var inkBounds = overlays.InkBounds;
        overlays.ConsumeInk();
        overlays.ArmPen(false);
        if (inkBounds is { } b) Place(b);
        var question = _input.Text;
        _input.Clear();
        _answerIsMine = true;
        _answer.Source = "";
        _notes.Children.Clear();
        AppServices.SpeechOut?.Stop();
        await Session.AskAsync(question, null, new ScreenAsk(_monitor, ink, _subject), [.. _attachments]);
        if (IsVisible) _input.Focus();
    }
}
