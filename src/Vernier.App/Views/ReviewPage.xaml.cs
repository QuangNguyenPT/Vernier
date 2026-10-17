using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vernier.Core.Learning;

namespace Vernier.App.Views;

public partial class ReviewPage : UserControl, IRefreshable
{
    private StudyItem? _current;
    private Card? _card;

    public ReviewPage()
    {
        InitializeComponent();
        Focusable = true;
        PreviewKeyDown += OnKey;
    }

    public void Refresh()
    {
        var now = DateTimeOffset.UtcNow;
        var queue = AppServices.Store.StudyQueue(now);
        Queue.ItemsSource = queue;
        var cards = queue.Count(q => q.Kind == "card");
        var mistakes = queue.Count(q => q.Kind == "misconception");
        Summary.Text = queue.Count == 0
            ? "Nothing is due. Cards appear here when their interval elapses; mistakes and weak concepts appear as the tutor records them."
            : $"{cards} card(s) due, {mistakes} unresolved mistake(s), {queue.Count - cards - mistakes} concept(s) still being built."
              + (AppServices.Store.HasSampleData ? "  Items marked (Sample) are example data; remove them in Settings › Data." : "");
        NothingText.Text = queue.Count == 0 ? "You are up to date." : "Choose an item on the left, or press Enter to start at the top.";
        Show(null);
        if (queue.Count > 0) Queue.SelectedIndex = 0;
    }

    private void OnSelect(object sender, SelectionChangedEventArgs e) => Show(Queue.SelectedItem as StudyItem);

    private void Show(StudyItem? item)
    {
        _current = item;
        _card = item?.CardId is { } id ? AppServices.Store.Cards().FirstOrDefault(c => c.Id == id) : null;
        Nothing.Visibility = item is null ? Visibility.Visible : Visibility.Collapsed;
        CardView.Visibility = _card is not null ? Visibility.Visible : Visibility.Collapsed;
        TopicView.Visibility = item is not null && _card is null ? Visibility.Visible : Visibility.Collapsed;

        if (_card is not null)
        {
            Front.Text = _card.Front;
            Back.Text = _card.Back;
            CardMeta.Text = $"{(_card.Concept.Length > 0 ? _card.Concept + " · " : "")}interval {_card.Schedule.IntervalDays} d · ease {_card.Schedule.Ease:0.00} · {_card.Schedule.Repetitions} successful review(s)";
            BackHost.Visibility = Visibility.Collapsed;
            Grades.Visibility = Visibility.Collapsed;
            Reveal.Visibility = Visibility.Visible;
        }
        else if (item is not null)
        {
            TopicMeta.Text = item.Kind == "misconception" ? "unresolved mistake" : "concept still being built";
            TopicTitle.Text = item.Title;
            TopicReason.Text = item.Reason;
            ResolveButton.Visibility = item.Kind == "misconception" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnReveal(object sender, RoutedEventArgs e)
    {
        BackHost.Visibility = Visibility.Visible;
        Grades.Visibility = Visibility.Visible;
        Reveal.Visibility = Visibility.Collapsed;
    }

    private void OnGrade(object sender, RoutedEventArgs e) => Grade(int.Parse((string)((Button)sender).Tag));

    private void Grade(int grade)
    {
        if (_card is null || Grades.Visibility != Visibility.Visible) return;
        var next = AppServices.Store.ReviewCard(_card.Id, grade, DateTimeOffset.UtcNow, AppServices.Settings.ReviewIntervalsDays);
        AppServices.Log.Info($"Card reviewed, grade {grade}, next in {next.IntervalDays} day(s).");
        Refresh();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (_card is null) return;
        if (e.Key == Key.Space && Reveal.Visibility == Visibility.Visible) { OnReveal(sender, e); e.Handled = true; }
        else if (Grades.Visibility == Visibility.Visible && e.Key is >= Key.D1 and <= Key.D4)
        {
            Grade(new[] { 1, 3, 4, 5 }[e.Key - Key.D1]);
            e.Handled = true;
        }
    }

    private void OnAskTutor(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        var question = _current.Kind == "misconception"
            ? $"I keep making this mistake in {_current.Title}: \"{_current.Reason}\". Don't just tell me the rule: give me one short question that tests whether I still think that way."
            : $"Help me build my understanding of {_current.Title}. Start with one short diagnostic question to see what I already know.";
        (Window.GetWindow(this) as MainWindow)?.AskTutor(question);
    }

    private void OnResolve(object sender, RoutedEventArgs e)
    {
        if (_current is not { Kind: "misconception" } item) return;
        var match = AppServices.Store.Misconceptions(includeResolved: false).FirstOrDefault(m => m.ConceptId == item.ConceptId && m.Description == item.Reason);
        if (match is not null) AppServices.Store.ResolveMisconception(match.Id);
        Refresh();
    }
}
