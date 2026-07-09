using TradingBot.Domain;

namespace TradingBot.Risk;

/// <summary>
/// Fail-closed gate for live order eligibility.
/// All conditions must pass; any missing approval or unsafe default blocks.
/// </summary>
public sealed class LiveOrderGate
{
    public RiskDecision Evaluate(TradingSafetySettings settings, LiveOrderContext context)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(context);

        var blocks = new List<BlockedReason>();

        if (settings.KillSwitch)
        {
            blocks.Add(BlockedReason.KillSwitchActive);
        }

        if (!settings.AllowLiveOrders)
        {
            blocks.Add(BlockedReason.LiveOrdersNotAllowed);
        }

        if (settings.OrderMode != OrderMode.Live)
        {
            blocks.Add(BlockedReason.OrderModeNotLive);
        }

        if (!context.ManualApprovalPresent)
        {
            blocks.Add(BlockedReason.ManualApprovalMissing);
        }

        if (context.HasUnknownState)
        {
            blocks.Add(BlockedReason.UnknownState);
        }

        if (context.HasMissingData)
        {
            blocks.Add(BlockedReason.MissingData);
        }

        if (context.HasStaleMarketData)
        {
            blocks.Add(BlockedReason.StaleMarketData);
        }

        if (context.HasApiError)
        {
            blocks.Add(BlockedReason.ApiError);
        }

        // Live implementation is intentionally disabled during harness phase.
        if (!context.LiveImplementationEnabled)
        {
            blocks.Add(BlockedReason.LiveImplementationDisabled);
        }

        return blocks.Count == 0 ? RiskDecision.Allow() : RiskDecision.Block(blocks.ToArray());
    }
}

/// <summary>Runtime context for live eligibility (expand as features land).</summary>
public sealed class LiveOrderContext
{
    public bool ManualApprovalPresent { get; init; }
    public bool HasUnknownState { get; init; }
    public bool HasMissingData { get; init; }
    public bool HasStaleMarketData { get; init; }
    public bool HasApiError { get; init; }

    /// <summary>Must remain false until live readiness checklist is complete.</summary>
    public bool LiveImplementationEnabled { get; init; }
}
