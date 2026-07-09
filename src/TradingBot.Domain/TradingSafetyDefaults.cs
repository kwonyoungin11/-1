namespace TradingBot.Domain;

/// <summary>Hard-coded safe defaults. Environment overrides must not weaken these without explicit owner approval gates.</summary>
public static class TradingSafetyDefaults
{
    public const bool AllowLiveOrders = false;
    public const bool KillSwitch = true;
    public const OrderMode OrderMode = OrderMode.DryRun;
    public const int MarketDataMaxStalenessSeconds = 5;
}
