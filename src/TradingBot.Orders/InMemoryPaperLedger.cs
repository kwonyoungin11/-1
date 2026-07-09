using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Process-local paper fill ledger. Safe for concurrent append from paper router.</summary>
public sealed class InMemoryPaperLedger : IPaperLedger
{
    private readonly object _sync = new();
    private readonly List<PaperFillRecord> _fills = new();

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _fills.Count;
            }
        }
    }

    public void Append(PaperFillRecord fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        lock (_sync)
        {
            _fills.Add(fill);
        }
    }

    public IReadOnlyList<PaperFillRecord> GetSnapshot()
    {
        lock (_sync)
        {
            return _fills.ToArray();
        }
    }
}
