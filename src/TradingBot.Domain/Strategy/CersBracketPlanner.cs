namespace TradingBot.Domain;

/// <summary>
/// Builds a long LIMIT bracket from CERS entry rules:
/// entry = last, stop = entry × (1 − 1.2%), TP = entry × (1 + expected × 1.5).
/// Quantity from equity risk or catalog base. Display / dry-run metadata only.
/// Not investment advice. Live orders remain gated.
/// </summary>
public static class CersBracketPlanner
{
    /// <summary>
    /// Plan a long CERS bracket. Invalid when price ≤ 0 or expected edge ≤ 0.
    /// </summary>
    public static TradeBracketPlan PlanLong(
        string symbol,
        decimal lastPrice,
        double expectedEdge,
        decimal equity,
        decimal riskPercentPerTrade = 1m,
        decimal? baseQuantity = null)
    {
        var sym = string.IsNullOrWhiteSpace(symbol) ? "?" : symbol.Trim().ToUpperInvariant();
        var baseQty = baseQuantity ?? StrategyCatalog.BaseQuantity(TradingStrategyKind.CERS비용회귀);

        if (lastPrice <= 0m)
        {
            return TradeBracketPlan.Invalid(sym, "가격 비정상 — CERS 브래킷 불가 · 실주문 없음");
        }

        if (expectedEdge <= 0 || double.IsNaN(expectedEdge) || double.IsInfinity(expectedEdge))
        {
            return TradeBracketPlan.Invalid(sym, "CERS 엣지 없음 — 브래킷 불가 · 실주문 없음");
        }

        var entry = lastPrice;
        var stop = entry * (1m - (decimal)CersPreset.StopLossPct);
        var tpMultiple = (decimal)(expectedEdge * CersPreset.TakeProfitExpectedMultiple);
        var takeProfit = entry * (1m + tpMultiple);

        if (stop <= 0m || takeProfit <= entry)
        {
            return TradeBracketPlan.Invalid(sym, "CERS SL/TP 비정상 — 브래킷 불가 · 실주문 없음");
        }

        var stopDistance = entry - stop;
        var stopPct = (decimal)(CersPreset.StopLossPct * 100.0); // 1.2

        decimal quantity;
        if (equity > 0m && riskPercentPerTrade > 0m)
        {
            var size = PositionRiskSizer.Calculate(
                equity,
                riskPercentPerTrade,
                stopPct,
                entry);
            quantity = size.IsValid && size.Quantity > 0m ? size.Quantity : baseQty;
        }
        else
        {
            quantity = baseQty;
        }

        if (quantity <= 0m)
        {
            return TradeBracketPlan.Invalid(sym, "수량 0 — CERS 브래킷 불가 · 실주문 없음");
        }

        var riskAmt = quantity * stopDistance;
        var rewardAmt = quantity * (takeProfit - entry);
        var rr = riskAmt > 0m ? Math.Round(rewardAmt / riskAmt, 2) : 0m;
        var notional = quantity * entry;
        var fees = TossUsEquityCommissionSchedule.EstimateRoundTrip(notional, quantity * takeProfit);
        var netReward = Math.Max(0m, rewardAmt - fees.TotalUsd);
        var netRisk = riskAmt + fees.TotalUsd;
        var netRr = netRisk > 0m ? Math.Round(netReward / netRisk, 2) : 0m;

        return new TradeBracketPlan(
            Symbol: sym,
            Side: "BUY",
            OrderType: "LIMIT",
            EntryLimit: entry,
            StopPrice: stop,
            TakeProfitPrice: takeProfit,
            Quantity: quantity,
            StopDistancePerShare: stopDistance,
            RiskAmount: Math.Round(riskAmt, 2),
            RewardAmount: Math.Round(rewardAmt, 2),
            RewardRiskRatio: rr,
            Notional: Math.Round(notional, 2),
            Atr: null,
            StopSource: BracketStopSource.Percent,
            IsValid: true,
            OwnerMessage:
                $"CERS LIMIT {entry:N2} · SL {stop:N2} (−{CersPreset.StopLossPct:P1}) · " +
                $"TP {takeProfit:N2} (exp×{CersPreset.TakeProfitExpectedMultiple}) · " +
                $"수량 {quantity:N0} · 리스크 ${riskAmt:N2} · 수수료≈${fees.TotalUsd:N2} · " +
                $"R:R 1:{rr:N2} (수수료후≈1:{netRr:N2}) · 실주문 잠금 · 투자 조언 아님",
            EstimatedCommissionUsd: fees.TotalUsd,
            NetRewardAmount: Math.Round(netReward, 2),
            NetRewardRiskRatio: netRr);
    }
}
