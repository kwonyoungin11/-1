namespace TradingBot.Web.Services;

/// <summary>
/// Owner-facing evidence snapshot for cockpit pages (web-local DTO).
/// Live is always reported blocked in this host — no secrets, no account numbers.
/// </summary>
public sealed record WebEvidenceSummary
{
    /// <summary>Count of dry-run ledger entries accumulated this process.</summary>
    public required int DryRunCount { get; init; }

    /// <summary>Count of virtual paper fills accumulated this process.</summary>
    public required int PaperFillCount { get; init; }

    /// <summary>
    /// Recent / configured order modes observed by the host (e.g. dry_run, paper).
    /// Never includes live as an executable mode on this host.
    /// </summary>
    public required IReadOnlyList<string> LastModes { get; init; }

    /// <summary>
    /// Always <c>true</c> for TradingBot.Web — live submission is fail-closed by construction.
    /// </summary>
    public required bool LiveBlocked { get; init; }
}
