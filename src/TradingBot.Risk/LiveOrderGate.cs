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

        // Owner must explicitly confirm in the cockpit before any live transport call.
        if (!context.ManualApprovalPresent)
        {
            blocks.Add(BlockedReason.ManualApprovalMissing);
        }

        if (!context.LiveImplementationEnabled)
        {
            blocks.Add(BlockedReason.LiveImplementationDisabled);
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

        return blocks.Count == 0 ? RiskDecision.Allow() : RiskDecision.Block(blocks.ToArray());
    }
}

/// <summary>Runtime context for live eligibility (expand as features land).</summary>
public sealed record LiveOrderContext
{
    public bool ManualApprovalPresent { get; init; }
    public bool HasUnknownState { get; init; }
    public bool HasMissingData { get; init; }
    public bool HasStaleMarketData { get; init; }
    public bool HasApiError { get; init; }

    /// <summary>True when owner-enabled live host + transport is wired (still gate-checked).</summary>
    public bool LiveImplementationEnabled { get; init; }
}
