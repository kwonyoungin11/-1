namespace TradingBot.Backtesting;

/// <summary>One completed long round-trip from a bar backtest (simulation only).</summary>
public sealed record BacktestTrade(
    int EntryIndex,
    int ExitIndex,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal PnLUsd,
    decimal ReturnPct,
    string ExitReason,
    string EntryReason);
