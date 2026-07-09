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
    TrendFollowParameters? TrendFollow = null,
    /// <summary>Owner news-day: halve size, no aggressive reprice (not auto news trading).</summary>
    bool NewsDay = false,
    /// <summary>Symbol regulatory warning / halt-style flag from data layer.</summary>
    bool SymbolWarningActive = false,
    /// <summary>
    /// Chart / cache candles for bar strategies (CERS etc.). Cap at display bar count.
    /// Null when harness has not attached a series yet — CERS fails closed (hold).
    /// </summary>
    IReadOnlyList<CandlePoint>? Candles = null,
    /// <summary>Optional open CERS long for exit evaluation. Null = flat.</summary>
    CersOpenPosition? CersPosition = null)
{
    /// <summary>Safe practice defaults (100k equity, 1% risk, 2% stop, 3% max daily loss).</summary>
    public static PracticeStrategyContext CreateSafeDefaults() => new();

    /// <summary>Effective risk % after news-day / warning multipliers.</summary>
    public decimal EffectiveRiskPercentPerTrade =>
        RiskPercentPerTrade * LimitOrderLifecyclePolicy.SizeMultiplier(NewsDay, SymbolWarningActive);
}
