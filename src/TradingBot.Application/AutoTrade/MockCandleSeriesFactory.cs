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
        double? seedPrice = null,
        TimeSpan? barStep = null)
    {
        var list = new List<CandlePoint>(count);
        var rnd = new Random(HashCode.Combine(symbol, endUtc.Day));
        var price = seedPrice ?? WatchlistCatalog.ChartSeedPrice(symbol);
        // 국내 주식은 원 단위 스케일 유지, 미국은 달러
        var isKr = symbol is { Length: 6 } && symbol.All(char.IsDigit);
        var step = barStep is { TotalSeconds: > 0 } s ? s : TimeSpan.FromMinutes(1);
        var t = endUtc - step * count;

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

            // Scale trend for multi-day bars
            if (step.TotalHours >= 20)
            {
                trend *= 8;
            }

            var open = price;
            var close = Math.Max(isKr ? 1000 : 1, open + trend + (rnd.NextDouble() - 0.5) * (isKr ? 40 : 0.6));
            var high = Math.Max(open, close) + rnd.NextDouble() * (isKr ? 80 : 1.2);
            var low = Math.Min(open, close) - rnd.NextDouble() * (isKr ? 80 : 1.2);
            var volBase = progress > 0.7 ? 2_200_000d : 900_000d;
            var vol = volBase + rnd.Next(0, 1_800_000);
            list.Add(new CandlePoint(t, open, high, low, close, vol));
            price = close;
            t = t.Add(step);
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
    /// 실데이터/연습 공통: 봉마다 거래대금 버블 1개 (ChartFanatics).
    /// 양봉 → 매수(초록), 음봉 → 매도(빨강). SizeWeight = √(상대 거래대금) 스케일 0.4~5.
    /// 난수 데모 없음 — 실봉 volume×close 기반.
    /// </summary>
    public static IReadOnlyList<TradeMarker> CreateVolumeBubbles(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
        {
            return Array.Empty<TradeMarker>();
        }

        var notionals = candles
            .Select(c => TradeMarker.VolumeNotionalSize(c.Volume, c.Close))
            .ToArray();
        var maxN = notionals.Max();
        if (maxN <= 0)
        {
            maxN = 1;
        }

        var markers = new List<TradeMarker>(candles.Count);
        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            var rel = notionals[i] / maxN;
            // √ 스케일: 초대형 봉만 거대, 중간 봉도 구분
            var weight = 0.4 + Math.Sqrt(Math.Clamp(rel, 0, 1)) * 4.6;
            var isUp = c.Close >= c.Open;
            var side = isUp ? TradeMarkerSide.매수 : TradeMarkerSide.매도;
            var label = isUp ? "상승규모" : "하락규모";
            markers.Add(new TradeMarker(c.Time, c.Close, side, label, weight));
        }

        return markers;
    }

    /// <summary>
    /// 하위 호환: 데모 = 볼륨 버블 (실데이터와 동일 규칙).
    /// </summary>
    public static IReadOnlyList<TradeMarker> CreateDemoMarkers(IReadOnlyList<CandlePoint> candles) =>
        CreateVolumeBubbles(candles);
}
