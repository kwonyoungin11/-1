namespace TradingBot.Backtesting.Tests;

public class StrategyEdgeTests
{
    [Fact]
    public void Mean_reverting_series_cers_ends_not_worse_than_buyhold_or_is_profitable()
    {
        var candles = SyntheticCandles.MeanReverting(count: 500, seed: 7, amplitude: 0.14, period: 48);
        // Engine max-hold disabled (0) so BuyHold is a true full-sample baseline.
        var config = new BacktestConfig(
            InitialCash: 10_000m,
            FeeRatePerSide: 0.001m,
            SlippageRatePerSide: 0.0005m,
            CooldownBarsAfterExit: 3,
            MaxHoldBars: 0,
            PeriodsPerYear: 252);

        var cers = BacktestEngine.Run(candles, new CersStrategy(), config);
        var buyHold = BacktestEngine.Run(candles, new BuyHoldStrategy(), config);

        var edgeOk = cers.FinalEquity >= buyHold.FinalEquity || cers.TotalReturnPct > 0m;
        Assert.True(
            edgeOk,
            $"CERS equity={cers.FinalEquity} ret={cers.TotalReturnPct}% trades={cers.TradeCount}; "
            + $"BuyHold equity={buyHold.FinalEquity} ret={buyHold.TotalReturnPct}%");
    }

    [Fact]
    public void Crash_series_cers_does_not_lose_as_much_as_buyhold()
    {
        // Prolonged decline: CERS crash filter should stay in cash more than buy&hold.
        var candles = SyntheticCandles.Crash(count: 250, startPrice: 100.0, dropPerBar: 0.012);
        var config = new BacktestConfig(
            InitialCash: 10_000m,
            FeeRatePerSide: 0.001m,
            SlippageRatePerSide: 0.0005m,
            CooldownBarsAfterExit: 3,
            MaxHoldBars: 0,
            PeriodsPerYear: 252);

        var cers = BacktestEngine.Run(candles, new CersStrategy(), config);
        var buyHold = BacktestEngine.Run(candles, new BuyHoldStrategy(), config);

        Assert.True(
            cers.FinalEquity >= buyHold.FinalEquity,
            $"CERS final {cers.FinalEquity} should protect vs BuyHold {buyHold.FinalEquity}");
        Assert.True(
            cers.MaxDrawdownPct <= buyHold.MaxDrawdownPct + 0.000001m,
            $"CERS MDD {cers.MaxDrawdownPct} should not exceed BuyHold MDD {buyHold.MaxDrawdownPct}");
        Assert.True(
            cers.TotalReturnPct >= buyHold.TotalReturnPct,
            $"CERS ret {cers.TotalReturnPct}% vs BuyHold {buyHold.TotalReturnPct}%");
    }

    [Fact]
    public void Determinism_cers_and_buyhold_stable_across_runs()
    {
        var candles = SyntheticCandles.MeanReverting(count: 180, seed: 99);
        var config = new BacktestConfig(InitialCash: 10_000m);

        var c1 = BacktestEngine.Run(candles, new CersStrategy(), config);
        var c2 = BacktestEngine.Run(candles, new CersStrategy(), config);
        var b1 = BacktestEngine.Run(candles, new BuyHoldStrategy(), config);
        var b2 = BacktestEngine.Run(candles, new BuyHoldStrategy(), config);

        Assert.Equal(c1.FinalEquity, c2.FinalEquity);
        Assert.Equal(c1.TradeCount, c2.TradeCount);
        Assert.Equal(b1.FinalEquity, b2.FinalEquity);
        Assert.Equal(b1.TradeCount, b2.TradeCount);
        Assert.Contains("simulation", c1.Notes, StringComparison.OrdinalIgnoreCase);
    }
}
