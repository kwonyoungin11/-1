namespace TradingBot.Domain;

/// <summary>
/// One accepted (or attempted) dry-run route record for audit / replay scaffolding.
/// Never represents a live broker fill. No secrets.
/// </summary>
public sealed record DryRunLedgerEntry(
    Guid EntryId,
    DateTimeOffset RecordedAtUtc,
    OrderCandidate Candidate,
    bool Accepted,
    string Mode,
    string Message);
