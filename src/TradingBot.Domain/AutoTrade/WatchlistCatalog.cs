namespace TradingBot.Domain;

/// <summary>대상 주식 종류별 관심 종목 카탈로그 (연습/읽기 전용).</summary>
public static class WatchlistCatalog
{
    public static IReadOnlyList<StockMarketKind> AllKinds { get; } =
    [
        StockMarketKind.나스닥,
        StockMarketKind.나스닥테크,
        StockMarketKind.나스닥코어3,
        StockMarketKind.미국주식,
        StockMarketKind.미국ETF,
        StockMarketKind.국내주식,
    ];

    public static IReadOnlyList<string> KindLabels { get; } =
        AllKinds.Select(k => k.ToString()).ToArray();

    public static IReadOnlyList<string> ResolveSymbols(StockMarketKind kind) => kind switch
    {
        StockMarketKind.나스닥 => ["AAPL", "MSFT", "NVDA", "AMZN", "META", "GOOGL", "TSLA"],
        StockMarketKind.나스닥테크 => ["NVDA", "AVGO", "AMD", "INTC", "CRM", "ADBE", "ORCL"],
        // 코어 3종 프리셋: QQQ + NVDA + AAPL (투자 권유 아님, 연습/시그널 유니버스)
        StockMarketKind.나스닥코어3 => ["QQQ", "NVDA", "AAPL"],
        StockMarketKind.미국주식 => ["AAPL", "MSFT", "JPM", "JNJ", "XOM", "WMT", "BAC"],
        StockMarketKind.미국ETF => ["SPY", "QQQ", "IWM", "DIA", "VTI"],
        StockMarketKind.국내주식 => ["005930", "000660", "035420", "051910", "005380"],
        _ => ["AAPL"],
    };

    public static string Describe(StockMarketKind kind) => kind switch
    {
        StockMarketKind.나스닥 => "나스닥 대표 대형주 7종",
        StockMarketKind.나스닥테크 => "나스닥 기술·반도체 중심",
        StockMarketKind.나스닥코어3 => "나스닥 코어 3종 (QQQ·NVDA·AAPL) — 연습 유니버스",
        StockMarketKind.미국주식 => "미국 대형 우량·금융·소비",
        StockMarketKind.미국ETF => "미국 대표 ETF (지수 추종)",
        StockMarketKind.국내주식 => "국내 대표 대형주 (코드)",
        _ => "관심 종목",
    };

    /// <summary>차트 seed 가격 (연습 봉). 심볼별 고정 시드.</summary>
    public static double ChartSeedPrice(string symbol) => symbol switch
    {
        "AAPL" => 190,
        "MSFT" => 420,
        "NVDA" => 120,
        "AMZN" => 185,
        "META" => 510,
        "GOOGL" => 175,
        "TSLA" => 250,
        "AVGO" => 160,
        "AMD" => 145,
        "INTC" => 32,
        "CRM" => 280,
        "ADBE" => 520,
        "ORCL" => 140,
        "JPM" => 198,
        "JNJ" => 155,
        "XOM" => 110,
        "WMT" => 70,
        "BAC" => 38,
        "SPY" => 540,
        "QQQ" => 470,
        "IWM" => 210,
        "DIA" => 400,
        "VTI" => 270,
        "005930" => 72000,
        "000660" => 190000,
        "035420" => 210000,
        "051910" => 320000,
        "005380" => 240000,
        _ => 100 + Math.Abs(symbol.GetHashCode(StringComparison.Ordinal) % 400),
    };
}
