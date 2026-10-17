using Vernier.Core.Agents;
using Vernier.Core.Learning;
using Vernier.Core.Providers;
using Vernier.Core.Settings;

namespace Vernier.Tests;

/// <summary>
/// LIVE tests against the real Claude Code CLI using this machine's sign-in. They cost usage, so
/// they only run when VERNIER_LIVE=1. Without it they return immediately and prove nothing.
/// </summary>
public class LiveClaudeCodeTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("VERNIER_LIVE") == "1";

    [Fact]
    public async Task Claude_code_cli_answers_a_connection_test()
    {
        if (!Enabled) return;
        using var dir = new TempDir();
        var provider = new ClaudeCodeProvider(new ProviderSettings { Id = "claude-code", DisplayName = "Claude Code", Kind = ProviderKinds.ClaudeCode }, dir.Path);
        var (ok, message) = await ChatRunner.TestAsync(provider, default);
        Assert.True(ok, message);
        Assert.Empty(Directory.GetDirectories(dir.Path)); // the private work folder is removed afterwards
    }

    [Fact]
    public async Task A_real_tutoring_turn_runs_through_the_whole_pipeline()
    {
        if (!Enabled) return;
        using var dir = new TempDir();
        using var store = new LearningStore(Path.Combine(dir.Path, "l.db"));
        var provider = new ClaudeCodeProvider(new ProviderSettings { Id = "claude-code", DisplayName = "Claude Code", Kind = ProviderKinds.ClaudeCode }, dir.Path);
        var pipeline = new TutorPipeline(new MasteryTutor(), new Verifier(), store, new AppSettings(), new DiagnosticLog());
        var session = store.StartSession("live", "Ask");

        var turn = await pipeline.RunAsync(
            new TutorRequest(TutorMode.Ask, "Give me the answer directly: what is the kinetic energy of a 2 kg cart moving at 3 m/s? Show the formula.", null, [], session),
            provider, null, new PipelineCallbacks(), default);

        Assert.Contains("9", turn.Output.Text);
        Assert.True(turn.Output.StructuredBlockPresent, "the model did not return the vernier block");
        Assert.False(turn.Verification.HasFailures, turn.Verification.Summary);
        Assert.True(turn.Verification.Passed >= 1, "no numeric claim was recomputed: " + turn.Verification.Summary);
        Assert.Equal(2, store.Messages(session).Count);
        // The formula is written as TeX, so the answer typesets it (a stacked fraction, a real exponent).
        Assert.True(Vernier.Core.Agents.AnswerText.Parse(turn.Output.Text).SelectMany(b => b.Spans).Any(s => s.Kind == Vernier.Core.Agents.SpanKind.Math), "no typeset formula in: " + turn.Output.Text);
        Console.WriteLine(turn.Output.Text);
    }
}

public class LiveMcpTests
{
    private sealed class CodeTool(string code) : Vernier.Core.Tools.ITool
    {
        public int Runs { get; private set; }
        public Vernier.Core.Tools.ToolDefinition Definition { get; } = new("selftest_code",
            "Returns the secret check code. Call this tool, then tell the user the code exactly.",
            new System.Text.Json.Nodes.JsonObject { ["type"] = "object", ["properties"] = new System.Text.Json.Nodes.JsonObject(), ["additionalProperties"] = false }, Vernier.Core.Tools.ToolRisk.Observe);
        public Task<Vernier.Core.Tools.ToolResult> ExecuteAsync(System.Text.Json.Nodes.JsonObject args, Vernier.Core.Tools.ToolTurn turn, CancellationToken ct) { Runs++; return Task.FromResult(Vernier.Core.Tools.ToolResult.Ok("The code is " + code)); }
    }

    [Fact]
    public async Task The_real_claude_cli_connects_to_vernier_over_mcp_lists_the_tool_and_calls_it()
    {
        if (Environment.GetEnvironmentVariable("VERNIER_LIVE") != "1") return;
        var code = "VX" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(); // not guessable: the answer can only come from the tool result
        var tool = new CodeTool(code);
        var registry = new Vernier.Core.Tools.ToolRegistry();
        registry.Add(tool);
        var executor = new Vernier.Core.Tools.ToolExecutor(registry, new FixedApprover(Vernier.Core.Tools.ApprovalResolution.Deny));
        await using var server = new Vernier.Core.Tools.McpServer(executor);
        var turn = new Vernier.Core.Tools.ToolTurn([tool.Definition.Name], CancellationToken.None);
        using var grant = server.Open(turn);
        using var dir = new TempDir();
        var provider = new ClaudeCodeProvider(new ProviderSettings { Id = "claude-code", DisplayName = "Claude Code", Kind = ProviderKinds.ClaudeCode }, dir.Path);
        var session = new ToolSession([tool.Definition], turn, executor, server);
        var request = new ChatRequest("You are a test assistant. Use your tools when asked.", [ChatMessage.User("Call the selftest_code tool and reply with only the code it returns.")], Tools: session);
        var result = await ChatRunner.CompleteAsync(provider, request, TimeSpan.FromSeconds(120), CancellationToken.None);
        Assert.Contains(code, result.Text);
        Assert.Equal(1, tool.Runs);
        var ev = Assert.Single(turn.Events);
        Assert.Equal("selftest_code", ev.Tool);
        Assert.False(ev.IsError);
    }
}

/// <summary>REAL graphify CLI, if installed. No model call: `graphify update` only parses code. Returns early when the CLI is absent.</summary>
public class RealGraphifyTests
{
    [Fact]
    public async Task Graphify_update_output_is_read_by_the_graph_source()
    {
        if (Vernier.Core.Knowledge.GraphifySource.FindCli() is null) return;
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "geom.py"), "def area(r):\n    return 3.14159 * r * r\n\ndef volume(r, h):\n    return area(r) * h\n");

        var (ok, text) = await Vernier.Core.Knowledge.GraphifySource.UpdateAsync(dir.Path, default);
        Assert.True(ok, text);

        var graph = await new Vernier.Core.Knowledge.GraphifySource().LoadAsync(dir.Path, default);
        Assert.NotNull(graph);
        Assert.Contains(graph.Nodes, n => n.Label.StartsWith("area"));
        Assert.Contains(graph.Edges, e => e.Relation == "calls");
    }
}
