using TradingBot.Domain;

namespace TradingBot.Application;

public interface IReadOnlyPortfolioService
{
    Task<ReadOnlyPortfolioSnapshot> GetSnapshotAsync(
        IReadOnlyList<string> watchSymbols,
        CancellationToken cancellationToken);

    /// <summary>
    /// Chart candles (read-only). Live Toss when connected; mock may return synthetic series.
    /// interval must be Toss enum 1m|1d.
    /// </summary>
    Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken);

    /// <summary>
    /// Multi-page raw candles (1m|1d only). Used before client-side TF aggregation.
    /// </summary>
    Task<IReadOnlyList<CandlePoint>> GetCandlesPagedAsync(
        string symbol,
        string interval,
        int countPerPage,
        int maxPages,
        int targetTotal,
        CancellationToken cancellationToken);
}
