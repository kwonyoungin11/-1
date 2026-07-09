namespace TradingBot.Domain;

/// <summary>
/// 관심 종목 카탈로그 — 스페이스X(SPCX) + 비전마린(VMAR).
/// 토스증권 실시세/실주문 대상 유니버스. 투자 조언 아님.
/// </summary>
public static class WatchlistCatalog
{
    public const string SpaceXSymbol = "SPCX";
    public const string VmarSymbol = "VMAR";

    public static IReadOnlyList<StockMarketKind> AllKinds { get; } =
    [
        StockMarketKind.스페이스X,
        StockMarketKind.비전마린,
    ];

    public static IReadOnlyList<string> KindLabels { get; } =
        AllKinds.Select(k => k.ToString()).ToArray();

    public static IReadOnlyList<string> ResolveSymbols(StockMarketKind kind) => kind switch
    {
        StockMarketKind.스페이스X => [SpaceXSymbol],
        StockMarketKind.비전마린 => [VmarSymbol],
        _ => [],
    };

    public static string Describe(StockMarketKind kind) => kind switch
    {
        StockMarketKind.스페이스X => "스페이스X (SPCX) — 토스증권 단일 종목",
        StockMarketKind.비전마린 =>
            "비전 마린 테크놀로지 (VMAR) — 연습 1분봉 분할매매 · 투자 조언 아님",
        _ => "알 수 없는 종목",
    };

    /// <summary>차트 seed 가격 (실봉 없을 때만). SPCX / VMAR 연습 시드.</summary>
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

        if (symbol.Equals(VmarSymbol, StringComparison.OrdinalIgnoreCase))
        {
            return 3.5;
        }

        // 알 수 없는 심볼: SPCX 시드로 폴백 (기존 차트 동작 유지)
        return 85;
    }

    /// <summary>알려진 유니버스 심볼 여부 (대소문자 무시).</summary>
    public static bool IsKnownSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        return symbol.Equals(SpaceXSymbol, StringComparison.OrdinalIgnoreCase)
            || symbol.Equals(VmarSymbol, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 알려진 심볼을 카탈로그 정식 표기(SPCX / VMAR)로 정규화. 미지 심볼은 null.
    /// </summary>
    public static string? NormalizeKnownSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        if (symbol.Equals(SpaceXSymbol, StringComparison.OrdinalIgnoreCase))
        {
            return SpaceXSymbol;
        }

        if (symbol.Equals(VmarSymbol, StringComparison.OrdinalIgnoreCase))
        {
            return VmarSymbol;
        }

        return null;
    }

    /// <summary>하위 호환 기본 심볼 (항상 SPCX). VMAR은 비전마린 kind로 선택.</summary>
    public static string PrimarySymbol => SpaceXSymbol;
}
