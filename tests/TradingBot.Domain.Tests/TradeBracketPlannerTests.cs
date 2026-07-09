using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class TradeBracketPlannerTests
{
    [Fact]
    public void PlanLongLimit_atr_stop_and_2r_target()
    {
        var risk = SpacexRiskParameters.CreateSafeDefaults();
        var plan = TradeBracketPlanner.PlanLongLimit(
            "SPCX",
            lastPrice: 100m,
            equity: 100_000m,
            risk: risk,
            atr: 2.0, // stop distance = 2 * 1.5 = 3
            trend: TrendFollowParameters.CreateSafeDefaults());

        Assert.True(plan.IsValid);
        Assert.Equal("LIMIT", plan.OrderType);
        Assert.Equal("BUY", plan.Side);
        Assert.True(plan.EntryLimit > 0m && plan.EntryLimit <= 100m);
        Assert.True(plan.StopPrice < plan.EntryLimit);
        Assert.True(plan.TakeProfitPrice > plan.EntryLimit);
        Assert.Equal(BracketStopSource.Atr, plan.StopSource);
        Assert.True(plan.Quantity > 0m);
        Assert.True(plan.RewardRiskRatio >= 1.5m);
        Assert.Contains("실주문 잠금", plan.OwnerMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("보장", plan.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanLongLimit_falls_back_to_percent_without_atr()
    {
        var risk = SpacexRiskParameters.CreateSafeDefaults() with { UseAtrStops = true };
        var plan = TradeBracketPlanner.PlanLongLimit(
            "SPCX",
            lastPrice: 50m,
            equity: 50_000m,
            risk: risk,
            atr: null);

        Assert.Equal(BracketStopSource.Percent, plan.StopSource);
        Assert.True(plan.StopPrice < plan.EntryLimit);
    }

    [Fact]
    public void AtrCalculator_needs_enough_bars()
    {
        var shortSeries = Enumerable.Range(0, 5)
            .Select(i => new CandlePoint(DateTimeOffset.UtcNow.AddMinutes(i), 10, 11, 9, 10.5, 100))
            .ToList();
        Assert.Null(AtrCalculator.Compute(shortSeries, 14));

        var longer = Enumerable.Range(0, 40)
            .Select(i => new CandlePoint(
                DateTimeOffset.UtcNow.AddMinutes(i),
                100 + i * 0.1,
                101 + i * 0.1,
                99 + i * 0.1,
                100.2 + i * 0.1,
                1000))
            .ToList();
        var atr = AtrCalculator.Compute(longer, 14);
        Assert.NotNull(atr);
        Assert.True(atr > 0);
    }

    [Fact]
    public void Invalid_price_returns_invalid_plan()
    {
        var plan = TradeBracketPlanner.PlanLongLimit(
            "SPCX",
            lastPrice: 0m,
            equity: 10_000m,
            risk: SpacexRiskParameters.CreateSafeDefaults(),
            atr: 1.0);
        Assert.False(plan.IsValid);
        Assert.Equal(0m, plan.Quantity);
    }
}
