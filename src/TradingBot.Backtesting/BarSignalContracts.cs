using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Bar-level long-only action for offline backtest engines.
/// Flat = no change; EnterLong / ExitLong are discrete events.
/// Simulation only — not investment advice; not a live order path.
/// </summary>
public enum BarAction
{
    Flat = 0,
    EnterLong = 1,
    ExitLong = 2,
}

/// <summary>
/// Precomputable bar signal source for long-only backtests.
/// Call <see cref="Prepare"/> once per candle series, then sample per bar index.
/// </summary>
public interface IBarSignalSource
{
    string Name { get; }

    void Prepare(IReadOnlyList<CandlePoint> candles);

    BarAction ActionAt(int barIndex);

    /// <summary>Human-readable entry rationale when <see cref="ActionAt"/> is EnterLong; otherwise null.</summary>
    string? EntryReasonAt(int barIndex);
}
