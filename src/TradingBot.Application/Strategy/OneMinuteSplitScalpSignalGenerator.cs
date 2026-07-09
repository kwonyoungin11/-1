using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// 1분봉 분할 스캘프 연습 신호: 유효 시세 + 수량 &gt; 0 이면 매수 후보 1건 (총량).
/// 실제 분할 레그는 <see cref="OrderCandidatePipeline"/> 이 생성. 투자 조언 아님 · 실주문 아님.
/// </summary>
public sealed class OneMinuteSplitScalpSignalGenerator : IStrategySignalGenerator
{
    public TradingStrategyKind Kind => TradingStrategyKind.일분분할스캘프;

    public StrategySignal Generate(
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (string.IsNullOrWhiteSpace(quote.Symbol) || quote.LastPrice is null or <= 0)
        {
            return Invalid(quote, nowUtc);
        }

        if (baseOrderQuantity <= 0)
        {
            return new StrategySignal(
                quote.Symbol,
                SignalSide.Hold,
                null,
                quote.LastPrice,
                "one_minute_split_scalp_v1",
                "관망 — 수량 정책 없음 (분할 연습 · 투자 조언 아님 · 실주문 아님)",
                nowUtc,
                IsActionable: false);
        }

        return new StrategySignal(
            quote.Symbol,
            SignalSide.Buy,
            baseOrderQuantity,
            quote.LastPrice,
            "one_minute_split_scalp_v1",
            $"일분분할스캘프 매수 후보 {quote.Symbol} @ {quote.LastPrice} · 총수량 {baseOrderQuantity} · 분할 지정가 연습 (투자 조언 아님 · 실주문 아님)",
            nowUtc,
            IsActionable: true);
    }

    private static StrategySignal Invalid(QuoteSnapshot quote, DateTimeOffset nowUtc) =>
        new(
            quote.Symbol ?? string.Empty,
            SignalSide.None,
            null,
            null,
            "one_minute_split_scalp_v1",
            "시세 없음 — 분할 신호 없음 (투자 조언 아님 · 실주문 아님)",
            nowUtc,
            IsActionable: false);
}
