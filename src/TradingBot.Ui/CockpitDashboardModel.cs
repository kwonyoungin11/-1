namespace TradingBot.Ui;

/// <summary>
/// Home cockpit composition: safety snapshot + risk rows + candidate rows.
/// Prefer this for Overview binding; <see cref="CockpitSnapshot"/> remains the core strip.
/// </summary>
public sealed class CockpitDashboardModel
{
    public required CockpitSnapshot Snapshot { get; init; }
    public required IReadOnlyList<RiskGateRowViewModel> RiskGates { get; init; }
    public required IReadOnlyList<OrderCandidateRowViewModel> OrderCandidates { get; init; }

    /// <summary>Safe defaults: live closed, no candidates, default risk block messages.</summary>
    public static CockpitDashboardModel CreateSafeDefault()
    {
        var snapshot = CockpitSnapshot.CreateSafeDefault();
        return new CockpitDashboardModel
        {
            Snapshot = snapshot,
            RiskGates = CockpitDashboardMapper.MapDefaultSafetyGateRows(),
            OrderCandidates = Array.Empty<OrderCandidateRowViewModel>(),
        };
    }

    public bool IsLiveTradingVisuallyOpen => Snapshot.IsLiveTradingVisuallyOpen;
    public LiveLockState LiveLock => Snapshot.LiveLock;
    public string SafetyHeadline => Snapshot.SafetyHeadline;
}
