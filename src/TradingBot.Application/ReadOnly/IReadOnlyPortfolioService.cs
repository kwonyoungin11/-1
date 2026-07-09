using TradingBot.Domain;

namespace TradingBot.Application;

public interface IReadOnlyPortfolioService
{
    Task<ReadOnlyPortfolioSnapshot> GetSnapshotAsync(
        IReadOnlyList<string> watchSymbols,
        CancellationToken cancellationToken);

    /// <summary>
    /// Chart candles (read-only). Live Toss when connected; mock may return synthetic series.
    /// </summary>
    Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken);
}
