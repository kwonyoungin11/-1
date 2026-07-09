using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Append-only store of dry-run route outcomes. Not a live order journal.</summary>
public interface IDryRunLedger
{
    void Append(DryRunLedgerEntry entry);

    /// <summary>Thread-safe snapshot of all entries in append order.</summary>
    IReadOnlyList<DryRunLedgerEntry> GetSnapshot();

    int Count { get; }
}
