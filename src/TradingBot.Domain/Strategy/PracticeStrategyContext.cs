namespace TradingBot.Domain;

/// <summary>
/// Practice risk/sizing context for <c>OrderCandidatePipeline</c>.
/// Trader rules as config — not investment advice. Not a live order instruction.
/// </summary>
public sealed record PracticeStrategyContext(
    decimal Equity = 100_000m,
    decimal RiskPercentPerTrade = 1m,
    decimal StopLossPercent = 2m,
    decimal MaxDailyLossPercent = 3m,
    decimal? DayStartEquity = null,
    decimal? CurrentEquity = null,
    TrendFollowParameters? TrendFollow = null)
{
    /// <summary>Safe practice defaults (100k equity, 1% risk, 2% stop, 3% max daily loss).</summary>
    public static PracticeStrategyContext CreateSafeDefaults() => new();
}
