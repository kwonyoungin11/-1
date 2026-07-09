namespace TradingBot.Runner;

/// <summary>
/// Owner-safe outcome of one harness run. No secrets, no order API side effects.
/// Exit code is fail-closed: success only when live remains blocked.
/// </summary>
public interface IHarnessRunResult
{
    string SafetyHeadline { get; }

    string ConnectionSummary { get; }

    int CandidateCount { get; }

    int DryRunLedgerCount { get; }

    int RiskGateRowCount { get; }

    int UiCandidateCount { get; }

    int AuditEntryCount { get; }

    bool LiveSubmissionBlocked { get; }

    bool IsLiveTradingVisuallyOpen { get; }

    /// <summary>0 when live is blocked (expected safe harness); 1 if live path appears open.</summary>
    int ExitCode { get; }
}
