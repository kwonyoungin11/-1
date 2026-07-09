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

    /// <summary>Parsed market value USD when summary string is numeric; otherwise null.</summary>
    public decimal? MarketValueUsdDecimal { get; init; }

    /// <summary>Cash buying power for the requested currency (typically USD for NASDAQ).</summary>
    public decimal? CashBuyingPower { get; init; }

    /// <summary>Currency code of <see cref="CashBuyingPower"/> (e.g. USD, KRW).</summary>
    public string? CashCurrency { get; init; }

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
        MarketValueUsdDecimal = null,
        CashBuyingPower = null,
        CashCurrency = null,
        AsOfUtc = DateTimeOffset.UtcNow,
        BlockMessages = new[] { reason },
    };
}
