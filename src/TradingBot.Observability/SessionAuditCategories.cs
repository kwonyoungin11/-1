namespace TradingBot.Observability;

/// <summary>
/// Stable category and correlation identifiers for session / auto-trade practice audit events.
/// Matches cockpit harness usage: category <c>session</c>, correlation <c>auto_start</c> / <c>auto_stop</c>.
/// </summary>
public static class SessionAuditCategories
{
    /// <summary>Category for market session and auto-trade practice lifecycle events.</summary>
    public const string Session = "session";

    /// <summary>Correlation id when an auto-trade practice session is started (or start is rejected).</summary>
    public const string AutoStart = "auto_start";

    /// <summary>Correlation id when an auto-trade practice session is stopped (or stop is rejected).</summary>
    public const string AutoStop = "auto_stop";
}
