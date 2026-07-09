namespace TradingBot.Backtesting;

/// <summary>
/// Aggregate outcome of a deterministic bar backtest.
/// Not investment advice; simulation only.
/// </summary>
public sealed record BacktestResult(
    string StrategyName,
    decimal InitialCash,
    decimal FinalEquity,
    decimal TotalReturnPct,
    decimal MaxDrawdownPct,
    double Sharpe,
    int TradeCount,
    double WinRatePct,
    double ProfitFactor,
    double AvgHoldBars,
    IReadOnlyList<BacktestTrade> Trades,
    IReadOnlyList<EquityPoint> EquityCurve,
    string Notes);
