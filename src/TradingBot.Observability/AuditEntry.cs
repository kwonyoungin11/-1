namespace TradingBot.Observability;

/// <summary>
/// Immutable audit record for replay and incident review.
/// Must never carry secrets, tokens, account numbers, or raw credentials.
/// </summary>
/// <param name="Timestamp">UTC timestamp when the event was recorded.</param>
/// <param name="Category">Stable category (e.g. risk, order, safety, market).</param>
/// <param name="Message">Human-readable message; must already be redacted or will be redacted on append.</param>
/// <param name="CorrelationId">Cross-component correlation / client order / request id (non-secret).</param>
/// <param name="Severity">Audit severity.</param>
public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string Category,
    string Message,
    string CorrelationId,
    AuditSeverity Severity = AuditSeverity.Information);
