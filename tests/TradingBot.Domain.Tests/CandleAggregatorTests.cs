using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class CandleAggregatorTests
{
    [Fact]
    public void Empty_source_returns_empty()
    {
        Assert.Empty(CandleAggregator.Aggregate(Array.Empty<CandlePoint>(), ChartTimeframe.분봉5));
    }

    [Fact]
    public void No_aggregation_for_1m_and_1d()
    {
        var src = MakeMinutes(6, start: DateTimeOffset.Parse("2026-03-25T10:00:00Z"));
        var a = CandleAggregator.Aggregate(src, ChartTimeframe.분봉1);
        Assert.Equal(6, a.Count);
        var d = CandleAggregator.Aggregate(src, ChartTimeframe.일봉);
        Assert.Equal(6, d.Count);
    }

    [Fact]
    public void Aggregate_5m_ohlcv_rules()
    {
        // 10:00, 10:01, 10:02, 10:03, 10:04 → one 5m bucket at 10:00
        var t0 = DateTimeOffset.Parse("2026-03-25T10:00:00Z");
        var src = new List<CandlePoint>
        {
            new(t0, 100, 105, 99, 102, 10),
            new(t0.AddMinutes(1), 102, 110, 101, 108, 20),
            new(t0.AddMinutes(2), 108, 109, 100, 101, 5),
            new(t0.AddMinutes(3), 101, 103, 100, 102, 7),
            new(t0.AddMinutes(4), 102, 104, 98, 99, 8),
        };
        var agg = CandleAggregator.Aggregate(src, ChartTimeframe.분봉5);
        Assert.Single(agg);
        var b = agg[0];
        Assert.Equal(t0, b.Time);
        Assert.Equal(100, b.Open);
        Assert.Equal(110, b.High);
        Assert.Equal(98, b.Low);
        Assert.Equal(99, b.Close);
        Assert.Equal(50, b.Volume);
    }

    [Fact]
    public void Aggregate_15m_two_buckets()
    {
        var t0 = DateTimeOffset.Parse("2026-03-25T10:00:00Z");
        var src = MakeMinutes(20, t0);
        var agg = CandleAggregator.Aggregate(src, ChartTimeframe.분봉15);
        Assert.Equal(2, agg.Count); // 10:00 and 10:15
        Assert.Equal(t0, agg[0].Time);
        Assert.Equal(t0.AddMinutes(15), agg[1].Time);
        Assert.Equal(src.Sum(c => c.Volume), agg.Sum(c => c.Volume), 3);
    }

    [Fact]
    public void Weekly_monday_utc_bucket()
    {
        // Wednesday 2026-03-25 → week start Monday 2026-03-23
        var wed = DateTimeOffset.Parse("2026-03-25T15:00:00Z");
        var mon = CandleAggregator.FloorToMondayUtc(wed);
        Assert.Equal(DayOfWeek.Monday, mon.DayOfWeek);
        Assert.Equal(new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.Zero), mon);

        var days = new[]
        {
            new CandlePoint(DateTimeOffset.Parse("2026-03-23T00:00:00Z"), 10, 12, 9, 11, 1),
            new CandlePoint(DateTimeOffset.Parse("2026-03-24T00:00:00Z"), 11, 13, 10, 12, 2),
            new CandlePoint(DateTimeOffset.Parse("2026-03-25T00:00:00Z"), 12, 14, 11, 13, 3),
        };
        var w = CandleAggregator.Aggregate(days, ChartTimeframe.주봉);
        Assert.Single(w);
        Assert.Equal(10, w[0].Open);
        Assert.Equal(14, w[0].High);
        Assert.Equal(9, w[0].Low);
        Assert.Equal(13, w[0].Close);
        Assert.Equal(6, w[0].Volume);
    }

    [Theory]
    [InlineData("1m", ChartTimeframe.분봉1)]
    [InlineData("5m", ChartTimeframe.분봉5)]
    [InlineData("15m", ChartTimeframe.분봉15)]
    [InlineData("60m", ChartTimeframe.분봉60)]
    [InlineData("240m", ChartTimeframe.분봉240)]
    [InlineData("1D", ChartTimeframe.일봉)]
    [InlineData("1W", ChartTimeframe.주봉)]
    public void Catalog_try_parse_and_source_interval(string label, ChartTimeframe expected)
    {
        Assert.True(ChartTimeframeCatalog.TryParse(label, out var tf));
        Assert.Equal(expected, tf);
        var src = ChartTimeframeCatalog.SourceTossInterval(tf);
        Assert.True(src is "1m" or "1d");
        if (tf is ChartTimeframe.분봉5 or ChartTimeframe.분봉15 or ChartTimeframe.분봉240)
        {
            Assert.Equal("1m", src);
            Assert.True(ChartTimeframeCatalog.NeedsAggregation(tf));
        }

        if (tf == ChartTimeframe.주봉)
        {
            Assert.Equal("1d", src);
            Assert.True(ChartTimeframeCatalog.NeedsAggregation(tf));
        }
    }

    [Fact]
    public void Catalog_has_nine_timeframes()
    {
        Assert.Equal(9, ChartTimeframeCatalog.All.Count);
        Assert.Equal(9, ChartTimeframeCatalog.Labels.Count);
        Assert.Contains("1m", ChartTimeframeCatalog.Labels);
        Assert.Contains("15m", ChartTimeframeCatalog.Labels);
        Assert.Contains("1W", ChartTimeframeCatalog.Labels);
    }

    private static List<CandlePoint> MakeMinutes(int count, DateTimeOffset start)
    {
        var list = new List<CandlePoint>(count);
        var px = 100.0;
        for (var i = 0; i < count; i++)
        {
            var o = px;
            var c = px + 0.5;
            list.Add(new CandlePoint(start.AddMinutes(i), o, c + 0.2, o - 0.2, c, 10 + i));
            px = c;
        }

        return list;
    }
}
