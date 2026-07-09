using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>평균회귀 연습: 추세 점수 반대 방향 후보.</summary>
public sealed class MeanReversionSignalGenerator : IStrategySignalGenerator
{
    public TradingStrategyKind Kind => TradingStrategyKind.평균회귀;

    public StrategySignal Generate(
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(quote);
        if (string.IsNullOrWhiteSpace(quote.Symbol) || quote.LastPrice is null or <= 0)
        {
            return new StrategySignal(
                quote.Symbol ?? "",
                SignalSide.None,
                null,
                null,
                "mean_reversion_v1",
                "시세 없음",
                nowUtc,
                false);
        }

        if (baseOrderQuantity <= 0)
        {
            return new StrategySignal(
                quote.Symbol,
                SignalSide.Hold,
                null,
                quote.LastPrice,
                "mean_reversion_v1",
                "수량 정책 없음",
                nowUtc,
                false);
        }

        // 추세 점수의 반대 = 평균회귀
        var score = -TrendFollowSignalGenerator.MomentumScore(quote.Symbol, quote.LastPrice.Value, nowUtc);
        if (Math.Abs(score) < 0.18m)
        {
            return new StrategySignal(
                quote.Symbol,
                SignalSide.Hold,
                null,
                quote.LastPrice,
                "mean_reversion_v1",
                $"평균회귀 관망 {quote.Symbol} (이탈 약함 · 투자 조언 아님)",
                nowUtc,
                false);
        }

        var side = score > 0 ? SignalSide.Buy : SignalSide.Sell;
        var qty = Math.Max(1m, Math.Round(baseOrderQuantity * (1m + Math.Min(1.5m, Math.Abs(score))), 2));
        var sideKr = side == SignalSide.Buy ? "매수" : "매도";
        return new StrategySignal(
            quote.Symbol,
            side,
            qty,
            quote.LastPrice,
            "mean_reversion_v1",
            $"평균회귀 {sideKr} 후보 {quote.Symbol} @ {quote.LastPrice} · 수량 {qty} (투자 조언 아님 · 실주문 아님)",
            nowUtc,
            IsActionable: true);
    }
}
