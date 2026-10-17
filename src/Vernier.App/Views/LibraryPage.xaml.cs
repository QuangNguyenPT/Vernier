using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vernier.Core.Knowledge;
using Vernier.Core.Learning;
using Vernier.Core.Security;

namespace Vernier.App.Views;

public partial class LibraryPage : UserControl, IRefreshable
{
    private sealed record SessionRow(long Id, string Title, string Detail);

    public LibraryPage() => InitializeComponent();

    public void Refresh()
    {
        var store = AppServices.Store;
        var concepts = store.ConceptStates();
        Concepts.ItemsSource = concepts;
        ConceptsEmpty.Visibility = concepts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var formulas = store.Formulas();
        Formulas.ItemsSource = formulas;
        Examples.ItemsSource = store.Examples();
        FormulasEmpty.Visibility = formulas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var mistakes = store.Misconceptions();
        Mistakes.ItemsSource = mistakes;
        MistakesEmpty.Visibility = mistakes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        LoadSessions();
    }

    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || ConceptsView is null) return;
        ConceptsView.Visibility = Vis(TabConcepts.IsChecked);
        FormulasView.Visibility = Vis(TabFormulas.IsChecked);
        MistakesView.Visibility = Vis(TabMistakes.IsChecked);
        SessionsView.Visibility = Vis(TabSessions.IsChecked);

        static Visibility Vis(bool? on) => on == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPractise(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ConceptState c) return;
        (Window.GetWindow(this) as MainWindow)?.AskTutor(
            $"Give me one practice problem on {c.Name} that is a step harder than a textbook example and shaped differently from ones I have seen. Wait for my answer before explaining.");
    }

    private void OnToggleMistake(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not Misconception m) return;
        AppServices.Store.ResolveMisconception(m.Id, !m.Resolved);
        Refresh();
    }

    // ---------- sessions ----------

    private void OnSearch(object sender, TextChangedEventArgs e) => LoadSessions();

    private void LoadSessions()
    {
        var query = Search.Text.Trim();
        List<SessionRow> rows = query.Length == 0
            ? [.. AppServices.Store.Sessions().Select(s => new SessionRow(s.Id, s.Title, $"{s.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · {s.Mode} · {s.MessageCount} message(s)"))]
            : [.. AppServices.Store.Search(query).Select(h => new SessionRow(h.SessionId, h.Snippet, $"{h.At.ToLocalTime():yyyy-MM-dd} · {h.SessionTitle}"))];
        Sessions.ItemsSource = rows;
        SessionsEmpty.Text = query.Length == 0 ? "No saved sessions yet." : $"Nothing found for \"{query}\".";
        SessionsEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowSession(null);
    }

    private void OnSessionSelected(object sender, SelectionChangedEventArgs e) => ShowSession(Sessions.SelectedItem as SessionRow);

    private void ShowSession(SessionRow? row)
    {
        ToVault.IsEnabled = DeleteSession.IsEnabled = row is not null;
        if (row is null) { SessionText.Text = "Select a session to read it."; return; }
        var sb = new StringBuilder();
        foreach (var m in AppServices.Store.Messages(row.Id))
            sb.Append(m.Role == "user" ? "YOU\n" : "TUTOR\n").Append(m.Text).Append("\n\n");
        SessionText.Text = sb.ToString().TrimEnd();
    }

    private void OnDeleteSession(object sender, RoutedEventArgs e)
    {
        if (Sessions.SelectedItem is not SessionRow row) return;
        if (!ConfirmDialog.Ask(Window.GetWindow(this), "Delete this session?", "The conversation and its search entries are removed from this computer. Concepts, cards and mistakes learned from it are kept.", "Delete", danger: true)) return;
        AppServices.Store.DeleteSession(row.Id);
        LoadSessions();
    }

    private void OnSessionToVault(object sender, RoutedEventArgs e)
    {
        if (Sessions.SelectedItem is not SessionRow row) return;
        var owner = Window.GetWindow(this);
        if (string.IsNullOrWhiteSpace(AppServices.Settings.VaultPath))
        {
            MessageBox.Show(owner!, "Choose an Obsidian vault folder first, under Notes & files.", "Vernier", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var vault = new ObsidianVault(AppServices.Settings.VaultPath);
            var session = AppServices.Store.Sessions().First(s => s.Id == row.Id);
            var change = vault.ProposeSessionNote(session.Title, session.StartedAt, AppServices.Store.Messages(row.Id).Select(m => (m.Role, m.Text)), []);
            if (!ConfirmDialog.Ask(owner, change.Summary, change.Diff, "Write note", mono: true)) return;
            vault.Apply(change);
            AppServices.Log.Info("Wrote session note " + change.RelativePath);
        }
        catch (Exception ex) when (ex is IOException or PathGuardException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner!, ex.Message, "Vernier", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

/// <summary>Mastery as five graduations: filled up to the level reached. A scale, not a percentage bar, because the levels are discrete claims.</summary>
public sealed class LevelScale : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(MasteryLevel), typeof(LevelScale), new FrameworkPropertyMetadata(MasteryLevel.Unseen, FrameworkPropertyMetadataOptions.AffectsRender));

    public MasteryLevel Level
    {
        get => (MasteryLevel)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var rule = (Brush)FindResource("RuleStrong");
        var ink = (Brush)FindResource("Ink");
        var accent = (Brush)FindResource("Accent");
        const int steps = 4;
        var step = (ActualWidth - 2) / steps;
        dc.DrawLine(new Pen(rule, 1), new Point(0, ActualHeight - 0.5), new Point(ActualWidth, ActualHeight - 0.5));
        var reached = (int)Level;
        if (reached > 0) dc.DrawRectangle(reached == steps ? accent : ink, null, new Rect(0, ActualHeight - 4, reached * step, 4));
        for (var i = 0; i <= steps; i++)
        {
            var x = Math.Round(i * step) + 0.5;
            dc.DrawLine(new Pen(i <= reached ? ink : rule, 1), new Point(x, 0), new Point(x, ActualHeight));
        }
    }
}
