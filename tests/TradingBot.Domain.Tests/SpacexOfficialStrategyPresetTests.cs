using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class SpacexOfficialStrategyPresetTests
{
    [Fact]
    public void Final_preset_is_trend_follow_15m()
    {
        Assert.Equal(TradingStrategyKind.추세추종, SpacexOfficialStrategyPreset.Strategy);
        Assert.Equal(ChartTimeframe.분봉15, SpacexOfficialStrategyPreset.Timeframe);
        Assert.Equal(ChartTimeframe.분봉60, SpacexOfficialStrategyPreset.AlternateTimeframe);
        Assert.True(SpacexOfficialStrategyPreset.Risk.UseAtrStops);
        Assert.Equal(2.0m, SpacexOfficialStrategyPreset.Risk.TakeProfitR);
        Assert.Contains("추세추종", SpacexOfficialStrategyPreset.OwnerSummary, StringComparison.Ordinal);
        Assert.Contains("실주문 잠금", SpacexOfficialStrategyPreset.OwnerSummary, StringComparison.Ordinal);
    }
}
