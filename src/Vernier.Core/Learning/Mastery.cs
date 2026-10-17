namespace Vernier.Core.Learning;

public enum MasteryLevel { Unseen, Introduced, Practicing, Demonstrated, Mastered }

public static class EvidenceKinds
{
    public const string Diagnostic = "diagnostic";   // answered a small targeted question
    public const string Explain = "explain";         // explained it in their own words
    public const string Transfer = "transfer";       // solved a new, differently-shaped problem
    public const string Recall = "recall";           // retrieved it later from memory (card review)
    public const string SelfReport = "self_report";  // "I get it" — recorded, never counted

    public static readonly string[] Counted = [Diagnostic, Explain, Transfer, Recall];
    public static bool IsKnown(string? kind) => kind == SelfReport || Counted.Contains(kind);
}

public sealed record Evidence(string Kind, bool Correct, DateTimeOffset At);

public sealed record MasteryAssessment(MasteryLevel Level, double Score, string Reason);

/// <summary>
/// Mastery is inferred from demonstrated performance only. One right answer, or the learner
/// saying they understand, never moves a concept past Practicing.
/// </summary>
public static class MasteryModel
{
    public static MasteryAssessment Assess(IReadOnlyList<Evidence> evidence, int openMisconceptions = 0)
    {
        if (evidence.Count == 0) return new(MasteryLevel.Unseen, 0, "No activity yet.");

        var counted = evidence.Where(e => EvidenceKinds.Counted.Contains(e.Kind)).OrderBy(e => e.At).ToList();
        if (counted.Count == 0)
            return new(MasteryLevel.Introduced, 0.1, "Seen, but nothing demonstrated yet (self-reports are not counted).");

        var recent = counted.TakeLast(6).ToList();
        var accuracy = recent.Count(e => e.Correct) / (double)recent.Count;
        var correct = counted.Where(e => e.Correct).ToList();
        var kinds = correct.Select(e => e.Kind).Distinct().Count();
        var hasTransfer = correct.Any(e => e.Kind == EvidenceKinds.Transfer);
        // Retention: a correct answer at least a day after the first correct one.
        var retained = correct.Count >= 2 && (correct[^1].At - correct[0].At) >= TimeSpan.FromDays(1);
        var lastWrong = !counted[^1].Correct;

        var score = Math.Round(Math.Min(1.0,
            0.45 * accuracy + 0.15 * Math.Min(kinds, 3) / 3.0 + (hasTransfer ? 0.2 : 0) + (retained ? 0.2 : 0)), 2);

        if (correct.Count == 0)
            return new(MasteryLevel.Introduced, score, "Attempted, not yet answered correctly.");
        if (openMisconceptions > 0)
            return new(MasteryLevel.Practicing, Math.Min(score, 0.5), $"{openMisconceptions} unresolved misconception(s).");
        if (correct.Count < 3 || kinds < 2 || accuracy < 0.7 || lastWrong)
            return new(MasteryLevel.Practicing, score,
                correct.Count == 1 ? "One correct answer is not yet evidence of mastery."
                : kinds < 2 ? "Correct so far, but only in one kind of task."
                : lastWrong ? "Most recent attempt was incorrect."
                : "Still building consistency.");
        if (hasTransfer && retained && accuracy >= 0.8)
            return new(MasteryLevel.Mastered, score, "Correct across task types, on a transfer problem, and retained over time.");
        return new(MasteryLevel.Demonstrated, score,
            !hasTransfer ? "Consistent; not yet shown on a transfer problem." : "Consistent; not yet shown to be retained after a day.");
    }
}

public sealed record CardSchedule(double Ease, int IntervalDays, int Repetitions, DateTimeOffset Due);

/// <summary>SM-2 spaced repetition with configurable first intervals. Grade: 0 (blackout) .. 5 (perfect).</summary>
public static class Sm2
{
    public const double MinEase = 1.3;

    public static CardSchedule Next(CardSchedule current, int grade, DateTimeOffset now, IReadOnlyList<int>? firstIntervalsDays = null)
    {
        grade = Math.Clamp(grade, 0, 5);
        firstIntervalsDays = firstIntervalsDays is { Count: > 0 } ? firstIntervalsDays : [1, 6];

        var ease = Math.Max(MinEase, current.Ease + (0.1 - (5 - grade) * (0.08 + (5 - grade) * 0.02)));
        if (grade < 3)
            // Lapse: start over tomorrow, keep the reduced ease.
            return new CardSchedule(ease, Math.Max(1, firstIntervalsDays[0]), 0, now.AddDays(Math.Max(1, firstIntervalsDays[0])));

        var reps = current.Repetitions + 1;
        var interval = reps <= firstIntervalsDays.Count
            ? Math.Max(1, firstIntervalsDays[reps - 1])
            : Math.Max(current.IntervalDays + 1, (int)Math.Round(current.IntervalDays * ease));
        return new CardSchedule(ease, interval, reps, now.AddDays(interval));
    }

    public static CardSchedule New(DateTimeOffset now) => new(2.5, 0, 0, now);
}
