using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// Minimal deterministic signal for Phase 3 scaffolding.
/// Not investment advice. Produces educational/demo signals for pipeline testing only.
/// </summary>
public sealed class SimpleNasDaqSignalGenerator
{
    public const string StrategyName = "simple_nasdaq_scaffold_v1";

    /// <summary>
    /// If a usable quote exists, emits a non-actionable Hold by default,
    /// or an actionable Buy scaffold when quantity/price are provided by policy.
    /// </summary>
    public StrategySignal Generate(
        QuoteSnapshot? quote,
        decimal defaultOrderQuantity,
        DateTimeOffset nowUtc)
    {
        if (quote is null
            || string.IsNullOrWhiteSpace(quote.Symbol)
            || quote.LastPrice is null or <= 0)
        {
            return new StrategySignal(
                Symbol: quote?.Symbol ?? string.Empty,
                Side: SignalSide.None,
                SuggestedQuantity: null,
                ReferencePrice: null,
                StrategyName: StrategyName,
                OwnerMessage: "시세 없음 — 신호 없음 (투자 조언 아님)",
                CreatedAtUtc: nowUtc,
                IsActionable: false);
        }

        if (defaultOrderQuantity <= 0)
        {
            return new StrategySignal(
                quote.Symbol,
                SignalSide.Hold,
                null,
                quote.LastPrice,
                StrategyName,
                "관망 신호 — 수량 정책 없음 (투자 조언 아님)",
                nowUtc,
                IsActionable: false);
        }

        // Scaffold only: emits a candidate-shaped buy signal for dry-run pipeline tests.
        // Owner must never treat this as a recommendation.
        return new StrategySignal(
            quote.Symbol,
            SignalSide.Buy,
            defaultOrderQuantity,
            quote.LastPrice,
            StrategyName,
            $"scaffold 매수 후보 신호 {quote.Symbol} @ {quote.LastPrice} (투자 조언 아님, 실주문 아님)",
            nowUtc,
            IsActionable: true);
    }
}
