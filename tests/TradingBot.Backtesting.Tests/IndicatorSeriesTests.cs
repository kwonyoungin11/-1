using TradingBot.Backtesting;
using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

public class IndicatorSeriesTests
{
    [Fact]
    public void Sma_known_values()
    {
        double[] values = [1, 2, 3, 4, 5];
        var sma = IndicatorSeries.Sma(values, 3);

        Assert.Equal(5, sma.Length);
        Assert.True(double.IsNaN(sma[0]));
        Assert.True(double.IsNaN(sma[1]));
        Assert.Equal(2.0, sma[2], precision: 10);
        Assert.Equal(3.0, sma[3], precision: 10);
        Assert.Equal(4.0, sma[4], precision: 10);
    }

    [Fact]
    public void Ema_seed_then_tracks()
    {
        double[] values = [1, 2, 3, 4, 5];
        var ema = IndicatorSeries.Ema(values, 3);
        // k=0.5; seed=2; then 3, 4
        Assert.True(double.IsNaN(ema[0]));
        Assert.True(double.IsNaN(ema[1]));
        Assert.Equal(2.0, ema[2], precision: 10);
        Assert.Equal(3.0, ema[3], precision: 10);
        Assert.Equal(4.0, ema[4], precision: 10);
    }

    [Fact]
    public void Rsi_all_gains_is_100()
    {
        var closes = Enumerable.Range(1, 20).Select(i => 10.0 + i).ToArray();
        var rsi = IndicatorSeries.Rsi(closes, 14);
        Assert.True(double.IsNaN(rsi[13]));
        Assert.Equal(100.0, rsi[14], precision: 10);
    }

    [Fact]
    public void Atr_positive_after_seed()
    {
        var candles = MakeRisingCandles(30);
        var atr = IndicatorSeries.Atr(candles, 14);
        Assert.True(double.IsNaN(atr[12]));
        Assert.False(double.IsNaN(atr[13]));
        Assert.True(atr[13] > 0);
        Assert.True(atr[^1] > 0);
    }

    [Fact]
    public void Lag1Autocorr_flat_returns_near_zero_or_nan_early()
    {
        // Constant price → zero returns after first → autocorr undefined/0 after warm-up.
        var closes = Enumerable.Repeat(100.0, 50).ToArray();
        var rho = IndicatorSeries.Lag1Autocorr(closes, 30);
        for (var i = 0; i < 30; i++)
        {
            Assert.True(double.IsNaN(rho[i]));
        }

        // All-zero returns → den=0 → we return 0.
        Assert.Equal(0.0, rho[30], precision: 10);
    }

    [Fact]
    public void VolumeZScore_zero_on_constant_volume()
    {
        var vols = Enumerable.Repeat(1000.0, 40).ToArray();
        var z = IndicatorSeries.VolumeZScore(vols, 20);
        Assert.True(double.IsNaN(z[18]));
        Assert.Equal(0.0, z[19], precision: 10);
        Assert.Equal(0.0, z[^1], precision: 10);
    }

    private static List<CandlePoint> MakeRisingCandles(int count)
    {
        var list = new List<CandlePoint>(count);
        var t0 = DateTimeOffset.Parse("2026-01-01T14:30:00Z");
        for (var i = 0; i < count; i++)
        {
            var c = 100.0 + i;
            list.Add(new CandlePoint(t0.AddMinutes(i), c - 0.5, c + 0.5, c - 0.8, c, 1000 + i * 10));
        }

        return list;
    }
}
