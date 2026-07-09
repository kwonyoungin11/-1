using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

public class EngineTests
{
    [Fact]
    public void Buy_then_sell_with_fees_reduces_cash_vs_no_fee()
    {
        // Flat open path so PnL ≈ pure costs when fees/slip on.
        var candles = SyntheticCandles.Flat(count: 8, price: 100.0);

        var withFees = BacktestEngine.Run(
            candles,
            new ScriptedSignal("fee_on", enterAt: 1, exitAt: 4),
            new BacktestConfig(
                InitialCash: 10_000m,
                FeeRatePerSide: 0.001m,
                SlippageRatePerSide: 0.0005m,
                CooldownBarsAfterExit: 0,
                MaxHoldBars: 100));

        var noFees = BacktestEngine.Run(
            candles,
            new ScriptedSignal("fee_off", enterAt: 1, exitAt: 4),
            new BacktestConfig(
                InitialCash: 10_000m,
                FeeRatePerSide: 0m,
                SlippageRatePerSide: 0m,
                CooldownBarsAfterExit: 0,
                MaxHoldBars: 100));

        Assert.Equal(1, withFees.TradeCount);
        Assert.Equal(1, noFees.TradeCount);
        Assert.True(
            withFees.FinalEquity < noFees.FinalEquity,
            $"fee equity {withFees.FinalEquity} should be < no-fee {noFees.FinalEquity}");
        Assert.True(withFees.FinalEquity < withFees.InitialCash);
        Assert.Equal(noFees.InitialCash, noFees.FinalEquity);
    }

    [Fact]
    public void No_lookahead_entry_price_equals_next_open_plus_slip()
    {
        var slip = 0.0005m;
        var candles = new List<CandlePoint>
        {
            Candle(0, open: 100, high: 101, low: 99, close: 100.5),
            Candle(1, open: 100.5, high: 102, low: 100, close: 101),
            Candle(2, open: 110, high: 111, low: 109, close: 110.5), // entry fill open
            Candle(3, open: 111, high: 112, low: 110, close: 111),
            Candle(4, open: 112, high: 113, low: 111, close: 112), // exit fill open
            Candle(5, open: 112, high: 113, low: 111, close: 112),
        };

        // Signal enter on bar 1 close → fill at bar 2 open
        // Signal exit on bar 3 close → fill at bar 4 open
        var result = BacktestEngine.Run(
            candles,
            new ScriptedSignal("look", enterAt: 1, exitAt: 3),
            new BacktestConfig(
                InitialCash: 10_000m,
                FeeRatePerSide: 0m,
                SlippageRatePerSide: slip,
                CooldownBarsAfterExit: 0,
                MaxHoldBars: 100));

        Assert.Equal(1, result.TradeCount);
        var trade = result.Trades[0];
        var expectedEntry = 110m * (1m + slip);
        var expectedExit = 112m * (1m - slip);
        Assert.Equal(2, trade.EntryIndex);
        Assert.Equal(4, trade.ExitIndex);
        Assert.Equal(expectedEntry, trade.EntryPrice);
        Assert.Equal(expectedExit, trade.ExitPrice);
    }

    [Fact]
    public void Same_candles_yield_identical_result_twice()
    {
        var candles = SyntheticCandles.MeanReverting(count: 200, seed: 42);
        var config = new BacktestConfig(
            InitialCash: 10_000m,
            FeeRatePerSide: 0.001m,
            SlippageRatePerSide: 0.0005m,
            CooldownBarsAfterExit: 3,
            MaxHoldBars: 40);

        var a = BacktestEngine.Run(candles, new CersStrategy(), config);
        var b = BacktestEngine.Run(candles, new CersStrategy(), config);

        Assert.Equal(a.FinalEquity, b.FinalEquity);
        Assert.Equal(a.TotalReturnPct, b.TotalReturnPct);
        Assert.Equal(a.MaxDrawdownPct, b.MaxDrawdownPct);
        Assert.Equal(a.TradeCount, b.TradeCount);
        Assert.Equal(a.Sharpe, b.Sharpe);
        Assert.Equal(a.WinRatePct, b.WinRatePct);
        Assert.Equal(a.ProfitFactor, b.ProfitFactor);
        Assert.Equal(a.Trades.Count, b.Trades.Count);
        for (var i = 0; i < a.Trades.Count; i++)
        {
            Assert.Equal(a.Trades[i], b.Trades[i]);
        }

        Assert.Equal(a.EquityCurve.Count, b.EquityCurve.Count);
        for (var i = 0; i < a.EquityCurve.Count; i++)
        {
            Assert.Equal(a.EquityCurve[i], b.EquityCurve[i]);
        }
    }

    private static CandlePoint Candle(
        int i,
        double open,
        double high,
        double low,
        double close,
        double volume = 1_000)
    {
        var t = new DateTimeOffset(2026, 1, 2, 14, 30, 0, TimeSpan.Zero).AddMinutes(i);
        return new CandlePoint(t, open, high, low, close, volume);
    }

    /// <summary>Enter on one bar index, exit on another (signals on those closes).</summary>
    private sealed class ScriptedSignal : IBarSignalSource
    {
        private readonly int _enterAt;
        private readonly int _exitAt;

        public ScriptedSignal(string name, int enterAt, int exitAt)
        {
            Name = name;
            _enterAt = enterAt;
            _exitAt = exitAt;
        }

        public string Name { get; }

        public void Prepare(IReadOnlyList<CandlePoint> candles)
        {
            ArgumentNullException.ThrowIfNull(candles);
        }

        public BarAction ActionAt(int barIndex)
        {
            if (barIndex == _enterAt)
            {
                return BarAction.EnterLong;
            }

            if (barIndex == _exitAt)
            {
                return BarAction.ExitLong;
            }

            return BarAction.Flat;
        }

        public string? EntryReasonAt(int barIndex) =>
            barIndex == _enterAt ? "scripted_enter" : null;
    }
}
