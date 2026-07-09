namespace TradingBot.Backtesting;

/// <summary>
/// Split buy / split sell ladder parameters (research backtest).
/// Percent fields are percent-points (0.10 = 0.10%, not 10%).
/// Not investment advice; simulation only.
/// </summary>
public sealed record SplitLadderParams(
    int BuyLegs,
    double BuyStepPercent,
    int SellLegs,
    double SellStepPercent,
    double StopLossFromAvgPercent,
    double TakeProfitFromAvgPercent,
    double EntryThreshold,
    int MaxHoldBars,
    int CooldownBarsAfterExit = 3)
{
    /// <summary>Project default (VmarOneMinuteScalpPreset style): 3 legs · 0.1% step.</summary>
    public static SplitLadderParams ProjectDefault { get; } = new(
        BuyLegs: 3,
        BuyStepPercent: 0.10,
        SellLegs: 3,
        SellStepPercent: 0.10,
        StopLossFromAvgPercent: 1.2,
        TakeProfitFromAvgPercent: 1.5,
        EntryThreshold: 0.006,
        MaxHoldBars: 40,
        CooldownBarsAfterExit: 3);

    public string Name =>
        $"SPLIT_B{BuyLegs}@{BuyStepPercent:0.##}_S{SellLegs}@{SellStepPercent:0.##}" +
        $"_SL{StopLossFromAvgPercent:0.##}_TP{TakeProfitFromAvgPercent:0.##}" +
        $"_thr{EntryThreshold:0.####}_H{MaxHoldBars}";

    public void Validate()
    {
        if (BuyLegs < 2 || BuyLegs > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(BuyLegs), "BuyLegs must be 2..8.");
        }

        if (SellLegs < 1 || SellLegs > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(SellLegs), "SellLegs must be 1..8.");
        }

        if (BuyStepPercent < 0 || SellStepPercent < 0
            || StopLossFromAvgPercent <= 0 || TakeProfitFromAvgPercent <= 0
            || EntryThreshold <= 0 || MaxHoldBars < 1 || CooldownBarsAfterExit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SplitLadderParams), "Invalid split ladder params.");
        }
    }
}
