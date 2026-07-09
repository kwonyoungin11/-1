namespace TradingBot.Runner;

/// <summary>Immutable harness run summary for console / tests.</summary>
public sealed record HarnessRunResult(
    string SafetyHeadline,
    string ConnectionSummary,
    int CandidateCount,
    int DryRunLedgerCount,
    int RiskGateRowCount,
    int UiCandidateCount,
    int AuditEntryCount,
    bool LiveSubmissionBlocked,
    bool IsLiveTradingVisuallyOpen) : IHarnessRunResult
{
    /// <inheritdoc />
    public int ExitCode =>
        LiveSubmissionBlocked && !IsLiveTradingVisuallyOpen ? 0 : 1;
}
