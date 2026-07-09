namespace TradingBot.Observability;

/// <summary>
/// Small helpers that build (and optionally append) auto-trade practice session audit events.
/// Free-text messages are redacted via <see cref="AuditMessageRedactor"/> so tokens/secrets
/// never leave these helpers as raw material.
/// </summary>
/// <remarks>
/// Live order execution is never implied: these events describe practice / cockpit session lifecycle only.
/// </remarks>
public static class SessionAuditEvents
{
    private const string DefaultStartMessage = "자동매매(연습) 시작 — 실주문 없음";
    private const string DefaultStopMessage = "자동매매(연습) 종료";

    /// <summary>
    /// Create a redacted audit entry for auto-trade practice start.
    /// </summary>
    /// <param name="ownerMessage">Owner-facing status text from the session service (may be null/empty).</param>
    /// <param name="timestampUtc">Event time; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <param name="severity">Audit severity; defaults to Information.</param>
    public static AuditEntry CreateAutoTradeStart(
        string? ownerMessage,
        DateTimeOffset? timestampUtc = null,
        AuditSeverity severity = AuditSeverity.Information)
        => Create(
            SessionAuditCategories.AutoStart,
            ownerMessage,
            DefaultStartMessage,
            timestampUtc,
            severity);

    /// <summary>
    /// Create a redacted audit entry for auto-trade practice stop.
    /// </summary>
    public static AuditEntry CreateAutoTradeStop(
        string? ownerMessage,
        DateTimeOffset? timestampUtc = null,
        AuditSeverity severity = AuditSeverity.Information)
        => Create(
            SessionAuditCategories.AutoStop,
            ownerMessage,
            DefaultStopMessage,
            timestampUtc,
            severity);

    /// <summary>
    /// Append a redacted auto-trade practice start event to the audit log.
    /// </summary>
    public static void AppendAutoTradeStart(
        IAuditLog log,
        string? ownerMessage,
        DateTimeOffset? timestampUtc = null,
        AuditSeverity severity = AuditSeverity.Information)
    {
        ArgumentNullException.ThrowIfNull(log);
        log.Append(CreateAutoTradeStart(ownerMessage, timestampUtc, severity));
    }

    /// <summary>
    /// Append a redacted auto-trade practice stop event to the audit log.
    /// </summary>
    public static void AppendAutoTradeStop(
        IAuditLog log,
        string? ownerMessage,
        DateTimeOffset? timestampUtc = null,
        AuditSeverity severity = AuditSeverity.Information)
    {
        ArgumentNullException.ThrowIfNull(log);
        log.Append(CreateAutoTradeStop(ownerMessage, timestampUtc, severity));
    }

    private static AuditEntry Create(
        string correlationId,
        string? ownerMessage,
        string defaultMessage,
        DateTimeOffset? timestampUtc,
        AuditSeverity severity)
    {
        var raw = string.IsNullOrWhiteSpace(ownerMessage) ? defaultMessage : ownerMessage.Trim();
        // Redact before the entry exists so callers who skip InMemoryAuditLog still never retain secrets.
        var safeMessage = AuditMessageRedactor.Redact(raw);

        return new AuditEntry(
            Timestamp: timestampUtc ?? DateTimeOffset.UtcNow,
            Category: SessionAuditCategories.Session,
            Message: safeMessage,
            CorrelationId: correlationId,
            Severity: severity);
    }
}
