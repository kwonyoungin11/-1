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
    /// 전략별 기본 보조지표 세트.
    /// 추세추종 → SMA20/SMA60; 평균회귀 → 볼린저; 모멘텀 → SMA10 + 종가 모멘텀 밴드;
    /// 단순연습 → SMA20; 관망 → SMA20.
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
            ],
            TradingStrategyKind.평균회귀 => Bollinger(closes, period: 20, stdMult: 2.0),
            TradingStrategyKind.모멘텀돌파 =>
            [
                new ChartIndicatorLine("SMA10", Sma(closes, 10)),
                new ChartIndicatorLine("모멘텀고", RollingMax(closes, 20)),
            ],
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
