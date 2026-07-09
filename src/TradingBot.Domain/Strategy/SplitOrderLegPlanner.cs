namespace TradingBot.Domain;

/// <summary>분할 주문 한 레그 (순수 도메인 · 실행 아님).</summary>
public sealed record SplitOrderLeg(string Side, decimal Quantity, decimal LimitPrice, int LegIndex);

/// <summary>
/// 총 수량을 정수 주 레그로 나누고 기준가 대비 step% 간격 LIMIT 가격을 계산.
/// Fail-closed: 조건 불충족 시 빈 목록. 투자 조언 아님 · 실주문 경로 아님.
/// </summary>
public static class SplitOrderLegPlanner
{
    /// <summary>
    /// BUY: leg i price = refPrice * (1 - i * stepPercent/100).
    /// SELL: leg i price = refPrice * (1 + i * stepPercent/100).
    /// Empty if totalQty &lt; legCount or refPrice &lt;= 0 or legCount &lt; 2.
    /// </summary>
    public static IReadOnlyList<SplitOrderLeg> Plan(
        string side,
        decimal totalQuantity,
        decimal referencePrice,
        int legCount,
        decimal stepPercent)
    {
        if (legCount < 2
            || totalQuantity < legCount
            || referencePrice <= 0m
            || stepPercent < 0m)
        {
            return [];
        }

        var normalizedSide = NormalizeSide(side);
        if (normalizedSide is null)
        {
            return [];
        }

        var quantities = SplitWholeShares(totalQuantity, legCount);
        if (quantities.Count != legCount)
        {
            return [];
        }

        var legs = new List<SplitOrderLeg>(legCount);
        var stepFraction = stepPercent / 100m;
        var isBuy = normalizedSide == "BUY";

        for (var i = 0; i < legCount; i++)
        {
            var signedStep = isBuy ? -i * stepFraction : i * stepFraction;
            var limitPrice = referencePrice * (1m + signedStep);
            if (limitPrice <= 0m)
            {
                return [];
            }

            legs.Add(new SplitOrderLeg(normalizedSide, quantities[i], limitPrice, i));
        }

        return legs;
    }

    private static string? NormalizeSide(string? side)
    {
        if (string.IsNullOrWhiteSpace(side))
        {
            return null;
        }

        if (side.Equals("BUY", StringComparison.OrdinalIgnoreCase))
        {
            return "BUY";
        }

        if (side.Equals("SELL", StringComparison.OrdinalIgnoreCase))
        {
            return "SELL";
        }

        return null;
    }

    /// <summary>
    /// Split total into legCount whole-share legs; remainder goes to earliest legs.
    /// Requires totalQuantity &gt;= legCount so each leg gets at least 1.
    /// </summary>
    private static IReadOnlyList<decimal> SplitWholeShares(decimal totalQuantity, int legCount)
    {
        var totalWhole = decimal.Floor(totalQuantity);
        if (totalWhole < legCount)
        {
            return [];
        }

        var baseQty = decimal.Floor(totalWhole / legCount);
        var remainder = totalWhole - (baseQty * legCount);
        var result = new decimal[legCount];
        for (var i = 0; i < legCount; i++)
        {
            var extra = i < remainder ? 1m : 0m;
            result[i] = baseQty + extra;
            if (result[i] < 1m)
            {
                return [];
            }
        }

        return result;
    }
}
