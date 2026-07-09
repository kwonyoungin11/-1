namespace TradingBot.Domain;

/// <summary>
/// Published-style Toss Securities US equity commission schedule for planning only.
/// Rates can change; verify on account statement. Not investment advice.
/// Sources: Toss US stock 0.1% standard (from 2025-12), SEC fee on sell, ≤$10 notional free brokerage note.
/// </summary>
public static class TossUsEquityCommissionSchedule
{
    /// <summary>US equity brokerage rate per side (buy or sell).</summary>
    public const decimal BrokerageRate = 0.001m; // 0.1%

    /// <summary>SEC fee rate on sell notional (approx; published rates change).</summary>
    public const decimal SecFeeRate = 0.0000206m;

    public const decimal MinSecFeeUsd = 0.01m;

    /// <summary>Open API note: brokerage waived when fill notional ≤ this per order.</summary>
    public const decimal FreeBrokerageMaxNotionalUsd = 10m;

    /// <summary>Floor: amounts under $0.01 become 0 (Toss: $0.01 미만 절사).</summary>
    public static decimal FloorToCent(decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        // Truncate toward zero to cents (절사)
        return Math.Floor(amount * 100m) / 100m;
    }

    public static decimal BrokerageFee(decimal notionalUsd)
    {
        if (notionalUsd <= 0m)
        {
            return 0m;
        }

        if (notionalUsd <= FreeBrokerageMaxNotionalUsd)
        {
            return 0m;
        }

        return FloorToCent(notionalUsd * BrokerageRate);
    }

    public static decimal SecFeeOnSell(decimal sellNotionalUsd)
    {
        if (sellNotionalUsd <= 0m)
        {
            return 0m;
        }

        var raw = sellNotionalUsd * SecFeeRate;
        if (raw <= 0m)
        {
            return 0m;
        }

        // Published practice: SEC has a small minimum; rate also changes over time.
        return Math.Max(FloorToCent(raw), MinSecFeeUsd);
    }

    /// <summary>
    /// Round-trip estimate: buy brokerage + sell brokerage + SEC on sell.
    /// FX conversion spread not included (assume USD cash).
    /// </summary>
    public static CommissionEstimate EstimateRoundTrip(
        decimal buyNotionalUsd,
        decimal sellNotionalUsd)
    {
        var buy = BrokerageFee(buyNotionalUsd);
        var sell = BrokerageFee(sellNotionalUsd);
        var sec = SecFeeOnSell(sellNotionalUsd);
        var total = buy + sell + sec;
        var baseN = buyNotionalUsd > 0m ? buyNotionalUsd : sellNotionalUsd;
        var pct = baseN > 0m ? Math.Round(total / baseN * 100m, 4) : 0m;
        return new CommissionEstimate(
            BuyBrokerage: buy,
            SellBrokerage: sell,
            SecFee: sec,
            TotalUsd: total,
            TotalAsPercentOfBuyNotional: pct,
            FxExcluded: true,
            OwnerNote:
                "토스 미국주식 위탁 약 0.1%/쪽 + 매도 SEC 근사 · $10 이하 위탁 0 · 환전 별도 · 계좌 요율 확인 · 투자 조언 아님");
    }
}

/// <summary>Planned commission breakdown (USD).</summary>
public sealed record CommissionEstimate(
    decimal BuyBrokerage,
    decimal SellBrokerage,
    decimal SecFee,
    decimal TotalUsd,
    decimal TotalAsPercentOfBuyNotional,
    bool FxExcluded,
    string OwnerNote);
