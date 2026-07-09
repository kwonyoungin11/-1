using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using TradingBot.Domain;

namespace TradingBot.Infrastructure.Toss.News;

/// <summary>
/// Real news for SPCX/SpaceX:
/// 1) Google News RSS (no API key)
/// 2) Finnhub company-news if FINNHUB_API_KEY set
/// No fake "모의" headlines when live fetch fails — returns empty + caller shows status.
/// Display/gate only. Not investment advice.
/// </summary>
public sealed class CompositeSpaceXNewsFeed : INewsFeed
{
    private readonly HttpClient _http;
    private readonly string? _finnhubKey;
    private string _lastSourceNote = "미로드";

    public string LastSourceNote => _lastSourceNote;

    public CompositeSpaceXNewsFeed(HttpClient? http = null, string? finnhubApiKey = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TradingBotCockpit/1.0 (SPCX research; +https://localhost)");
        }

        _finnhubKey = string.IsNullOrWhiteSpace(finnhubApiKey) ? null : finnhubApiKey.Trim();
    }

    public static CompositeSpaceXNewsFeed FromEnvironment()
    {
        var env = EnvFile.LoadMergedWithProcess(FindRepoRoot());
        env.TryGetValue("FINNHUB_API_KEY", out var key);
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TradingBotCockpit/1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return new CompositeSpaceXNewsFeed(http, key);
    }

    public async Task<IReadOnlyList<NewsHeadline>> GetHeadlinesAsync(
        string symbol,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var sym = string.IsNullOrWhiteSpace(symbol)
            ? WatchlistCatalog.SpaceXSymbol
            : symbol.Trim().ToUpperInvariant();
        var take = Math.Clamp(maxCount, 1, 30);
        var errors = new List<string>();

        // 1) Google News RSS — real public headlines, no key
        try
        {
            var rss = await FetchGoogleNewsRssAsync(sym, take, cancellationToken).ConfigureAwait(false);
            if (rss.Count > 0)
            {
                _lastSourceNote = $"Google News RSS · {rss.Count}건 · 실헤드라인";
                return rss;
            }

            errors.Add("Google RSS 0건");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add($"Google RSS:{ex.GetType().Name}");
        }

        // 2) Finnhub optional
        if (!string.IsNullOrWhiteSpace(_finnhubKey))
        {
            try
            {
                var fh = await FetchFinnhubAsync(sym, take, cancellationToken).ConfigureAwait(false);
                if (fh.Count > 0)
                {
                    _lastSourceNote = $"Finnhub · {fh.Count}건 · 실헤드라인";
                    return fh;
                }

                errors.Add("Finnhub 0건");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"Finnhub:{ex.GetType().Name}");
            }
        }
        else
        {
            errors.Add("Finnhub키없음");
        }

        _lastSourceNote = "실뉴스 실패 · " + string.Join(" · ", errors) + " · 모의 없음";
        return Array.Empty<NewsHeadline>();
    }

    private async Task<IReadOnlyList<NewsHeadline>> FetchGoogleNewsRssAsync(
        string symbol,
        int take,
        CancellationToken cancellationToken)
    {
        // Query SpaceX + ticker for US market news
        var q = Uri.EscapeDataString($"{symbol} OR SpaceX OR \"Space Exploration Technologies\"");
        var url =
            $"https://news.google.com/rss/search?q={q}&hl=en-US&gl=US&ceid=US:en";

        using var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Google RSS HTTP {(int)resp.StatusCode}");
        }

        var xml = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        var items = doc.Descendants("item").ToList();
        if (items.Count == 0)
        {
            items = doc.Descendants().Where(e => e.Name.LocalName is "item" or "entry").ToList();
        }

        var list = new List<NewsHeadline>();
        var i = 0;
        foreach (var item in items)
        {
            if (list.Count >= take)
            {
                break;
            }

            var title = item.Element("title")?.Value
                        ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var link = item.Element("link")?.Value
                       ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value
                       ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Value;
            var pub = item.Element("pubDate")?.Value
                      ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "pubDate")?.Value
                      ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "published")?.Value;
            var source = item.Element("source")?.Value
                         ?? "Google News";

            DateTimeOffset published = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(pub)
                && DateTimeOffset.TryParse(pub, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                published = parsed.ToUniversalTime();
            }

            list.Add(new NewsHeadline(
                Id: $"gn-{symbol}-{i}-{published.ToUnixTimeSeconds()}",
                Title: title.Trim(),
                Source: source.Trim(),
                PublishedAtUtc: published,
                Url: string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
                IsMaterialEvent: LooksMaterial(title),
                SymbolHint: symbol));
            i++;
        }

        return list;
    }

    private async Task<IReadOnlyList<NewsHeadline>> FetchFinnhubAsync(
        string symbol,
        int take,
        CancellationToken cancellationToken)
    {
        var to = DateTime.UtcNow.Date;
        var from = to.AddDays(-14);
        var url =
            $"https://finnhub.io/api/v1/company-news?symbol={Uri.EscapeDataString(symbol)}" +
            $"&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&token={Uri.EscapeDataString(_finnhubKey!)}";

        using var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            return Array.Empty<NewsHeadline>();
        }

        var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var items = JsonSerializer.Deserialize<List<FinnhubNewsDto>>(json) ?? new List<FinnhubNewsDto>();
        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.Headline))
            .OrderByDescending(x => x.Datetime)
            .Take(take)
            .Select(x =>
            {
                var published = DateTimeOffset.FromUnixTimeSeconds(x.Datetime);
                var title = x.Headline!.Trim();
                return new NewsHeadline(
                    Id: $"fh-{x.Id ?? x.Datetime}",
                    Title: title,
                    Source: string.IsNullOrWhiteSpace(x.Source) ? "Finnhub" : x.Source!,
                    PublishedAtUtc: published,
                    Url: x.Url,
                    IsMaterialEvent: LooksMaterial(title),
                    SymbolHint: symbol);
            })
            .ToList();
    }

    public static bool LooksMaterial(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var t = title.ToUpperInvariant();
        string[] keys =
        [
            "LOCKUP", "SEC", "HALT", "INVESTIGATION", "WARNING", "VI ",
            "락업", "거래정지", "공시", "경고", "소송", "CONTRACT", "LAUNCH", "IPO",
        ];
        return keys.Any(k => t.Contains(k, StringComparison.Ordinal));
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TradingBot.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed class FinnhubNewsDto
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("headline")]
        public string? Headline { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("datetime")]
        public long Datetime { get; set; }
    }
}
