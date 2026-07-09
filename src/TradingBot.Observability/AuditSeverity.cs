namespace TradingBot.Observability;

/// <summary>Severity of an audit entry (structured, audit-friendly logging).</summary>
public enum AuditSeverity
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
}
