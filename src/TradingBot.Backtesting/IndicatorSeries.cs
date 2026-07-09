using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Pure deterministic indicator math on <see cref="double"/> arrays / <see cref="CandlePoint"/> lists.
/// Warm-up bars use <see cref="double.NaN"/>. No I/O. Not investment advice.
/// </summary>
public static class IndicatorSeries
{
    /// <summary>Default epsilon for price/volume denominators.</summary>
    public const double Eps = 1e-12;

    /// <summary>
    /// Simple moving average. SMA[i] = mean(values[i-period+1..i]). NaN until index period-1.
    /// </summary>
    public static double[] Sma(IReadOnlyList<double> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = values.Count;
        var result = CreateNaNArray(n);
        if (n < period)
        {
            return result;
        }

        double sum = 0;
        for (var i = 0; i < n; i++)
        {
            sum += values[i];
            if (i >= period)
            {
                sum -= values[i - period];
            }

            if (i >= period - 1)
            {
                result[i] = sum / period;
            }
        }

        return result;
    }

    /// <summary>
    /// Exponential moving average. Seed = SMA of first <paramref name="period"/> values;
    /// subsequent bars use k = 2/(period+1). NaN until seed index (period-1).
    /// </summary>
    public static double[] Ema(IReadOnlyList<double> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = values.Count;
        var result = CreateNaNArray(n);
        if (n < period)
        {
            return result;
        }

        double sum = 0;
        for (var i = 0; i < period; i++)
        {
            sum += values[i];
        }

        var ema = sum / period;
        var seedIndex = period - 1;
        result[seedIndex] = ema;

        var k = 2.0 / (period + 1);
        for (var i = seedIndex + 1; i < n; i++)
        {
            ema = values[i] * k + ema * (1.0 - k);
            result[i] = ema;
        }

        return result;
    }

    /// <summary>
    /// Population standard deviation over a rolling window of <paramref name="period"/>.
    /// NaN until index period-1.
    /// </summary>
    public static double[] Stdev(IReadOnlyList<double> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = values.Count;
        var result = CreateNaNArray(n);
        if (n < period)
        {
            return result;
        }

        for (var i = period - 1; i < n; i++)
        {
            double sum = 0;
            for (var j = i - period + 1; j <= i; j++)
            {
                sum += values[j];
            }

            var mean = sum / period;
            double varSum = 0;
            for (var j = i - period + 1; j <= i; j++)
            {
                var d = values[j] - mean;
                varSum += d * d;
            }

            result[i] = Math.Sqrt(varSum / period);
        }

        return result;
    }

    /// <summary>
    /// Wilder RSI (0–100). Average gain/loss seeded over first <paramref name="period"/>
    /// changes; then smoothed with Wilder. NaN until index == period (needs period deltas).
    /// </summary>
    public static double[] Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = closes.Count;
        var result = CreateNaNArray(n);
        // Need period changes → first RSI at index == period.
        if (n <= period)
        {
            return result;
        }

        double gainSum = 0;
        double lossSum = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0)
            {
                gainSum += change;
            }
            else
            {
                lossSum -= change;
            }
        }

        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;
        result[period] = WilderRsiFromAverages(avgGain, avgLoss);

        for (var i = period + 1; i < n; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0.0;
            var loss = change < 0 ? -change : 0.0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            result[i] = WilderRsiFromAverages(avgGain, avgLoss);
        }

        return result;
    }

    /// <summary>
    /// Wilder ATR series from OHLCV candles.
    /// TR[0] = high-low; TR[i] = max(h-l, |h-prevC|, |l-prevC|).
    /// Seed ATR at index period-1 = SMA of first <paramref name="period"/> TRs;
    /// then ATR[i] = ((ATR[i-1]*(period-1)) + TR[i]) / period. NaN until seed.
    /// </summary>
    public static double[] Atr(IReadOnlyList<CandlePoint> candles, int period = 14)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = candles.Count;
        var result = CreateNaNArray(n);
        if (n < period)
        {
            return result;
        }

        var trs = new double[n];
        trs[0] = candles[0].High - candles[0].Low;
        for (var i = 1; i < n; i++)
        {
            var h = candles[i].High;
            var l = candles[i].Low;
            var prevClose = candles[i - 1].Close;
            trs[i] = Math.Max(h - l, Math.Max(Math.Abs(h - prevClose), Math.Abs(l - prevClose)));
        }

        double sum = 0;
        for (var i = 0; i < period; i++)
        {
            sum += trs[i];
        }

        var atr = sum / period;
        var seedIndex = period - 1;
        result[seedIndex] = atr;

        for (var i = seedIndex + 1; i < n; i++)
        {
            atr = ((atr * (period - 1)) + trs[i]) / period;
            result[i] = atr;
        }

        return result;
    }

    /// <summary>
    /// Volume z-score: (vol - SMA(vol, period)) / (stdev(vol, period) + eps).
    /// NaN until SMA/Stdev available. Does not clip.
    /// </summary>
    public static double[] VolumeZScore(IReadOnlyList<double> volumes, int period = 20, double eps = Eps)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var sma = Sma(volumes, period);
        var std = Stdev(volumes, period);
        var n = volumes.Count;
        var result = CreateNaNArray(n);
        for (var i = 0; i < n; i++)
        {
            if (double.IsNaN(sma[i]) || double.IsNaN(std[i]))
            {
                continue;
            }

            result[i] = (volumes[i] - sma[i]) / (std[i] + eps);
        }

        return result;
    }

    /// <summary>
    /// Lag-1 Pearson autocorrelation of simple returns over a trailing window of
    /// <paramref name="window"/> returns (requires window+1 closes for the first value).
    /// For bar i, uses returns r[i-window+1..i] and correlates consecutive pairs.
    /// NaN until enough history (index &gt;= window).
    /// </summary>
    public static double[] Lag1Autocorr(IReadOnlyList<double> closes, int window = 30, double eps = Eps)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (window < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be at least 2.");
        }

        var n = closes.Count;
        var result = CreateNaNArray(n);
        if (n < window + 1)
        {
            return result;
        }

        // Simple returns: r[i] = (c[i]-c[i-1])/max(c[i-1],eps); r[0] unused.
        var rets = new double[n];
        rets[0] = double.NaN;
        for (var i = 1; i < n; i++)
        {
            rets[i] = (closes[i] - closes[i - 1]) / Math.Max(closes[i - 1], eps);
        }

        // First complete window of `window` returns ends at index `window`
        // (returns at indices 1..window).
        for (var i = window; i < n; i++)
        {
            // Pairs: (r[t-1], r[t]) for t in [i-window+2 .. i] → window-1 pairs.
            var pairCount = window - 1;
            if (pairCount < 1)
            {
                continue;
            }

            double sumX = 0, sumY = 0;
            var startT = i - window + 2;
            for (var t = startT; t <= i; t++)
            {
                sumX += rets[t - 1];
                sumY += rets[t];
            }

            var meanX = sumX / pairCount;
            var meanY = sumY / pairCount;

            double num = 0, denX = 0, denY = 0;
            for (var t = startT; t <= i; t++)
            {
                var dx = rets[t - 1] - meanX;
                var dy = rets[t] - meanY;
                num += dx * dy;
                denX += dx * dx;
                denY += dy * dy;
            }

            var den = Math.Sqrt(denX * denY);
            result[i] = den < eps ? 0.0 : num / den;
        }

        return result;
    }

    /// <summary>
    /// Simple returns series: r[i] = (c[i]-c[i-1])/max(c[i-1],eps). r[0] = NaN.
    /// </summary>
    public static double[] SimpleReturns(IReadOnlyList<double> closes, double eps = Eps)
    {
        ArgumentNullException.ThrowIfNull(closes);
        var n = closes.Count;
        var result = CreateNaNArray(n);
        for (var i = 1; i < n; i++)
        {
            result[i] = (closes[i] - closes[i - 1]) / Math.Max(closes[i - 1], eps);
        }

        return result;
    }

    /// <summary>Clip <paramref name="x"/> to [min, max].</summary>
    public static double Clamp(double x, double min, double max)
    {
        if (x < min)
        {
            return min;
        }

        if (x > max)
        {
            return max;
        }

        return x;
    }

    /// <summary>Extract close prices from candles.</summary>
    public static double[] Closes(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = candles[i].Close;
        }

        return result;
    }

    /// <summary>Extract volumes from candles.</summary>
    public static double[] Volumes(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = candles[i].Volume;
        }

        return result;
    }

    /// <summary>
    /// Lower wick quality in [0, 1]: (min(open,close) - low) / max(high-low, eps).
    /// High value = long lower shadow (rejection of lows).
    /// </summary>
    public static double[] LowerWickRatio(IReadOnlyList<CandlePoint> candles, double eps = Eps)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            var c = candles[i];
            var range = c.High - c.Low;
            var bodyLow = Math.Min(c.Open, c.Close);
            result[i] = (bodyLow - c.Low) / Math.Max(range, eps);
            if (result[i] < 0)
            {
                result[i] = 0;
            }
            else if (result[i] > 1)
            {
                result[i] = 1;
            }
        }

        return result;
    }

    private static double WilderRsiFromAverages(double avgGain, double avgLoss)
    {
        if (avgLoss == 0.0)
        {
            return avgGain == 0.0 ? 50.0 : 100.0;
        }

        var rs = avgGain / avgLoss;
        return 100.0 - (100.0 / (1.0 + rs));
    }

    private static double[] CreateNaNArray(int n)
    {
        var result = new double[n];
        Array.Fill(result, double.NaN);
        return result;
    }
}
