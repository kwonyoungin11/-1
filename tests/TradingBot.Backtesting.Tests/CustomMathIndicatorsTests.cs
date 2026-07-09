using TradingBot.Backtesting;
using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

public class CustomMathIndicatorsTests
{
    [Fact]
    public void Cers_empty_and_short_series_safe()
    {
        Assert.Empty(CustomMathIndicators.Cers(Array.Empty<CandlePoint>()));

        var shortSeries = MakeCandles(10, dipAt: -1);
        var cers = CustomMathIndicators.Cers(shortSeries);
        Assert.Equal(10, cers.Length);
        Assert.All(cers, v => Assert.True(double.IsNaN(v)));
    }

    [Fact]
    public void Cers_produces_non_negative_edge_when_defined()
    {
        // Long enough for EMA21 + RSI14 + ATR14 + vol20 + lag1 W30.
        var candles = MakeCandles(80, dipAt: 70);
        var cers = CustomMathIndicators.Cers(candles);

        Assert.Equal(80, cers.Length);
        var defined = cers.Where(v => !double.IsNaN(v)).ToArray();
        Assert.NotEmpty(defined);
        Assert.All(defined, v => Assert.True(v >= 0.0));
    }

    [Fact]
    public void MathEdge_higher_on_dip_with_volume()
    {
        var flat = MakeCandles(60, dipAt: -1);
        var dipped = MakeCandles(60, dipAt: 55, volSpike: true);

        var edgeFlat = CustomMathIndicators.MathEdge(flat);
        var edgeDip = CustomMathIndicators.MathEdge(dipped);

        var lastFlat = edgeFlat[^1];
        var lastDip = edgeDip[^1];
        Assert.False(double.IsNaN(lastDip));
        // Dip below EMA with volume should yield higher edge than flat series.
        if (!double.IsNaN(lastFlat))
        {
            Assert.True(lastDip >= lastFlat);
        }

        Assert.True(lastDip >= 0.0);
    }

    [Fact]
    public void Lvrs_and_Fei_bounded_when_defined()
    {
        var candles = MakeCandles(80, dipAt: 70, volSpike: true);
        var lvrs = CustomMathIndicators.Lvrs(candles);
        var fei = CustomMathIndicators.Fei(candles);

        foreach (var v in lvrs.Where(x => !double.IsNaN(x)))
        {
            Assert.InRange(v, 0.0, 1.01);
        }

        foreach (var v in fei.Where(x => !double.IsNaN(x)))
        {
            Assert.InRange(v, 0.0, 1.01);
        }
    }

    [Fact]
    public void All_indicators_same_length_as_input()
    {
        var candles = MakeCandles(50, dipAt: 40);
        Assert.Equal(50, CustomMathIndicators.Cers(candles).Length);
        Assert.Equal(50, CustomMathIndicators.MathEdge(candles).Length);
        Assert.Equal(50, CustomMathIndicators.Lvrs(candles).Length);
        Assert.Equal(50, CustomMathIndicators.Fei(candles).Length);
    }

    /// <summary>
    /// Synthetic OHLCV: mild uptrend, optional sharp dip + volume spike at dipAt.
    /// </summary>
    private static List<CandlePoint> MakeCandles(int count, int dipAt, bool volSpike = false)
    {
        var list = new List<CandlePoint>(count);
        var t0 = DateTimeOffset.Parse("2026-03-01T14:30:00Z");
        for (var i = 0; i < count; i++)
        {
            var basePx = 50.0 + i * 0.05;
            if (dipAt >= 0 && i >= dipAt && i < dipAt + 3)
            {
                basePx -= 2.5 + (i - dipAt) * 0.4;
            }

            var open = basePx - 0.05;
            var close = basePx;
            var high = Math.Max(open, close) + 0.15;
            var low = Math.Min(open, close) - 0.35; // lower wick bias
            var vol = 1000.0 + i;
            if (volSpike && dipAt >= 0 && i >= dipAt && i < dipAt + 3)
            {
                vol *= 4.0;
            }

            list.Add(new CandlePoint(t0.AddMinutes(i), open, high, low, close, vol));
        }

        return list;
    }
}
