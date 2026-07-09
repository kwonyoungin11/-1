using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// 하위 호환 래퍼 → <see cref="SimplePracticeSignalGenerator"/>.
/// </summary>
public sealed class SimpleNasDaqSignalGenerator
{
    public const string StrategyName = "simple_nasdaq_scaffold_v1";
    private readonly SimplePracticeSignalGenerator _inner = new();

    public StrategySignal Generate(
        QuoteSnapshot? quote,
        decimal defaultOrderQuantity,
        DateTimeOffset nowUtc)
    {
        if (quote is null)
        {
            return new StrategySignal(
                string.Empty,
                SignalSide.None,
                null,
                null,
                StrategyName,
                "시세 없음 — 신호 없음 (투자 조언 아님)",
                nowUtc,
                IsActionable: false);
        }

        return _inner.Generate(quote, defaultOrderQuantity, nowUtc);
    }
}
