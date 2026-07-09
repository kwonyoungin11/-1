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

    /// <summary>
    /// Loads runtime settings from environment / .env. Code defaults remain fail-closed when keys are absent.
    /// </summary>
    public static TradingSafetySettings FromEnvironment(IDictionary<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);

        static bool GetBool(IDictionary<string, string?> e, string key, bool defaultValue)
        {
            if (!e.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            {
                return defaultValue;
            }

            return v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || v.Trim() == "1";
        }

        static int GetInt(IDictionary<string, string?> e, string key, int defaultValue)
        {
            if (!e.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            {
                return defaultValue;
            }

            return int.TryParse(v.Trim(), out var n) ? n : defaultValue;
        }

        static decimal? GetDecimal(IDictionary<string, string?> e, string key)
        {
            if (!e.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            {
                return null;
            }

            return decimal.TryParse(v.Trim(), out var d) ? d : null;
        }

        static OrderMode ParseOrderMode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return TradingSafetyDefaults.OrderMode;
            }

            return raw.Trim().ToLowerInvariant() switch
            {
                "live" => OrderMode.Live,
                "paper" => OrderMode.Paper,
                "dry_run" or "dryrun" => OrderMode.DryRun,
                _ => TradingSafetyDefaults.OrderMode,
            };
        }

        env.TryGetValue("ORDER_MODE", out var orderModeRaw);

        return new TradingSafetySettings
        {
            AllowLiveOrders = GetBool(env, "ALLOW_LIVE_ORDERS", TradingSafetyDefaults.AllowLiveOrders),
            KillSwitch = GetBool(env, "KILL_SWITCH", TradingSafetyDefaults.KillSwitch),
            OrderMode = ParseOrderMode(orderModeRaw),
            MarketDataMaxStalenessSeconds = GetInt(
                env,
                "MARKET_DATA_MAX_STALENESS_SECONDS",
                TradingSafetyDefaults.MarketDataMaxStalenessSeconds),
            MaxOrderNotional = GetDecimal(env, "MAX_ORDER_NOTIONAL"),
            MaxDailyLoss = GetDecimal(env, "MAX_DAILY_LOSS"),
            MaxPositionSize = GetDecimal(env, "MAX_POSITION_SIZE"),
            MaxSymbolPositionRatio = GetDecimal(env, "MAX_SYMBOL_POSITION_RATIO"),
            MaxOpenOrders = GetInt(env, "MAX_OPEN_ORDERS", 0) is var n and > 0 ? n : null,
        };
    }
}
