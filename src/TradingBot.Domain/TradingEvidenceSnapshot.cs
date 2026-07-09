namespace TradingBot.Domain;

/// <summary>
/// Point-in-time trading evidence from dry-run ledger + paper fills.
/// Used for readiness / cockpit display. Live orders are never included.
/// </summary>
public sealed record TradingEvidenceSnapshot(
    DateTimeOffset CapturedAtUtc,
    EvidenceSummary Summary,
    IReadOnlyList<string> RecentDryRunSymbols,
    IReadOnlyList<string> RecentPaperSymbols,
    IReadOnlyList<DryRunLedgerEntry> DryRunEntries,
    IReadOnlyList<PaperFillRecord> PaperFills);
