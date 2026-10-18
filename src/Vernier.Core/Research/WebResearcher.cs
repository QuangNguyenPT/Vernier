using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Vernier.Core.Research;

public sealed record WebHit(string Title, Uri Url);

/// <summary>Search results for a query. One real backend (DuckDuckGo's HTML page) and a fake in the tests.</summary>
public interface ISearchBackend
{
    /// <exception cref="SearchUnavailableException">The search engine refused or changed; not the same as finding nothing.</exception>
    Task<IReadOnlyList<WebHit>> SearchAsync(string query, CancellationToken ct);
}

public sealed class SearchUnavailableException(string message) : Exception(message);

/// <summary>A page that loaded: its title and readable text.</summary>
public sealed record WebPage(Uri Url, string Title, string Text);

/// <summary>Fetches pages from the public web only. Implemented by <see cref="SafeWeb"/>; faked in the tests.</summary>
public interface IWebFetcher
{
    /// <returns>The page, or the reason it does not count (shown to the model as a dead source).</returns>
    Task<(WebPage? Page, string? Dead)> FetchAsync(Uri url, CancellationToken ct);
}

/// <param name="Sources">Pages that loaded with readable text, with the paragraphs that best match the query.</param>
/// <param name="Dead">Pages that did not count, with why: they are not sources.</param>
public sealed record ResearchResult(string Query, IReadOnlyList<(WebPage Page, string Excerpt)> Sources, IReadOnlyList<string> Dead, string? Problem);

/// <summary>
/// One research step: search, then read candidate pages in parallel until enough of them actually load with readable text.
/// A dead link, an error page or a page with nothing to read is not a source. Sources are kept from different sites.
/// </summary>
public sealed class WebResearcher(ISearchBackend search, IWebFetcher web)
{
    public const int Candidates = 8, KeepAtMost = 5, Parallel = 4, ExcerptChars = 1200;
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(15);

    public async Task<ResearchResult> ResearchAsync(string query, CancellationToken ct)
    {
        IReadOnlyList<WebHit> hits;
        try { hits = await search.SearchAsync(query, ct).ConfigureAwait(false); }
        catch (SearchUnavailableException e) { return new ResearchResult(query, [], [], "the search engine did not answer (" + e.Message + ")"); }
        catch (HttpRequestException e) { return new ResearchResult(query, [], [], "search failed: " + e.Message); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new ResearchResult(query, [], [], "search timed out"); }

        // At most two pages from one site, so three sources are three views.
        var candidates = hits.GroupBy(h => h.Url.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase)).SelectMany(g => g.Take(2)).Take(Candidates).ToList();
        if (candidates.Count == 0) return new ResearchResult(query, [], [], "the search found nothing; try other words");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(Budget);
        using var gate = new SemaphoreSlim(Parallel);
        var sources = new List<(WebPage, string)>();
        var dead = new List<string>();
        var tasks = candidates.Select(async hit =>
        {
            await gate.WaitAsync(budget.Token).ConfigureAwait(false);
            try
            {
                lock (sources) if (sources.Count >= KeepAtMost) return;
                var (page, why) = await web.FetchAsync(hit.Url, budget.Token).ConfigureAwait(false);
                lock (sources)
                {
                    if (page is not null && sources.Count < KeepAtMost) sources.Add((page, Excerpt(page.Text, query)));
                    else if (page is null) dead.Add($"{hit.Url.Host} ({why})");
                }
            }
            finally { gate.Release(); }
        }).ToList();
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { lock (sources) dead.Add("the rest did not load in time"); }
        return new ResearchResult(query, [.. sources], [.. dead], null);
    }

    /// <summary>The paragraphs of a page that share the most words with the query, in page order, up to <see cref="ExcerptChars"/>.</summary>
    public static string Excerpt(string text, string query)
    {
        var terms = query.ToLowerInvariant().Split([' ', ',', '.', '?', ':', ';', '"', '\''], StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3).Distinct().ToList();
        var paragraphs = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(p => p.Length >= 40).ToList();
        var ranked = paragraphs.Select((p, i) => (p, i, hits: terms.Count(t => p.Contains(t, StringComparison.OrdinalIgnoreCase)))).OrderByDescending(x => x.hits).ThenBy(x => x.i).ToList();
        var chosen = new List<(string P, int I)>();
        var length = 0;
        foreach (var (p, i, _) in ranked)
        {
            if (length > 0 && length + p.Length > ExcerptChars) continue;
            chosen.Add((p.Length > ExcerptChars ? p[..ExcerptChars] + "…" : p, i));
            length += p.Length;
            if (length >= ExcerptChars) break;
        }
        return string.Join("\n", chosen.OrderBy(c => c.I).Select(c => c.P));
    }
}

/// <summary>DuckDuckGo's plain HTML results page (no key). Its markup can change: no results with a challenge page reads as unavailable, not as nothing found.</summary>
public sealed partial class DuckDuckGoSearch(SafeWeb web) : ISearchBackend
{
    [GeneratedRegex(@"<a\b[^>]*\bclass=""[^""]*\bresult__a\b[^""]*""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex ResultLink();
    [GeneratedRegex(@"\bhref=""([^""]+)""", RegexOptions.IgnoreCase)] private static partial Regex Href();

    public async Task<IReadOnlyList<WebHit>> SearchAsync(string query, CancellationToken ct)
    {
        var url = new Uri("https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query));
        var (status, html) = await web.GetTextAsync(url, ct).ConfigureAwait(false);
        if (status == HttpStatusCode.Accepted || html is null) throw new SearchUnavailableException(status == HttpStatusCode.Accepted ? "it asked for a check that a program cannot pass" : $"HTTP {(int)status}");
        var hits = Parse(html);
        if (hits.Count == 0 && (html.Contains("anomaly", StringComparison.OrdinalIgnoreCase) || html.Contains("captcha", StringComparison.OrdinalIgnoreCase)))
            throw new SearchUnavailableException("it asked for a check that a program cannot pass");
        return hits;
    }

    /// <summary>Result links from the results page; DuckDuckGo wraps each in a redirect whose uddg parameter is the real address. Ads are skipped.</summary>
    public static IReadOnlyList<WebHit> Parse(string html)
    {
        var hits = new List<WebHit>();
        foreach (Match m in ResultLink().Matches(html))
        {
            if (Href().Match(m.Value) is not { Success: true } href) continue;
            var raw = WebUtility.HtmlDecode(href.Groups[1].Value);
            if (raw.StartsWith("//", StringComparison.Ordinal)) raw = "https:" + raw;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var link)) continue;
            if (link.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase))
            {
                var uddg = link.Query.TrimStart('?').Split('&').FirstOrDefault(p => p.StartsWith("uddg=", StringComparison.Ordinal));
                if (uddg is null || !Uri.TryCreate(Uri.UnescapeDataString(uddg[5..]), UriKind.Absolute, out link)) continue; // an ad, or something else of theirs
            }
            if (link.Scheme is not ("http" or "https")) continue;
            var title = WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", "")).Trim();
            if (hits.All(h => h.Url != link)) hits.Add(new WebHit(title, link));
        }
        return hits;
    }
}

/// <summary>
/// The only way Vernier reaches the web. Public addresses only: every connection is made to an address checked at connect
/// time (so a name that resolves to this computer or the local network is refused, and DNS cannot be switched after the
/// check), every redirect is checked again, no cookies, no proxy, size and time caps.
/// </summary>
public sealed partial class SafeWeb : IWebFetcher, IDisposable
{
    public const int MaxBytes = 1_500_000, MaxRedirects = 3, MinReadableChars = 400;
    private readonly HttpClient _http;

    public SafeWeb(Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
    {
        resolve ??= (host, ct) => Dns.GetHostAddressesAsync(host, ct);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false, AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5), PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, ct) =>
            {
                var addresses = await resolve(context.DnsEndPoint.Host, ct).ConfigureAwait(false);
                var address = addresses.FirstOrDefault(IsPublic) ?? throw new HttpRequestException($"{context.DnsEndPoint.Host} is not a public address");
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                var connected = false;
                try
                {
                    await socket.ConnectAsync(address, context.DnsEndPoint.Port, ct).ConfigureAwait(false);
                    connected = true;
                    return new NetworkStream(socket, ownsSocket: true);
                }
                finally
                {
                    if (!connected) socket.Dispose();
                }
            },
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Vernier/0.4 (study tutor)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain;q=0.9");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en;q=0.9");
    }

    /// <summary>Public unicast only: not this computer, the local network, link-local, carrier NAT, multicast or reserved space (IPv4 inside IPv6 too).</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                     (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127) || (b[0] == 192 && b[1] == 0 && b[2] == 0) || (b[0] == 198 && b[1] is 18 or 19));
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast ||
                     (b[0] & 0xFE) == 0xFC || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8));
        }
        return false;
    }

    /// <summary>A text resource with redirects followed (each checked again). Null text for anything that is not text.</summary>
    public async Task<(HttpStatusCode Status, string? Text)> GetTextAsync(Uri url, CancellationToken ct)
    {
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (url.Scheme is not ("http" or "https")) return (HttpStatusCode.BadRequest, null);
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } next)
            {
                url = next.IsAbsoluteUri ? next : new Uri(url, next);
                continue;
            }
            if (!response.IsSuccessStatusCode) return (response.StatusCode, null);
            if (response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml" or "text/plain")) return (HttpStatusCode.UnsupportedMediaType, null);
            if (response.Content.Headers.ContentLength > MaxBytes) return (HttpStatusCode.RequestEntityTooLarge, null);
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var buffer = new byte[MaxBytes];
            var read = 0;
            int n;
            while (read < MaxBytes && (n = await stream.ReadAsync(buffer.AsMemory(read, MaxBytes - read), ct).ConfigureAwait(false)) > 0) read += n;
            var charset = response.Content.Headers.ContentType?.CharSet;
            Encoding encoding;
            try { encoding = charset is { Length: > 0 } ? Encoding.GetEncoding(charset.Trim('"')) : Encoding.UTF8; }
            catch (ArgumentException) { encoding = Encoding.UTF8; }
            return (response.StatusCode, encoding.GetString(buffer, 0, read));
        }
        return (HttpStatusCode.LoopDetected, null);
    }

    public async Task<(WebPage? Page, string? Dead)> FetchAsync(Uri url, CancellationToken ct)
    {
        try
        {
            var (status, html) = await GetTextAsync(url, ct).ConfigureAwait(false);
            if (html is null) return (null, status == HttpStatusCode.UnsupportedMediaType ? "not a web page" : $"HTTP {(int)status}");
            var (title, text) = Readable.Extract(html);
            return text.Length < MinReadableChars ? (null, "nothing to read") : (new WebPage(url, title, text), null);
        }
        catch (HttpRequestException e) { return (null, e.Message.Contains("public", StringComparison.Ordinal) ? "not a public address" : "did not load"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return (null, "timed out"); }
        catch (IOException) { return (null, "connection dropped"); }
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>The readable text of an HTML page: no scripts, styles, navigation, headers, footers or forms; paragraphs on their own lines.</summary>
public static partial class Readable
{
    [GeneratedRegex(@"<(script|style|noscript|svg|nav|header|footer|aside|form|template|iframe)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex Noise();
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)] private static partial Regex Comment();
    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex Title();
    [GeneratedRegex(@"</?(p|div|br|li|ul|ol|h[1-6]|tr|table|section|article|blockquote|pre|dd|dt)\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Block();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex Tag();
    [GeneratedRegex(@"[ \t\f\v ]+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\n\s*\n+")] private static partial Regex Lines();

    public static (string Title, string Text) Extract(string html)
    {
        var title = Title().Match(html) is { Success: true } t ? WebUtility.HtmlDecode(Tag().Replace(t.Groups[1].Value, "")).Trim() : "";
        var s = Comment().Replace(html, " ");
        s = Noise().Replace(s, " ");
        s = Block().Replace(s, "\n");
        s = Tag().Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        s = Spaces().Replace(s, " ");
        s = string.Join("\n", s.Split('\n').Select(l => l.Trim()));
        s = Lines().Replace(s, "\n").Trim();
        return (title.Length > 160 ? title[..160] : title, s);
    }
}
