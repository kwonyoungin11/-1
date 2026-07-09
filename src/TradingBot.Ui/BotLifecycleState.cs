namespace TradingBot.Ui;

/// <summary>High-level bot lifecycle for owner cockpit (not an order router).</summary>
public enum BotLifecycleState
{
    Bootstrapping = 0,
    HarnessReady = 1,
    AwaitingReadOnlyConnect = 2,
    ReadOnlyConnected = 3,
    SignalComputing = 4,
    RiskEvaluating = 5,
    DryRunActive = 6,
    PaperActive = 7,
    LiveLocked = 8,
    LiveReadyPendingApproval = 9,
    LiveArmed = 10,
    Degraded = 11,
    Error = 12,
    KillSwitchActive = 13,
}
