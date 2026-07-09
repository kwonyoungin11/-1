using TradingBot.Domain;

namespace TradingBot.Application;

public sealed class HoldOnlySignalGenerator : IStrategySignalGenerator
{
    public TradingStrategyKind Kind => TradingStrategyKind.관망만;

    public StrategySignal Generate(
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return new StrategySignal(
            quote.Symbol,
            SignalSide.Hold,
            null,
            quote.LastPrice,
            "hold_only_v1",
            "관망만 — 후보 없음 (투자 조언 아님 · 실주문 아님)",
            nowUtc,
            IsActionable: false);
    }
}
