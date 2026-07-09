using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class TossUsEquityCommissionTests
{
    [Fact]
    public void Brokerage_free_at_or_below_10_usd()
    {
        Assert.Equal(0m, TossUsEquityCommissionSchedule.BrokerageFee(10m));
        Assert.Equal(0m, TossUsEquityCommissionSchedule.BrokerageFee(5m));
    }

    [Fact]
    public void Brokerage_0_1_percent_on_10k()
    {
        // 10000 * 0.001 = 10
        Assert.Equal(10m, TossUsEquityCommissionSchedule.BrokerageFee(10_000m));
    }

    [Fact]
    public void Round_trip_10k_about_20_plus_sec()
    {
        var e = TossUsEquityCommissionSchedule.EstimateRoundTrip(10_000m, 10_000m);
        Assert.Equal(10m, e.BuyBrokerage);
        Assert.Equal(10m, e.SellBrokerage);
        Assert.True(e.SecFee >= 0.01m);
        Assert.True(e.TotalUsd >= 20.01m && e.TotalUsd < 21m);
    }

    [Fact]
    public void Bracket_includes_commission_estimate()
    {
        var plan = TradeBracketPlanner.PlanLongLimit(
            "SPCX",
            lastPrice: 100m,
            equity: 100_000m,
            risk: SpacexRiskParameters.CreateSafeDefaults(),
            atr: 2.0,
            trend: TrendFollowParameters.CreateSafeDefaults());

        Assert.True(plan.IsValid);
        Assert.True(plan.EstimatedCommissionUsd > 0m);
        Assert.True(plan.NetRewardRiskRatio > 0m);
        Assert.True(plan.NetRewardRiskRatio <= plan.RewardRiskRatio);
        Assert.Contains("수수료", plan.OwnerMessage, StringComparison.Ordinal);
    }
}
