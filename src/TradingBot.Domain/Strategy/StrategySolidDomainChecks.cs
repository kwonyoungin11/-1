namespace TradingBot.Domain;

/// <summary>
/// Pure Domain pieces of the machine-checkable "strategy solid" stop condition.
/// No Risk / Application types — full stack report lives in Application
/// (<c>StrategySolidEvaluator</c>).
/// </summary>
public static class StrategySolidDomainChecks
{
    /// <summary>스페이스X 유니버스: enum + ResolveSymbols = [SPCX].</summary>
    public static bool IsSpacexUniverseOk()
    {
        if (!Enum.IsDefined(StockMarketKind.스페이스X))
        {
            return false;
        }

        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.스페이스X);
        if (symbols is null || symbols.Count != 1)
        {
            return false;
        }

        return symbols[0].Equals(WatchlistCatalog.SpaceXSymbol, StringComparison.Ordinal);
    }

    /// <summary>비전마린 유니버스: enum + ResolveSymbols = [VMAR].</summary>
    public static bool IsVmarUniverseOk()
    {
        if (!Enum.IsDefined(StockMarketKind.비전마린))
        {
            return false;
        }

        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.비전마린);
        if (symbols is null || symbols.Count != 1)
        {
            return false;
        }

        return symbols[0].Equals(WatchlistCatalog.VmarSymbol, StringComparison.Ordinal)
            && WatchlistCatalog.IsKnownSymbol(WatchlistCatalog.VmarSymbol);
    }

    /// <summary>하위 호환 별칭 — 코어3 제거 후 스페이스X 검사로 연결.</summary>
    public static bool IsCore3UniverseOk() => IsSpacexUniverseOk();

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

    /// <summary>All Domain-only strategy-solid pieces pass (both universes + sizer + trend).</summary>
    public static bool AllDomainChecksOk() =>
        IsSpacexUniverseOk()
        && IsVmarUniverseOk()
        && IsPositionRiskSizerOk()
        && IsTrendFollowParametersOk();
}
