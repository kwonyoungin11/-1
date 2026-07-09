namespace TradingBot.Domain;

/// <summary>
/// Machine-checkable live readiness status for ops artifacts + safety defaults.
/// Never means "live orders are open".
/// </summary>
public enum LiveReadinessStatus
{
    /// <summary>Safety defaults intact; one or more required ops artifacts are missing or invalid.</summary>
    BlockedMissingEvidence = 0,

    /// <summary>
    /// Safety defaults intact and required ops artifacts present.
    /// Owner may begin the unlock process; live is still not enabled by this status alone.
    /// </summary>
    ReadyForOwnerUnlock = 1,

    /// <summary>Fail-closed safety defaults are weakened or missing — do not trade.</summary>
    BrokenDefaults = 2,
}
