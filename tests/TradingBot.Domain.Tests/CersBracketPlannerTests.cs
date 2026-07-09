using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class CersBracketPlannerTests
{
    [Fact]
    public void PlanLong_sets_entry_stop_tp_from_cers_rules()
    {
        const decimal last = 100m;
        const double expected = 0.01; // TP = entry * (1 + 0.01*1.5) = 101.5
        var plan = CersBracketPlanner.PlanLong(
            symbol: "VMAR",
            lastPrice: last,
            expectedEdge: expected,
            equity: 100_000m,
            riskPercentPerTrade: 1m);

        Assert.True(plan.IsValid);
        Assert.Equal("BUY", plan.Side);
        Assert.Equal("LIMIT", plan.OrderType);
        Assert.Equal(last, plan.EntryLimit);
        Assert.Equal(last * (1m - (decimal)CersPreset.StopLossPct), plan.StopPrice);
        Assert.Equal(
            last * (1m + (decimal)(expected * CersPreset.TakeProfitExpectedMultiple)),
            plan.TakeProfitPrice);
        Assert.True(plan.Quantity > 0m);
        Assert.Equal(BracketStopSource.Percent, plan.StopSource);
        Assert.Contains("실주문", plan.OwnerMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("보장", plan.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanLong_uses_base_quantity_when_equity_risk_yields_zero()
    {
        // Tiny equity / wide stop → risk sizer qty 0 → fall back to base.
        var plan = CersBracketPlanner.PlanLong(
            symbol: "VMAR",
            lastPrice: 100m,
            expectedEdge: 0.01,
            equity: 1m,
            riskPercentPerTrade: 0.01m,
            baseQuantity: 3m);

        Assert.True(plan.IsValid);
        Assert.Equal(3m, plan.Quantity);
    }

    [Fact]
    public void PlanLong_invalid_when_price_non_positive()
    {
        var plan = CersBracketPlanner.PlanLong(
            symbol: "VMAR",
            lastPrice: 0m,
            expectedEdge: 0.01,
            equity: 10_000m);

        Assert.False(plan.IsValid);
        Assert.Equal(0m, plan.Quantity);
        Assert.Equal(BracketStopSource.Invalid, plan.StopSource);
    }

    [Fact]
    public void PlanLong_invalid_when_expected_edge_non_positive()
    {
        var plan = CersBracketPlanner.PlanLong(
            symbol: "VMAR",
            lastPrice: 50m,
            expectedEdge: 0,
            equity: 10_000m);

        Assert.False(plan.IsValid);
    }
}
