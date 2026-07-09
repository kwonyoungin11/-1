using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Process-local dry-run ledger. Safe for concurrent append from dry-run router.</summary>
public sealed class InMemoryDryRunLedger : IDryRunLedger
{
    private readonly object _sync = new();
    private readonly List<DryRunLedgerEntry> _entries = new();

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    public void Append(DryRunLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_sync)
        {
            _entries.Add(entry);
        }
    }

    public IReadOnlyList<DryRunLedgerEntry> GetSnapshot()
    {
        lock (_sync)
        {
            return _entries.ToArray();
        }
    }
}
