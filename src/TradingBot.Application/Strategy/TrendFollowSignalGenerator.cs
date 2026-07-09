using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// 추세추종 연습: 심볼·시간 기반 모의 모멘텀으로 매수/매도 방향 결정.
/// (실시세 연결 전 deterministic mock — 투자 조언 아님)
/// </summary>
public sealed class TrendFollowSignalGenerator : IStrategySignalGenerator
{
    public TradingStrategyKind Kind => TradingStrategyKind.추세추종;

    public StrategySignal Generate(
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(quote);
        if (!TryValid(quote, baseOrderQuantity, out var invalid))
        {
            return invalid!;
        }

        var score = MomentumScore(quote.Symbol, quote.LastPrice!.Value, nowUtc);
        // score > 0 → 상승 추세 가정 → 매수, < 0 → 매도
        if (Math.Abs(score) < 0.15m)
        {
            return new StrategySignal(
                quote.Symbol,
                SignalSide.Hold,
                null,
                quote.LastPrice,
                "trend_follow_v1",
                $"추세추종 관망 {quote.Symbol} (모멘텀 약함 · 투자 조언 아님)",
                nowUtc,
                IsActionable: false);
        }

        var side = score > 0 ? SignalSide.Buy : SignalSide.Sell;
        var qty = Math.Max(1m, Math.Round(baseOrderQuantity * (1m + Math.Min(2m, Math.Abs(score))), 2));
        var sideKr = side == SignalSide.Buy ? "매수" : "매도";
        return new StrategySignal(
            quote.Symbol,
            side,
            qty,
            quote.LastPrice,
            "trend_follow_v1",
            $"추세추종 {sideKr} 후보 {quote.Symbol} @ {quote.LastPrice} · 수량 {qty} (규모↑=버블↑ · 투자 조언 아님)",
            nowUtc,
            IsActionable: true);
    }

    internal static decimal MomentumScore(string symbol, decimal price, DateTimeOffset nowUtc)
    {
        // 결정론적 pseudo-momentum: 가격 위치 + 분 단위 드리프트
        var bucket = (nowUtc.Minute / 5) % 6;
        var hash = Math.Abs(HashCode.Combine(symbol, nowUtc.Day));
        var bias = ((hash % 100) - 50) / 100m; // -0.5..0.49
        var priceBias = price >= 200m ? 0.2m : price <= 50m ? -0.2m : 0m;
        var timeBias = (bucket - 2.5m) / 10m;
        return Math.Clamp(bias + priceBias + timeBias, -1.5m, 1.5m);
    }

    private static bool TryValid(QuoteSnapshot quote, decimal qty, out StrategySignal? invalid)
    {
        if (string.IsNullOrWhiteSpace(quote.Symbol) || quote.LastPrice is null or <= 0)
        {
            invalid = new StrategySignal(
                quote.Symbol ?? "",
                SignalSide.None,
                null,
                null,
                "trend_follow_v1",
                "시세 없음",
                DateTimeOffset.UtcNow,
                false);
            return false;
        }

        if (qty <= 0)
        {
            invalid = new StrategySignal(
                quote.Symbol,
                SignalSide.Hold,
                null,
                quote.LastPrice,
                "trend_follow_v1",
                "수량 정책 없음",
                DateTimeOffset.UtcNow,
                false);
            return false;
        }

        invalid = null;
        return true;
    }
}
