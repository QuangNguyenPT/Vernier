using Vernier.Core.Context;
using Vernier.Core.Providers;
using Vernier.Core.Security;

namespace Vernier.App.Context;

/// <summary>
/// Optional: when the learner moves on from a substantial episode, a model the learner chose writes
/// one line about what it was ("choosing u in integration by parts"). Only a local model is used in
/// the background, so nothing leaves the computer without a question being asked. Without one,
/// episodes keep the facts the engine recorded by code.
/// </summary>
public sealed class EpisodeSummariser
{
    private readonly ContextEngine _engine;
    private readonly SemaphoreSlim _one = new(1, 1);

    public EpisodeSummariser(ContextEngine engine)
    {
        _engine = engine;
        engine.EpisodeEnded += e => _ = SummariseAsync(e);
    }

    private async Task SummariseAsync(Episode e)
    {
        var settings = AppServices.Settings;
        if (e.ActiveSeconds < 180 || e.Summary is not null || settings.ContextModelProviderId is not { } id) return;
        if (settings.Providers.FirstOrDefault(p => p.Id == id) is not { IsLocal: true } chosen) return; // background calls stay on this computer
        if (!await _one.WaitAsync(0)) return; // one at a time; a busy model is not queued behind
        try
        {
            var provider = AppServices.Providers.Create(chosen);
            var material = $"Title: {e.Title}\nApp: {e.App}\n" + (e.MediaTitle is { } m ? $"Video: {m}\n" : "") +
                           (e.Entities.Count > 0 ? "On screen: " + string.Join(" | ", e.Entities.Take(12)) + "\n" : "") +
                           (e.RecentLines.Count > 0 ? "Text seen: " + string.Join(" / ", e.RecentLines.TakeLast(12)) + "\n" : "") +
                           (e.LastQuestion is { } q ? $"Learner asked: {q}\n" : "");
            var request = new ChatRequest(
                "You write a single plain line (at most 15 words) saying what topic a learner was working on, from notes about their screen. No preamble. " + Untrusted.SystemRule,
                [ChatMessage.User(Untrusted.Wrap("episode", material, 3000))], MaxTokens: 60);
            var result = await ChatRunner.CompleteAsync(provider, request, TimeSpan.FromSeconds(40), CancellationToken.None);
            _engine.Summarise(e, result.Text);
        }
        catch (ProviderException ex) { AppServices.Log.Warn($"Context summary skipped ({ex.Kind})."); }
        catch (OperationCanceledException) { }
        finally { _one.Release(); }
    }
}
