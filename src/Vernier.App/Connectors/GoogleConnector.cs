using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Vernier.Core.Connectors;
using Vernier.Core.Security;

namespace Vernier.App.Connectors;

/// <summary>
/// Google Docs for this learner: sign-in in their own browser to a loopback redirect, the refresh
/// token kept in Windows Credential Manager (never in settings or logs), access tokens in memory.
/// Uses an OAuth client the learner (or their school) registers; Vernier ships none.
/// </summary>
public sealed class GoogleConnector(ISecretStore secrets, HttpClient http)
{
    public const string ClientSecretName = "vernier-google-client-secret", RefreshName = "vernier-google-refresh";
    public static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);
    private GoogleOAuth.Tokens? _tokens;

    public bool IsConnected => !string.IsNullOrWhiteSpace(AppServices.Settings.GoogleClientId) && secrets.Get(RefreshName) is not null;

    public async Task ConnectAsync(string clientId, CancellationToken ct)
    {
        var (listener, redirect) = Listen();
        using var listening = listener;
        var pkce = GoogleOAuth.NewPkce();
        Process.Start(new ProcessStartInfo(GoogleOAuth.AuthorizeUrl(clientId, redirect, pkce)) { UseShellExecute = true })?.Dispose();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SignInTimeout);
        HttpListenerContext reply;
        while (true)
        {
            var pending = listener.GetContextAsync();
            _ = pending.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted); // faults when the listener closes
            try { reply = await pending.WaitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ConnectorException("Sign-in was not finished in the browser within five minutes."); }
            if (GoogleOAuth.IsForThisSignIn(reply.Request.Url!, pkce)) break;
            // Anything else on this port (a favicon request, another local program) is not the reply; keep waiting.
            reply.Response.StatusCode = 404;
            reply.Response.Close();
        }

        GoogleOAuth.Tokens tokens;
        try
        {
            var code = GoogleOAuth.CodeFrom(reply.Request.Url!, pkce);
            tokens = await GoogleOAuth.ExchangeAsync(http, clientId, secrets.Get(ClientSecretName), code, pkce.Verifier, redirect, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            if (tokens.RefreshToken is null) throw new ConnectorException("Google did not grant lasting access. Remove Vernier under your Google account's third-party access, then connect again.");
        }
        catch (Exception e) when (e is ConnectorException or HttpRequestException)
        {
            await Page(reply, "Vernier did not connect: " + e.Message).ConfigureAwait(false);
            throw;
        }
        secrets.Set(RefreshName, tokens.RefreshToken);
        _tokens = tokens;
        await Page(reply, "Vernier is connected to Google Docs. You can close this tab.").ConfigureAwait(false);
    }

    // A free loopback port, bound by the listener itself; another program taking the port in between just means another try.
    private static (HttpListener Listener, string Redirect) Listen()
    {
        for (var attempt = 0; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var redirect = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(redirect);
            try
            {
                listener.Start();
                return (listener, redirect);
            }
            catch (HttpListenerException) when (attempt < 4) { listener.Close(); }
        }
    }

    private readonly SemaphoreSlim _refreshing = new(1, 1);

    public async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        await _refreshing.WaitAsync(ct).ConfigureAwait(false); // one refresh at a time; the others use its result
        try
        {
            if (_tokens is { } t && t.ExpiresAt > DateTimeOffset.UtcNow) return t.AccessToken;
            var refresh = secrets.Get(RefreshName) ?? throw new ConnectorException("Google Docs is not connected (Settings › Connectors).");
            var clientId = AppServices.Settings.GoogleClientId ?? throw new ConnectorException("No Google OAuth client id is set (Settings › Connectors).");
            _tokens = await GoogleOAuth.RefreshAsync(http, clientId, secrets.Get(ClientSecretName), refresh, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            if (_tokens.RefreshToken is { } rotated && rotated != refresh) secrets.Set(RefreshName, rotated);
            return _tokens.AccessToken;
        }
        finally { _refreshing.Release(); }
    }

    /// <summary>Forgets the sign-in here and asks Google to revoke it. Local removal happens even if Google cannot be reached.</summary>
    public async Task DisconnectAsync()
    {
        var refresh = secrets.Get(RefreshName);
        secrets.Delete(RefreshName);
        _tokens = null;
        if (refresh is null) return;
        try
        {
            using var content = new FormUrlEncodedContent([new("token", refresh)]);
            using var response = await http.PostAsync(GoogleOAuth.RevokeEndpoint, content).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) AppServices.Log.Warn($"Google answered {(int)response.StatusCode} to revoking the sign-in; it was removed from this computer.");
        }
        catch (Exception e) when (e is HttpRequestException or TimeoutException or TaskCanceledException)
        {
            AppServices.Log.Warn("Google did not confirm revoking the sign-in; it was removed from this computer. " + e.GetType().Name);
        }
    }

    public GoogleDocsClient Docs() => new(http, AccessTokenAsync);

    private static async Task Page(HttpListenerContext context, string message)
    {
        var html = Encoding.UTF8.GetBytes($"<!doctype html><meta charset=utf-8><title>Vernier</title><body style=\"font:16px Segoe UI,sans-serif;margin:3em\">{WebUtility.HtmlEncode(message)}</body>");
        context.Response.ContentType = "text/html; charset=utf-8";
        try { await context.Response.OutputStream.WriteAsync(html).ConfigureAwait(false); }
        catch (Exception e) when (e is HttpListenerException or IOException) { AppServices.Log.Warn("The browser closed before the sign-in page was sent: " + e.GetType().Name); }
        finally { context.Response.Close(); }
    }
}
