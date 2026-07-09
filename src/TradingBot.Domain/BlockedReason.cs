namespace TradingBot.Domain;

/// <summary>Machine-readable reason a trade or live path is blocked (fail-closed).</summary>
public sealed record BlockedReason(string Code, string Message)
{
    public static BlockedReason KillSwitchActive { get; } =
        new("kill_switch_active", "Kill switch is active. All live trading is blocked.");

    public static BlockedReason LiveOrdersNotAllowed { get; } =
        new("live_orders_not_allowed", "ALLOW_LIVE_ORDERS is false. Live trading is blocked.");

    public static BlockedReason OrderModeNotLive { get; } =
        new("order_mode_not_live", "ORDER_MODE is not live. Live trading is blocked.");

    public static BlockedReason ManualApprovalMissing { get; } =
        new("manual_approval_missing", "Manual approval is required before live trading.");

    public static BlockedReason UnknownState { get; } =
        new("unknown_state", "System state is unknown. Fail-closed block.");

    public static BlockedReason MissingData { get; } =
        new("missing_data", "Required data is missing. Fail-closed block.");

    public static BlockedReason StaleMarketData { get; } =
        new("stale_market_data", "Market data is stale. Fail-closed block.");

    public static BlockedReason ApiError { get; } =
        new("api_error", "API error occurred. Fail-closed block.");

    public static BlockedReason LiveImplementationDisabled { get; } =
        new("live_implementation_disabled", "Live order implementation is disabled in this phase.");
}
