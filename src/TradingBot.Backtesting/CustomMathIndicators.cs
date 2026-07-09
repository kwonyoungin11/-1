using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Cost-aware mean-reversion custom indicators for backtest comparison suites.
/// Pure static math on candle series; warm-up = NaN. Not investment advice.
/// </summary>
public static class CustomMathIndicators
{
    public const double Eps = IndicatorSeries.Eps;

    /// <summary>
    /// <b>CERS — Cost-aware Edge Reversion Score</b> (primary custom edge estimate).
    /// <para>
    /// Per bar (fractional expected reversion edge; compare to round-trip costs externally):
    /// </para>
    /// <code>
    /// mu        = EMA(close, 21)
    /// sigma     = ATR(14) / max(close, eps)          // normalized vol (diagnostic)
    /// dev       = (mu - close) / max(close, eps)     // &gt;0 when price below mean
    /// rho_lag1  = lag-1 autocorrelation of returns over W=30
    /// kappa     = clamp(1 + rho_lag1, 0.25, 2.0)     // mean-reversion regime scale
    /// rsi14     = RSI(14)
    /// rsi_boost = max(0, (35 - rsi14) / 35)
    /// vol_z     = (vol - SMA(vol,20)) / (stdev(vol,20)+eps); clip to [0, 3]
    /// vol_boost = 1 + 0.35 * (vol_z / 3)             // unused in core product; diagnostic
    /// expected  = kappa * max(0, dev)
    ///             * (0.45 + 0.35*rsi_boost + 0.20*min(1, vol_z/2))
    /// range     = high - low
    /// lower_wick = (min(open,close) - low) / max(range, eps)
    /// expected *= (0.85 + 0.15 * lower_wick)
    /// </code>
    /// Early bars without full warm-up return NaN.
    /// </summary>
    /// <param name="candles">Chronological OHLCV bars (oldest first).</param>
    /// <returns>Expected edge series aligned to <paramref name="candles"/>.</returns>
    public static double[] Cers(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        if (n == 0)
        {
            return Array.Empty<double>();
        }

        var closes = IndicatorSeries.Closes(candles);
        var volumes = IndicatorSeries.Volumes(candles);

        var mu = IndicatorSeries.Ema(closes, 21);
        var atr = IndicatorSeries.Atr(candles, 14);
        var rsi = IndicatorSeries.Rsi(closes, 14);
        var volZRaw = IndicatorSeries.VolumeZScore(volumes, 20);
        var rho = IndicatorSeries.Lag1Autocorr(closes, 30);
        var lowerWick = IndicatorSeries.LowerWickRatio(candles);

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
            // sigma kept for formula documentation / future cost scaling; not in product.
            _ = atr[i] / close;

            var dev = (mu[i] - closes[i]) / close;
            var kappa = IndicatorSeries.Clamp(1.0 + rho[i], 0.25, 2.0);
            var rsiBoost = Math.Max(0.0, (35.0 - rsi[i]) / 35.0);
            var volZ = IndicatorSeries.Clamp(volZRaw[i], 0.0, 3.0);
            // vol_boost documented; quality mix uses min(1, vol_z/2) instead.
            _ = 1.0 + 0.35 * (volZ / 3.0);

            var quality = 0.45 + 0.35 * rsiBoost + 0.20 * Math.Min(1.0, volZ / 2.0);
            var expected = kappa * Math.Max(0.0, dev) * quality;
            expected *= 0.85 + 0.15 * lowerWick[i];
            result[i] = expected;
        }

        return result;
    }

    /// <summary>
    /// <b>MathEdge</b> (prior research winner on cost-aware mean reversion).
    /// <code>
    /// edge = max(0, (EMA21 - Close) / Close) * rsi_oversold_factor * vol_factor
    /// rsi_oversold_factor = max(0, (35 - RSI14) / 35)
    /// vol_z     = clip( (vol - SMA20(vol)) / (stdev20(vol)+eps) , 0, 3)
    /// vol_factor = 1 + 0.35 * (vol_z / 3)
    /// </code>
    /// Positive when price is below EMA21 with oversold RSI and elevated volume.
    /// Compare to round-trip cost (e.g. edge &gt; 2 × ~0.003). NaN on warm-up.
    /// </summary>
    public static double[] MathEdge(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        if (n == 0)
        {
            return Array.Empty<double>();
        }

        var closes = IndicatorSeries.Closes(candles);
        var volumes = IndicatorSeries.Volumes(candles);
        var ema21 = IndicatorSeries.Ema(closes, 21);
        var rsi = IndicatorSeries.Rsi(closes, 14);
        var volZRaw = IndicatorSeries.VolumeZScore(volumes, 20);

        var result = new double[n];
        Array.Fill(result, double.NaN);

        for (var i = 0; i < n; i++)
        {
            if (double.IsNaN(ema21[i]) || double.IsNaN(rsi[i]) || double.IsNaN(volZRaw[i]))
            {
                continue;
            }

            var close = Math.Max(closes[i], Eps);
            var meanDev = Math.Max(0.0, (ema21[i] - closes[i]) / close);
            var rsiOversold = Math.Max(0.0, (35.0 - rsi[i]) / 35.0);
            var volZ = IndicatorSeries.Clamp(volZRaw[i], 0.0, 3.0);
            var volFactor = 1.0 + 0.35 * (volZ / 3.0);
            result[i] = meanDev * rsiOversold * volFactor;
        }

        return result;
    }

    /// <summary>
    /// <b>LVRS — Liquidity Vacuum Reversal Score</b> (simplified comparison suite).
    /// <code>
    /// vol_z_n      = clip(vol_z, 0, 3) / 3                         // [0,1]
    /// lower_wick   = (min(o,c)-low) / max(high-low, eps)           // [0,1]
    /// rsi_oversold = max(0, (35 - RSI14) / 35)                     // [0,1]
    /// consec_down  = min(1, consecutive down closes / 5)           // [0,1]
    /// reclaim      = 1 if close &gt; open and close &gt; prev close else 0
    /// LVRS = 0.25·vol_z_n + 0.20·lower_wick + 0.25·rsi_oversold
    ///      + 0.15·consec_down + 0.15·reclaim
    /// </code>
    /// Score in roughly [0, 1]. NaN until RSI/volume warm-up.
    /// </summary>
    public static double[] Lvrs(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        if (n == 0)
        {
            return Array.Empty<double>();
        }

        var closes = IndicatorSeries.Closes(candles);
        var volumes = IndicatorSeries.Volumes(candles);
        var rsi = IndicatorSeries.Rsi(closes, 14);
        var volZRaw = IndicatorSeries.VolumeZScore(volumes, 20);
        var lowerWick = IndicatorSeries.LowerWickRatio(candles);

        var result = new double[n];
        Array.Fill(result, double.NaN);

        var consecDown = 0;
        for (var i = 0; i < n; i++)
        {
            if (i > 0 && closes[i] < closes[i - 1])
            {
                consecDown++;
            }
            else if (i > 0)
            {
                consecDown = 0;
            }

            if (double.IsNaN(rsi[i]) || double.IsNaN(volZRaw[i]))
            {
                continue;
            }

            var volZn = IndicatorSeries.Clamp(volZRaw[i], 0.0, 3.0) / 3.0;
            var rsiOs = Math.Max(0.0, (35.0 - rsi[i]) / 35.0);
            var consec = Math.Min(1.0, consecDown / 5.0);
            var reclaim = 0.0;
            if (i > 0
                && candles[i].Close > candles[i].Open
                && candles[i].Close > candles[i - 1].Close)
            {
                reclaim = 1.0;
            }

            result[i] = 0.25 * volZn
                + 0.20 * lowerWick[i]
                + 0.25 * rsiOs
                + 0.15 * consec
                + 0.15 * reclaim;
        }

        return result;
    }

    /// <summary>
    /// <b>FEI — Fade Exhaustion Index</b> (simplified comparison suite).
    /// <code>
    /// drop5    = max(0, (close[i-5] - close[i]) / max(close[i-5], eps))   // 5-bar drop fraction
    /// drop5_n  = min(1, drop5 / 0.03)                                      // normalize ~3% drop
    /// vol_z_n  = clip(vol_z, 0, 3) / 3
    /// rsi7_os  = max(0, (30 - RSI7) / 30)
    /// green    = 1 if close &gt; open else 0
    /// FEI = 0.35·drop5_n + 0.25·vol_z_n + 0.25·rsi7_os + 0.15·green
    /// </code>
    /// Score in roughly [0, 1]. NaN until enough history for RSI7 + 5-bar drop + vol z.
    /// </summary>
    public static double[] Fei(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        if (n == 0)
        {
            return Array.Empty<double>();
        }

        var closes = IndicatorSeries.Closes(candles);
        var volumes = IndicatorSeries.Volumes(candles);
        var rsi7 = IndicatorSeries.Rsi(closes, 7);
        var volZRaw = IndicatorSeries.VolumeZScore(volumes, 20);

        var result = new double[n];
        Array.Fill(result, double.NaN);

        for (var i = 0; i < n; i++)
        {
            if (i < 5 || double.IsNaN(rsi7[i]) || double.IsNaN(volZRaw[i]))
            {
                continue;
            }

            var prior = Math.Max(closes[i - 5], Eps);
            var drop5 = Math.Max(0.0, (closes[i - 5] - closes[i]) / prior);
            var drop5N = Math.Min(1.0, drop5 / 0.03);
            var volZn = IndicatorSeries.Clamp(volZRaw[i], 0.0, 3.0) / 3.0;
            var rsi7Os = Math.Max(0.0, (30.0 - rsi7[i]) / 30.0);
            var green = candles[i].Close > candles[i].Open ? 1.0 : 0.0;

            result[i] = 0.35 * drop5N
                + 0.25 * volZn
                + 0.25 * rsi7Os
                + 0.15 * green;
        }

        return result;
    }
}
