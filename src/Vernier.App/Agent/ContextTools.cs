using System.Globalization;
using System.Net.Http;
using System.Text.Json.Nodes;
using Vernier.Core.Agents;
using Vernier.Core.Tools;

namespace Vernier.App.Agent;

/// <summary>
/// The tutor's view of the learner's workflow beyond the pixels: what is in front, what was open
/// before, what is playing and where in it, and a way to pause or rewind it. All of it requires
/// the learner's "remember what I'm working on" setting; the gate checks it on every call.
/// </summary>
public static class ContextTools
{
    public static IEnumerable<ITool> All() => [new GetContext(), new MediaControl(), new VideoInfo(), new SessionRecall()];
    public static readonly string[] Names = ["get_context", "media_control", "video_info", "session_recall"];

    private static JsonObject Schema(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray([.. required.Select(r => (JsonNode)r)]), ["additionalProperties"] = false,
    };

    private sealed class GetContext : ITool
    {
        public ToolDefinition Definition { get; } = new("get_context",
            "What the learner is doing and has been doing, observed on their computer: the episode in front (app, page or video, time spent, rewinds, questions), what is playing and its position, the page address, selected text, and earlier episodes ranked against 'about'. Each fact says where it came from and how old it is.",
            Schema(new JsonObject { ["about"] = new JsonObject { ["type"] = "string", ["maxLength"] = 200, ["description"] = "What to look for in earlier work, e.g. \"the formula from the lecture\"." } }), ToolRisk.Observe, UntrustedOutput: true);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var hub = AppServices.Context ?? throw new ToolException("Activity context is not running.");
            var front = AppServices.Session.ActionWindow;
            var work = await AppServices.Ui.InvokeAsync(() => hub.SnapshotAsync(front, readContent: !AppServices.Session.CaptureBlocked)).Task.Unwrap().ConfigureAwait(false);
            var query = args["about"]?.GetValue<string>() ?? "";
            var episodes = hub.Engine.Select(query, DateTimeOffset.UtcNow, max: 8);
            if (!AppServices.Session.IncludeEarlierContext) episodes = episodes.WithoutEarlier(); // the learner's "Leave out earlier" holds for the tool too
            if (AppServices.Session.CaptureBlocked) episodes = episodes.WithoutScreenText();
            var text = (episodes.IsEmpty ? "" : episodes.Render() + "\n") + (work.IsEmpty ? "" : work.Render());
            return ToolResult.Ok(text.Length == 0 ? "Nothing has been observed yet." : text);
        }
    }

    private sealed class SessionRecall : ITool
    {
        public ToolDefinition Definition { get; } = new("session_recall",
            "Looks up earlier turns of this same conversation with the learner (what they asked, what you answered, which screen it was about, checks, misconceptions, what was left open). Use it when the session digest has folded a turn you need, or to check what was already explained before explaining again.",
            Schema(new JsonObject { ["about"] = new JsonObject { ["type"] = "string", ["maxLength"] = 200, ["description"] = "Words that the turn would contain, e.g. \"torque misconception\"." } }, "about"),
            ToolRisk.Observe, UntrustedOutput: true);

        public Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok(AppServices.Session.Recall(args["about"]!.GetValue<string>())));
    }

    private sealed class MediaControl : ITool
    {
        public ToolDefinition Definition { get; } = new("media_control",
            "Pauses, plays or moves the video or audio the learner is playing (through Windows' media controls). Pause before marking a frame so the marks stay true; seek to a time to show the moment you are explaining. The learner can undo it with one key.",
            Schema(new JsonObject
            {
                ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("pause", "play", "seek") },
                ["to"] = new JsonObject { ["type"] = "string", ["maxLength"] = 10, ["description"] = "For seek: the time as m:ss or h:mm:ss." },
            }, "action"),
            ToolRisk.Control);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var hub = AppServices.Context ?? throw new ToolException("Activity context is not running.");
            var action = args["action"]!.GetValue<string>();
            TimeSpan? to = null;
            if (action == "seek")
            {
                var text = args["to"]?.GetValue<string>() ?? throw new ToolException("seek needs a time in \"to\", e.g. 12:35.");
                if (!TimeSpan.TryParseExact(text, [@"m\:ss", @"mm\:ss", @"h\:mm\:ss"], CultureInfo.InvariantCulture, out var t))
                    throw new ToolException($"\"{text}\" is not a time like 12:35 or 1:02:03.");
                to = t;
            }
            var done = await AppServices.Ui.InvokeAsync(() => hub.ControlMediaAsync(action, to)).Task.Unwrap().ConfigureAwait(false);
            if (!done) return ToolResult.Fail("Nothing is playing through Windows' media controls, or the app refused. Ask the learner to do it.");
            var now = await AppServices.Ui.InvokeAsync(() => hub.ReadMediaAsync()).Task.Unwrap().ConfigureAwait(false);
            return ToolResult.Ok($"Done: {action}{(to is { } t2 ? " to " + MediaState.Clock(t2) : "")}." +
                (now is { } m ? $" Now \"{m.Title}\" {(m.Playing ? "playing" : "paused")}{(m.Position is { } p ? " at " + MediaState.Clock(p) : "")}." : ""));
        }
    }

    /// <summary>
    /// Public facts about an online video from its page address, through the provider's documented
    /// oEmbed endpoint. Captions are not available through any supported public API, and none are invented.
    /// </summary>
    private sealed class VideoInfo : ITool
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

        public ToolDefinition Definition { get; } = new("video_info",
            "Title and channel of an online video (YouTube or Vimeo) from its address, from the site's public oEmbed service. It does not return captions or a transcript: for what is said or shown, use look on the screen, the current media position from get_context, or ask the learner.",
            Schema(new JsonObject { ["url"] = new JsonObject { ["type"] = "string", ["maxLength"] = 500 } }, "url"),
            ToolRisk.Observe, UntrustedOutput: true);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            if (!Uri.TryCreate(args["url"]!.GetValue<string>(), UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
                throw new ToolException("Give the video's https address.");
            var host = url.Host.ToLowerInvariant();
            var endpoint = host is "www.youtube.com" or "youtube.com" or "m.youtube.com" or "youtu.be" ? "https://www.youtube.com/oembed?format=json&url="
                : host is "vimeo.com" or "www.vimeo.com" ? "https://vimeo.com/api/oembed.json?url="
                : throw new ToolException("Only YouTube and Vimeo addresses are supported.");
            string body;
            try { body = await Http.GetStringAsync(endpoint + Uri.EscapeDataString(url.ToString()), ct).ConfigureAwait(false); }
            catch (HttpRequestException e) { throw new ToolException("The video site did not answer: " + e.Message); }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ToolException("The video site did not answer in time."); }
            if (Vernier.Core.Providers.ProviderHttp.TryParse(body) is not JsonObject o) throw new ToolException("The video site's answer was not readable.");
            var position = url.Query.Contains("t=", StringComparison.Ordinal) ? " The address carries a start time." : "";
            return ToolResult.Ok($"\"{o["title"]?.GetValue<string>()}\" by {o["author_name"]?.GetValue<string>()} ({o["provider_name"]?.GetValue<string>()}).{position} No transcript is available through this service.");
        }
    }
}
