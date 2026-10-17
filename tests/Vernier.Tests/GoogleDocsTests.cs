using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Vernier.Core.Connectors;
using Xunit;

namespace Vernier.Tests;

/// <summary>Fixture tests: the requests Vernier sends to Google and how it reads the answers. No live Google account is used.</summary>
public class GoogleDocsTests
{
    private sealed class Recorder(Func<HttpRequestMessage, string, (HttpStatusCode, string)> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Seen.Add((request, body));
            var (status, text) = respond(request, body);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pkce_matches_the_RFC_7636_example_and_the_sign_in_asks_only_for_drive_file()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", GoogleOAuth.ChallengeFor("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
        var pkce = GoogleOAuth.NewPkce();
        Assert.InRange(pkce.Verifier.Length, 43, 128);
        var url = new Uri(GoogleOAuth.AuthorizeUrl("client-1.apps.googleusercontent.com", "http://127.0.0.1:5123/", pkce));
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        Assert.Equal("https://www.googleapis.com/auth/drive.file", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(pkce.Challenge, query["code_challenge"]);
        Assert.Equal(pkce.State, query["state"]);
        Assert.Equal("http://127.0.0.1:5123/", query["redirect_uri"]);
        Assert.DoesNotContain(pkce.Verifier, url.ToString()); // the verifier never goes through the browser
    }

    [Fact]
    public void The_redirect_is_accepted_only_for_this_sign_in()
    {
        var pkce = GoogleOAuth.NewPkce();
        Assert.Equal("abc", GoogleOAuth.CodeFrom(new Uri($"http://127.0.0.1:5123/?state={pkce.State}&code=abc"), pkce));
        Assert.Contains("did not match", Assert.Throws<ConnectorException>(() => GoogleOAuth.CodeFrom(new Uri("http://127.0.0.1:5123/?state=forged&code=abc"), pkce)).Message);
        Assert.Contains("cancelled", Assert.Throws<ConnectorException>(() => GoogleOAuth.CodeFrom(new Uri($"http://127.0.0.1:5123/?state={pkce.State}&error=access_denied"), pkce)).Message);
    }

    [Fact]
    public async Task The_code_is_exchanged_with_the_verifier_and_a_refresh_keeps_the_refresh_token()
    {
        var handler = new Recorder((_, body) => body.Contains("grant_type=authorization_code")
            ? (HttpStatusCode.OK, """{"access_token":"at1","refresh_token":"rt1","expires_in":3599}""")
            : (HttpStatusCode.OK, """{"access_token":"at2","expires_in":3599}"""));
        var http = new HttpClient(handler);
        var tokens = await GoogleOAuth.ExchangeAsync(http, "cid", "secret", "the-code", "the-verifier", "http://127.0.0.1:5123/", Now, default);
        Assert.Equal(("at1", "rt1"), (tokens.AccessToken, tokens.RefreshToken));
        Assert.Equal(Now.AddSeconds(3599 - 60), tokens.ExpiresAt); // renewed a minute early
        var sent = handler.Seen[0];
        Assert.Equal(GoogleOAuth.TokenEndpoint, sent.Request.RequestUri!.ToString());
        Assert.Contains("code_verifier=the-verifier", sent.Body);
        Assert.Contains("code=the-code", sent.Body);

        var refreshed = await GoogleOAuth.RefreshAsync(http, "cid", null, "rt1", Now, default);
        Assert.Equal(("at2", "rt1"), (refreshed.AccessToken, refreshed.RefreshToken));
        Assert.DoesNotContain("client_secret", handler.Seen[1].Body); // omitted when there is none
    }

    [Fact]
    public async Task A_revoked_sign_in_says_to_connect_again()
    {
        var http = new HttpClient(new Recorder((_, _) => (HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")));
        var e = await Assert.ThrowsAsync<ConnectorException>(() => GoogleOAuth.RefreshAsync(http, "cid", null, "old", Now, default));
        Assert.Contains("Connect again", e.Message);
    }

    [Fact]
    public async Task Docs_are_created_read_and_appended_to_with_the_bearer_token()
    {
        var handler = new Recorder((request, _) => request.Method == HttpMethod.Get
            ? (HttpStatusCode.OK, """{"body":{"content":[{"paragraph":{"elements":[{"textRun":{"content":"Torque\n"}}]}},{"table":{"tableRows":[{"tableCells":[{"content":[{"paragraph":{"elements":[{"textRun":{"content":"r × F\n"}}]}}]}]}]}}]}}""")
            : request.RequestUri!.AbsolutePath.EndsWith(":batchUpdate", StringComparison.Ordinal) ? (HttpStatusCode.OK, "{}")
            : (HttpStatusCode.OK, """{"documentId":"doc_123456789012"}"""));
        var docs = new GoogleDocsClient(new HttpClient(handler), _ => Task.FromResult("token-1"));

        Assert.Equal("doc_123456789012", await docs.CreateAsync("Vernier notes", default));
        Assert.Equal("Torque\nr × F\n", await docs.ReadTextAsync("doc_123456789012", default));
        await docs.AppendAsync("doc_123456789012", "Torque is r x F.", default);

        Assert.All(handler.Seen, s => Assert.Equal("Bearer token-1", s.Request.Headers.Authorization!.ToString()));
        var append = JsonNode.Parse(handler.Seen[2].Body)!;
        Assert.Equal("\nTorque is r x F.\n", append["requests"]![0]!["insertText"]!["text"]!.GetValue<string>());
        Assert.NotNull(append["requests"]![0]!["insertText"]!["endOfSegmentLocation"]); // at the end: nothing already there is overwritten
    }

    [Fact]
    public async Task A_document_Vernier_did_not_create_is_explained_not_retried()
    {
        var docs = new GoogleDocsClient(new HttpClient(new Recorder((_, _) => (HttpStatusCode.Forbidden, """{"error":{}}"""))), _ => Task.FromResult("t"));
        var e = await Assert.ThrowsAsync<ConnectorException>(() => docs.ReadTextAsync("doc_123456789012", default));
        Assert.Contains("only open documents it created", e.Message);
    }

    [Theory]
    [InlineData("https://docs.google.com/document/d/1AbC-dEf_ghIJklmnOP/edit#heading=h.1", "1AbC-dEf_ghIJklmnOP")]
    [InlineData("1AbC-dEf_ghIJklmnOP", "1AbC-dEf_ghIJklmnOP")]
    public void Document_ids_come_from_ids_or_links(string input, string id) => Assert.Equal(id, GoogleDocsClient.Id(input));

    [Fact]
    public void A_path_in_place_of_an_id_is_refused() => Assert.Throws<ConnectorException>(() => GoogleDocsClient.Id("../../drive/v3/files"));
}
