using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

/// <summary>Deterministic OHLCV factories for offline backtest unit tests.</summary>
public static class SyntheticCandles
{
    public static IReadOnlyList<CandlePoint> Flat(
        int count,
        double price,
        int startIndex = 0,
        double volume = 1_000)
    {
        var list = new List<CandlePoint>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(Bar(startIndex + i, price, price, price, price, volume));
        }

        return list;
    }

    public static IReadOnlyList<CandlePoint> Trend(
        int count,
        double startPrice,
        double driftPerBar,
        double volume = 1_000,
        int startIndex = 0)
    {
        var list = new List<CandlePoint>(count);
        var px = startPrice;
        for (var i = 0; i < count; i++)
        {
            var next = px * (1.0 + driftPerBar);
            var o = px;
            var c = next;
            var h = Math.Max(o, c) * 1.001;
            var l = Math.Min(o, c) * 0.999;
            list.Add(Bar(startIndex + i, o, h, l, c, volume));
            px = next;
        }

        return list;
    }

    public static IReadOnlyList<CandlePoint> Crash(
        int count,
        double startPrice,
        double dropPerBar,
        double volume = 2_000,
        int startIndex = 0)
    {
        // Always-down path with mild lower wicks (still red closes).
        var list = new List<CandlePoint>(count);
        var px = startPrice;
        for (var i = 0; i < count; i++)
        {
            var next = Math.Max(0.01, px * (1.0 - dropPerBar));
            var o = px;
            var c = next;
            var h = o * 1.001;
            var l = c * 0.995;
            list.Add(Bar(startIndex + i, o, h, l, c, volume * (1.0 + 0.01 * i)));
            px = next;
        }

        return list;
    }

    /// <summary>
    /// Mean-reverting synthetic path with staged dip → hammer reclaim → mean recovery cycles.
    /// Built so cost-aware MR (CERS) can book positive trades vs buy&hold noise.
    /// <paramref name="seed"/> retained for API stability (cycle phase offset).
    /// </summary>
    public static IReadOnlyList<CandlePoint> MeanReverting(
        int count,
        int seed,
        double level = 100.0,
        double amplitude = 0.12,
        int period = 48,
        double volume = 1_500)
    {
        _ = amplitude;
        _ = period;
        var list = new List<CandlePoint>(count);
        var phaseOffset = Math.Abs(seed) % 7;

        // Warm-up flat so EMA/RSI seed cleanly.
        var warm = Math.Min(45, count);
        for (var i = 0; i < warm; i++)
        {
            list.Add(Bar(i, level, level * 1.001, level * 0.999, level, volume));
        }

        var idx = warm;
        var cycle = 0;
        while (idx < count)
        {
            // 1) Controlled sell-off (~8–12%) over 6 red bars — SMA not free-falling forever.
            var dropBars = 6;
            var dropTotal = 0.09 + 0.01 * ((cycle + phaseOffset) % 3);
            var step = 1.0 - dropTotal / dropBars;
            var px = list[^1].Close;
            for (var d = 0; d < dropBars && idx < count; d++, idx++)
            {
                var o = px;
                var c = Math.Max(1.0, o * step);
                var h = o * 1.001;
                var l = c * 0.997;
                list.Add(Bar(idx, o, h, l, c, volume * 1.6));
                px = c;
            }

            // 2) Hammer reclaim bar (green, long lower wick, volume spike) — CERS entry.
            if (idx < count)
            {
                var o = px;
                var c = o * 1.006;
                var l = o * 0.988;
                var h = c * 1.002;
                list.Add(Bar(idx, o, h, l, c, volume * 3.0));
                px = c;
                idx++;
            }

            // 3) Recovery toward mean over ~12 green bars (clears SL, hits mean/TP).
            var recoverBars = 12;
            for (var r = 0; r < recoverBars && idx < count; r++, idx++)
            {
                var o = px;
                var c = Math.Min(level * 1.01, o * 1.008);
                // Last few bars pin to level so EMA touch exit can fire.
                if (r >= recoverBars - 3)
                {
                    c = level + (r - (recoverBars - 3)) * 0.05;
                }

                var h = Math.Max(o, c) * 1.002;
                var l = Math.Min(o, c) * 0.999;
                list.Add(Bar(idx, o, h, l, c, volume * 1.2));
                px = c;
            }

            // 4) Quiet plateau near level (SMA flattens; cooldown friendly).
            for (var p = 0; p < 10 && idx < count; p++, idx++)
            {
                var o = level;
                var c = level;
                list.Add(Bar(idx, o, level * 1.001, level * 0.999, c, volume));
                px = c;
            }

            cycle++;
        }

        return list;
    }

    private static CandlePoint Bar(
        int index,
        double open,
        double high,
        double low,
        double close,
        double volume)
    {
        // Keep high/low consistent.
        high = Math.Max(high, Math.Max(open, close));
        low = Math.Min(low, Math.Min(open, close));
        var t = new DateTimeOffset(2026, 1, 5, 14, 30, 0, TimeSpan.Zero).AddMinutes(index);
        return new CandlePoint(t, open, high, low, close, volume);
    }
}
