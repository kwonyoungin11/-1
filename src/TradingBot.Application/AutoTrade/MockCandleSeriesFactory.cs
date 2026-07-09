using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>고품질 차트 데모용 봉·마커 생성 (실시세 연결 전 연습 데이터).</summary>
public static class MockCandleSeriesFactory
{
    public static IReadOnlyList<CandlePoint> CreateSeries(
        string symbol,
        int count,
        DateTimeOffset endUtc,
        double seedPrice = 190)
    {
        var list = new List<CandlePoint>(count);
        var rnd = new Random(HashCode.Combine(symbol, endUtc.Day));
        var price = seedPrice;
        var t = endUtc.AddMinutes(-count * 5);

        for (var i = 0; i < count; i++)
        {
            var drift = (rnd.NextDouble() - 0.48) * 1.2;
            var open = price;
            var close = Math.Max(1, open + drift);
            var high = Math.Max(open, close) + rnd.NextDouble() * 0.8;
            var low = Math.Min(open, close) - rnd.NextDouble() * 0.8;
            var vol = 1_000_000 + rnd.Next(0, 500_000);
            list.Add(new CandlePoint(t, open, high, low, close, vol));
            price = close;
            t = t.AddMinutes(5);
        }

        return list;
    }

    public static IReadOnlyList<TradeMarker> MarkersFromPaperFills(
        IReadOnlyList<PaperFillRecord> fills)
    {
        return fills
            .Select(f =>
            {
                var side = f.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase)
                    ? TradeMarkerSide.매수
                    : TradeMarkerSide.매도;
                var label = side == TradeMarkerSide.매수 ? "매수" : "매도";
                return new TradeMarker(f.FilledAtUtc, (double)f.Price, side, label);
            })
            .ToList();
    }

    public static IReadOnlyList<TradeMarker> CreateDemoMarkers(IReadOnlyList<CandlePoint> candles)
    {
        if (candles.Count < 20)
        {
            return Array.Empty<TradeMarker>();
        }

        var buy = candles[candles.Count / 3];
        var sell = candles[(candles.Count * 2) / 3];
        return
        [
            new TradeMarker(buy.Time, buy.Low * 0.998, TradeMarkerSide.매수, "매수"),
            new TradeMarker(sell.Time, sell.High * 1.002, TradeMarkerSide.매도, "매도"),
        ];
    }
}
