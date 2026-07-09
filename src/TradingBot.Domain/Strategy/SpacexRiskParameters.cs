namespace TradingBot.Domain;

/// <summary>
/// SPCX-oriented limit entry + ATR stop / R-multiple take-profit defaults.
/// Professional risk framing for volatile single-name; not investment advice.
/// Live orders remain gated outside this type.
/// </summary>
public sealed record SpacexRiskParameters(
    decimal RiskPercentPerTrade,
    decimal StopLossR,
    decimal TakeProfitR,
    int AtrPeriod,
    decimal AtrStopMultiple,
    decimal LimitOffsetAtrFraction,
    bool UseAtrStops,
    decimal FallbackStopLossPercent)
{
    /// <summary>
    /// Conservative SPCX defaults: 1% risk, ATR×1.5 stop, 2R target, slight limit offset.
    /// </summary>
    public static SpacexRiskParameters CreateSafeDefaults() => new(
        RiskPercentPerTrade: 1.0m,
        StopLossR: 1.0m,
        TakeProfitR: 2.0m,
        AtrPeriod: AtrCalculator.DefaultPeriod,
        AtrStopMultiple: 1.5m,
        LimitOffsetAtrFraction: 0.1m,
        UseAtrStops: true,
        FallbackStopLossPercent: 2.0m);
}
