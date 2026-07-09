namespace TradingBot.Domain;

/// <summary>How orders are handled. Live is never the default.</summary>
public enum OrderMode
{
    DryRun = 0,
    Paper = 1,
    Live = 2,
}
