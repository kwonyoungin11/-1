using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingBot.Domain;

namespace TradingBot.Infrastructure.Toss.News;

/// <summary>
/// SpaceX/SPCX news: Finnhub company-news when FINNHUB_API_KEY is set;
/// otherwise curated mock rotating headlines (always works offline).
/// Display/gate only — never auto-buys. Not investment advice.
/// </summary>
public sealed class CompositeSpaceXNewsFeed : INewsFeed
{
    private readonly HttpClient? _http;
    private readonly string? _finnhubKey;
    private static int _mockTick;

    public CompositeSpaceXNewsFeed(HttpClient? http = null, string? finnhubApiKey = null)
    {
        _http = http;
        _finnhubKey = string.IsNullOrWhiteSpace(finnhubApiKey) ? null : finnhubApiKey.Trim();
    }

    public static CompositeSpaceXNewsFeed FromEnvironment()
    {
        var env = EnvFile.LoadMergedWithProcess(FindRepoRoot());
        env.TryGetValue("FINNHUB_API_KEY", out var key);
        if (string.IsNullOrWhiteSpace(key))
        {
            env.TryGetValue("TOSS_NEWS_API_KEY", out key); // optional alias, unused by Toss
        }

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        return new CompositeSpaceXNewsFeed(http, key);
    }

    public async Task<IReadOnlyList<NewsHeadline>> GetHeadlinesAsync(
        string symbol,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var sym = string.IsNullOrWhiteSpace(symbol) ? WatchlistCatalog.SpaceXSymbol : symbol.Trim().ToUpperInvariant();
        var take = Math.Clamp(maxCount, 1, 30);

        if (_http is not null && !string.IsNullOrWhiteSpace(_finnhubKey))
        {
            try
            {
                var live = await FetchFinnhubAsync(sym, take, cancellationToken).ConfigureAwait(false);
                if (live.Count > 0)
                {
                    return live;
                }
            }
            catch
            {
                // Fall through to mock — never crash cockpit for news.
            }
        }

        return BuildMockHeadlines(sym, take);
    }

    private async Task<IReadOnlyList<NewsHeadline>> FetchFinnhubAsync(
        string symbol,
        int take,
        CancellationToken cancellationToken)
    {
        // Finnhub free company-news: uses ticker; SPCX may or may not be listed.
        var to = DateTime.UtcNow.Date;
        var from = to.AddDays(-7);
        var url =
            $"https://finnhub.io/api/v1/company-news?symbol={Uri.EscapeDataString(symbol)}" +
            $"&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&token={Uri.EscapeDataString(_finnhubKey!)}";

        using var resp = await _http!.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            // try SpaceX common aliases
            return Array.Empty<NewsHeadline>();
        }

        var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var items = JsonSerializer.Deserialize<List<FinnhubNewsDto>>(json) ?? new List<FinnhubNewsDto>();
        return items
            .Where(i => !string.IsNullOrWhiteSpace(i.Headline))
            .OrderByDescending(i => i.Datetime)
            .Take(take)
            .Select(i =>
            {
                var published = DateTimeOffset.FromUnixTimeSeconds(i.Datetime);
                var title = i.Headline!.Trim();
                return new NewsHeadline(
                    Id: $"fh-{i.Id ?? i.Datetime}",
                    Title: title,
                    Source: string.IsNullOrWhiteSpace(i.Source) ? "Finnhub" : i.Source!,
                    PublishedAtUtc: published,
                    Url: i.Url,
                    IsMaterialEvent: LooksMaterial(title),
                    SymbolHint: symbol);
            })
            .ToList();
    }

    public static IReadOnlyList<NewsHeadline> BuildMockHeadlines(string symbol, int take)
    {
        var tick = Interlocked.Increment(ref _mockTick);
        var now = DateTimeOffset.UtcNow;
        var templates = new (string Title, string Source, bool Material)[]
        {
            ($"{symbol}: 시장 변동성 주시 — 포스트 IPO 구간 (모의)", "MockWire", false),
            ($"SpaceX/Starlink 관련 관측 헤드라인 샘플 #{tick} (모의)", "MockWire", false),
            ($"{symbol} 거래량·스프레드 모니터링 (모의 피드)", "MockDesk", false),
            ("락업·지수 이벤트 캘린더는 수동 확인 권장 (모의)", "MockDesk", true),
            ($"{symbol}: 자동매매는 뉴스 감성 매수 금지 — 차단/축소만 (모의)", "Policy", true),
            ("규제·경고 플래그는 토스 warnings API로 별도 확인 (모의)", "MockReg", true),
        };

        return templates
            .Take(take)
            .Select((t, i) => new NewsHeadline(
                Id: $"mock-{tick}-{i}",
                Title: t.Title,
                Source: t.Source,
                PublishedAtUtc: now.AddMinutes(-(i * 7 + tick % 5)),
                Url: null,
                IsMaterialEvent: t.Material,
                SymbolHint: symbol))
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
            "락업", "거래정지", "공시", "경고", "소송", "FDA", "CONTRACT", "LAUNCH",
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
