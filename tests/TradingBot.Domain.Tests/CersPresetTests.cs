using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class CersPresetTests
{
    [Fact]
    public void Constants_match_backtest_cers_strategy_settings()
    {
        Assert.Equal(21, CersPreset.EmaPeriod);
        Assert.Equal(14, CersPreset.RsiPeriod);
        Assert.Equal(14, CersPreset.AtrPeriod);
        Assert.Equal(20, CersPreset.VolSmaPeriod);
        Assert.Equal(30, CersPreset.AutocorrWindow);

        Assert.Equal(0.001m, CersPreset.FeeRatePerSide);
        Assert.Equal(0.0005m, CersPreset.SlippageRatePerSide);
        Assert.Equal(0.003, CersPreset.RoundTripCost, precision: 9);
        Assert.Equal(
            0.003,
            2.0 * ((double)CersPreset.FeeRatePerSide + (double)CersPreset.SlippageRatePerSide),
            precision: 9);

        Assert.Equal(2.0, CersPreset.ThresholdMultiple);
        Assert.Equal(0.006, CersPreset.EntryThreshold, precision: 9);
        Assert.Equal(
            CersPreset.ThresholdMultiple * CersPreset.RoundTripCost,
            CersPreset.EntryThreshold,
            precision: 9);

        Assert.Equal(0.012, CersPreset.StopLossPct);
        Assert.Equal(1.5, CersPreset.TakeProfitExpectedMultiple);
        Assert.Equal(40, CersPreset.MaxHoldBars);

        Assert.Equal(ChartTimeframe.분봉1, CersPreset.Timeframe);
        Assert.Equal(TradingStrategyKind.CERS비용회귀, CersPreset.Strategy);
        Assert.Equal(6, (int)TradingStrategyKind.CERS비용회귀);
    }

    [Fact]
    public void OwnerSummary_mentions_cers_and_live_gate_without_profit_guarantee()
    {
        var summary = CersPreset.OwnerSummary;
        Assert.False(string.IsNullOrWhiteSpace(summary));
        Assert.Contains("CERS", summary, StringComparison.OrdinalIgnoreCase);

        var hasGate =
            summary.Contains("실주문", StringComparison.Ordinal)
            || summary.Contains("게이트", StringComparison.Ordinal);
        Assert.True(hasGate, "OwnerSummary must mention 실주문 or 게이트");

        Assert.DoesNotContain("보장", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("수익 보장", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("profit guarantee", summary, StringComparison.OrdinalIgnoreCase);
    }
}
