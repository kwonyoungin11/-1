using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>전략 신호 생성기. 주문 실행이 아님 · 투자 조언 아님.</summary>
public interface IStrategySignalGenerator
{
    TradingStrategyKind Kind { get; }

    StrategySignal Generate(
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc);
}
