using System.Text.Json;
using System.Text.Json.Serialization;
using Vernier.Core.Security;

namespace Vernier.Core.Settings;

public static class ProviderKinds
{
    public const string Anthropic = "anthropic";
    public const string OpenAiCompatible = "openai-compatible";
    public const string Gemini = "gemini";
    public const string ClaudeCode = "claude-code";
    public const string CodexCli = "codex-cli";
}

public sealed class ProviderSettings
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = ProviderKinds.OpenAiCompatible;
    public string DisplayName { get; set; } = "";
    public string? BaseUrl { get; set; }
    public string Model { get; set; } = "";
    /// <summary>Name of the secret (env var or credential-store entry). Null for keyless local servers.</summary>
    public string? ApiKeyName { get; set; }
    /// <summary>The user's statement that this model accepts images. Never assumed for local models.</summary>
    public bool SupportsVision { get; set; }
    /// <summary>
    /// A server on this computer. The flag holds only while the endpoint is a loopback address: a "local" preset whose
    /// endpoint was edited to point elsewhere is remote, so the remote-provider gates (notes, files, background calls) apply.
    /// </summary>
    public bool IsLocal
    {
        get => _isLocal && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var u) && u.IsLoopback;
        set => _isLocal = value;
    }
    private bool _isLocal;
    /// <summary>"max_completion_tokens" (OpenAI) or "max_tokens" (most compatible servers).</summary>
    public string MaxTokensField { get; set; } = "max_tokens";
    /// <summary>The model can call tools (function calling). On for hosted APIs; off for local servers unless the loaded model is known to handle it.</summary>
    public bool SupportsTools { get; set; } = true;
    /// <summary>Reasoning effort where the provider supports it (low, medium, high, xhigh, max). Empty: the provider default.</summary>
    public string? Effort { get; set; }

    /// <summary>A copy that uses another model or effort, for a route. Blank keeps this provider's own value.</summary>
    public ProviderSettings With(string? model, string? effort)
    {
        var copy = (ProviderSettings)MemberwiseClone();
        if (!string.IsNullOrWhiteSpace(model)) copy.Model = model;
        if (!string.IsNullOrWhiteSpace(effort)) copy.Effort = effort;
        return copy;
    }
}

public enum CapturePolicyMode { Standard, RequestExclusion, ShareSafe }

public sealed class LearnerProfile
{
    public string Level { get; set; } = "undergraduate";
    public string Goals { get; set; } = "";
    public string Pace { get; set; } = "steady";
    public string Assessment { get; set; } = "";
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public bool FirstRunComplete { get; set; }
    public string Theme { get; set; } = "dark";

    public List<ProviderSettings> Providers { get; set; } = DefaultProviders();
    public string ActiveProviderId { get; set; } = "claude-code";
    /// <summary>Optional second model for the Verifier. Null = deterministic checks only.</summary>
    public string? VerifierProviderId { get; set; }
    /// <summary>Use <see cref="Routes"/> to pick a model per kind of task. Off: every question goes to the default model.</summary>
    public bool AutoRoute { get; set; }
    public List<Providers.ModelRoute> Routes { get; set; } = [];

    // Every capability that observes, records, uploads or acts is off until the user turns it on.
    public bool AllowScreenCapture { get; set; }
    public bool AllowMicrophone { get; set; }
    public bool AllowSendNotesToRemote { get; set; }
    public bool AllowComputerControl { get; set; }
    /// <summary>
    /// Keep context of my work on this computer: which windows, pages and videos, for how long, what I rewound. On by
    /// default at the owner's direction (2026-10-16): context is the core of the tutor. Shown at first run and on upgrade.
    /// </summary>
    public bool AllowActivityContext { get; set; } = true;
    /// <summary>Also read the text of the window in front when it changes (local OCR, memory only). Needs screen capture allowed.</summary>
    public bool ReadScreenForContext { get; set; } = true;
    /// <summary>Offer help (a notification, at most twice an hour) when the learner seems stuck, e.g. rewinding the same part of a video. Worked out by code; nothing is sent.</summary>
    public bool ProactiveSuggestions { get; set; } = true;
    /// <summary>The learner has seen the context notice (first run, or after upgrading to a build with the context engine).</summary>
    public bool ContextNoticeShown { get; set; }
    public int ContextRetentionDays { get; set; } = 14;
    /// <summary>Apps never recorded, by process name (in addition to private windows and password managers).</summary>
    public List<string> ContextExcludedApps { get; set; } = [];
    /// <summary>Optional model that writes a one-line summary of an episode when it ends. Null: summaries are built by code only.</summary>
    public string? ContextModelProviderId { get; set; }
    public bool AcknowledgedObserveCost { get; set; }

    public CapturePolicyMode CapturePolicy { get; set; } = CapturePolicyMode.Standard;
    public int ObserveIntervalSeconds { get; set; } = 15;
    public int MaxImageEdge { get; set; } = 1568;
    public int ChangeThresholdBits { get; set; } = 6;

    public double SpeechRate { get; set; } = 1.0;
    public bool SpeakAnswers { get; set; }

    public string? VaultPath { get; set; }
    /// <summary>The learner's own Google OAuth client (Desktop app). Not a secret; its client secret lives in Credential Manager.</summary>
    public string? GoogleClientId { get; set; }
    public string? GoogleNotesDocId { get; set; }
    public string? WorkspacePath { get; set; }

    /// <summary>First intervals in days for a new card; SM-2 takes over afterwards.</summary>
    public int[] ReviewIntervalsDays { get; set; } = [1, 3];
    public int RetainSessionsDays { get; set; } = 0; // 0 = keep until deleted

    public LearnerProfile Learner { get; set; } = new();

    public ProviderSettings? ActiveProvider => Providers.FirstOrDefault(p => p.Id == ActiveProviderId);

    public static List<ProviderSettings> DefaultProviders() =>
    [
        new() { Id = "claude-code", Kind = ProviderKinds.ClaudeCode, DisplayName = "Claude Code (local CLI login)", Model = "", SupportsVision = true },
        new() { Id = "codex", Kind = ProviderKinds.CodexCli, DisplayName = "ChatGPT account (Codex CLI login)", Model = "", SupportsVision = true },
        new() { Id = "anthropic", Kind = ProviderKinds.Anthropic, DisplayName = "Anthropic Claude", Model = "claude-opus-5-5", ApiKeyName = "ANTHROPIC_API_KEY", SupportsVision = true },
        new() { Id = "openai", Kind = ProviderKinds.OpenAiCompatible, DisplayName = "OpenAI", BaseUrl = "https://api.openai.com/v1", Model = "", ApiKeyName = "OPENAI_API_KEY", SupportsVision = true, MaxTokensField = "max_completion_tokens" },
        new() { Id = "gemini", Kind = ProviderKinds.Gemini, DisplayName = "Google Gemini", BaseUrl = "https://generativelanguage.googleapis.com/v1beta", Model = "", ApiKeyName = "GEMINI_API_KEY", SupportsVision = true },
        new() { Id = "moonshot", Kind = ProviderKinds.OpenAiCompatible, DisplayName = "Moonshot AI / Kimi", BaseUrl = "https://api.moonshot.ai/v1", Model = "", ApiKeyName = "MOONSHOT_API_KEY", SupportsVision = false },
        // Free models (ids ending in ":free") need only a free OpenRouter key. Free endpoints may log prompts: see openrouter.ai/models.
        new() { Id = "openrouter", Kind = ProviderKinds.OpenAiCompatible, DisplayName = "OpenRouter (free models available)", BaseUrl = "https://openrouter.ai/api/v1", Model = "", ApiKeyName = "OPENROUTER_API_KEY", SupportsVision = false },
        new() { Id = "local", Kind = ProviderKinds.OpenAiCompatible, DisplayName = "Local server (Ollama / LM Studio)", BaseUrl = "http://localhost:11434/v1", Model = "", IsLocal = true, SupportsVision = false, SupportsTools = false },
    ];
}

/// <summary>JSON settings file. Holds no secrets. A corrupt file is set aside, not silently overwritten.</summary>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string Path { get; } = path;
    public string? LoadWarning { get; private set; }

    public AppSettings Load()
    {
        if (!File.Exists(Path)) return new AppSettings();
        try
        {
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json) ?? new AppSettings();
            if (s.Providers.Count == 0) s.Providers = AppSettings.DefaultProviders();
            // Provider kinds added in later versions appear for existing users without touching their own entries.
            foreach (var d in AppSettings.DefaultProviders())
                if (s.Providers.All(p => p.Id != d.Id)) s.Providers.Add(d);
            s.ObserveIntervalSeconds = Math.Clamp(s.ObserveIntervalSeconds, 3, 600);
            s.MaxImageEdge = Math.Clamp(s.MaxImageEdge, 480, 2576);
            s.SpeechRate = Math.Clamp(s.SpeechRate, 0.5, 2.0);
            return s;
        }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException)
        {
            var aside = Path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            try { File.Move(Path, aside, overwrite: true); } catch (IOException) { }
            LoadWarning = $"Settings could not be read and were reset. The old file was kept as {System.IO.Path.GetFileName(aside)}.";
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
        File.Move(tmp, Path, overwrite: true);
    }
}

/// <summary>Bounded, redacting log. Screen text and prompts are never passed to it by the app.</summary>
public sealed class DiagnosticLog
{
    private const int MaxEntries = 500;
    private const long MaxFileBytes = 512 * 1024;
    private readonly LinkedList<string> _entries = new();
    private readonly string? _file;
    private readonly object _gate = new();

    public event Action<string>? EntryAdded;

    public DiagnosticLog(string? file = null)
    {
        _file = file;
        if (file is not null) Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? e = null) =>
        Write("ERROR", e is null ? message : $"{message}: {e.GetType().Name}: {e.Message}");

    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate) return [.. _entries];
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {level} {SecretRedactor.Redact(message)}";
        lock (_gate)
        {
            _entries.AddLast(line);
            if (_entries.Count > MaxEntries) _entries.RemoveFirst();
            if (_file is not null)
            {
                try
                {
                    if (File.Exists(_file) && new FileInfo(_file).Length > MaxFileBytes)
                        File.Move(_file, _file + ".1", overwrite: true);
                    File.AppendAllText(_file, line + Environment.NewLine);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        EntryAdded?.Invoke(line);
    }
}
