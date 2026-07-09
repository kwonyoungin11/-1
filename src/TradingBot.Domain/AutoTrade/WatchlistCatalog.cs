namespace TradingBot.Domain;

/// <summary>
/// 관심 종목 카탈로그 — 스페이스X(SPCX) 단일 종목만.
/// 토스증권 실시세/실주문 대상 유니버스. 투자 조언 아님.
/// </summary>
public static class WatchlistCatalog
{
    public const string SpaceXSymbol = "SPCX";

    public static IReadOnlyList<StockMarketKind> AllKinds { get; } =
    [
        StockMarketKind.스페이스X,
    ];

    public static IReadOnlyList<string> KindLabels { get; } =
        AllKinds.Select(k => k.ToString()).ToArray();

    public static IReadOnlyList<string> ResolveSymbols(StockMarketKind kind) => kind switch
    {
        StockMarketKind.스페이스X => [SpaceXSymbol],
        _ => [SpaceXSymbol],
    };

    public static string Describe(StockMarketKind kind) => kind switch
    {
        StockMarketKind.스페이스X => "스페이스X (SPCX) — 토스증권 단일 종목",
        _ => "스페이스X (SPCX)",
    };

    /// <summary>차트 seed 가격 (실봉 없을 때만). SPCX 연습 시드.</summary>
    public static double ChartSeedPrice(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return 85;
        }

        if (symbol.Equals(SpaceXSymbol, StringComparison.OrdinalIgnoreCase)
            || symbol.Equals("SPACEX", StringComparison.OrdinalIgnoreCase))
        {
            return 85;
        }

        // 알 수 없는 심볼도 SPCX 시드로 폴백 (단일 유니버스 강제)
        return 85;
    }

    /// <summary>단일 유니버스 심볼 (항상 SPCX).</summary>
    public static string PrimarySymbol => SpaceXSymbol;
}
