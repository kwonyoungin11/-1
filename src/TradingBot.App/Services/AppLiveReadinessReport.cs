namespace TradingBot.App.Services;

/// <summary>
/// Owner-facing live readiness snapshot for the desktop host.
/// Never claims live is open; file presence alone cannot unlock submission.
/// </summary>
public sealed record AppLiveReadinessReport(
    bool LiveBlocked,
    bool IsLiveSubmissionEnabled,
    string OwnerUnlockStatus,
    bool ChecklistPresent,
    bool EvidenceDocPresent,
    bool LiveReadinessArtifactDirPresent,
    bool AutomationScriptPresent,
    bool GatedLiveRouterRegistered,
    bool GatedLiveRouterUsedInPractice,
    bool SettingsAllowLiveOrders,
    bool SettingsKillSwitch,
    string SettingsOrderMode,
    IReadOnlyList<string> OwnerUnlockArtifactFiles,
    string Summary)
{
    /// <summary>Absolute host policy: report never enables live submission.</summary>
    public bool EnablesLive => false;
}
