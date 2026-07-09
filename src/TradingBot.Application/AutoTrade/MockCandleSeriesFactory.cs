using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// ChartFanatics 스타일 연습 차트: 다크 캔들 + 규모별 초록/빨강 버블.
/// 버블 SizeWeight = 체결 규모(거래대금 ≈ 거래량×가격 또는 수량×가격).
/// </summary>
public static class MockCandleSeriesFactory
{
    public static IReadOnlyList<CandlePoint> CreateSeries(
        string symbol,
        int count,
        DateTimeOffset endUtc,
        double? seedPrice = null)
    {
        var list = new List<CandlePoint>(count);
        var rnd = new Random(HashCode.Combine(symbol, endUtc.Day));
        var price = seedPrice ?? WatchlistCatalog.ChartSeedPrice(symbol);
        // 국내 주식은 원 단위 스케일 유지, 미국은 달러
        var isKr = symbol.Length == 6 && symbol.All(char.IsDigit);
        var t = endUtc.AddMinutes(-count);

        for (var i = 0; i < count; i++)
        {
            var progress = i / (double)Math.Max(1, count - 1);
            double trend;
            if (progress < 0.72)
            {
                trend = (isKr ? 80 : 0.35) + rnd.NextDouble() * (isKr ? 120 : 0.55);
            }
            else
            {
                trend = -(isKr ? 150 : 0.9) - rnd.NextDouble() * (isKr ? 200 : 1.4);
            }

            var open = price;
            var close = Math.Max(isKr ? 1000 : 1, open + trend + (rnd.NextDouble() - 0.5) * (isKr ? 40 : 0.6));
            var high = Math.Max(open, close) + rnd.NextDouble() * (isKr ? 80 : 1.2);
            var low = Math.Min(open, close) - rnd.NextDouble() * (isKr ? 80 : 1.2);
            var volBase = progress > 0.7 ? 2_200_000d : 900_000d;
            var vol = volBase + rnd.Next(0, 1_800_000);
            list.Add(new CandlePoint(t, open, high, low, close, vol));
            price = close;
            t = t.AddMinutes(1);
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
                // 버블 크기 = 체결 규모 (수량 × 가격)
                var size = TradeMarker.NotionalSize(f.Quantity, f.Price);
                return new TradeMarker(f.FilledAtUtc, (double)f.Price, side, label, size);
            })
            .ToList();
    }

    /// <summary>
    /// 봉마다 거래대금(거래량×종가)에 비례한 매수/매도 버블.
    /// SizeWeight가 클수록 원이 큼 = 그날 체결 규모가 큼.
    /// </summary>
    public static IReadOnlyList<TradeMarker> CreateDemoMarkers(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count < 8)
        {
            return Array.Empty<TradeMarker>();
        }

        var rnd = new Random(42);
        var notionals = candles
            .Select(c => TradeMarker.VolumeNotionalSize(c.Volume, c.Close))
            .ToArray();
        var maxN = notionals.Max();
        if (maxN <= 0)
        {
            maxN = 1;
        }

        var markers = new List<TradeMarker>(candles.Count * 2);

        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            var notional = notionals[i];
            var rel = notional / maxN;
            var bubbleCount = rel > 0.75 ? 3 : rel > 0.4 ? 2 : 1;
            var isUp = c.Close >= c.Open;
            var progress = i / (double)Math.Max(1, candles.Count - 1);

            for (var b = 0; b < bubbleCount; b++)
            {
                var buyBias = progress < 0.72
                    ? (isUp ? 0.72 : 0.45)
                    : (isUp ? 0.35 : 0.18);
                var isBuy = rnd.NextDouble() < buyBias;
                var side = isBuy ? TradeMarkerSide.매수 : TradeMarkerSide.매도;

                var mid = (c.High + c.Low) / 2.0;
                var span = Math.Max(0.15, c.High - c.Low);
                var y = mid + (rnd.NextDouble() - 0.5) * span * 0.9;
                if (isBuy)
                {
                    y = Math.Min(y, c.Close + span * 0.15);
                }
                else
                {
                    y = Math.Max(y, c.Close - span * 0.15);
                }

                // 규모 가중: 거래대금 상대값 + 후반 급락 구간 강조
                // LiveCharts Weight로 쓰이므로 0.3~5 범위로 정규화
                var weight = 0.35 + rel * 3.2 + (progress > 0.7 ? 0.9 : 0) + rnd.NextDouble() * 0.5;
                if (b == 0 && rel > 0.85)
                {
                    weight += 1.8; // 초대형 버블 = 초대형 체결 규모
                }

                var label = isBuy ? "매수" : "매도";
                markers.Add(new TradeMarker(c.Time.AddSeconds(b * 7), y, side, label, weight));
            }
        }

        return markers;
    }
}
