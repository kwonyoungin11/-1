namespace TradingBot.Domain;

/// <summary>
/// Snapshot of an open long position for CERS exit evaluation.
/// </summary>
/// <param name="EntryPrice">Fill / reference entry price.</param>
/// <param name="BarsHeld">Bars since entry (inclusive of current evaluation bar).</param>
/// <param name="EntryExpected">Expected edge at entry (used for dynamic TP = expected × 1.5).</param>
public sealed record CersOpenPosition(
    double EntryPrice,
    int BarsHeld,
    double EntryExpected);

/// <summary>
/// Evaluates CERS long entry / exit intent on the last candle.
/// Produces <see cref="StrategySignal"/> candidates only — never places orders.
/// Rules mirror backtest <c>CersStrategy</c>. Not investment advice.
/// </summary>
public static class CersEvaluator
{
    public const string StrategyName = "CERS비용회귀";

    /// <summary>
    /// Evaluate last bar. Optional overrides are for unit tests / paper injection only.
    /// </summary>
    public static StrategySignal Evaluate(
        string symbol,
        IReadOnlyList<CandlePoint> candles,
        CersOpenPosition? openLong,
        DateTimeOffset? nowUtc = null,
        double? expectedEdgeOverride = null,
        double? emaOverride = null,
        decimal? suggestedQuantity = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var sym = string.IsNullOrWhiteSpace(symbol) ? "?" : symbol.Trim().ToUpperInvariant();
        var qty = suggestedQuantity ?? StrategyCatalog.BaseQuantity(TradingStrategyKind.CERS비용회귀);

        if (candles is null || candles.Count == 0)
        {
            return Hold(sym, qty, now, "봉 없음 · CERS 관망 · 실주문 없음");
        }

        var last = candles[^1];
        var close = last.Close;
        if (close <= 0 || double.IsNaN(close) || double.IsInfinity(close))
        {
            return Hold(sym, qty, now, "가격 비정상 · CERS 관망 · 실주문 없음");
        }

        var expected = expectedEdgeOverride
            ?? CersMath.LastExpectedEdge(candles)
            ?? double.NaN;

        var ema = emaOverride ?? CersMath.LastEma(candles);

        if (openLong is not null)
        {
            return EvaluateExit(sym, qty, now, close, openLong, ema);
        }

        return EvaluateEntry(sym, qty, now, close, expected);
    }

    private static StrategySignal EvaluateEntry(
        string symbol,
        decimal qty,
        DateTimeOffset now,
        double close,
        double expected)
    {
        if (double.IsNaN(expected) || double.IsInfinity(expected))
        {
            return Hold(symbol, qty, now, "CERS 워밍업/데이터 부족 · 관망 · 실주문 없음");
        }

        if (expected > CersPreset.EntryThreshold)
        {
            return new StrategySignal(
                Symbol: symbol,
                Side: SignalSide.Buy,
                SuggestedQuantity: qty,
                ReferencePrice: (decimal)close,
                StrategyName: StrategyName,
                OwnerMessage:
                    $"CERS 진입 후보 · exp={expected:F4}>{CersPreset.EntryThreshold:F4} · " +
                    "실주문 게이트 · 투자 조언 아님",
                CreatedAtUtc: now,
                IsActionable: true);
        }

        return Hold(
            symbol,
            qty,
            now,
            $"CERS 엣지 부족 · exp={expected:F4}≤{CersPreset.EntryThreshold:F4} · 관망 · 실주문 없음");
    }

    private static StrategySignal EvaluateExit(
        string symbol,
        decimal qty,
        DateTimeOffset now,
        double close,
        CersOpenPosition open,
        double? ema)
    {
        if (open.EntryPrice <= 0)
        {
            return Hold(symbol, qty, now, "진입가 비정상 · CERS 관망 · 실주문 없음");
        }

        var ret = (close - open.EntryPrice) / open.EntryPrice;
        var tpLevel = open.EntryExpected * CersPreset.TakeProfitExpectedMultiple;
        var meanTouch = ema is double e && !double.IsNaN(e) && close >= e;

        if (ret <= -CersPreset.StopLossPct)
        {
            return Sell(symbol, qty, now, close,
                $"CERS 손절 · ret={ret:P2} ≤ -{CersPreset.StopLossPct:P1} · 실주문 게이트");
        }

        if (ret >= tpLevel && tpLevel > 0)
        {
            return Sell(symbol, qty, now, close,
                $"CERS 익절 · ret={ret:P2} ≥ entryExpected×{CersPreset.TakeProfitExpectedMultiple} · 실주문 게이트");
        }

        if (meanTouch)
        {
            return Sell(symbol, qty, now, close,
                "CERS 평균회귀 터치 · close≥EMA21 · 실주문 게이트");
        }

        if (open.BarsHeld >= CersPreset.MaxHoldBars)
        {
            return Sell(symbol, qty, now, close,
                $"CERS 최대 보유 {CersPreset.MaxHoldBars}봉 · 시간 청산 · 실주문 게이트");
        }

        return Hold(
            symbol,
            qty,
            now,
            $"CERS 보유 중 · hold={open.BarsHeld} · ret={ret:P2} · 청산 조건 미충족 · 실주문 없음");
    }

    private static StrategySignal Hold(
        string symbol,
        decimal qty,
        DateTimeOffset now,
        string message) =>
        new(
            Symbol: symbol,
            Side: SignalSide.Hold,
            SuggestedQuantity: qty,
            ReferencePrice: null,
            StrategyName: StrategyName,
            OwnerMessage: message,
            CreatedAtUtc: now,
            IsActionable: false);

    private static StrategySignal Sell(
        string symbol,
        decimal qty,
        DateTimeOffset now,
        double close,
        string message) =>
        new(
            Symbol: symbol,
            Side: SignalSide.Sell,
            SuggestedQuantity: qty,
            ReferencePrice: (decimal)close,
            StrategyName: StrategyName,
            OwnerMessage: message,
            CreatedAtUtc: now,
            IsActionable: true);
}
