using Vernier.Core.Agents;
using Vernier.Core.Context;
using Vernier.Core.Settings;
using Xunit;

namespace Vernier.Tests;

public class SessionMemoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static TurnNote Note(int i, string? concept = null, string? mistake = null, string? open = null, int failed = 0) => new(
        T0.AddMinutes(i), "chrome — Lecture 4", $"question number {i} about the problem", concept ?? $"concept {i % 5}", $"Answer {i}: the first sentence.",
        1, failed, 0, mistake, open);

    [Fact]
    public void Gist_is_the_first_sentence_without_markup_or_the_vernier_block()
    {
        Assert.Equal("The kinetic energy is 9 J.", SessionMemory.Gist("**The kinetic energy is 9 J.** Then more text.\n```vernier\n{\"concept\":\"KE\"}\n```"));
        Assert.Equal("Use E_k = 1/2 m v^{2} here.", SessionMemory.Gist(@"Use \( E_k = 1/2 m v^{2} \) here. Next."));
        Assert.True(SessionMemory.Gist(new string('x', 400)).Length <= 150);
    }

    [Fact]
    public void The_recent_turns_are_not_repeated_and_nothing_is_rendered_until_there_are_older_ones()
    {
        var m = new SessionMemory();
        for (var i = 1; i <= MasteryTutor.HistoryTurns; i++) m.Add(Note(i));
        Assert.Equal("", m.Render(MasteryTutor.HistoryTurns)); // all of them are in the recent window
        m.Add(Note(9));
        var digest = m.Render(MasteryTutor.HistoryTurns);
        Assert.Contains("#1 ", digest);
        Assert.DoesNotContain("#2 ", digest);
        Assert.Contains("question number 1", digest);
    }

    [Fact]
    public void A_long_session_folds_its_oldest_turns_but_keeps_failures_misconceptions_and_open_questions()
    {
        var m = new SessionMemory();
        m.Add(Note(1, "torque", mistake: "treats torque as a force", open: "why r cross F?", failed: 1));
        for (var i = 2; i <= 120; i++) m.Add(Note(i));
        var digest = m.Render(MasteryTutor.HistoryTurns);
        Assert.True(digest.Length <= SessionMemory.Budget + 100, $"digest was {digest.Length} characters");
        Assert.Contains("folded", digest);
        Assert.Contains("torque", digest);
        Assert.Contains("treats torque as a force", digest);
        Assert.Contains("why r cross F?", digest);
        Assert.Contains("1 check(s) failed", digest);
        Assert.Contains("#112 ", digest); // the newest older turn is still a line of its own
        Assert.DoesNotContain("#3 ", digest);
    }

    [Fact]
    public void Clear_forgets_the_session()
    {
        var m = new SessionMemory();
        m.Add(Note(1));
        m.Clear();
        Assert.Equal(0, m.Count);
        Assert.Equal("", m.Render(0));
    }

    [Fact]
    public void The_request_carries_the_digest_fenced_so_screen_titles_in_it_stay_data()
    {
        var m = new SessionMemory();
        for (var i = 1; i <= 12; i++) m.Add(Note(i));
        var request = new MasteryTutor().BuildRequest(TutorMode.Ask, "and now?", null, [], new LearnerProfile(), LearningSummary.None, false, null, sessionDigest: m.Render(MasteryTutor.HistoryTurns));
        var user = request.Messages[^1].Text;
        Assert.Contains("Earlier in this same session", user);
        Assert.Contains("<untrusted", user);
        Assert.Contains("question number 1 ", user);
        Assert.DoesNotContain("question number 12", user); // the newest turns travel as messages, not in the digest
    }

    [Fact]
    public void The_engine_describes_the_whole_session_by_its_own_time_not_earlier_days()
    {
        var store = new MemoryStore();
        var before = new ContextEngine(new ContextOptions(), store, T0.AddMinutes(-41));
        Use(before, "chrome", "Lecture 4 - YouTube", T0.AddMinutes(-40), TimeSpan.FromMinutes(30)); // an earlier run, ending 10 minutes before this one
        before.Flush();

        var e = new ContextEngine(new ContextOptions(), store, T0);
        Use(e, "chrome", "Lecture 4 - YouTube", T0, TimeSpan.FromMinutes(10)); // continues the earlier run's episode (within the rejoin gap)
        Use(e, "code", "notes.md - Visual Studio Code", T0.AddMinutes(11), TimeSpan.FromMinutes(25));
        Use(e, "chrome", "Lecture 4 - YouTube", T0.AddMinutes(37), TimeSpan.FromMinutes(5));
        e.Asked("what is torque", T0.AddMinutes(40));

        var line = e.SessionLine(T0.AddMinutes(45))!;
        Assert.Contains("Visual Studio Code", line);
        Assert.Contains("25 min", line);                 // the longest first
        Assert.Contains("Lecture 4", line);
        Assert.Contains("Lecture 4 - YouTube\" 10 min", line); // this run's 10 minutes, not the earlier run's 30 it continued
        Assert.Contains("40 min of work", line);               // 10 + 25 + 5
        Assert.Contains("45 min ago", line);
        var selection = e.Select("what is torque", T0.AddMinutes(45));
        Assert.Contains("- Session: ", selection.Render());
        Assert.Contains("whole session", selection.Chip());
        Assert.DoesNotContain("Session", selection.WithoutEarlier().Render());
    }

    private static void Use(ContextEngine e, string app, string title, DateTimeOffset at, TimeSpan length)
    {
        for (var t = TimeSpan.Zero; t <= length; t += TimeSpan.FromSeconds(1))
        {
            e.Focus(app, title, null, at + t);
            e.Tick(at + t, TimeSpan.Zero, mediaPlaying: false);
        }
    }

    private sealed class MemoryStore : IEpisodeStore
    {
        private readonly List<Episode> _saved = [];
        public IReadOnlyList<Episode> LoadSince(DateTimeOffset since) => [.. _saved.Where(e => e.End >= since).Select(Copy)];
        public void Save(Episode e) { _saved.RemoveAll(x => x.Id == e.Id && e.Id != 0); if (e.Id == 0) e.Id = _saved.Count + 1; _saved.Add(Copy(e)); }
        public void DeleteBefore(DateTimeOffset before) => _saved.RemoveAll(e => e.End < before);
        public void DeleteSince(DateTimeOffset since) => _saved.RemoveAll(e => e.End >= since);
        private static Episode Copy(Episode e)
        {
            var c = new Episode { Id = e.Id, App = e.App, Key = e.Key, Title = e.Title, Url = e.Url, Start = e.Start, End = e.End, ActiveSeconds = e.ActiveSeconds, Questions = e.Questions, LastQuestion = e.LastQuestion, Dirty = false };
            return c;
        }
    }
}

public class SessionRecallTests
{
    private static TurnNote Note(int i, string question, string gist) => new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero).AddMinutes(i), "chrome — Lecture", question, null, gist, 0, 0, 0, null, null);

    [Fact]
    public void A_turn_folded_out_of_the_digest_can_still_be_recalled_in_full()
    {
        var m = new SessionMemory();
        m.Add(Note(1, "why does torque use a cross product?", "Because only the perpendicular part of the force turns the body."));
        for (var i = 2; i <= 150; i++) m.Add(Note(i, $"filler question {i} about heat", "Heat flows from hot to cold."));
        Assert.DoesNotContain("perpendicular", m.Render(8)); // folded: the answer is gone, only a short form of the question remains
        var recalled = m.Recall("cross product torque");
        Assert.Contains("only the perpendicular part of the force turns the body", recalled);
        Assert.Contains("No earlier turn matches", m.Recall("quantum chromodynamics"));
        Assert.Contains("No earlier turns", new SessionMemory().Recall("anything"));
    }
}
