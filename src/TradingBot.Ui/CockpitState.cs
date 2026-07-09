namespace TradingBot.Ui;

/// <summary>Legacy thin view; prefer <see cref="CockpitSnapshot"/>.</summary>
public sealed record CockpitState(
    string BotState,
    bool LiveOrdersAllowed,
    bool KillSwitchActive,
    string OrderMode,
    string SafetySummary)
{
    public static CockpitState CreateSafeDefault()
    {
        var snap = CockpitSnapshot.CreateSafeDefault();
        return new(
            BotState: snap.BotState.ToString(),
            LiveOrdersAllowed: snap.AllowLiveOrders,
            KillSwitchActive: snap.KillSwitchActive,
            OrderMode: "dry_run",
            SafetySummary: snap.SafetyHeadline);
    }
}
