using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

public class CersIndicatorTests
{
    [Fact]
    public void Cers_higher_when_price_far_below_ema_with_low_rsi()
    {
        // Phase A: elevated level to seed EMA high.
        // Phase B: sharp drop → price << EMA, RSI depressed → CERS should spike.
        var elevated = SyntheticCandles.Flat(count: 40, price: 100.0, startIndex: 0, volume: 1_000);
        var crashIntoOversold = SyntheticCandles.Crash(
            count: 30,
            startPrice: 100.0,
            dropPerBar: 0.03,
            startIndex: 40,
            volume: 5_000);

        var series = elevated.Concat(crashIntoOversold).ToList();
        var cers = CustomMathIndicators.Cers(series);

        // Warm-up requires ~EMA21 + ATR14 + RSI14 + lag30 + volZ20 → use late bars.
        var lateElevated = MaxFinite(cers, from: 30, to: 39);
        var lateCrash = MaxFinite(cers, from: 55, to: series.Count - 1);

        Assert.True(
            lateCrash > lateElevated,
            $"expected crash CERS {lateCrash} > elevated CERS {lateElevated}");
        Assert.True(lateCrash > 0, "oversold deep-discount bar should produce positive CERS");
    }

    [Fact]
    public void Cers_near_zero_when_price_above_ema()
    {
        // Steady uptrend: close tends to sit at/above rising EMA → dev <= 0 → CERS ~0.
        var up = SyntheticCandles.Trend(count: 80, startPrice: 50.0, driftPerBar: 0.01, volume: 1_000);
        var cers = CustomMathIndicators.Cers(up);

        var tail = cers.Skip(40).Where(v => !double.IsNaN(v)).ToArray();
        Assert.NotEmpty(tail);
        Assert.All(tail, v => Assert.True(v >= -1e-9 && v < 0.01, $"unexpected CERS {v} in uptrend"));
    }

    private static double MaxFinite(IReadOnlyList<double> values, int from, int to)
    {
        var max = double.NegativeInfinity;
        var found = false;
        var end = Math.Min(to, values.Count - 1);
        for (var i = Math.Max(0, from); i <= end; i++)
        {
            var v = values[i];
            if (double.IsNaN(v) || double.IsInfinity(v))
            {
                continue;
            }

            found = true;
            if (v > max)
            {
                max = v;
            }
        }

        Assert.True(found, $"no finite CERS values in [{from},{to}]");
        return max;
    }
}
