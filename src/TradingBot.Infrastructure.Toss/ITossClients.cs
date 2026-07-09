using TradingBot.Domain;

namespace TradingBot.Infrastructure.Toss;

public interface ITossAuthClient
{
    Task<TossAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public interface ITossAccountClient
{
    Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(CancellationToken cancellationToken);

    Task<HoldingsReadModel> GetHoldingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Cash-only buying power for the given currency (KRW or USD). Read-only; no orders.
    /// </summary>
    Task<BuyingPowerSnapshot> GetBuyingPowerAsync(string currency, CancellationToken cancellationToken);
}

public interface ITossMarketDataClient
{
    Task<IReadOnlyList<QuoteSnapshot>> GetPricesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken);

    Task<UsMarketSessionSnapshot> GetUsMarketCalendarAsync(
        DateOnly? date,
        CancellationToken cancellationToken);

    /// <summary>
    /// OHLCV candles for chart (read-only). Toss interval enum: <c>1m</c>, <c>1d</c>.
    /// Max count 200 per official OpenAPI. Single page unless using <see cref="GetCandlesPagedAsync"/>.
    /// </summary>
    Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken);

    /// <summary>
    /// Multi-page candles via <c>before</c>/<c>nextBefore</c>. Still only interval 1m|1d.
    /// Stops on empty page, null nextBefore, maxPages, or targetTotal bars.
    /// </summary>
    Task<IReadOnlyList<CandlePoint>> GetCandlesPagedAsync(
        string symbol,
        string interval,
        int countPerPage,
        int maxPages,
        int targetTotal,
        CancellationToken cancellationToken);
}

/// <summary>One candles API page (mapped).</summary>
public sealed record CandlePageResult(
    IReadOnlyList<CandlePoint> Candles,
    string? NextBefore);

public interface ITossOrderClient
{
    bool IsLiveSubmissionEnabled { get; }
}

public interface ITossClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ITossRedactor
{
    string MaskToken(string? token);
    string MaskAccount(string? account);
}

public sealed record TossAccessToken(string AccessToken, string TokenType, long ExpiresInSeconds);

public sealed record HoldingsReadModel(
    string? MarketValueUsd,
    IReadOnlyList<HoldingSummary> Items);

public sealed class BlockedTossOrderClient : ITossOrderClient
{
    public bool IsLiveSubmissionEnabled => false;
}

public sealed class SystemTossClock : ITossClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class DomainTossRedactor : ITossRedactor
{
    public string MaskToken(string? token) => SecretRedactor.MaskToken(token);

    public string MaskAccount(string? account) => SecretRedactor.MaskAccount(account);
}
