using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Long-only bar signal strategies for the 6m multi-strategy suite.
/// Offline simulation only — not investment advice; never places live orders.
/// </summary>
/// <remarks>
/// // merge: prefer shared IndicatorSeries / CustomMathIndicators when present.
/// Local helpers keep this branch buildable in isolation.
/// </remarks>
public sealed class BuyHoldStrategy : PrecomputedBarSignalSource
{
    public override string Name => "BuyHold";

    protected override void Compute(
        IReadOnlyList<CandlePoint> candles,
        BarAction[] actions,
        string?[] entryReasons)
    {
        var entered = false;
        for (var i = 0; i < candles.Count; i++)
        {
            if (entered)
            {
                actions[i] = BarAction.Flat;
                continue;
            }

            if (IsValidPrice(candles[i].Close))
            {
                actions[i] = BarAction.EnterLong;
                entryReasons[i] = "buy_hold_first_valid_bar";
                entered = true;
            }
        }
    }
}

/// <summary>RSI(14) mean reversion: enter &lt; 30, exit &gt; 70.</summary>
public sealed class RsiMeanReversionStrategy : PrecomputedBarSignalSource
{
    public const int RsiPeriod = 14;
    public const double EnterBelow = 30.0;
    public const double ExitAbove = 70.0;

    public override string Name => "RsiMeanReversion";

    protected override void Compute(
        IReadOnlyList<CandlePoint> candles,
        BarAction[] actions,
        string?[] entryReasons)
    {
        var closes = ExtractCloses(candles);
        // merge: prefer IndicatorSeries.Rsi
        var rsi = LocalIndicators.Rsi(closes, RsiPeriod);
        var inPos = false;

        for (var i = 0; i < candles.Count; i++)
        {
            var r = rsi[i];
            if (double.IsNaN(r))
            {
                actions[i] = BarAction.Flat;
                continue;
            }

            if (!inPos)
            {
                if (r < EnterBelow && IsValidPrice(candles[i].Close))
                {
                    actions[i] = BarAction.EnterLong;
                    entryReasons[i] = $"rsi14={r:F1}<{EnterBelow:F0}";
                    inPos = true;
                }
            }
            else if (r > ExitAbove)
            {
                actions[i] = BarAction.ExitLong;
                inPos = false;
            }
        }
    }
}

/// <summary>EMA9 / EMA21 cross: up-cross enter, down-cross exit.</summary>
public sealed class EmaCrossStrategy : PrecomputedBarSignalSource
{
    public const int FastPeriod = 9;
    public const int SlowPeriod = 21;

    public override string Name => "EmaCross";

    protected override void Compute(
        IReadOnlyList<CandlePoint> candles,
        BarAction[] actions,
        string?[] entryReasons)
    {
        var closes = ExtractCloses(candles);
        // merge: prefer IndicatorSeries.Ema
        var emaFast = LocalIndicators.Ema(closes, FastPeriod);
        var emaSlow = LocalIndicators.Ema(closes, SlowPeriod);
        var inPos = false;

        for (var i = 0; i < candles.Count; i++)
        {
            if (i == 0
                || double.IsNaN(emaFast[i])
                || double.IsNaN(emaSlow[i])
                || double.IsNaN(emaFast[i - 1])
                || double.IsNaN(emaSlow[i - 1]))
            {
                actions[i] = BarAction.Flat;
                continue;
            }

            var crossUp = emaFast[i - 1] <= emaSlow[i - 1] && emaFast[i] > emaSlow[i];
            var crossDown = emaFast[i - 1] >= emaSlow[i - 1] && emaFast[i] < emaSlow[i];

            if (!inPos)
            {
                if (crossUp && IsValidPrice(candles[i].Close))
                {
                    actions[i] = BarAction.EnterLong;
                    entryReasons[i] = $"ema{FastPeriod}_cross_above_ema{SlowPeriod}";
                    inPos = true;
                }
            }
            else if (crossDown)
            {
                actions[i] = BarAction.ExitLong;
                inPos = false;
            }
        }
    }
}

/// <summary>
/// MathEdge mean-reversion: enter when edge &gt; 2×roundtrip (0.003),
/// exit mean-touch / SL 1.5% / TP 2.2% / max hold 30 bars.
/// </summary>
public sealed class MathEdgeStrategy : PrecomputedBarSignalSource
{
    public const double RoundTripCost = 0.003;
    public const double EntryMultiple = 2.0;
    public const double StopLossPct = 0.015;
    public const double TakeProfitPct = 0.022;
    public const int MaxHoldBars = 30;

    public override string Name => "MathEdge";

    protected override void Compute(
        IReadOnlyList<CandlePoint> candles,
        BarAction[] actions,
        string?[] entryReasons)
    {
        // merge: prefer CustomMathIndicators.ComputeMathEdge
        var edge = LocalIndicators.ComputeMathEdge(candles);
        var closes = ExtractCloses(candles);
        var ema21 = LocalIndicators.Ema(closes, 21);
        var threshold = EntryMultiple * RoundTripCost;

        var inPos = false;
        var entryPrice = 0.0;
        var entryBar = -1;

        for (var i = 0; i < candles.Count; i++)
        {
            var close = candles[i].Close;
            if (!inPos)
            {
                var e = edge[i];
                if (!double.IsNaN(e) && e > threshold && IsValidPrice(close))
                {
                    actions[i] = BarAction.EnterLong;
                    entryReasons[i] = $"math_edge={e:F4}>{threshold:F4}";
                    inPos = true;
                    entryPrice = close;
                    entryBar = i;
                }

                continue;
            }

            var hold = i - entryBar;
            var ret = (close - entryPrice) / entryPrice;
            var meanTouch = !double.IsNaN(ema21[i]) && close >= ema21[i];

            if (ret <= -StopLossPct || ret >= TakeProfitPct || meanTouch || hold >= MaxHoldBars)
            {
                actions[i] = BarAction.ExitLong;
                inPos = false;
                entryPrice = 0;
                entryBar = -1;
            }
        }
    }
}

/// <summary>
/// PRIMARY CERS strategy: enter when expected &gt; thr×roundtrip (thr=2),
/// exit close≥EMA21 / SL 1.2% / TP=expected×1.5 / max hold 40.
/// </summary>
public sealed class CersStrategy : PrecomputedBarSignalSource
{
    public const double RoundTripCost = 0.003;
    public const double ThresholdMultiple = 2.0;
    public const double StopLossPct = 0.012;
    public const int MaxHoldBars = 40;

    public override string Name => "CERS";

    protected override void Compute(
        IReadOnlyList<CandlePoint> candles,
        BarAction[] actions,
        string?[] entryReasons)
    {
        // merge: prefer CustomMathIndicators.ComputeCers
        var expected = LocalIndicators.ComputeCers(candles);
        var closes = ExtractCloses(candles);
        var ema21 = LocalIndicators.Ema(closes, 21);
        var threshold = ThresholdMultiple * RoundTripCost;

        var inPos = false;
        var entryPrice = 0.0;
        var entryBar = -1;
        var entryExpected = 0.0;

        for (var i = 0; i < candles.Count; i++)
        {
            var close = candles[i].Close;
            if (!inPos)
            {
                var exp = expected[i];
                if (!double.IsNaN(exp) && exp > threshold && IsValidPrice(close))
                {
                    actions[i] = BarAction.EnterLong;
                    entryReasons[i] = $"cers_exp={exp:F4}>{threshold:F4}";
                    inPos = true;
                    entryPrice = close;
                    entryBar = i;
                    entryExpected = exp;
                }

                continue;
            }

            var hold = i - entryBar;
            var ret = (close - entryPrice) / entryPrice;
            var tp = entryExpected * 1.5;
            var meanTouch = !double.IsNaN(ema21[i]) && close >= ema21[i];

            if (ret <= -StopLossPct || ret >= tp || meanTouch || hold >= MaxHoldBars)
            {
                actions[i] = BarAction.ExitLong;
                inPos = false;
                entryPrice = 0;
                entryBar = -1;
                entryExpected = 0;
            }
        }
    }
}

/// <summary>
/// Looser CERS variant: thr=1.5×roundtrip, fixed TP 3%, max hold 50.
/// </summary>
public sealed class CersLooseStrategy : PrecomputedBarSignalSource
{
    public const double RoundTripCost = 0.003;
    public const double ThresholdMultiple = 1.5;
    public const double StopLossPct = 0.012;
    public const double TakeProfitPct = 0.03;
    public const int MaxHoldBars = 50;

    public override string Name => "CERS_Loose";

    protected override void Compute(
        IReadOnlyList<CandlePoint> candles,
        BarAction[] actions,
        string?[] entryReasons)
    {
        // merge: prefer CustomMathIndicators.ComputeCers
        var expected = LocalIndicators.ComputeCers(candles);
        var closes = ExtractCloses(candles);
        var ema21 = LocalIndicators.Ema(closes, 21);
        var threshold = ThresholdMultiple * RoundTripCost;

        var inPos = false;
        var entryPrice = 0.0;
        var entryBar = -1;

        for (var i = 0; i < candles.Count; i++)
        {
            var close = candles[i].Close;
            if (!inPos)
            {
                var exp = expected[i];
                if (!double.IsNaN(exp) && exp > threshold && IsValidPrice(close))
                {
                    actions[i] = BarAction.EnterLong;
                    entryReasons[i] = $"cers_loose_exp={exp:F4}>{threshold:F4}";
                    inPos = true;
                    entryPrice = close;
                    entryBar = i;
                }

                continue;
            }

            var hold = i - entryBar;
            var ret = (close - entryPrice) / entryPrice;
            var meanTouch = !double.IsNaN(ema21[i]) && close >= ema21[i];

            if (ret <= -StopLossPct || ret >= TakeProfitPct || meanTouch || hold >= MaxHoldBars)
            {
                actions[i] = BarAction.ExitLong;
                inPos = false;
                entryPrice = 0;
                entryBar = -1;
            }
        }
    }
}

/// <summary>Shared prepare/action array scaffolding for bar strategies.</summary>
public abstract class PrecomputedBarSignalSource : IBarSignalSource
{
    private BarAction[] _actions = Array.Empty<BarAction>();
    private string?[] _entryReasons = Array.Empty<string?>();
    private bool _prepared;

    public abstract string Name { get; }

    public void Prepare(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        _actions = new BarAction[n];
        _entryReasons = new string?[n];
        if (n > 0)
        {
            Compute(candles, _actions, _entryReasons);
        }

        _prepared = true;
    }

    public BarAction ActionAt(int barIndex)
    {
        EnsurePrepared();
        if ((uint)barIndex >= (uint)_actions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(barIndex));
        }

        return _actions[barIndex];
    }

    public string? EntryReasonAt(int barIndex)
    {
        EnsurePrepared();
        if ((uint)barIndex >= (uint)_entryReasons.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(barIndex));
        }

        return _entryReasons[barIndex];
    }

    protected abstract void Compute(
        IReadOnlyList<CandlePoint> candles,
        BarAction[] actions,
        string?[] entryReasons);

    private void EnsurePrepared()
    {
        if (!_prepared)
        {
            throw new InvalidOperationException($"{Name}: call Prepare before ActionAt/EntryReasonAt.");
        }
    }

    protected static double[] ExtractCloses(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var closes = new double[candles.Count];
        for (var i = 0; i < candles.Count; i++)
        {
            closes[i] = candles[i].Close;
        }

        return closes;
    }

    protected static bool IsValidPrice(double price) =>
        price > 0 && !double.IsNaN(price) && !double.IsInfinity(price);
}

/// <summary>
/// Thin indicator + CERS/MathEdge math so this branch builds alone.
/// // merge: prefer shared IndicatorSeries / CustomMathIndicators
/// </summary>
internal static class LocalIndicators
{
    private const double Eps = 1e-12;

    public static double[] Ema(IReadOnlyList<double> closes, int period)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = closes.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (n < period)
        {
            return result;
        }

        double sum = 0;
        for (var i = 0; i < period; i++)
        {
            sum += closes[i];
        }

        var ema = sum / period;
        var seed = period - 1;
        result[seed] = ema;
        var k = 2.0 / (period + 1);
        for (var i = seed + 1; i < n; i++)
        {
            ema = closes[i] * k + ema * (1.0 - k);
            result[i] = ema;
        }

        return result;
    }

    public static double[] Sma(IReadOnlyList<double> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = values.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
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

    public static double[] Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = closes.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
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
        result[period] = WilderRsi(avgGain, avgLoss);

        for (var i = period + 1; i < n; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0.0;
            var loss = change < 0 ? -change : 0.0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            result[i] = WilderRsi(avgGain, avgLoss);
        }

        return result;
    }

    public static double[] Atr(IReadOnlyList<CandlePoint> candles, int period = 14)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = candles.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (n <= period)
        {
            return result;
        }

        var tr = new double[n];
        tr[0] = Math.Max(0, candles[0].High - candles[0].Low);
        for (var i = 1; i < n; i++)
        {
            var h = candles[i].High;
            var l = candles[i].Low;
            var prevClose = candles[i - 1].Close;
            tr[i] = Math.Max(h - l, Math.Max(Math.Abs(h - prevClose), Math.Abs(l - prevClose)));
        }

        double sum = 0;
        for (var i = 1; i <= period; i++)
        {
            sum += tr[i];
        }

        var atr = sum / period;
        result[period] = atr;
        for (var i = period + 1; i < n; i++)
        {
            atr = ((atr * (period - 1)) + tr[i]) / period;
            result[i] = atr;
        }

        return result;
    }

    /// <summary>
    /// MathEdge: (EMA21-Close)/Close × RSI oversold × volume factor.
    /// // merge: prefer CustomMathIndicators.ComputeMathEdge
    /// </summary>
    public static double[] ComputeMathEdge(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (n == 0)
        {
            return result;
        }

        var closes = new double[n];
        var vols = new double[n];
        for (var i = 0; i < n; i++)
        {
            closes[i] = candles[i].Close;
            vols[i] = candles[i].Volume;
        }

        var ema21 = Ema(closes, 21);
        var rsi14 = Rsi(closes, 14);
        var volSma = Sma(vols, 20);
        var volStd = RollingStdev(vols, 20);

        for (var i = 0; i < n; i++)
        {
            if (double.IsNaN(ema21[i]) || double.IsNaN(rsi14[i]) || double.IsNaN(volSma[i]))
            {
                continue;
            }

            var close = Math.Max(closes[i], Eps);
            var discount = Math.Max(0.0, (ema21[i] - close) / close);
            var rsiOs = Math.Max(0.0, (35.0 - rsi14[i]) / 35.0);
            var std = double.IsNaN(volStd[i]) ? 0.0 : volStd[i];
            var volZ = std > Eps ? (vols[i] - volSma[i]) / (std + Eps) : 0.0;
            volZ = Math.Clamp(volZ, 0.0, 3.0);
            var volFactor = 1.0 + 0.35 * (volZ / 3.0);
            result[i] = discount * rsiOs * volFactor;
        }

        return result;
    }

    /// <summary>
    /// CERS — Cost-aware Edge Reversion Score (expected edge per bar).
    /// // merge: prefer CustomMathIndicators.ComputeCers
    /// </summary>
    public static double[] ComputeCers(IReadOnlyList<CandlePoint> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var n = candles.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (n == 0)
        {
            return result;
        }

        var closes = new double[n];
        var vols = new double[n];
        for (var i = 0; i < n; i++)
        {
            closes[i] = candles[i].Close;
            vols[i] = candles[i].Volume;
        }

        var mu = Ema(closes, 21);
        var atr = Atr(candles, 14);
        var rsi14 = Rsi(closes, 14);
        var volSma = Sma(vols, 20);
        var volStd = RollingStdev(vols, 20);
        var kappa = Lag1AutocorrKappa(closes, window: 30);

        for (var i = 0; i < n; i++)
        {
            if (double.IsNaN(mu[i]) || double.IsNaN(atr[i]) || double.IsNaN(rsi14[i]) || double.IsNaN(volSma[i]))
            {
                continue;
            }

            var close = Math.Max(closes[i], Eps);
            var dev = Math.Max(0.0, (mu[i] - close) / close);
            var rsiBoost = Math.Max(0.0, (35.0 - rsi14[i]) / 35.0);
            var std = double.IsNaN(volStd[i]) ? 0.0 : volStd[i];
            var volZ = std > Eps ? (vols[i] - volSma[i]) / (std + Eps) : 0.0;
            volZ = Math.Clamp(volZ, 0.0, 3.0);
            var k = double.IsNaN(kappa[i]) ? 1.0 : kappa[i];

            var expected = k * dev * (0.45 + 0.35 * rsiBoost + 0.20 * Math.Min(1.0, volZ / 2.0));

            var range = candles[i].High - candles[i].Low;
            var bodyLow = Math.Min(candles[i].Open, candles[i].Close);
            var lowerWick = range > Eps ? Math.Max(0.0, (bodyLow - candles[i].Low) / range) : 0.0;
            expected *= 0.85 + 0.15 * Math.Clamp(lowerWick, 0.0, 1.0);

            result[i] = expected;
        }

        return result;
    }

    private static double[] RollingStdev(IReadOnlyList<double> values, int period)
    {
        var n = values.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (n < period || period <= 1)
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

    /// <summary>kappa = clamp(1 + rho_lag1, 0.25, 2.0) over trailing window of returns.</summary>
    private static double[] Lag1AutocorrKappa(IReadOnlyList<double> closes, int window)
    {
        var n = closes.Count;
        var kappa = new double[n];
        Array.Fill(kappa, double.NaN);
        if (n < window + 1)
        {
            return kappa;
        }

        for (var i = window; i < n; i++)
        {
            // returns r[t] = close[t]/close[t-1]-1 over [i-window+1 .. i]
            var count = window;
            var r = new double[count];
            var ok = true;
            for (var k = 0; k < count; k++)
            {
                var idx = i - window + 1 + k;
                var prev = closes[idx - 1];
                if (prev <= Eps)
                {
                    ok = false;
                    break;
                }

                r[k] = (closes[idx] / prev) - 1.0;
            }

            if (!ok || count < 3)
            {
                continue;
            }

            // lag-1 corr of r[0..count-1]
            var m = count - 1;
            double mean0 = 0;
            double mean1 = 0;
            for (var k = 0; k < m; k++)
            {
                mean0 += r[k];
                mean1 += r[k + 1];
            }

            mean0 /= m;
            mean1 /= m;
            double num = 0;
            double den0 = 0;
            double den1 = 0;
            for (var k = 0; k < m; k++)
            {
                var a = r[k] - mean0;
                var b = r[k + 1] - mean1;
                num += a * b;
                den0 += a * a;
                den1 += b * b;
            }

            var den = Math.Sqrt(den0 * den1);
            var rho = den > Eps ? num / den : 0.0;
            kappa[i] = Math.Clamp(1.0 + rho, 0.25, 2.0);
        }

        return kappa;
    }

    private static double WilderRsi(double avgGain, double avgLoss)
    {
        if (avgLoss == 0.0)
        {
            return avgGain == 0.0 ? 50.0 : 100.0;
        }

        var rs = avgGain / avgLoss;
        return 100.0 - (100.0 / (1.0 + rs));
    }
}
