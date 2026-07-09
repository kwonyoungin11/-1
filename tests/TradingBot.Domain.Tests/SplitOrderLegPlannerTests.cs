using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class SplitOrderLegPlannerTests
{
    [Fact]
    public void Plan_buy_three_legs_steps_down_from_reference()
    {
        var legs = SplitOrderLegPlanner.Plan(
            side: "BUY",
            totalQuantity: 6m,
            referencePrice: 10m,
            legCount: 3,
            stepPercent: 0.10m);

        Assert.Equal(3, legs.Count);
        Assert.Equal(6m, legs.Sum(l => l.Quantity));
        Assert.All(legs, l => Assert.Equal("BUY", l.Side));

        // leg i price = ref * (1 - i * step/100)
        Assert.Equal(10m, legs[0].LimitPrice);
        Assert.Equal(9.99m, legs[1].LimitPrice); // 10 * (1 - 0.001)
        Assert.Equal(9.98m, legs[2].LimitPrice); // 10 * (1 - 0.002)
        Assert.Equal(0, legs[0].LegIndex);
        Assert.Equal(1, legs[1].LegIndex);
        Assert.Equal(2, legs[2].LegIndex);
    }

    [Fact]
    public void Plan_sell_three_legs_steps_up_from_reference()
    {
        var legs = SplitOrderLegPlanner.Plan(
            side: "SELL",
            totalQuantity: 6m,
            referencePrice: 10m,
            legCount: 3,
            stepPercent: 0.10m);

        Assert.Equal(3, legs.Count);
        Assert.Equal(6m, legs.Sum(l => l.Quantity));
        Assert.All(legs, l => Assert.Equal("SELL", l.Side));

        Assert.Equal(10m, legs[0].LimitPrice);
        Assert.Equal(10.01m, legs[1].LimitPrice); // 10 * (1 + 0.001)
        Assert.Equal(10.02m, legs[2].LimitPrice); // 10 * (1 + 0.002)
    }

    [Fact]
    public void Plan_splits_quantity_into_whole_share_legs_summing_to_total()
    {
        var legs = SplitOrderLegPlanner.Plan("BUY", 10m, 5m, legCount: 3, stepPercent: 0.10m);
        Assert.Equal(3, legs.Count);
        Assert.Equal(10m, legs.Sum(l => l.Quantity));
        Assert.All(legs, l => Assert.True(l.Quantity == decimal.Floor(l.Quantity)));
        Assert.All(legs, l => Assert.True(l.Quantity >= 1m));
    }

    [Fact]
    public void Plan_fail_closed_when_total_qty_less_than_leg_count()
    {
        var legs = SplitOrderLegPlanner.Plan("BUY", 2m, 10m, legCount: 3, stepPercent: 0.10m);
        Assert.Empty(legs);
    }

    [Fact]
    public void Plan_fail_closed_when_reference_price_not_positive()
    {
        Assert.Empty(SplitOrderLegPlanner.Plan("BUY", 6m, 0m, 3, 0.10m));
        Assert.Empty(SplitOrderLegPlanner.Plan("BUY", 6m, -1m, 3, 0.10m));
    }

    [Fact]
    public void Plan_fail_closed_when_leg_count_less_than_two()
    {
        Assert.Empty(SplitOrderLegPlanner.Plan("BUY", 6m, 10m, legCount: 1, stepPercent: 0.10m));
        Assert.Empty(SplitOrderLegPlanner.Plan("BUY", 6m, 10m, legCount: 0, stepPercent: 0.10m));
    }

    [Fact]
    public void Plan_normalizes_side_case()
    {
        var buy = SplitOrderLegPlanner.Plan("buy", 6m, 10m, 3, 0.10m);
        Assert.Equal(3, buy.Count);
        Assert.All(buy, l => Assert.Equal("BUY", l.Side));

        var sell = SplitOrderLegPlanner.Plan("Sell", 6m, 10m, 3, 0.10m);
        Assert.Equal(3, sell.Count);
        Assert.All(sell, l => Assert.Equal("SELL", l.Side));
    }
}
