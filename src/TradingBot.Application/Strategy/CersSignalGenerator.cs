using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// CERS 비용인식 평균회귀 연습 신호 생성기.
/// Candle series는 <see cref="StrategySignalRouter"/> 가 practice 컨텍스트로 주입 (Solution B).
/// 주문 실행이 아님 · 투자 조언 아님 · 실주문 게이트 유지.
/// </summary>
public sealed class CersSignalGenerator : IStrategySignalGenerator
{
    public TradingStrategyKind Kind => TradingStrategyKind.CERS비용회귀;

    /// <summary>
    /// Interface path without candles → fail-closed hold (관망).
    /// Prefer <see cref="GenerateFromCandles"/> via router + practice.
    /// </summary>
    public StrategySignal Generate(
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc) =>
        GenerateFromCandles(quote, baseOrderQuantity, nowUtc, candles: null, openLong: null);

    /// <summary>
    /// Evaluate CERS on candle series (+ optional open long). Delegates to domain evaluator.
    /// </summary>
    public static StrategySignal GenerateFromCandles(
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc,
        IReadOnlyList<CandlePoint>? candles,
        CersOpenPosition? openLong = null)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var symbol = string.IsNullOrWhiteSpace(quote.Symbol) ? "?" : quote.Symbol.Trim().ToUpperInvariant();
        var qty = baseOrderQuantity > 0m
            ? baseOrderQuantity
            : StrategyCatalog.BaseQuantity(TradingStrategyKind.CERS비용회귀);

        if (candles is null || candles.Count == 0)
        {
            return new StrategySignal(
                Symbol: symbol,
                Side: SignalSide.Hold,
                SuggestedQuantity: qty > 0m ? qty : null,
                ReferencePrice: quote.LastPrice,
                StrategyName: CersEvaluator.StrategyName,
                OwnerMessage: "봉 없음 · CERS 관망 · 실주문 없음 · 투자 조언 아님",
                CreatedAtUtc: nowUtc,
                IsActionable: false);
        }

        return CersEvaluator.Evaluate(
            symbol: symbol,
            candles: candles,
            openLong: openLong,
            nowUtc: nowUtc,
            suggestedQuantity: qty > 0m ? qty : null);
    }
}
