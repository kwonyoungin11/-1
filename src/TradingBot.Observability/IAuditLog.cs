namespace TradingBot.Observability;

/// <summary>
/// Append-only audit log for trading safety events.
/// Implementations must not store secrets and should redact free-text messages.
/// </summary>
public interface IAuditLog
{
    /// <summary>Append an entry. Message is redacted by the implementation when needed.</summary>
    void Append(AuditEntry entry);

    /// <summary>Convenience append with UTC timestamp.</summary>
    void Append(
        string category,
        string message,
        string correlationId,
        AuditSeverity severity = AuditSeverity.Information);

    /// <summary>Most recent entries, oldest first within the returned window.</summary>
    IReadOnlyList<AuditEntry> GetRecent(int count = 100);

    /// <summary>Full in-memory snapshot (copy), oldest first.</summary>
    IReadOnlyList<AuditEntry> Snapshot();

    /// <summary>Number of retained entries.</summary>
    int Count { get; }
}
