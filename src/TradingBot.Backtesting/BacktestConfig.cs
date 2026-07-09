namespace TradingBot.Backtesting;

/// <summary>
/// Deterministic long-only bar backtest parameters.
/// Live orders are never produced by this module.
/// </summary>
public sealed record BacktestConfig(
    decimal InitialCash = 10_000m,
    decimal FeeRatePerSide = 0.001m,
    decimal SlippageRatePerSide = 0.0005m,
    int CooldownBarsAfterExit = 3,
    bool RegularSessionOnly = true,
    int MaxHoldBars = 60,
    /// <summary>
    /// Bars per year for Sharpe annualization.
    /// Null → estimate from median bar spacing (1m US ≈ 98_280, daily ≈ 252).
    /// </summary>
    double? PeriodsPerYear = null)
{
    /// <summary>Default 1-minute US session annualization: 252 * 390.</summary>
    public const double DefaultOneMinutePeriodsPerYear = 98_280d;

    /// <summary>Default daily annualization.</summary>
    public const double DefaultDailyPeriodsPerYear = 252d;
}
