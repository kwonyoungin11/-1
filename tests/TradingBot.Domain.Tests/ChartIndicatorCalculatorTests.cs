using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class ChartIndicatorCalculatorTests
{
    [Fact]
    public void Sma_known_values_on_small_series()
    {
        // closes: 1,2,3,4,5  period 3
        // index 0,1 null; 2=(1+2+3)/3=2; 3=(2+3+4)/3=3; 4=(3+4+5)/3=4
        double[] closes = [1, 2, 3, 4, 5];
        var sma = ChartIndicatorCalculator.Sma(closes, 3);

        Assert.Equal(5, sma.Count);
        Assert.Null(sma[0]);
        Assert.Null(sma[1]);
        Assert.Equal(2.0, sma[2]!.Value, precision: 10);
        Assert.Equal(3.0, sma[3]!.Value, precision: 10);
        Assert.Equal(4.0, sma[4]!.Value, precision: 10);
    }

    [Fact]
    public void Ema_null_until_seed_then_tracks_with_known_seed()
    {
        // period 3: seed at i=2 is SMA(1,2,3)=2
        // k = 2/(3+1) = 0.5
        // i=3: 4*0.5 + 2*0.5 = 3
        // i=4: 5*0.5 + 3*0.5 = 4
        double[] closes = [1, 2, 3, 4, 5];
        var ema = ChartIndicatorCalculator.Ema(closes, 3);

        Assert.Equal(5, ema.Count);
        Assert.Null(ema[0]);
        Assert.Null(ema[1]);
        Assert.Equal(2.0, ema[2]!.Value, precision: 10);
        Assert.Equal(3.0, ema[3]!.Value, precision: 10);
        Assert.Equal(4.0, ema[4]!.Value, precision: 10);
    }

    [Fact]
    public void Ema_monotonic_after_seed_on_rising_series()
    {
        double[] closes = Enumerable.Range(1, 30).Select(i => (double)i).ToArray();
        var ema = ChartIndicatorCalculator.Ema(closes, 9);

        Assert.Null(ema[7]);
        Assert.NotNull(ema[8]);

        for (var i = 9; i < ema.Count; i++)
        {
            Assert.NotNull(ema[i]);
            Assert.True(ema[i]!.Value > ema[i - 1]!.Value, $"EMA should rise at index {i}");
        }
    }

    [Fact]
    public void Rsi_null_early_bars_and_bounded_when_defined()
    {
        // Mixed up/down so RSI is not stuck at 0 or 100.
        double[] closes =
        [
            44, 44.34, 44.09, 43.61, 44.33, 44.83, 45.10, 45.42,
            45.84, 46.08, 45.89, 46.03, 45.61, 46.28, 46.28, 46.00,
            46.03, 46.41, 46.22, 45.64,
        ];
        var rsi = ChartIndicatorCalculator.Rsi(closes, period: 14);

        Assert.Equal(closes.Length, rsi.Count);
        for (var i = 0; i < 14; i++)
        {
            Assert.Null(rsi[i]);
        }

        for (var i = 14; i < rsi.Count; i++)
        {
            Assert.NotNull(rsi[i]);
            Assert.InRange(rsi[i]!.Value, 0.0, 100.0);
        }
    }

    [Fact]
    public void Rsi_all_gains_is_100_after_seed()
    {
        double[] closes = Enumerable.Range(1, 20).Select(i => 10.0 + i).ToArray();
        var rsi = ChartIndicatorCalculator.Rsi(closes, period: 14);

        Assert.Null(rsi[13]);
        Assert.Equal(100.0, rsi[14]!.Value, precision: 10);
        Assert.Equal(100.0, rsi[^1]!.Value, precision: 10);
    }

    [Fact]
    public void ForStrategy_추세추종_includes_sma_and_ema_not_rsi()
    {
        var candles = Enumerable.Range(0, 80)
            .Select(i => new CandlePoint(
                DateTimeOffset.UtcNow.AddMinutes(i - 80),
                100 + i * 0.1,
                101 + i * 0.1,
                99 + i * 0.1,
                100.5 + i * 0.1,
                1_000))
            .ToList();

        var lines = ChartIndicatorCalculator.ForStrategy(candles, TradingStrategyKind.추세추종);
        var names = lines.Select(l => l.Name).ToArray();

        Assert.Equal(["SMA20", "SMA60", "EMA9", "EMA21"], names);
        Assert.DoesNotContain(lines, l => l.Name.StartsWith("RSI", StringComparison.Ordinal));

        var ema9 = lines.Single(l => l.Name == "EMA9").Values;
        Assert.Null(ema9[7]);
        Assert.NotNull(ema9[8]);
        Assert.Equal(candles.Count, ema9.Count);
    }

    [Fact]
    public void Ema_and_Rsi_throw_on_invalid_period()
    {
        double[] closes = [1, 2, 3];
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartIndicatorCalculator.Ema(closes, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartIndicatorCalculator.Rsi(closes, 0));
    }

    [Fact]
    public void ForStrategy_CERS비용회귀_includes_EMA21_and_CERS_lines()
    {
        var candles = Enumerable.Range(0, 80)
            .Select(i => new CandlePoint(
                DateTimeOffset.UtcNow.AddMinutes(i - 80),
                100 + i * 0.1,
                101 + i * 0.1,
                99 + i * 0.1,
                100.5 + i * 0.1,
                1_000))
            .ToList();

        var lines = ChartIndicatorCalculator.ForStrategy(candles, TradingStrategyKind.CERS비용회귀);
        var names = lines.Select(l => l.Name).ToArray();

        Assert.Contains("EMA21", names);
        Assert.True(
            names.Any(n => n is "CERS" or "CERS edge"),
            $"expected CERS line name, got [{string.Join(", ", names)}]");
    }

    [Fact]
    public void ForStrategy_CERS비용회귀_CERS_line_length_equals_candle_count()
    {
        var candles = Enumerable.Range(0, 60)
            .Select(i => new CandlePoint(
                DateTimeOffset.UtcNow.AddMinutes(i - 60),
                50 + i * 0.05,
                51 + i * 0.05,
                49 + i * 0.05,
                50.2 + i * 0.05,
                2_000))
            .ToList();

        var lines = ChartIndicatorCalculator.ForStrategy(candles, TradingStrategyKind.CERS비용회귀);
        var cers = lines.Single(l => l.Name is "CERS" or "CERS edge");

        Assert.Equal(candles.Count, cers.Values.Count);
        Assert.Equal(candles.Count, lines.Single(l => l.Name == "EMA21").Values.Count);
    }
}
