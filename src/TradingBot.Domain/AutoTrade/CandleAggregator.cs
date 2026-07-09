namespace TradingBot.Domain;

/// <summary>
/// OHLCV 봉 집계. 토스 원본 1m/1d → UI 5m…240m / 주봉.
/// 규칙: O=첫 open, H=max high, L=min low, C=마지막 close, V=sum volume.
/// 주봉: UTC 기준 월요일 00:00 시작 주 버킷 (문서화 고정).
/// </summary>
public static class CandleAggregator
{
    /// <summary>
    /// <paramref name="source"/> 를 시간순(오름차순)으로 정렬한 뒤 대상 TF 로 집계.
    /// 집계 불필요 TF 는 복사본 반환. 빈 입력 → 빈 출력.
    /// </summary>
    public static IReadOnlyList<CandlePoint> Aggregate(
        IReadOnlyList<CandlePoint> source,
        ChartTimeframe target)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count == 0)
        {
            return Array.Empty<CandlePoint>();
        }

        if (!ChartTimeframeCatalog.NeedsAggregation(target))
        {
            return source.OrderBy(c => c.Time).ToArray();
        }

        var ordered = source.OrderBy(c => c.Time).ToList();

        if (ChartTimeframeCatalog.IsWeeklyAggregation(target))
        {
            return AggregateByWeekMondayUtc(ordered);
        }

        var minutes = ChartTimeframeCatalog.AggregationMinutes(target)
                      ?? throw new InvalidOperationException($"No aggregation minutes for {target}");
        return AggregateByMinutes(ordered, minutes);
    }

    public static IReadOnlyList<CandlePoint> AggregateByMinutes(
        IReadOnlyList<CandlePoint> orderedAscending,
        int minutes)
    {
        ArgumentNullException.ThrowIfNull(orderedAscending);
        if (minutes <= 1)
        {
            return orderedAscending.ToArray();
        }

        if (orderedAscending.Count == 0)
        {
            return Array.Empty<CandlePoint>();
        }

        var buckets = new Dictionary<long, List<CandlePoint>>();
        foreach (var c in orderedAscending)
        {
            var bucketStart = FloorToMinuteBucket(c.Time, minutes);
            var key = bucketStart.UtcTicks;
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<CandlePoint>();
                buckets[key] = list;
            }

            list.Add(c);
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => MergeBucket(new DateTimeOffset(kv.Key, TimeSpan.Zero), kv.Value))
            .ToArray();
    }

    /// <summary>UTC Monday 00:00 시작 주 버킷.</summary>
    public static IReadOnlyList<CandlePoint> AggregateByWeekMondayUtc(
        IReadOnlyList<CandlePoint> orderedAscending)
    {
        ArgumentNullException.ThrowIfNull(orderedAscending);
        if (orderedAscending.Count == 0)
        {
            return Array.Empty<CandlePoint>();
        }

        var buckets = new Dictionary<long, List<CandlePoint>>();
        foreach (var c in orderedAscending)
        {
            var weekStart = FloorToMondayUtc(c.Time);
            var key = weekStart.UtcTicks;
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<CandlePoint>();
                buckets[key] = list;
            }

            list.Add(c);
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => MergeBucket(new DateTimeOffset(kv.Key, TimeSpan.Zero), kv.Value))
            .ToArray();
    }

    public static DateTimeOffset FloorToMinuteBucket(DateTimeOffset t, int minutes)
    {
        var utc = t.ToUniversalTime();
        var totalMinutes = (long)(utc - DateTimeOffset.UnixEpoch).TotalMinutes;
        var floored = totalMinutes - (totalMinutes % minutes);
        return DateTimeOffset.UnixEpoch.AddMinutes(floored);
    }

    public static DateTimeOffset FloorToMondayUtc(DateTimeOffset t)
    {
        var utc = t.ToUniversalTime();
        var date = utc.Date; // midnight UTC of that day
        // DayOfWeek: Sunday=0 ... Saturday=6. Monday=1 → days since Monday
        var dow = (int)date.DayOfWeek;
        var daysFromMonday = dow == 0 ? 6 : dow - 1;
        var monday = date.AddDays(-daysFromMonday);
        return new DateTimeOffset(monday, TimeSpan.Zero);
    }

    private static CandlePoint MergeBucket(DateTimeOffset bucketTime, List<CandlePoint> bars)
    {
        // bars assumed chronological insertion; re-order to be safe
        bars.Sort((a, b) => a.Time.CompareTo(b.Time));
        var first = bars[0];
        var last = bars[^1];
        var high = bars.Max(b => b.High);
        var low = bars.Min(b => b.Low);
        var vol = bars.Sum(b => b.Volume);
        return new CandlePoint(bucketTime, first.Open, high, low, last.Close, vol);
    }
}
