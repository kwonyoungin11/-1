using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// 모멘텀 돌파 연습: 강한 모멘텀일 때만 행동, 수량(규모)을 크게 → 차트 버블↑.
/// </summary>
public sealed class MomentumBreakoutSignalGenerator : IStrategySignalGenerator
{
    public TradingStrategyKind Kind => TradingStrategyKind.모멘텀돌파;

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
                "momentum_breakout_v1",
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
                "momentum_breakout_v1",
                "수량 정책 없음",
                nowUtc,
                false);
        }

        var score = TrendFollowSignalGenerator.MomentumScore(quote.Symbol, quote.LastPrice.Value, nowUtc);
        // 돌파: 임계값 높음 — 가끔만 신호
        if (Math.Abs(score) < 0.45m)
        {
            return new StrategySignal(
                quote.Symbol,
                SignalSide.Hold,
                null,
                quote.LastPrice,
                "momentum_breakout_v1",
                $"모멘텀돌파 대기 {quote.Symbol} (돌파 미충족 · 투자 조언 아님)",
                nowUtc,
                false);
        }

        var side = score > 0 ? SignalSide.Buy : SignalSide.Sell;
        // 규모 크게: 버블 SizeWeight = 수량×가격 이므로 qty 확대
        var qty = Math.Max(3m, Math.Round(baseOrderQuantity * (2.5m + Math.Abs(score)), 2));
        var sideKr = side == SignalSide.Buy ? "매수" : "매도";
        return new StrategySignal(
            quote.Symbol,
            side,
            qty,
            quote.LastPrice,
            "momentum_breakout_v1",
            $"모멘텀돌파 {sideKr} 후보 {quote.Symbol} @ {quote.LastPrice} · 수량 {qty} (큰 규모·큰 버블 · 투자 조언 아님)",
            nowUtc,
            IsActionable: true);
    }
}
