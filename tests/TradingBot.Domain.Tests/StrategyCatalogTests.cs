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
        Assert.Contains(TradingStrategyKind.일분분할스캘프, StrategyCatalog.All);
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
    public void BaseQuantity_one_minute_split_scalp_is_six_divisible_by_three_legs()
    {
        var qty = StrategyCatalog.BaseQuantity(TradingStrategyKind.일분분할스캘프);
        Assert.Equal(6m, qty);
        Assert.Equal(0m, qty % 3m);
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

    [Fact]
    public void Describe_trend_follow_is_honest_not_investment_advice()
    {
        var description = StrategyCatalog.Describe(TradingStrategyKind.추세추종);
        Assert.Contains("투자 조언 아님", description, StringComparison.Ordinal);
        Assert.DoesNotContain("보장", description, StringComparison.Ordinal);
        Assert.DoesNotContain("추천 종목", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_one_minute_split_scalp_is_practice_with_fee_warning()
    {
        var description = StrategyCatalog.Describe(TradingStrategyKind.일분분할스캘프);
        Assert.Contains("15m", description, StringComparison.Ordinal);
        Assert.Contains("분할", description, StringComparison.Ordinal);
        Assert.Contains("수수료", description, StringComparison.Ordinal);
        Assert.Contains("dry-run", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("투자 조언 아님", description, StringComparison.Ordinal);
        Assert.DoesNotContain("보장", description, StringComparison.Ordinal);
    }
}
