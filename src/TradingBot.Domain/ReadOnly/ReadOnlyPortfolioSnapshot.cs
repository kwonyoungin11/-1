namespace TradingBot.Domain;

/// <summary>Aggregated read-only view for cockpit. Never contains raw secrets.</summary>
public sealed class ReadOnlyPortfolioSnapshot
{
    public required ConnectionStatus ConnectionStatus { get; init; }
    public required string ConnectionOwnerMessage { get; init; }
    public required IReadOnlyList<AccountSummary> Accounts { get; init; }
    public required IReadOnlyList<HoldingSummary> Holdings { get; init; }
    public required IReadOnlyList<QuoteSnapshot> Quotes { get; init; }
    public required UsMarketSessionSnapshot? UsMarket { get; init; }
    public required string? MarketValueUsdSummary { get; init; }
    public required DateTimeOffset AsOfUtc { get; init; }
    public required IReadOnlyList<string> BlockMessages { get; init; }

    public static ReadOnlyPortfolioSnapshot Disconnected(string reason) => new()
    {
        ConnectionStatus = ConnectionStatus.Disconnected,
        ConnectionOwnerMessage = reason,
        Accounts = Array.Empty<AccountSummary>(),
        Holdings = Array.Empty<HoldingSummary>(),
        Quotes = Array.Empty<QuoteSnapshot>(),
        UsMarket = null,
        MarketValueUsdSummary = null,
        AsOfUtc = DateTimeOffset.UtcNow,
        BlockMessages = new[] { reason },
    };
}
