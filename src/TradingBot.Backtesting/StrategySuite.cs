namespace TradingBot.Backtesting;

/// <summary>
/// Factory for the default long-only bar signal suite (6m multi-strategy compare).
/// Offline simulation catalog only — not investment advice.
/// </summary>
public static class StrategySuite
{
    /// <summary>
    /// Default suite: baselines + MathEdge + primary/loose CERS.
    /// Fresh instances each call (stateful Prepare).
    /// </summary>
    public static IReadOnlyList<IBarSignalSource> AllDefault() =>
    [
        new BuyHoldStrategy(),
        new RsiMeanReversionStrategy(),
        new EmaCrossStrategy(),
        new MathEdgeStrategy(),
        new CersStrategy(),
        new CersLooseStrategy(),
    ];
}
