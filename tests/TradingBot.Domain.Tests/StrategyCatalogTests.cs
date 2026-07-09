using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class StrategyCatalogTests
{
    [Fact]
    public void All_strategies_are_listed()
    {
        Assert.NotEmpty(StrategyCatalog.All);
        Assert.Contains(TradingStrategyKind.관망만, StrategyCatalog.All);
        Assert.Contains(TradingStrategyKind.단순연습전략, StrategyCatalog.All);
        Assert.Contains(TradingStrategyKind.추세추종, StrategyCatalog.All);
        Assert.Contains(TradingStrategyKind.평균회귀, StrategyCatalog.All);
        Assert.Contains(TradingStrategyKind.모멘텀돌파, StrategyCatalog.All);
    }

    [Fact]
    public void Labels_match_All_count_and_are_non_empty()
    {
        Assert.Equal(StrategyCatalog.All.Count, StrategyCatalog.Labels.Count);
        Assert.All(StrategyCatalog.Labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
    }

    [Fact]
    public void BaseQuantity_관망만_is_zero_others_positive()
    {
        Assert.Equal(0m, StrategyCatalog.BaseQuantity(TradingStrategyKind.관망만));

        foreach (var kind in StrategyCatalog.All)
        {
            var qty = StrategyCatalog.BaseQuantity(kind);
            if (kind == TradingStrategyKind.관망만)
            {
                Assert.Equal(0m, qty);
            }
            else
            {
                Assert.True(qty > 0m, $"BaseQuantity({kind}) must be > 0, was {qty}");
            }
        }
    }

    [Fact]
    public void BaseQuantity_momentum_is_at_least_three()
    {
        Assert.True(StrategyCatalog.BaseQuantity(TradingStrategyKind.모멘텀돌파) >= 3m);
    }

    [Fact]
    public void Describe_is_non_empty_for_every_strategy()
    {
        foreach (var kind in StrategyCatalog.All)
        {
            var description = StrategyCatalog.Describe(kind);
            Assert.False(string.IsNullOrWhiteSpace(description), $"Describe({kind}) empty");
        }
    }
}
