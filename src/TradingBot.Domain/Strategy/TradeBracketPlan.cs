namespace TradingBot.Domain;

/// <summary>How stop distance was derived.</summary>
public enum BracketStopSource
{
    Atr = 0,
    Percent = 1,
    Invalid = 2,
}

/// <summary>
/// Planned LIMIT entry + stop + take-profit levels (bracket / OCO plan).
/// Display and dry-run metadata only unless owner unlocks live orders.
/// Not investment advice. Commissions are estimates (Toss US schedule).
/// </summary>
public sealed record TradeBracketPlan(
    string Symbol,
    string Side,
    string OrderType,
    decimal EntryLimit,
    decimal StopPrice,
    decimal TakeProfitPrice,
    decimal Quantity,
    decimal StopDistancePerShare,
    decimal RiskAmount,
    decimal RewardAmount,
    decimal RewardRiskRatio,
    decimal Notional,
    decimal? Atr,
    BracketStopSource StopSource,
    bool IsValid,
    string OwnerMessage,
    decimal EstimatedCommissionUsd = 0m,
    decimal NetRewardAmount = 0m,
    decimal NetRewardRiskRatio = 0m)
{
    public static TradeBracketPlan Invalid(string symbol, string reason) => new(
        Symbol: symbol,
        Side: "BUY",
        OrderType: "LIMIT",
        EntryLimit: 0m,
        StopPrice: 0m,
        TakeProfitPrice: 0m,
        Quantity: 0m,
        StopDistancePerShare: 0m,
        RiskAmount: 0m,
        RewardAmount: 0m,
        RewardRiskRatio: 0m,
        Notional: 0m,
        Atr: null,
        StopSource: BracketStopSource.Invalid,
        IsValid: false,
        OwnerMessage: reason);
}

/// <summary>
/// Builds a long LIMIT bracket from last price, equity risk, ATR or % stop, and R-multiples.
/// Short side reserved for later (symmetric math).
/// </summary>
public static class TradeBracketPlanner
{
    /// <summary>
    /// Long-only LIMIT plan for SPCX-style names.
    /// </summary>
    public static TradeBracketPlan PlanLongLimit(
        string symbol,
        decimal lastPrice,
        decimal equity,
        SpacexRiskParameters risk,
        double? atr,
        TrendFollowParameters? trend = null)
    {
        ArgumentNullException.ThrowIfNull(risk);
        if (string.IsNullOrWhiteSpace(symbol) || lastPrice <= 0m || equity <= 0m)
        {
            return TradeBracketPlan.Invalid(
                symbol ?? "?",
                "가격·잔액 부족 — 지정가 계획 불가 · 실주문 없음");
        }

        var tpR = trend?.TakeProfitR > 0 ? trend.TakeProfitR : risk.TakeProfitR;
        if (tpR <= 0m)
        {
            tpR = 2.0m;
        }

        decimal stopDistance;
        BracketStopSource source;
        if (risk.UseAtrStops && atr is double a && a > 0)
        {
            stopDistance = (decimal)a * risk.AtrStopMultiple;
            source = BracketStopSource.Atr;
        }
        else
        {
            stopDistance = lastPrice * (risk.FallbackStopLossPercent / 100m);
            source = BracketStopSource.Percent;
        }

        if (stopDistance <= 0m || stopDistance >= lastPrice)
        {
            return TradeBracketPlan.Invalid(
                symbol,
                "손절 거리 비정상 — 계획 무효 · 실주문 없음");
        }

        var offset = risk.UseAtrStops && atr is double a2 && a2 > 0
            ? (decimal)a2 * risk.LimitOffsetAtrFraction
            : lastPrice * 0.001m;
        var entry = Math.Max(0.01m, Math.Round(lastPrice - offset, 2, MidpointRounding.AwayFromZero));

        var stop = Math.Round(entry - stopDistance, 2, MidpointRounding.AwayFromZero);
        if (stop <= 0m)
        {
            stop = Math.Round(entry * 0.5m, 2, MidpointRounding.AwayFromZero);
            stopDistance = entry - stop;
        }

        var takeProfit = Math.Round(entry + (stopDistance * tpR), 2, MidpointRounding.AwayFromZero);

        var stopPct = entry > 0m ? (stopDistance / entry) * 100m : risk.FallbackStopLossPercent;
        var size = PositionRiskSizer.Calculate(
            equity,
            risk.RiskPercentPerTrade,
            stopPct,
            entry);

        if (!size.IsValid || size.Quantity <= 0m)
        {
            return new TradeBracketPlan(
                Symbol: symbol.Trim().ToUpperInvariant(),
                Side: "BUY",
                OrderType: "LIMIT",
                EntryLimit: entry,
                StopPrice: stop,
                TakeProfitPrice: takeProfit,
                Quantity: 0m,
                StopDistancePerShare: stopDistance,
                RiskAmount: 0m,
                RewardAmount: 0m,
                RewardRiskRatio: tpR,
                Notional: 0m,
                Atr: atr is double ad ? (decimal)ad : null,
                StopSource: source,
                IsValid: false,
                OwnerMessage:
                    "수량 0 (리스크 예산 < 1주 손절) · 지정가 계획만 표시 · 실주문 잠금 · 투자 조언 아님");
        }

        var riskAmt = size.Quantity * stopDistance;
        var rewardAmt = size.Quantity * (takeProfit - entry);
        var rr = riskAmt > 0m ? Math.Round(rewardAmt / riskAmt, 2) : tpR;
        var buyNotional = size.Quantity * entry;
        var sellNotionalTp = size.Quantity * takeProfit;
        var fees = TossUsEquityCommissionSchedule.EstimateRoundTrip(buyNotional, sellNotionalTp);
        var netReward = Math.Max(0m, rewardAmt - fees.TotalUsd);
        // Net risk roughly risk + buy fee (stop exit still pays sell fees) — conservative: risk + full RT fees
        var netRisk = riskAmt + fees.TotalUsd;
        var netRr = netRisk > 0m ? Math.Round(netReward / netRisk, 2) : 0m;
        var srcLabel = source == BracketStopSource.Atr ? $"ATR×{risk.AtrStopMultiple}" : $"%{risk.FallbackStopLossPercent}";

        return new TradeBracketPlan(
            Symbol: symbol.Trim().ToUpperInvariant(),
            Side: "BUY",
            OrderType: "LIMIT",
            EntryLimit: entry,
            StopPrice: stop,
            TakeProfitPrice: takeProfit,
            Quantity: size.Quantity,
            StopDistancePerShare: stopDistance,
            RiskAmount: Math.Round(riskAmt, 2),
            RewardAmount: Math.Round(rewardAmt, 2),
            RewardRiskRatio: rr,
            Notional: Math.Round(buyNotional, 2),
            Atr: atr is double ad2 ? Math.Round((decimal)ad2, 4) : null,
            StopSource: source,
            IsValid: true,
            OwnerMessage:
                $"지정가 LIMIT {entry:N2} · SL {stop:N2} ({srcLabel}) · TP {takeProfit:N2} ({tpR}R) · " +
                $"수량 {size.Quantity:N0} · 리스크 ${riskAmt:N2} · 수수료≈${fees.TotalUsd:N2} · " +
                $"R:R 1:{rr:N2} (수수료후≈1:{netRr:N2}) · 실주문 잠금 · 투자 조언 아님",
            EstimatedCommissionUsd: fees.TotalUsd,
            NetRewardAmount: Math.Round(netReward, 2),
            NetRewardRiskRatio: netRr);
    }
}
