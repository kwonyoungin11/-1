using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>기존 단순 연습: 유효 시세 + 수량 &gt; 0 이면 매수 후보 1건.</summary>
public sealed class SimplePracticeSignalGenerator : IStrategySignalGenerator
{
    public TradingStrategyKind Kind => TradingStrategyKind.단순연습전략;

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
                "simple_practice_v1",
                "관망 — 수량 정책 없음 (투자 조언 아님)",
                nowUtc,
                IsActionable: false);
        }

        return new StrategySignal(
            quote.Symbol,
            SignalSide.Buy,
            baseOrderQuantity,
            quote.LastPrice,
            "simple_practice_v1",
            $"단순연습 매수 후보 {quote.Symbol} @ {quote.LastPrice} · 수량 {baseOrderQuantity} (투자 조언 아님 · 실주문 아님)",
            nowUtc,
            IsActionable: true);
    }

    private static StrategySignal Invalid(QuoteSnapshot quote, DateTimeOffset nowUtc) =>
        new(
            quote.Symbol ?? string.Empty,
            SignalSide.None,
            null,
            null,
            "simple_practice_v1",
            "시세 없음 — 신호 없음",
            nowUtc,
            IsActionable: false);
}
