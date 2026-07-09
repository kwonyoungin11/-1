using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class VmarOneMinuteScalpPresetTests
{
    [Fact]
    public void Preset_targets_vmar_fifteen_minute_split_scalp()
    {
        Assert.Equal(WatchlistCatalog.VmarSymbol, VmarOneMinuteScalpPreset.Symbol);
        Assert.Equal("VMAR", VmarOneMinuteScalpPreset.Symbol);
        Assert.Equal(TradingStrategyKind.일분분할스캘프, VmarOneMinuteScalpPreset.Strategy);
        Assert.Equal(ChartTimeframe.분봉15, VmarOneMinuteScalpPreset.Timeframe);
    }

    [Fact]
    public void Preset_uses_three_legs_and_point_one_percent_step()
    {
        Assert.Equal(3, VmarOneMinuteScalpPreset.LegCount);
        Assert.Equal(0.10m, VmarOneMinuteScalpPreset.PriceStepPercent);
    }

    [Fact]
    public void Risk_percent_per_trade_is_tighter_than_one_percent()
    {
        Assert.Equal(0.5m, VmarOneMinuteScalpPreset.RiskPercentPerTrade);
        Assert.True(VmarOneMinuteScalpPreset.RiskPercentPerTrade < 1m);
    }

    [Fact]
    public void OwnerSummary_is_practice_only_live_locked_not_advice()
    {
        var summary = VmarOneMinuteScalpPreset.OwnerSummary;
        Assert.False(string.IsNullOrWhiteSpace(summary));
        Assert.Contains("연습", summary, StringComparison.Ordinal);
        Assert.Contains("실주문", summary, StringComparison.Ordinal);
        Assert.Contains("투자 조언 아님", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("보장", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("수익", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Base_quantity_from_catalog_divides_evenly_by_leg_count()
    {
        var qty = StrategyCatalog.BaseQuantity(VmarOneMinuteScalpPreset.Strategy);
        Assert.True(qty >= VmarOneMinuteScalpPreset.LegCount);
        Assert.Equal(0m, qty % VmarOneMinuteScalpPreset.LegCount);
    }
}
