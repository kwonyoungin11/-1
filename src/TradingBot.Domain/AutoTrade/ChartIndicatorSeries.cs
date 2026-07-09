namespace TradingBot.Domain;

/// <summary>차트 오버레이용 단일 라인 (시간축 정렬 값, NaN=미표시).</summary>
public sealed record ChartIndicatorLine(
    string Name,
    IReadOnlyList<double?> Values);

/// <summary>
/// 봉 시리즈 + 매매 전략에 맞는 보조지표.
/// 순수 계산 — UI/주문과 분리. 투자 조언 아님.
/// </summary>
public static class ChartIndicatorCalculator
{
    /// <summary>
    /// Strategy overlay set for chart price scale.
    /// 추세추종 → SMA20/SMA60 + EMA9/EMA21 (price-scale safe; RSI is public API only).
    /// 평균회귀 → Bollinger; 모멘텀 → SMA10 + rolling high; 단순연습/관망 → SMA20.
    /// CERS비용회귀 → EMA21 + CERS expected edge (NaN warm-up → null).
    /// </summary>
    public static IReadOnlyList<ChartIndicatorLine> ForStrategy(
        IReadOnlyList<CandlePoint> candles,
        TradingStrategyKind strategy)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
        {
            return Array.Empty<ChartIndicatorLine>();
        }

        var closes = candles.Select(c => c.Close).ToArray();

        return strategy switch
        {
            TradingStrategyKind.추세추종 =>
            [
                new ChartIndicatorLine("SMA20", Sma(closes, 20)),
                new ChartIndicatorLine("SMA60", Sma(closes, 60)),
                new ChartIndicatorLine("EMA9", Ema(closes, 9)),
                new ChartIndicatorLine("EMA21", Ema(closes, 21)),
            ],
            TradingStrategyKind.평균회귀 => Bollinger(closes, period: 20, stdMult: 2.0),
            TradingStrategyKind.모멘텀돌파 =>
            [
                new ChartIndicatorLine("SMA10", Sma(closes, 10)),
                new ChartIndicatorLine("모멘텀고", RollingMax(closes, 20)),
            ],
            TradingStrategyKind.CERS비용회귀 => CersOverlays(candles, closes),
            TradingStrategyKind.단순연습전략 =>
            [
                new ChartIndicatorLine("SMA20", Sma(closes, 20)),
            ],
            TradingStrategyKind.관망만 =>
            [
                new ChartIndicatorLine("SMA20", Sma(closes, 20)),
            ],
            _ =>
            [
                new ChartIndicatorLine("SMA20", Sma(closes, 20)),
            ],
        };
    }

    /// <summary>
    /// CERS chart overlays: EMA21 (mean reversion anchor) + per-bar expected edge.
    /// Edge from <see cref="CersMath.ComputeExpectedEdge"/>; NaN/∞ → null for chart gaps.
    /// Aligns with <see cref="CersMath.LastEma"/> / last expected edge at the final bar.
    /// </summary>
    private static IReadOnlyList<ChartIndicatorLine> CersOverlays(
        IReadOnlyList<CandlePoint> candles,
        IReadOnlyList<double> closes)
    {
        var ema21 = Ema(closes, CersPreset.EmaPeriod);
        var rawEdge = CersMath.ComputeExpectedEdge(candles);
        var edge = ToNullableSeries(rawEdge);

        return
        [
            new ChartIndicatorLine("EMA21", ema21),
            new ChartIndicatorLine("CERS", edge),
        ];
    }

    private static IReadOnlyList<double?> ToNullableSeries(IReadOnlyList<double> values)
    {
        var n = values.Count;
        var result = new double?[n];
        for (var i = 0; i < n; i++)
        {
            var v = values[i];
            result[i] = double.IsNaN(v) || double.IsInfinity(v) ? null : v;
        }

        return result;
    }

    public static IReadOnlyList<double?> Sma(IReadOnlyList<double> closes, int period)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = closes.Count;
        var result = new double?[n];
        if (n < period)
        {
            return result;
        }

        double sum = 0;
        for (var i = 0; i < n; i++)
        {
            sum += closes[i];
            if (i >= period)
            {
                sum -= closes[i - period];
            }

            if (i >= period - 1)
            {
                result[i] = sum / period;
            }
        }

        return result;
    }

    /// <summary>
    /// Exponential moving average. Seed = SMA of first <paramref name="period"/> closes;
    /// subsequent bars use k = 2/(period+1). Null until seed index (period-1).
    /// </summary>
    public static IReadOnlyList<double?> Ema(IReadOnlyList<double> closes, int period)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = closes.Count;
        var result = new double?[n];
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
        var seedIndex = period - 1;
        result[seedIndex] = ema;

        var k = 2.0 / (period + 1);
        for (var i = seedIndex + 1; i < n; i++)
        {
            ema = closes[i] * k + ema * (1.0 - k);
            result[i] = ema;
        }

        return result;
    }

    /// <summary>
    /// Wilder RSI (0–100). Average gain/loss seeded over first <paramref name="period"/>
    /// changes; then smoothed with Wilder. Null until index <paramref name="period"/>
    /// (needs period deltas). Public for future RSI pane; not overlaid on price scale.
    /// </summary>
    public static IReadOnlyList<double?> Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var n = closes.Count;
        var result = new double?[n];
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

    private static double WilderRsiFromAverages(double avgGain, double avgLoss)
    {
        if (avgLoss == 0.0)
        {
            return avgGain == 0.0 ? 50.0 : 100.0;
        }

        var rs = avgGain / avgLoss;
        return 100.0 - (100.0 / (1.0 + rs));
    }

    public static IReadOnlyList<ChartIndicatorLine> Bollinger(
        IReadOnlyList<double> closes,
        int period,
        double stdMult)
    {
        ArgumentNullException.ThrowIfNull(closes);
        var n = closes.Count;
        var mid = new double?[n];
        var upper = new double?[n];
        var lower = new double?[n];

        for (var i = period - 1; i < n; i++)
        {
            double sum = 0;
            for (var j = i - period + 1; j <= i; j++)
            {
                sum += closes[j];
            }

            var mean = sum / period;
            double varSum = 0;
            for (var j = i - period + 1; j <= i; j++)
            {
                var d = closes[j] - mean;
                varSum += d * d;
            }

            var std = Math.Sqrt(varSum / period);
            mid[i] = mean;
            upper[i] = mean + stdMult * std;
            lower[i] = mean - stdMult * std;
        }

        return
        [
            new ChartIndicatorLine("BB중간", mid),
            new ChartIndicatorLine("BB상단", upper),
            new ChartIndicatorLine("BB하단", lower),
        ];
    }

    public static IReadOnlyList<double?> RollingMax(IReadOnlyList<double> closes, int period)
    {
        ArgumentNullException.ThrowIfNull(closes);
        var n = closes.Count;
        var result = new double?[n];
        for (var i = 0; i < n; i++)
        {
            if (i < period - 1)
            {
                continue;
            }

            var max = closes[i - period + 1];
            for (var j = i - period + 2; j <= i; j++)
            {
                if (closes[j] > max)
                {
                    max = closes[j];
                }
            }

            result[i] = max;
        }

        return result;
    }
}
