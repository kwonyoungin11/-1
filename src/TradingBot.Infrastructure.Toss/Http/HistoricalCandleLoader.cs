using TradingBot.Domain;
using TradingBot.Infrastructure.Toss.Dto;

namespace TradingBot.Infrastructure.Toss.Http;

/// <summary>
/// Deep historical candle fetch for offline backtests.
/// Pages Toss <c>GET /api/v1/candles</c> with a high page cap (up to 300),
/// delay between pages, and merge-by-time. Does not change production
/// <see cref="LiveTossMarketDataClient"/> clamp of 5 pages.
/// Never logs secrets or tokens. Simulation data path only — not live orders.
/// </summary>
public sealed class HistoricalCandleLoader
{
    public const int DefaultMaxPages = 300;
    public const int DefaultCountPerPage = 200;
    public const int DefaultDelayMsMin = 150;
    public const int DefaultDelayMsMax = 250;
    public const int AbsoluteMaxPages = 300;
    private const int MaxCandleCount = 200;

    private readonly HttpClient _http;
    private readonly TossOptions _options;
    private readonly LiveTossAuthClient _auth;
    private readonly int _delayMsMin;
    private readonly int _delayMsMax;
    private readonly Random _rng;

    public HistoricalCandleLoader(
        HttpClient http,
        TossOptions options,
        LiveTossAuthClient auth,
        int delayMsMin = DefaultDelayMsMin,
        int delayMsMax = DefaultDelayMsMax,
        Random? rng = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        if (delayMsMin < 0 || delayMsMax < delayMsMin)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayMsMin),
                "Delay range must be non-negative and min <= max.");
        }

        _delayMsMin = delayMsMin;
        _delayMsMax = delayMsMax;
        _rng = rng ?? Random.Shared;
    }

    /// <summary>
    /// Builds a loader from env/.env when <see cref="TossOptions.AllowLiveHttp"/> and credentials exist.
    /// Returns null when live HTTP is blocked or credentials are missing (caller should use cache).
    /// </summary>
    public static HistoricalCandleLoader? TryCreateFromEnvironment(string? repoRoot = null)
    {
        repoRoot ??= TossReadOnlyFactory.ResolveRepoRoot();
        var env = EnvFile.LoadMergedWithProcess(repoRoot);
        var options = TossOptions.FromEnvironment(env);
        if (!options.AllowLiveHttp || !options.HasClientCredentials)
        {
            return null;
        }

        var baseUri = new Uri(EnsureTrailingSlash(options.BaseUrl));
        var http = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(60),
        };
        var auth = new LiveTossAuthClient(http, options);
        return new HistoricalCandleLoader(http, options, auth);
    }

    /// <summary>
    /// Page through candle history until <paramref name="targetBars"/>, empty page,
    /// null nextBefore, or <paramref name="maxPages"/>.
    /// </summary>
    public async Task<IReadOnlyList<CandlePoint>> LoadAsync(
        string symbol,
        string interval,
        int targetBars,
        int maxPages = DefaultMaxPages,
        int countPerPage = DefaultCountPerPage,
        IProgress<HistoricalCandleProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(interval)
            || !LiveTossMarketDataClient.AllowedCandleIntervals.Contains(interval))
        {
            throw new ArgumentException(
                "Candle interval must be one of: 1m, 1d (Toss OpenAPI enum).",
                nameof(interval));
        }

        LiveHttpGuard.EnsureAllowed(_options);

        var pageSize = Math.Clamp(countPerPage, 1, MaxCandleCount);
        var pages = Math.Clamp(maxPages, 1, AbsoluteMaxPages);
        var goal = Math.Clamp(targetBars, 1, pages * MaxCandleCount);

        var all = new List<CandlePoint>(Math.Min(goal, 4096));
        string? before = null;

        for (var page = 0; page < pages && all.Count < goal; page++)
        {
            if (page > 0)
            {
                var delay = _delayMsMin == _delayMsMax
                    ? _delayMsMin
                    : _rng.Next(_delayMsMin, _delayMsMax + 1);
                if (delay > 0)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            var path =
                $"api/v1/candles?symbol={Uri.EscapeDataString(symbol.Trim())}" +
                $"&interval={Uri.EscapeDataString(interval)}" +
                $"&count={pageSize}";
            if (!string.IsNullOrWhiteSpace(before))
            {
                path += $"&before={Uri.EscapeDataString(before)}";
            }

            CandlesResponseDto dto;
            try
            {
                dto = await GetJsonAsync<CandlesResponseDto>(
                        new Uri(path, UriKind.Relative),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException) when (all.Count > 0)
            {
                // Partial history on rate-limit / network after first page.
                progress?.Report(new HistoricalCandleProgress(page + 1, all.Count, before, Truncated: true));
                break;
            }

            var mapped = TossDtoMapper.MapCandlePage(dto);
            if (mapped.Candles.Count == 0)
            {
                progress?.Report(new HistoricalCandleProgress(page + 1, all.Count, before, Truncated: false));
                break;
            }

            all = LiveTossMarketDataClient.MergeCandlesByTime(mapped.Candles, all);
            progress?.Report(new HistoricalCandleProgress(page + 1, all.Count, mapped.NextBefore, Truncated: false));

            if (string.IsNullOrWhiteSpace(mapped.NextBefore))
            {
                break;
            }

            before = mapped.NextBefore;
        }

        if (all.Count > goal)
        {
            all = all.Skip(all.Count - goal).ToList();
        }

        return all;
    }

    private async Task<T> GetJsonAsync<T>(Uri relative, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relative);
        await _auth.ApplyBearerAsync(request, cancellationToken).ConfigureAwait(false);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Toss GET {relative} failed HTTP {(int)response.StatusCode} (body redacted).");
        }

        return TossJson.DeserializeRequired<T>(body);
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
}

/// <summary>Progress snapshot while paging historical candles (no secrets).</summary>
public sealed record HistoricalCandleProgress(
    int PagesFetched,
    int BarsSoFar,
    string? NextBefore,
    bool Truncated);
