using TradingBot.Domain;
using TradingBot.Infrastructure.Toss.Dto;

namespace TradingBot.Infrastructure.Toss.Http;

/// <summary>Read-only prices, candles, US market calendar. No order endpoints.</summary>
public sealed class LiveTossMarketDataClient : ITossMarketDataClient
{
    /// <summary>OpenAPI enum for GET /api/v1/candles interval.</summary>
    public static readonly HashSet<string> AllowedCandleIntervals =
        new(StringComparer.Ordinal) { "1m", "1d" };

    private const int MaxCandleCount = 200;

    private readonly HttpClient _http;
    private readonly TossOptions _options;
    private readonly LiveTossAuthClient _auth;

    public LiveTossMarketDataClient(HttpClient http, TossOptions options, LiveTossAuthClient auth)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
    }

    public async Task<IReadOnlyList<QuoteSnapshot>> GetPricesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        LiveHttpGuard.EnsureAllowed(_options);
        if (symbols.Count == 0)
        {
            return Array.Empty<QuoteSnapshot>();
        }

        var joined = string.Join(",", symbols.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
        var uri = new Uri($"api/v1/prices?symbols={Uri.EscapeDataString(joined)}", UriKind.Relative);
        var dto = await GetJsonAsync<PricesResponseDto>(uri, cancellationToken).ConfigureAwait(false);
        return TossDtoMapper.MapPrices(dto);
    }

    public async Task<UsMarketSessionSnapshot> GetUsMarketCalendarAsync(
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        LiveHttpGuard.EnsureAllowed(_options);
        var path = date is null
            ? "api/v1/market-calendar/US"
            : $"api/v1/market-calendar/US?date={date.Value:yyyy-MM-dd}";
        var dto = await GetJsonAsync<UsMarketCalendarResponseDto>(
                new Uri(path, UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);
        return TossDtoMapper.MapUsCalendar(dto);
    }

    public async Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(interval) || !AllowedCandleIntervals.Contains(interval))
        {
            throw new ArgumentException(
                "Candle interval must be one of: 1m, 1d (Toss OpenAPI enum).",
                nameof(interval));
        }

        LiveHttpGuard.EnsureAllowed(_options);
        var clamped = Math.Clamp(count, 1, MaxCandleCount);
        var path =
            $"api/v1/candles?symbol={Uri.EscapeDataString(symbol.Trim())}" +
            $"&interval={Uri.EscapeDataString(interval)}" +
            $"&count={clamped}";
        var dto = await GetJsonAsync<CandlesResponseDto>(
                new Uri(path, UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);
        return TossDtoMapper.MapCandles(dto);
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
}
