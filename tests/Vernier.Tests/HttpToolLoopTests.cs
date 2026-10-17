using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Providers;
using Vernier.Core.Settings;
using Vernier.Core.Tools;

namespace Vernier.Tests;

/// <summary>
/// OpenAI-compatible and Gemini tool loops against loopback servers speaking each wire format.
/// Simulated: it proves our request and response handling, not that a hosted API accepts it.
/// </summary>
public class HttpToolLoopTests
{
    private static (HttpListener Listener, string Url, List<string> Bodies, Task Serving) Serve(params string[] replies)
    {
        HttpListener listener;
        int port;
        for (var attempt = 0; ; attempt++)
        {
            port = Random.Shared.Next(49001, 60000);
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try { listener.Start(); break; }
            catch (HttpListenerException) when (attempt < 20) { listener.Close(); } // port taken by a parallel test
        }
        var bodies = new List<string>();
        var serving = Task.Run(async () =>
        {
            foreach (var reply in replies)
            {
                var ctx = await listener.GetContextAsync();
                using (var reader = new StreamReader(ctx.Request.InputStream)) bodies.Add(await reader.ReadToEndAsync());
                ctx.Response.ContentType = "text/event-stream";
                await ctx.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(reply));
                ctx.Response.Close();
            }
        });
        return (listener, $"http://127.0.0.1:{port}/", bodies, serving);
    }

    private static (ToolSession Tools, EchoTool Echo) Tools()
    {
        var registry = new ToolRegistry();
        var echo = new EchoTool("echo");
        registry.Add(echo);
        return (new ToolSession(registry.Definitions, new ToolTurn(["echo"], default), new ToolExecutor(registry, new FixedApprover(ApprovalResolution.Deny))), echo);
    }

    private static async Task<string> Drain(IChatProvider provider, ToolSession tools)
    {
        var text = new StringBuilder();
        await foreach (var c in provider.StreamAsync(new ChatRequest("sys", [ChatMessage.User("mark it")], Tools: tools), default))
            if (c.Text is not null) text.Append(c.Text);
        return text.ToString();
    }

    private static string Data(string json) => "data: " + json + "\n\n";

    [Fact]
    public async Task OpenAi_compatible_tool_calls_stream_in_fragments_and_results_go_back_as_tool_messages()
    {
        var first = Data("""{"choices":[{"delta":{"content":"Here. "}}]}""")
            + Data("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_a","type":"function","function":{"name":"echo","arguments":"{\"te"}}]}}]}""")
            + Data("""{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"xt\":\"one\"}"}}]}}]}""")
            + Data("""{"choices":[{"delta":{"tool_calls":[{"index":1,"id":"call_b","type":"function","function":{"name":"echo","arguments":"{\"text\":\"two\"}"}}]}}]}""")
            + Data("""{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}""") + Data("[DONE]");
        var second = Data("""{"choices":[{"delta":{"content":"Done."},"finish_reason":"stop"}]}""")
            + Data("""{"choices":[],"usage":{"prompt_tokens":5,"completion_tokens":2}}""") + Data("[DONE]");
        var (listener, url, bodies, serving) = Serve(first, second);
        using var _ = listener;
        var (tools, echo) = Tools();
        var provider = new OpenAiCompatibleProvider(new ProviderSettings { Id = "local", Kind = ProviderKinds.OpenAiCompatible, DisplayName = "Local", BaseUrl = url + "v1", Model = "m", IsLocal = true }, new MemorySecrets(), new HttpClient());

        Assert.Equal("Here. Done.", await Drain(provider, tools));
        await serving;
        Assert.Equal(2, echo.Runs);
        Assert.Equal("echo", JsonNode.Parse(bodies[0])!["tools"]![0]!["function"]!["name"]!.GetValue<string>());
        var messages = JsonNode.Parse(bodies[1])!["messages"]!.AsArray();
        var assistant = messages[2]!;
        Assert.Equal("Here. ", assistant["content"]!.GetValue<string>());
        Assert.Equal("""{"text":"one"}""", assistant["tool_calls"]![0]!["function"]!["arguments"]!.GetValue<string>());
        Assert.Equal("call_b", messages[4]!["tool_call_id"]!.GetValue<string>());
        Assert.Equal("echo two", messages[4]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task Gemini_function_calls_are_answered_and_the_models_parts_are_echoed_unchanged()
    {
        var first = Data("""{"candidates":[{"content":{"role":"model","parts":[{"text":"Here. "},{"functionCall":{"name":"echo","args":{"text":"g"}},"thoughtSignature":"ts-1"}]}}]}""");
        var second = Data("""{"candidates":[{"content":{"role":"model","parts":[{"text":"Done."}]}}],"usageMetadata":{"promptTokenCount":4,"candidatesTokenCount":2}}""");
        var (listener, url, bodies, serving) = Serve(first, second);
        using var _ = listener;
        var (tools, echo) = Tools();
        var secrets = new MemorySecrets();
        secrets.Set("G", "k");
        var provider = new GeminiProvider(new ProviderSettings { Id = "gemini", Kind = ProviderKinds.Gemini, DisplayName = "Gemini", BaseUrl = url, Model = "m", ApiKeyName = "G" }, secrets, new HttpClient());

        Assert.Equal("Here. Done.", await Drain(provider, tools));
        await serving;
        Assert.Equal(1, echo.Runs);
        var declared = JsonNode.Parse(bodies[0])!["tools"]![0]!["functionDeclarations"]![0]!["parameters"]!;
        Assert.Null(declared["additionalProperties"]); // Gemini rejects it; the executor still enforces it
        Assert.Null(declared["properties"]!["box"]!["additionalProperties"]);
        var contents = JsonNode.Parse(bodies[1])!["contents"]!.AsArray();
        Assert.Equal("ts-1", contents[1]!["parts"]![1]!["thoughtSignature"]!.GetValue<string>());
        Assert.Equal("echo g", contents[2]!["parts"]![0]!["functionResponse"]!["response"]!["content"]!.GetValue<string>());
    }
}
