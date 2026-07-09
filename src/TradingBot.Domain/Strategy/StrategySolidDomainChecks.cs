namespace TradingBot.Domain;

/// <summary>
/// Pure Domain pieces of the machine-checkable "strategy solid" stop condition.
/// No Risk / Application types — full stack report lives in Application
/// (<c>StrategySolidEvaluator</c>).
/// </summary>
public static class StrategySolidDomainChecks
{
    /// <summary>나스닥코어3 enum + ResolveSymbols count 3 with QQQ, NVDA, AAPL.</summary>
    public static bool IsCore3UniverseOk()
    {
        if (!Enum.IsDefined(StockMarketKind.나스닥코어3))
        {
            return false;
        }

        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.나스닥코어3);
        if (symbols is null || symbols.Count != 3)
        {
            return false;
        }

        // Order-independent membership (catalog currently returns QQQ, NVDA, AAPL).
        return symbols.Contains("QQQ", StringComparer.Ordinal)
            && symbols.Contains("NVDA", StringComparer.Ordinal)
            && symbols.Contains("AAPL", StringComparer.Ordinal);
    }

    /// <summary>Known vector: equity 100000, risk 1%, stop 2%, price 100 → quantity 500.</summary>
    public static bool IsPositionRiskSizerOk()
    {
        var result = PositionRiskSizer.Calculate(
            equity: 100_000m,
            riskPercentPerTrade: 1m,
            stopLossPercent: 2m,
            price: 100m);
        return result.Quantity == 500m && result.IsValid;
    }

    /// <summary><see cref="TrendFollowParameters.CreateSafeDefaults"/> is non-null with positive practice fields.</summary>
    public static bool IsTrendFollowParametersOk()
    {
        var p = TrendFollowParameters.CreateSafeDefaults();
        return p is not null
            && p.StopLossR > 0m
            && p.TakeProfitR > 0m
            && p.CooldownBars > 0
            && p.MinMomentumScore > 0m;
    }

    /// <summary>All Domain-only strategy-solid pieces pass.</summary>
    public static bool AllDomainChecksOk() =>
        IsCore3UniverseOk()
        && IsPositionRiskSizerOk()
        && IsTrendFollowParametersOk();
}
