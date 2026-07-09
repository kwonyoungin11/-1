namespace TradingBot.Domain;

/// <summary>
/// CERS (Cost-aware Edge Reversion Score) pure math on candle series.
/// Formula matches backtest <c>CustomMathIndicators.Cers</c>.
/// Domain-self-contained (no Backtesting reference). Warm-up = NaN.
/// Not investment advice. Not an order.
/// </summary>
public static class CersMath
{
    public const double Eps = 1e-12;

    /// <summary>
    /// Per-bar expected reversion edge (fraction). Compare to <see cref="CersPreset.EntryThreshold"/>.
    /// </summary>
    /// <code>
    /// mu = EMA21, dev = max(0, (mu - close) / close)
    /// kappa = clamp(1 + rho_lag1(W=30), 0.25, 2)
    /// rsi_boost = max(0, (35 - RSI14) / 35)
    /// vol_z = clip(volume z-score 20, 0, 3)
    /// quality = 0.45 + 0.35*rsi_boost + 0.20*min(1, vol_z/2)
    /// expected = kappa * dev * quality * (0.85 + 0.15*lower_wick)
    /// </code>
    public static double[] ComputeExpectedEdge(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        if (n == 0)
        {
            return Array.Empty<double>();
        }

        var closes = Closes(candles);
        var volumes = Volumes(candles);

        var mu = Ema(closes, CersPreset.EmaPeriod);
        var atr = Atr(candles, CersPreset.AtrPeriod);
        var rsi = Rsi(closes, CersPreset.RsiPeriod);
        var volZRaw = VolumeZScore(volumes, CersPreset.VolSmaPeriod);
        var rho = Lag1Autocorr(closes, CersPreset.AutocorrWindow);
        var lowerWick = LowerWickRatio(candles);

        var result = new double[n];
        Array.Fill(result, double.NaN);

        for (var i = 0; i < n; i++)
        {
            if (double.IsNaN(mu[i]) || double.IsNaN(atr[i]) || double.IsNaN(rsi[i])
                || double.IsNaN(volZRaw[i]) || double.IsNaN(rho[i]))
            {
                continue;
            }

            var close = Math.Max(closes[i], Eps);
            // ATR-normalized sigma is diagnostic-only in the backtest formula.
            _ = atr[i] / close;

            var dev = Math.Max(0.0, (mu[i] - closes[i]) / close);
            var kappa = Clamp(1.0 + rho[i], 0.25, 2.0);
            var rsiBoost = Math.Max(0.0, (35.0 - rsi[i]) / 35.0);
            var volZ = Clamp(volZRaw[i], 0.0, 3.0);
            var quality = 0.45 + 0.35 * rsiBoost + 0.20 * Math.Min(1.0, volZ / 2.0);
            var expected = kappa * dev * quality;
            expected *= 0.85 + 0.15 * lowerWick[i];
            result[i] = expected;
        }

        return result;
    }

    /// <summary>Last finite expected edge, or null if none.</summary>
    public static double? LastExpectedEdge(IReadOnlyList<CandlePoint> candles)
    {
        var series = ComputeExpectedEdge(candles);
        for (var i = series.Length - 1; i >= 0; i--)
        {
            if (!double.IsNaN(series[i]) && !double.IsInfinity(series[i]))
            {
                return series[i];
            }
        }

        return null;
    }

    /// <summary>EMA of closes at last bar (or null if warm-up incomplete).</summary>
    public static double? LastEma(IReadOnlyList<CandlePoint> candles, int period = CersPreset.EmaPeriod)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
        {
            return null;
        }

        var ema = Ema(Closes(candles), period);
        var last = ema[^1];
        return double.IsNaN(last) ? null : last;
    }

    // ── Indicator helpers (ported from Backtesting.IndicatorSeries) ──────────

    internal static double[] Ema(IReadOnlyList<double> values, int period)
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

    internal static double[] Sma(IReadOnlyList<double> values, int period)
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

    internal static double[] Stdev(IReadOnlyList<double> values, int period)
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

    internal static double[] Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = closes.Count;
        var result = CreateNaNArray(n);
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

    internal static double[] Atr(IReadOnlyList<CandlePoint> candles, int period = 14)
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

    internal static double[] VolumeZScore(IReadOnlyList<double> volumes, int period = 20, double eps = Eps)
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

    internal static double[] Lag1Autocorr(IReadOnlyList<double> closes, int window = 30, double eps = Eps)
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

        var rets = new double[n];
        rets[0] = double.NaN;
        for (var i = 1; i < n; i++)
        {
            rets[i] = (closes[i] - closes[i - 1]) / Math.Max(closes[i - 1], eps);
        }

        for (var i = window; i < n; i++)
        {
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

    internal static double[] LowerWickRatio(IReadOnlyList<CandlePoint> candles, double eps = Eps)
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

    internal static double Clamp(double x, double min, double max)
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

    internal static double[] Closes(IReadOnlyList<CandlePoint> candles)
    {
        var n = candles.Count;
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = candles[i].Close;
        }

        return result;
    }

    internal static double[] Volumes(IReadOnlyList<CandlePoint> candles)
    {
        var n = candles.Count;
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = candles[i].Volume;
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
