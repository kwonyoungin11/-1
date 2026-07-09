namespace TradingBot.Orders;

/// <summary>
/// Process-local index of seen <c>ClientOrderId</c> values for dry-run / paper idempotency.
/// Matching is <strong>ordinal case-sensitive</strong> (exact string equality).
/// Does not talk to Toss or any network. Not a live order journal.
/// </summary>
public sealed class ClientOrderIdIndex
{
    private readonly object _sync = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>Number of distinct client order ids recorded.</summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _seen.Count;
            }
        }
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="clientOrderId"/> was already registered (duplicate).
    /// </summary>
    public bool Contains(string clientOrderId)
    {
        ArgumentNullException.ThrowIfNull(clientOrderId);
        lock (_sync)
        {
            return _seen.Contains(clientOrderId);
        }
    }

    /// <summary>
    /// Atomically registers <paramref name="clientOrderId"/> if unseen.
    /// </summary>
    /// <returns>
    /// <c>true</c> if this call recorded the id (first submit);
    /// <c>false</c> if the id was already seen (duplicate — caller must reject and not append ledger).
    /// </returns>
    public bool TryRegister(string clientOrderId)
    {
        ArgumentNullException.ThrowIfNull(clientOrderId);
        lock (_sync)
        {
            return _seen.Add(clientOrderId);
        }
    }
}
