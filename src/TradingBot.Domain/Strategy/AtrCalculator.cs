namespace TradingBot.Domain;

/// <summary>
/// Wilder ATR (Average True Range) from OHLCV candles.
/// Used for volatility-based stop distance on high-beta names (e.g. SPCX).
/// Not investment advice.
/// </summary>
public static class AtrCalculator
{
    public const int DefaultPeriod = 14;

    /// <summary>
    /// Returns ATR at the last bar, or null if not enough data.
    /// </summary>
    public static double? Compute(IReadOnlyList<CandlePoint> candles, int period = DefaultPeriod)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (period < 1 || candles.Count < period + 1)
        {
            return null;
        }

        var ordered = candles.Count > 1 && candles[0].Time > candles[^1].Time
            ? candles.OrderBy(c => c.Time).ToArray()
            : candles;

        var trs = new double[ordered.Count];
        trs[0] = ordered[0].High - ordered[0].Low;
        for (var i = 1; i < ordered.Count; i++)
        {
            var h = ordered[i].High;
            var l = ordered[i].Low;
            var prevClose = ordered[i - 1].Close;
            var tr = Math.Max(h - l, Math.Max(Math.Abs(h - prevClose), Math.Abs(l - prevClose)));
            trs[i] = tr;
        }

        // Seed: simple average of first `period` TRs (bars 1..period if TR[0] unused optionally)
        double sum = 0;
        for (var i = 1; i <= period; i++)
        {
            sum += trs[i];
        }

        var atr = sum / period;
        for (var i = period + 1; i < trs.Length; i++)
        {
            atr = ((atr * (period - 1)) + trs[i]) / period;
        }

        return atr > 0 && !double.IsNaN(atr) ? atr : null;
    }
}
