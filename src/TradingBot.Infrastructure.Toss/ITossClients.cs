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
}

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
