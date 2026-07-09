namespace TradingBot.Ui;

/// <summary>High-level cockpit projection for non-developer owners. No live order actions.</summary>
public sealed record CockpitState(
    string BotState,
    bool LiveOrdersAllowed,
    bool KillSwitchActive,
    string OrderMode,
    string SafetySummary)
{
    public static CockpitState CreateSafeDefault() => new(
        BotState: "HarnessReady",
        LiveOrdersAllowed: false,
        KillSwitchActive: true,
        OrderMode: "dry_run",
        SafetySummary: "Live orders blocked. Kill switch on. Dry-run mode.");
}
