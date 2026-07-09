using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class TrendFollowParametersTests
{
    [Fact]
    public void CreateSafeDefaults_has_positive_risk_and_threshold()
    {
        var p = TrendFollowParameters.CreateSafeDefaults();

        Assert.Equal(1.0m, p.StopLossR);
        Assert.Equal(2.0m, p.TakeProfitR);
        Assert.Equal(3, p.CooldownBars);
        Assert.Equal(0.15m, p.MinMomentumScore);

        Assert.True(p.StopLossR > 0m);
        Assert.True(p.TakeProfitR > p.StopLossR);
        Assert.True(p.CooldownBars > 0);
        Assert.True(p.MinMomentumScore > 0m);
    }

    [Fact]
    public void Record_equality_holds_for_same_values()
    {
        var a = TrendFollowParameters.CreateSafeDefaults();
        var b = new TrendFollowParameters(1.0m, 2.0m, 3, 0.15m);
        Assert.Equal(a, b);
    }
}
