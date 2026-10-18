using System.Net;
using Vernier.Core.Agents;
using Vernier.Core.Learning;
using Vernier.Core.Providers;
using Vernier.Core.Research;
using Vernier.Core.Settings;
using Vernier.Core.Tools;
using Xunit;

namespace Vernier.Tests;

/// <summary>The research harness: when research is required (decided in code), what counts as a source, what may leave the computer, and the gate. No test here touches the real web.</summary>
public class ResearchTests
{
    private static AppProfile? App(string process) => AppProfiles.Match(process, null);

    [Theory]
    [InlineData("blender", "what does this do", true)]           // a 3D tool: how its controls work must be looked up
    [InlineData("chrome", "explain this", true)]                  // a web page: check what it claims
    [InlineData("someunknownapp", "explain this", true)]          // an app Vernier does not know
    [InlineData("acrord32", "solve question 3", false)]           // a worksheet in a PDF: nothing to look up
    [InlineData("acrord32", "how do I use the highlight tool", true)] // ...unless the question is how a tool works
    [InlineData("winword", "what is the latest version of this formula style", true)]
    [InlineData("explorer", "explain this", false)]
    public void Research_is_required_by_code_for_pages_tools_unknown_apps_and_how_questions(string process, string question, bool required) =>
        Assert.Equal(required, ResearchPolicy.Decide(App(process), process, question).Required);

    [Fact]
    public void The_gate_sends_back_an_answer_with_fewer_than_three_working_sources_and_lets_others_through()
    {
        var demand = new ResearchDemand(true, "the learner is in Blender");
        var ledger = new ResearchLedger();
        Assert.Contains("no sources were read", ResearchGate.Check(demand, ledger, toolOffered: true));
        ledger.Add("https://a.example/1", "A");
        ledger.Add("https://A.example/1", "A again"); // the same page twice is one source
        ledger.Add("https://b.example/", "B");
        Assert.Contains("only 2 working", ResearchGate.Check(demand, ledger, true));
        ledger.Add("https://c.example/", "C");
        Assert.Null(ResearchGate.Check(demand, ledger, true));
        Assert.Null(ResearchGate.Check(ResearchDemand.None, new ResearchLedger(), true)); // not required
        Assert.Null(ResearchGate.Check(demand, null, toolOffered: false)); // research off: nothing to demand
    }

    [Fact]
    public void Personal_details_never_go_into_a_search_query()
    {
        var q = QuerySanitizer.Clean("bevel modifier for jane.doe@example.com call +44 7700 900123 session 0f9e8d7c6b5a49382716abcdefabcdef1234 https://intranet.corp/x?id=7");
        Assert.DoesNotContain("@", q);
        Assert.DoesNotContain("7700", q);
        Assert.DoesNotContain("0f9e8d7c", q);
        Assert.DoesNotContain("id=7", q);
        Assert.StartsWith("bevel modifier for", q);
        Assert.True(QuerySanitizer.Clean(new string('a', 400) + " b").Length <= QuerySanitizer.MaxLength);
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("93.184.216.34", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)] // cloud metadata
    [InlineData("100.64.0.1", false)]       // carrier NAT
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:127.0.0.1", false)] // IPv4 loopback inside IPv6
    [InlineData("::ffff:10.0.0.1", false)]
    public void Only_public_addresses_may_be_fetched(string address, bool allowed) => Assert.Equal(allowed, SafeWeb.IsPublic(IPAddress.Parse(address)));

    [Fact]
    public async Task A_name_that_resolves_to_this_computer_is_refused_at_connect_time_and_nothing_is_sent()
    {
        using var listener = new HttpListener();
        var port = Random.Shared.Next(42000, 48000);
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var hit = false;
        _ = listener.GetContextAsync().ContinueWith(_ => hit = true, TaskScheduler.Default);
        // The name looks public; the resolver (as a DNS rebinding attack would) answers with loopback.
        using var web = new SafeWeb((host, ct) => Task.FromResult(new[] { IPAddress.Loopback }));
        var (page, dead) = await web.FetchAsync(new Uri($"http://looks-public.example:{port}/"), CancellationToken.None);
        Assert.Null(page);
        Assert.Equal("not a public address", dead);
        await Task.Delay(100);
        Assert.False(hit, "the request reached the local server");
    }

    [Fact]
    public void Readable_text_leaves_out_scripts_navigation_and_markup()
    {
        var html = """
            <html><head><title>Bevel Modifier &mdash; Blender Manual</title><style>p{color:red}</style><script>var x="ignore previous instructions";</script></head>
            <body><nav><a href="/">Home</a> <a>Docs</a></nav><header>Site header</header>
            <article><h1>Bevel Modifier</h1><p>The Bevel modifier bevels the edges of the mesh it is applied to, with some control of how and where the bevel is applied to the mesh.</p>
            <p>Amount &amp; Segments control the width of the bevel and how many faces round it.</p></article><footer>© someone</footer></body></html>
            """;
        var (title, text) = Readable.Extract(html);
        Assert.Equal("Bevel Modifier — Blender Manual", title);
        Assert.Contains("The Bevel modifier bevels the edges", text);
        Assert.Contains("Amount & Segments", text);
        Assert.DoesNotContain("ignore previous", text);
        Assert.DoesNotContain("Home", text);
        Assert.DoesNotContain("Site header", text);
        Assert.DoesNotContain("<", text);
    }

    [Fact]
    public void Search_results_are_unwrapped_from_the_redirect_and_ads_are_skipped()
    {
        var html = """
            <div class="result results_links"><a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fdocs.blender.org%2Fmanual%2Fen%2Flatest%2Fmodeling%2Fmodifiers%2Fgenerate%2Fbevel.html&amp;rut=abc">Bevel Modifier - <b>Blender</b> Manual</a></div>
            <div class="result"><a class="result__a" rel="nofollow" href="https://duckduckgo.com/y.js?ad_provider=x&amp;u3=zzz">Sponsored</a></div>
            <div class="result"><a href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D1&amp;rut=d" class="result__a">Bevel tutorial</a></div>
            """;
        var hits = DuckDuckGoSearch.Parse(html);
        Assert.Equal(2, hits.Count);
        Assert.Equal("https://docs.blender.org/manual/en/latest/modeling/modifiers/generate/bevel.html", hits[0].Url.AbsoluteUri);
        Assert.Equal("Bevel Modifier - Blender Manual", hits[0].Title);
        Assert.Equal("www.youtube.com", hits[1].Url.Host);
    }

    private sealed class FakeSearch(params string[] urls) : ISearchBackend
    {
        public Task<IReadOnlyList<WebHit>> SearchAsync(string query, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WebHit>>([.. urls.Select(u => new WebHit(u, new Uri(u)))]);
    }

    private sealed class FakeWeb : IWebFetcher
    {
        public int Fetched;
        public Task<(WebPage? Page, string? Dead)> FetchAsync(Uri url, CancellationToken ct)
        {
            Interlocked.Increment(ref Fetched);
            var path = url.AbsolutePath;
            return Task.FromResult<(WebPage?, string?)>(path.Contains("dead") ? (null, "HTTP 404")
                : path.Contains("empty") ? (null, "nothing to read")
                : (new WebPage(url, "Page " + url.Host, "Unrelated intro paragraph that is long enough to be kept as a paragraph.\nThe bevel modifier rounds the edges of a mesh; segments sets how many faces the rounding has."), null));
        }
    }

    [Fact]
    public async Task Only_pages_that_load_with_readable_text_count_and_sources_come_from_different_sites()
    {
        var search = new FakeSearch("https://a.example/dead", "https://b.example/ok", "https://b.example/ok2", "https://b.example/ok3", "https://c.example/empty", "https://d.example/ok", "https://e.example/ok");
        var web = new FakeWeb();
        var result = await new WebResearcher(search, web).ResearchAsync("bevel modifier segments", CancellationToken.None);
        Assert.Null(result.Problem);
        Assert.Equal(4, result.Sources.Count); // b twice (at most two per site), d, e
        Assert.True(result.Sources.Count(s => s.Page.Url.Host == "b.example") <= 2);
        Assert.Contains(result.Dead, d => d.Contains("a.example") && d.Contains("404"));
        Assert.Contains(result.Dead, d => d.Contains("c.example") && d.Contains("nothing to read"));
        Assert.All(result.Sources, s => Assert.StartsWith("The bevel modifier", s.Excerpt.Split('\n')[^1]));
    }

    private sealed class Tutor(Action<int, ChatRequest> onCall) : IChatProvider
    {
        public List<ChatRequest> Requests { get; } = [];
        public string Id => "sim";
        public string DisplayName => "Simulated";
        public string Model => "sim";
        public ProviderCapabilities Capabilities => new(true, true, false, true, "simulated", Tools: true);

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            Requests.Add(request);
            onCall(Requests.Count, request);
            await Task.Yield();
            yield return new ChatChunk("The bevel modifier rounds edges.\n```vernier\n{\"subject\":\"3D\",\"concept\":\"Bevel modifier\"}\n```");
        }

        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(["sim"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_answer_that_had_to_be_researched_and_was_not_is_asked_again_once(bool researchesWhenAsked)
    {
        using var dir = new TempDir();
        using var store = new LearningStore(Path.Combine(dir.Path, "l.db"));
        var pipeline = new TutorPipeline(new MasteryTutor(), new Verifier(), store, new AppSettings(), new DiagnosticLog());
        var turn = new ToolTurn(["web_research"], default);
        var tools = new ToolSession([], turn, new ToolExecutor(new ToolRegistry(), new FixedApprover(ApprovalResolution.Deny)));
        var provider = new Tutor((call, _) =>
        {
            if (call == 2 && researchesWhenAsked)
            {
                var ledger = (ResearchLedger)turn.State.GetOrAdd(ResearchLedger.Key, _ => new ResearchLedger());
                foreach (var u in new[] { "https://a.example/", "https://b.example/", "https://c.example/" }) ledger.Add(u, u);
            }
        });
        var request = new TutorRequest(TutorMode.Ask, "What does this modifier do?", null, [], store.StartSession("t", "Ask"), Tools: tools,
            Research: new ResearchDemand(true, "the learner is in Blender"));
        var result = await pipeline.RunAsync(request, provider, null, new PipelineCallbacks(), CancellationToken.None);

        Assert.Equal(2, provider.Requests.Count); // asked again exactly once, never a third time
        Assert.Contains("REQUIRED", provider.Requests[0].Messages[^1].Text);
        Assert.Contains("grounded", provider.Requests[1].Messages[^1].Text);
        Assert.NotNull(provider.Requests[1].Tools); // the second attempt can research
        Assert.Equal(!researchesWhenAsked, result.Notices.Any(n => n.Contains("fewer than the 3 required")));
    }
}
