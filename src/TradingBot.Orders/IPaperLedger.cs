using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Append-only store of virtual paper fills. Not a live order journal.</summary>
public interface IPaperLedger
{
    void Append(PaperFillRecord fill);

    /// <summary>Thread-safe snapshot of all fills in append order.</summary>
    IReadOnlyList<PaperFillRecord> GetSnapshot();

    int Count { get; }
}
