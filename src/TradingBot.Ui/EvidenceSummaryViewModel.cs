namespace TradingBot.Ui;

/// <summary>
/// Owner-facing evidence summary: dry-run accepts and paper fills.
/// Non-live phases always keep the live order path blocked.
/// Does not submit orders or unlock live trading.
/// </summary>
public sealed class EvidenceSummaryViewModel
{
    public required int DryRunAcceptedCount { get; init; }

    public required int PaperFillCount { get; init; }

    /// <summary>
    /// Live execution path is blocked. Defaults to true; mapper always sets true
    /// for non-live (dry-run / paper) evidence phases.
    /// </summary>
    public bool LivePathBlocked { get; init; } = true;

    /// <summary>Korean owner message for cockpit evidence strip.</summary>
    public required string OwnerMessage { get; init; }

    /// <summary>
    /// Maps ledger counts into an evidence summary.
    /// Always sets <see cref="LivePathBlocked"/> to true — this factory is for
    /// non-live phases only; it never unlocks live orders.
    /// </summary>
    public static EvidenceSummaryViewModel FromCounts(int dryRunAcceptedCount, int paperFillCount)
    {
        return new EvidenceSummaryViewModel
        {
            DryRunAcceptedCount = dryRunAcceptedCount,
            PaperFillCount = paperFillCount,
            LivePathBlocked = true,
            OwnerMessage = BuildOwnerMessage(dryRunAcceptedCount, paperFillCount),
        };
    }

    private static string BuildOwnerMessage(int dryRunAcceptedCount, int paperFillCount) =>
        $"dry-run 수락 {dryRunAcceptedCount}건, paper 체결 {paperFillCount}건. " +
        "실주문 경로는 차단되어 있습니다. (증거 수집 단계 — 투자 조언 아님)";
}
