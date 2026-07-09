using TradingBot.Domain;

namespace TradingBot.Application;

public interface IReadOnlyPortfolioService
{
    Task<ReadOnlyPortfolioSnapshot> GetSnapshotAsync(
        IReadOnlyList<string> watchSymbols,
        CancellationToken cancellationToken);
}
