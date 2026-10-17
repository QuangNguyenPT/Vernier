using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vernier.Core.Agents;
using Vernier.Core.Settings;

namespace Vernier.Core.Providers;

/// <summary>What a question needs from a model. Categories, not providers: the learner decides which model serves each.</summary>
public enum TaskKind { Quick, Tutor, Vision, Code }

/// <summary>The learner's choice for one kind of task. Blank model or effort means the provider's own.</summary>
public sealed class ModelRoute
{
    public TaskKind Task { get; set; }
    public string ProviderId { get; set; } = "";
    public string? Model { get; set; }
    public string? Effort { get; set; }
}

/// <param name="Why">Shown with the answer, so a different model is never used silently.</param>
public sealed record RouteDecision(ProviderSettings Provider, TaskKind Task, string Why);

/// <summary>
/// Picks the model for a question. With routing off, everything goes to the learner's default
/// model. A choice made in the ask bar always wins. With routing on, the route the learner set for
/// the task is used. Vernier never picks a provider the learner did not configure for that task.
/// </summary>
public static partial class ModelRouter
{
    [GeneratedRegex(@"```|\b(error|exception|stack trace|compile|bug|null reference|segfault|traceback|unit test|refactor)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CodeWords();

    public static TaskKind Classify(string question, bool hasInk, bool hasPictures, AppProfile? app, TutorMode mode)
    {
        if (hasInk || hasPictures) return TaskKind.Vision;
        if (app?.Category == "Programming" || CodeWords().IsMatch(question)) return TaskKind.Code;
        if (mode == TutorMode.Ask && question.Length <= 80) return TaskKind.Quick;
        return TaskKind.Tutor;
    }

    public static RouteDecision Choose(AppSettings settings, TaskKind task, ProviderSettings? chosenInBar)
    {
        if (chosenInBar is not null) return new(chosenInBar, task, "your choice in the ask bar");
        var fallback = settings.ActiveProvider ?? throw new ProviderException(ProviderErrorKind.NotConfigured, "No AI provider is selected. Choose one in Settings.");
        if (!settings.AutoRoute) return new(fallback, task, "your default model");
        var route = settings.Routes.FirstOrDefault(r => r.Task == task);
        if (route is null || settings.Providers.FirstOrDefault(p => p.Id == route.ProviderId) is not { } provider)
            return new(fallback, task, $"no route set for {Name(task)}; your default model");
        return new(provider.With(route.Model, route.Effort), task, $"routed as {Name(task)}");
    }

    public static string Name(TaskKind task) => task switch
    {
        TaskKind.Quick => "a quick question",
        TaskKind.Vision => "a question about something drawn or pictured",
        TaskKind.Code => "a programming question",
        _ => "tutoring",
    };

    /// <summary>Models a provider can be switched to from the ask bar. Only what the learner configured or the CLI documents.</summary>
    public static IReadOnlyList<(string Label, ProviderSettings Provider)> Options(AppSettings settings)
    {
        var options = new List<(string, ProviderSettings)>();
        foreach (var p in settings.Providers)
        {
            switch (p.Kind)
            {
                case ProviderKinds.ClaudeCode:
                    options.Add(($"{p.DisplayName} · default", p));
                    foreach (var alias in new[] { "opus", "sonnet", "haiku" }) options.Add(($"{p.DisplayName} · {alias}", p.With(alias, null)));
                    break;
                case ProviderKinds.CodexCli:
                    options.Add(($"{p.DisplayName} · default", p));
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(p.Model)) options.Add(($"{p.DisplayName} · {p.Model}", p));
                    break;
            }
        }
        return options;
    }

    public static readonly string[] Efforts = ["", "low", "medium", "high", "xhigh", "max"];
    public static bool SupportsEffort(ProviderSettings p) => p.Kind is ProviderKinds.ClaudeCode or ProviderKinds.Anthropic;
}

public sealed record RouteRecord(DateTimeOffset At, string Task, string Provider, string Model, string? Effort, string Why,
    double? FirstWordsSeconds, double TotalSeconds, long? InputTokens, long? OutputTokens, bool Ok);

/// <summary>
/// One line per answer: which model served which kind of task, how long it took and how many
/// tokens it used. Local only; it never contains the question or the answer.
/// </summary>
public sealed class RouteLog(string path)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private readonly object _gate = new();

    public void Add(RouteRecord record)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, JsonSerializer.Serialize(record, Json) + "\n");
        }
    }

    public IReadOnlyList<RouteRecord> Read(int last = 500)
    {
        lock (_gate)
        {
            if (!File.Exists(path)) return [];
            var records = new List<RouteRecord>();
            foreach (var line in File.ReadLines(path).TakeLast(last))
            {
                try { if (JsonSerializer.Deserialize<RouteRecord>(line, Json) is { } r) records.Add(r); }
                catch (JsonException) { } // a torn last line after a crash is skipped, not fatal
            }
            return records;
        }
    }

    /// <summary>Per model and task: answers, median time to first words, median total time, mean tokens.</summary>
    public string Summary()
    {
        var rows = Read().GroupBy(r => (r.Task, r.Provider, r.Model, r.Effort)).OrderBy(g => g.Key.Task).ToList();
        if (rows.Count == 0) return "No answers recorded yet.";
        static string Median(IEnumerable<double> xs)
        {
            var s = xs.OrderBy(x => x).ToList();
            return s.Count == 0 ? "—" : s[s.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }
        return string.Join("\n", rows.Select(g =>
            $"{g.Key.Task,-7} {g.Key.Provider} {g.Key.Model}{(string.IsNullOrEmpty(g.Key.Effort) ? "" : " · " + g.Key.Effort)}: {g.Count()} answer(s), " +
            $"first words {Median(g.Where(r => r.FirstWordsSeconds is not null).Select(r => r.FirstWordsSeconds!.Value))}, total {Median(g.Select(r => r.TotalSeconds))}, " +
            $"~{g.Average(r => (double)((r.InputTokens ?? 0) + (r.OutputTokens ?? 0))):0} tokens, {g.Count(r => !r.Ok)} failed"));
    }
}
