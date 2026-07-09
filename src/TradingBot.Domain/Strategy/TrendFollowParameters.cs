namespace TradingBot.Domain;

/// <summary>
/// Explicit trend-follow practice parameters (config only).
/// Not investment advice. Not a live order instruction.
/// R multiples are risk units relative to entry risk for candidate labeling / later risk wiring.
/// </summary>
public sealed record TrendFollowParameters(
    decimal StopLossR,
    decimal TakeProfitR,
    int CooldownBars,
    decimal MinMomentumScore)
{
    /// <summary>
    /// Safe practice defaults matching historical trend-follow mock threshold (0.15).
    /// Stop 1R / take 2R / 3-bar cooldown are conservative practice placeholders.
    /// </summary>
    public static TrendFollowParameters CreateSafeDefaults() => new(
        StopLossR: 1.0m,
        TakeProfitR: 2.0m,
        CooldownBars: 3,
        MinMomentumScore: 0.15m);
}
