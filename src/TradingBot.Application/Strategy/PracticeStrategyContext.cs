namespace TradingBot.Application;

/// <summary>
/// Practice-loop equity inputs for risk evaluation (daily loss halt).
/// Built from <see cref="AutoTradeSessionService"/> balance — not live account money.
/// </summary>
/// <param name="DayStartEquity">Equity at session / day start (base for absolute MaxDailyLoss).</param>
/// <param name="CurrentEquity">Current practice balance / mark-to-market equity.</param>
public sealed record PracticeStrategyContext(
    decimal DayStartEquity,
    decimal CurrentEquity);
