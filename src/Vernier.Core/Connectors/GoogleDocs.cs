using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vernier.Core.Connectors;

public sealed class ConnectorException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// OAuth 2.0 for an installed app (RFC 8252): the learner signs in to Google in their own browser,
/// Google redirects to a loopback address with a one-time code, and the code is exchanged with a
/// PKCE verifier (RFC 7636) so an intercepted code is useless. Vernier never sees the password.
/// The only scope is drive.file: Vernier can open the documents it created, and nothing else.
/// </summary>
public static class GoogleOAuth
{
    public const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";
    public const string Scope = "https://www.googleapis.com/auth/drive.file";

    public sealed record Pkce(string Verifier, string Challenge, string State);
    public sealed record Tokens(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);

    public static Pkce NewPkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        return new Pkce(verifier, ChallengeFor(verifier), Base64Url(RandomNumberGenerator.GetBytes(16)));
    }

    /// <summary>S256: the base64url SHA-256 of the verifier.</summary>
    public static string ChallengeFor(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string AuthorizeUrl(string clientId, string redirectUri, Pkce pkce) =>
        AuthorizeEndpoint + "?" + Query(new()
        {
            ["client_id"] = clientId, ["redirect_uri"] = redirectUri, ["response_type"] = "code", ["scope"] = Scope,
            ["code_challenge"] = pkce.Challenge, ["code_challenge_method"] = "S256", ["state"] = pkce.State,
            ["access_type"] = "offline", ["prompt"] = "consent",
        });

    /// <summary>True when a request to the loopback address carries this sign-in's state (anything else is ignored).</summary>
    public static bool IsForThisSignIn(Uri redirect, Pkce pkce) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(System.Web.HttpUtility.ParseQueryString(redirect.Query)["state"] ?? ""), Encoding.ASCII.GetBytes(pkce.State));

    /// <summary>The code from the redirect, after checking it answers this sign-in (state) and is not an error.</summary>
    public static string CodeFrom(Uri redirect, Pkce pkce)
    {
        var query = System.Web.HttpUtility.ParseQueryString(redirect.Query);
        if (query["error"] is { } error) throw new ConnectorException(error == "access_denied" ? "Sign-in was cancelled in the browser." : $"Google refused the sign-in ({error}).");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(query["state"] ?? ""), Encoding.ASCII.GetBytes(pkce.State)))
            throw new ConnectorException("The sign-in reply did not match this sign-in, so it was ignored.");
        return query["code"] is { Length: > 0 } code ? code : throw new ConnectorException("The sign-in reply had no code.");
    }

    public static Task<Tokens> ExchangeAsync(HttpClient http, string clientId, string? clientSecret, string code, string verifier, string redirectUri, DateTimeOffset now, CancellationToken ct) =>
        PostTokenAsync(http, new() { ["client_id"] = clientId, ["client_secret"] = clientSecret, ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirectUri, ["grant_type"] = "authorization_code" }, null, now, ct);

    public static Task<Tokens> RefreshAsync(HttpClient http, string clientId, string? clientSecret, string refreshToken, DateTimeOffset now, CancellationToken ct) =>
        PostTokenAsync(http, new() { ["client_id"] = clientId, ["client_secret"] = clientSecret, ["refresh_token"] = refreshToken, ["grant_type"] = "refresh_token" }, refreshToken, now, ct);

    private static async Task<Tokens> PostTokenAsync(HttpClient http, Dictionary<string, string?> form, string? keepRefresh, DateTimeOffset now, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form.Where(kv => kv.Value is not null).Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!)));
        using var response = await http.PostAsync(TokenEndpoint, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        JsonNode? json;
        try { json = JsonNode.Parse(body); }
        catch (JsonException) { throw new ConnectorException($"Google's sign-in service answered {(int)response.StatusCode} with something that was not JSON.", response.StatusCode); }
        if (!response.IsSuccessStatusCode || json?["access_token"]?.GetValue<string>() is not { Length: > 0 } access)
        {
            var error = json?["error"]?.GetValue<string>() ?? ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            throw new ConnectorException(error == "invalid_grant" ? "The Google sign-in has expired or was revoked. Connect again." : $"Google sign-in failed ({error}).", response.StatusCode);
        }
        var seconds = json["expires_in"]?.GetValue<int>() ?? 3600;
        return new Tokens(access, json["refresh_token"]?.GetValue<string>() ?? keepRefresh, now.AddSeconds(seconds - 60));
    }

    private static string Query(Dictionary<string, string> values) => string.Join("&", values.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>The Google Docs calls Vernier makes: create a document, read its text, add text at its end.</summary>
public sealed class GoogleDocsClient(HttpClient http, Func<CancellationToken, Task<string>> accessToken)
{
    public const string Api = "https://docs.googleapis.com/v1/documents";
    public const int MaxAppend = 20000;

    public async Task<string> CreateAsync(string title, CancellationToken ct)
    {
        var json = await SendAsync(HttpMethod.Post, Api, new JsonObject { ["title"] = title }, ct).ConfigureAwait(false);
        return json?["documentId"]?.GetValue<string>() ?? throw new ConnectorException("Google Docs did not return the new document's id.");
    }

    /// <summary>The document's text, paragraph by paragraph (tables and other elements contribute their text runs).</summary>
    public async Task<string> ReadTextAsync(string documentId, CancellationToken ct)
    {
        var json = await SendAsync(HttpMethod.Get, $"{Api}/{Id(documentId)}", null, ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o["textRun"]?["content"]?.GetValue<string>() is { } text) sb.Append(text);
                    foreach (var (_, child) in o) Walk(child);
                    break;
                case JsonArray a:
                    foreach (var child in a) Walk(child);
                    break;
            }
        }
        Walk(json?["body"]?["content"]);
        return sb.ToString();
    }

    public async Task AppendAsync(string documentId, string text, CancellationToken ct)
    {
        if (text.Length > MaxAppend) throw new ConnectorException($"That is longer than {MaxAppend} characters; nothing was added.");
        var request = new JsonObject
        {
            ["requests"] = new JsonArray(new JsonObject
            {
                ["insertText"] = new JsonObject { ["text"] = "\n" + text.TrimEnd() + "\n", ["endOfSegmentLocation"] = new JsonObject() },
            }),
        };
        await SendAsync(HttpMethod.Post, $"{Api}/{Id(documentId)}:batchUpdate", request, ct).ConfigureAwait(false);
    }

    /// <summary>A document id, or the id inside a docs.google.com link.</summary>
    public static string Id(string idOrUrl)
    {
        var s = idOrUrl.Trim();
        var at = s.IndexOf("/document/d/", StringComparison.Ordinal);
        if (at >= 0) s = s[(at + "/document/d/".Length)..].Split('/', '?', '#')[0];
        return s.Length is > 10 and < 200 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? s : throw new ConnectorException("That is not a Google Docs document id or link.");
    }

    private async Task<JsonNode?> SendAsync(HttpMethod method, string url, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await accessToken(ct).ConfigureAwait(false));
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new ConnectorException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Google no longer accepts the sign-in. Connect again in Settings.",
                HttpStatusCode.Forbidden or HttpStatusCode.NotFound => "Vernier cannot open that document. It can only open documents it created (that is all the permission it asked for).",
                _ => $"Google Docs answered {(int)response.StatusCode}.",
            }, response.StatusCode);
        try { return text.Length == 0 ? null : JsonNode.Parse(text); }
        catch (JsonException) { throw new ConnectorException("Google Docs answered with something that was not JSON."); }
    }
}
