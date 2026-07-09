namespace TradingBot.Domain;

/// <summary>
/// Aggregated counts and modes from dry-run + paper evidence only.
/// Never represents live broker activity. No secrets.
/// </summary>
public sealed record EvidenceSummary(
    int DryRunEntryCount,
    int DryRunAcceptedCount,
    int PaperFillCount,
    int TotalEvidenceCount,
    IReadOnlyList<string> ModesPresent,
    bool LiveModePresent);
