namespace TradingBot.Domain;

/// <summary>Runtime safety settings. Defaults are fail-closed.</summary>
public sealed class TradingSafetySettings
{
    public bool AllowLiveOrders { get; init; } = TradingSafetyDefaults.AllowLiveOrders;
    public bool KillSwitch { get; init; } = TradingSafetyDefaults.KillSwitch;
    public OrderMode OrderMode { get; init; } = TradingSafetyDefaults.OrderMode;
    public int MarketDataMaxStalenessSeconds { get; init; } = TradingSafetyDefaults.MarketDataMaxStalenessSeconds;
    public decimal? MaxOrderNotional { get; init; }
    public decimal? MaxDailyLoss { get; init; }
    public decimal? MaxPositionSize { get; init; }
    public decimal? MaxSymbolPositionRatio { get; init; }
    public int? MaxOpenOrders { get; init; }

    public static TradingSafetySettings CreateSafeDefaults() => new();
}
