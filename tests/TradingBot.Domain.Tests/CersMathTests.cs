using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class CersMathTests
{
    [Fact]
    public void ComputeExpectedEdge_empty_returns_empty()
    {
        var result = CersMath.ComputeExpectedEdge(Array.Empty<CandlePoint>());
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeExpectedEdge_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => CersMath.ComputeExpectedEdge(null!));
    }

    [Fact]
    public void ComputeExpectedEdge_synthetic_dip_below_ema_with_low_rsi_has_positive_last()
    {
        // Warm flat → sharp crash so close << EMA21 and RSI depressed.
        var candles = BuildDipSeries(count: 80, level: 100.0, crashBars: 12, dropPerBar: 0.02);
        var edge = CersMath.ComputeExpectedEdge(candles);

        Assert.Equal(candles.Count, edge.Length);
        var last = edge[^1];
        Assert.False(double.IsNaN(last), "last edge should be defined after warm-up + crash");
        Assert.True(last > 0, $"expected positive edge on deep dip, got {last}");
    }

    [Fact]
    public void ComputeExpectedEdge_uptrend_last_edge_near_zero()
    {
        var candles = BuildUptrend(count: 80, start: 50.0, drift: 0.008);
        var edge = CersMath.ComputeExpectedEdge(candles);
        var last = edge[^1];
        Assert.False(double.IsNaN(last));
        Assert.True(last >= -1e-12 && last < 0.01, $"uptrend edge should be ~0, got {last}");
    }

    [Fact]
    public void ComputeExpectedEdge_warmup_is_nan_then_finite()
    {
        var candles = BuildFlat(count: 50, price: 100.0);
        var edge = CersMath.ComputeExpectedEdge(candles);
        Assert.True(double.IsNaN(edge[0]));
        // Autocorr window = 30 requires index >= 30 for first full window.
        Assert.True(double.IsNaN(edge[Math.Min(20, edge.Length - 1)]));
        var late = edge[^1];
        Assert.False(double.IsNaN(late));
    }

    private static IReadOnlyList<CandlePoint> BuildFlat(int count, double price)
    {
        var list = new List<CandlePoint>(count);
        var t0 = DateTimeOffset.Parse("2026-01-02T14:30:00Z");
        for (var i = 0; i < count; i++)
        {
            list.Add(new CandlePoint(
                t0.AddMinutes(i),
                price,
                price * 1.001,
                price * 0.999,
                price,
                1_000));
        }

        return list;
    }

    private static IReadOnlyList<CandlePoint> BuildUptrend(int count, double start, double drift)
    {
        var list = new List<CandlePoint>(count);
        var t0 = DateTimeOffset.Parse("2026-01-02T14:30:00Z");
        var px = start;
        for (var i = 0; i < count; i++)
        {
            var next = px * (1.0 + drift);
            var o = px;
            var c = next;
            list.Add(new CandlePoint(
                t0.AddMinutes(i),
                o,
                Math.Max(o, c) * 1.001,
                Math.Min(o, c) * 0.999,
                c,
                1_000));
            px = next;
        }

        return list;
    }

    private static IReadOnlyList<CandlePoint> BuildDipSeries(
        int count,
        double level,
        int crashBars,
        double dropPerBar)
    {
        var list = new List<CandlePoint>(count);
        var t0 = DateTimeOffset.Parse("2026-01-02T14:30:00Z");
        var warm = count - crashBars;
        for (var i = 0; i < warm; i++)
        {
            list.Add(new CandlePoint(
                t0.AddMinutes(i),
                level,
                level * 1.001,
                level * 0.999,
                level,
                1_200));
        }

        var px = level;
        for (var i = 0; i < crashBars; i++)
        {
            var next = Math.Max(1.0, px * (1.0 - dropPerBar));
            var o = px;
            var c = next;
            var h = o * 1.001;
            var l = c * 0.99; // longer lower wick helps quality slightly
            list.Add(new CandlePoint(
                t0.AddMinutes(warm + i),
                o,
                h,
                l,
                c,
                3_000 + i * 200));
            px = next;
        }

        return list;
    }
}
