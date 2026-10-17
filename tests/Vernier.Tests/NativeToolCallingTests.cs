using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Providers;
using Vernier.Core.Settings;
using Vernier.Core.Tools;

namespace Vernier.Tests;

/// <summary>
/// The Anthropic provider's tool loop against a loopback server speaking the Messages streaming
/// format. Simulated: it proves the request/response handling through the real SDK, not that the
/// live API accepts it (no API key is available to the author).
/// </summary>
public class AnthropicToolLoopTests
{
    private static string Sse(params (string Event, string Data)[] events) =>
        string.Concat(events.Select(e => $"event: {e.Event}\ndata: {e.Data}\n\n"));

    private const string Start = """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":11,"output_tokens":1}}}""";

    private static readonly string FirstTurn = Sse(
        ("message_start", Start),
        ("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Mark the formula."}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig-abc"}}"""),
        ("content_block_stop", """{"type":"content_block_stop","index":0}"""),
        ("content_block_start", """{"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"Look here. "}}"""),
        ("content_block_stop", """{"type":"content_block_stop","index":1}"""),
        ("content_block_start", """{"type":"content_block_start","index":2,"content_block":{"type":"tool_use","id":"toolu_1","name":"echo","input":{}}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":2,"delta":{"type":"input_json_delta","partial_json":"{\"text\":"}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":2,"delta":{"type":"input_json_delta","partial_json":"\"hi\"}"}}"""),
        ("content_block_stop", """{"type":"content_block_stop","index":2}"""),
        ("message_delta", """{"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":20}}"""),
        ("message_stop", """{"type":"message_stop"}"""));

    private static readonly string SecondTurn = Sse(
        ("message_start", Start),
        ("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}"""),
        ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Done."}}"""),
        ("content_block_stop", """{"type":"content_block_stop","index":0}"""),
        ("message_delta", """{"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":3}}"""),
        ("message_stop", """{"type":"message_stop"}"""));

    [Fact]
    public async Task Tools_run_as_soon_as_their_arguments_arrive_and_the_assistant_turn_is_echoed_with_its_thinking()
    {
        var port = new Random().Next(40001, 49000);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var bodies = new List<string>();
        var toolRanBeforeSecondRequest = false;
        var echo = new EchoTool("echo");
        var serving = Task.Run(async () =>
        {
            foreach (var reply in new[] { FirstTurn, SecondTurn })
            {
                var ctx = await listener.GetContextAsync();
                using (var reader = new StreamReader(ctx.Request.InputStream)) bodies.Add(await reader.ReadToEndAsync());
                if (bodies.Count == 2) toolRanBeforeSecondRequest = echo.Runs == 1;
                ctx.Response.ContentType = "text/event-stream";
                var bytes = Encoding.UTF8.GetBytes(reply);
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        });

        var registry = new ToolRegistry();
        registry.Add(echo);
        var turn = new ToolTurn(["echo"], default);
        var tools = new ToolSession(registry.Definitions, turn, new ToolExecutor(registry, new FixedApprover(ApprovalResolution.Deny)));
        var secrets = new MemorySecrets();
        secrets.Set("TEST_KEY", "sk-test");
        var provider = new AnthropicProvider(new ProviderSettings { Id = "anthropic", Kind = ProviderKinds.Anthropic, DisplayName = "Anthropic", Model = "claude-opus-5-5", ApiKeyName = "TEST_KEY", BaseUrl = $"http://127.0.0.1:{port}" }, secrets);

        var text = new StringBuilder();
        var notes = new List<ToolCallNote>();
        TokenUsage? usage = null;
        await foreach (var chunk in provider.StreamAsync(new ChatRequest("system", [ChatMessage.User("mark it")], Tools: tools), default))
        {
            if (chunk.Text is not null) text.Append(chunk.Text);
            if (chunk.Tool is not null) notes.Add(chunk.Tool);
            if (chunk.Usage is not null) usage = chunk.Usage;
        }
        await serving;

        Assert.Equal("Look here. Done.", text.ToString());
        Assert.Equal(1, echo.Runs);
        Assert.True(toolRanBeforeSecondRequest, "the tool ran only after the model's turn ended");
        Assert.Equal(["echo:False", "echo:True"], notes.Select(n => $"{n.Name}:{n.Finished}"));

        var first = JsonNode.Parse(bodies[0])!;
        Assert.Equal("echo", first["tools"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("text", first["tools"]![0]!["input_schema"]!["required"]![0]!.GetValue<string>());
        Assert.True(first["tools"]![0]!["eager_input_streaming"]!.GetValue<bool>());

        var second = JsonNode.Parse(bodies[1])!["messages"]!.AsArray();
        var assistant = second[1]!["content"]!.AsArray();
        Assert.Equal("thinking", assistant[0]!["type"]!.GetValue<string>());
        Assert.Equal("sig-abc", assistant[0]!["signature"]!.GetValue<string>()); // signature preserved, or the API rejects the turn
        Assert.Equal("tool_use", assistant[2]!["type"]!.GetValue<string>());
        Assert.Equal("hi", assistant[2]!["input"]!["text"]!.GetValue<string>());
        var result = second[2]!["content"]![0]!;
        Assert.Equal("tool_result", result["type"]!.GetValue<string>());
        Assert.Equal("toolu_1", result["tool_use_id"]!.GetValue<string>());
        Assert.Equal("echo hi", result["content"]!.GetValue<string>());
        Assert.Equal(new TokenUsage(22, 23), usage);
    }
}
