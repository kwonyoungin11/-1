using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class PositionRiskSizerTests
{
    [Fact]
    public void Sizes_quantity_so_stop_loss_matches_risk_budget()
    {
        // equity 100_000, risk 1% → budget 1_000
        // price 100, stop 2% → 2 per share → qty = floor(1000/2) = 500
        var result = PositionRiskSizer.Calculate(
            equity: 100_000m,
            riskPercentPerTrade: 1.0m,
            stopLossPercent: 2.0m,
            price: 100m);

        Assert.Equal(500m, result.Quantity);
        Assert.Equal(1_000m, result.RiskBudget);
        Assert.Equal(2m, result.StopDistancePerShare);
        Assert.Equal(1_000m, result.PlannedLossAtStop);
        Assert.True(result.IsValid);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Floors_partial_share_quantity()
    {
        // budget 1000, stop distance 3 → 333.333… → floor 333
        var result = PositionRiskSizer.Calculate(
            equity: 100_000m,
            riskPercentPerTrade: 1.0m,
            stopLossPercent: 3.0m,
            price: 100m);

        Assert.Equal(333m, result.Quantity);
        Assert.True(result.PlannedLossAtStop <= result.RiskBudget);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Returns_zero_when_budget_smaller_than_one_share_stop()
    {
        // budget 10, stop distance 50 → floor(0.2) = 0
        var result = PositionRiskSizer.Calculate(
            equity: 1_000m,
            riskPercentPerTrade: 1.0m,
            stopLossPercent: 5.0m,
            price: 1_000m);

        Assert.Equal(0m, result.Quantity);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_equity_yields_zero(decimal equity)
    {
        var result = PositionRiskSizer.Calculate(equity, 1m, 2m, 100m);
        Assert.Equal(0m, result.Quantity);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Non_positive_risk_percent_yields_zero(decimal riskPct)
    {
        var result = PositionRiskSizer.Calculate(100_000m, riskPct, 2m, 100m);
        Assert.Equal(0m, result.Quantity);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_stop_percent_yields_zero(decimal stopPct)
    {
        var result = PositionRiskSizer.Calculate(100_000m, 1m, stopPct, 100m);
        Assert.Equal(0m, result.Quantity);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Non_positive_price_yields_zero(decimal price)
    {
        var result = PositionRiskSizer.Calculate(100_000m, 1m, 2m, price);
        Assert.Equal(0m, result.Quantity);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Half_percent_risk_produces_smaller_size()
    {
        var full = PositionRiskSizer.Calculate(100_000m, 1.0m, 2.0m, 50m);
        var half = PositionRiskSizer.Calculate(100_000m, 0.5m, 2.0m, 50m);

        Assert.Equal(full.Quantity / 2m, half.Quantity);
        Assert.True(half.IsValid);
    }

    [Fact]
    public void Planned_loss_never_exceeds_risk_budget_when_qty_positive()
    {
        var result = PositionRiskSizer.Calculate(50_000m, 1.5m, 1.25m, 87.5m);
        Assert.True(result.Quantity > 0m);
        Assert.True(result.PlannedLossAtStop <= result.RiskBudget + 0.0000001m);
    }
}
