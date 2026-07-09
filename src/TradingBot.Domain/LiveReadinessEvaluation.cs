namespace TradingBot.Domain;

/// <summary>
/// Structured result of <see cref="LiveReadinessEvaluator"/>.
/// <see cref="LiveReady"/> is never set true by the evaluator under fail-closed defaults.
/// </summary>
public sealed record LiveReadinessEvaluation(
    LiveReadinessStatus Status,
    bool LiveReady,
    bool SafetyIntact,
    string RootDirectory,
    string ArtifactDirectory,
    IReadOnlyList<string> MissingArtifacts,
    IReadOnlyList<string> PresentArtifacts,
    IReadOnlyList<string> Notes)
{
    /// <summary>
    /// Machine token aligned with <c>check-live-readiness.sh</c> LIVE_OWNER_UNLOCK_STATUS.
    /// Never implies live orders are open.
    /// </summary>
    public string ToOwnerUnlockStatusToken() => Status switch
    {
        LiveReadinessStatus.ReadyForOwnerUnlock => "ready_for_owner_unlock",
        LiveReadinessStatus.BrokenDefaults => "broken_defaults",
        _ => "blocked_missing_evidence",
    };
}
