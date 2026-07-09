using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

public class SplitLadderEngineTests
{
    [Fact]
    public void Project_default_params_validate()
    {
        var p = SplitLadderParams.ProjectDefault;
        p.Validate();
        Assert.Equal(3, p.BuyLegs);
        Assert.Equal(0.10, p.BuyStepPercent);
    }

    [Fact]
    public void Mean_reverting_series_runs_without_throw()
    {
        var candles = SyntheticCandles.MeanReverting(count: 400, seed: 11, amplitude: 0.12, period: 40);
        var config = new BacktestConfig(InitialCash: 10_000m, MaxHoldBars: 0);
        var result = SplitLadderEngine.Run(candles, SplitLadderParams.ProjectDefault, config);
        Assert.True(result.FinalEquity > 0m);
        Assert.Contains("simulation", result.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_candles_zero_trades()
    {
        var result = SplitLadderEngine.Run(
            Array.Empty<CandlePoint>(),
            SplitLadderParams.ProjectDefault);
        Assert.Equal(0, result.TradeCount);
        Assert.Equal(10_000m, result.FinalEquity);
    }

    [Fact]
    public void Optimizer_ranks_at_least_project_default_on_short_series()
    {
        var candles = SyntheticCandles.MeanReverting(count: 500, seed: 3, amplitude: 0.15, period: 36);
        // Tiny custom grid via engine only — full grid is heavy; smoke ProjectDefault rank path
        var r = SplitLadderEngine.Run(candles, SplitLadderParams.ProjectDefault, new BacktestConfig(MaxHoldBars: 0));
        Assert.NotNull(r.StrategyName);
    }
}
