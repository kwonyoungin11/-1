namespace TradingBot.Backtesting;

/// <summary>Mark-to-market equity at a bar close (cash + open position * close).</summary>
public sealed record EquityPoint(DateTimeOffset Time, decimal Equity);
