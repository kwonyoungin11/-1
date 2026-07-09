namespace TradingBot.Domain;

/// <summary>
/// 차트 시간봉. UI 9종. 토스 API 원본 interval 은 1m/1d 뿐 — 나머지는 클라이언트 집계.
/// </summary>
public enum ChartTimeframe
{
    분봉1 = 0,
    분봉5 = 1,
    분봉10 = 2,
    분봉15 = 3,
    분봉30 = 4,
    분봉60 = 5,
    분봉240 = 6,
    일봉 = 7,
    주봉 = 8,
}

/// <summary>시간봉 카탈로그 · 토스 소스 interval · 축 UnitWidth · UI 라벨.</summary>
public static class ChartTimeframeCatalog
{
    public static IReadOnlyList<ChartTimeframe> All { get; } =
    [
        ChartTimeframe.분봉1,
        ChartTimeframe.분봉5,
        ChartTimeframe.분봉10,
        ChartTimeframe.분봉15,
        ChartTimeframe.분봉30,
        ChartTimeframe.분봉60,
        ChartTimeframe.분봉240,
        ChartTimeframe.일봉,
        ChartTimeframe.주봉,
    ];

    public static IReadOnlyList<string> Labels { get; } =
        All.Select(UiLabel).ToArray();

    public static string UiLabel(ChartTimeframe tf) => tf switch
    {
        ChartTimeframe.분봉1 => "1m",
        ChartTimeframe.분봉5 => "5m",
        ChartTimeframe.분봉10 => "10m",
        ChartTimeframe.분봉15 => "15m",
        ChartTimeframe.분봉30 => "30m",
        ChartTimeframe.분봉60 => "60m",
        ChartTimeframe.분봉240 => "240m",
        ChartTimeframe.일봉 => "1D",
        ChartTimeframe.주봉 => "1W",
        _ => "1m",
    };

    public static string Describe(ChartTimeframe tf) => tf switch
    {
        ChartTimeframe.분봉1 => "1분 봉 (토스 1m 직행)",
        ChartTimeframe.분봉5 => "5분 봉 (1m 집계)",
        ChartTimeframe.분봉10 => "10분 봉 (1m 집계)",
        ChartTimeframe.분봉15 => "15분 봉 (1m 집계)",
        ChartTimeframe.분봉30 => "30분 봉 (1m 집계)",
        ChartTimeframe.분봉60 => "60분 봉 (1m 집계)",
        ChartTimeframe.분봉240 => "240분 봉 (1m 집계)",
        ChartTimeframe.일봉 => "일 봉 (토스 1d 직행)",
        ChartTimeframe.주봉 => "주 봉 (1d 집계 · 월요일 시작 UTC)",
        _ => "시간봉",
    };

    /// <summary>토스 OpenAPI 에 실제로 요청할 interval (1m 또는 1d만).</summary>
    public static string SourceTossInterval(ChartTimeframe tf) => tf switch
    {
        ChartTimeframe.일봉 or ChartTimeframe.주봉 => "1d",
        _ => "1m",
    };

    /// <summary>분 단위 집계 크기. null 이면 소스 봉 그대로 (1m 또는 1d).</summary>
    public static int? AggregationMinutes(ChartTimeframe tf) => tf switch
    {
        ChartTimeframe.분봉1 => null,
        ChartTimeframe.분봉5 => 5,
        ChartTimeframe.분봉10 => 10,
        ChartTimeframe.분봉15 => 15,
        ChartTimeframe.분봉30 => 30,
        ChartTimeframe.분봉60 => 60,
        ChartTimeframe.분봉240 => 240,
        ChartTimeframe.일봉 => null,
        ChartTimeframe.주봉 => null, // week aggregation is day-based, not minute
        _ => null,
    };

    public static bool IsWeeklyAggregation(ChartTimeframe tf) => tf == ChartTimeframe.주봉;

    public static bool NeedsAggregation(ChartTimeframe tf) =>
        AggregationMinutes(tf) is not null || IsWeeklyAggregation(tf);

    /// <summary>X축 UnitWidth / 라벨 간격용 봉 길이.</summary>
    public static TimeSpan BarDuration(ChartTimeframe tf) => tf switch
    {
        ChartTimeframe.분봉1 => TimeSpan.FromMinutes(1),
        ChartTimeframe.분봉5 => TimeSpan.FromMinutes(5),
        ChartTimeframe.분봉10 => TimeSpan.FromMinutes(10),
        ChartTimeframe.분봉15 => TimeSpan.FromMinutes(15),
        ChartTimeframe.분봉30 => TimeSpan.FromMinutes(30),
        ChartTimeframe.분봉60 => TimeSpan.FromMinutes(60),
        ChartTimeframe.분봉240 => TimeSpan.FromMinutes(240),
        ChartTimeframe.일봉 => TimeSpan.FromDays(1),
        ChartTimeframe.주봉 => TimeSpan.FromDays(7),
        _ => TimeSpan.FromMinutes(1),
    };

    /// <summary>
    /// 집계 전 원본 봉을 충분히 모으기 위한 목표 raw count (페이지 합산 상한 전).
    /// 예: 15m × 160 표시 ≈ 160×15 = 2400 1m (페이지 캡으로 잘림).
    /// </summary>
    public static int PreferredRawBarCount(ChartTimeframe tf, int targetDisplayBars = 160)
    {
        var minutes = AggregationMinutes(tf);
        if (minutes is int m && m > 1)
        {
            return Math.Clamp(targetDisplayBars * m, 200, 1000);
        }

        if (IsWeeklyAggregation(tf))
        {
            return Math.Clamp(targetDisplayBars * 5, 200, 1000); // ~5 trading days/week
        }

        return Math.Clamp(targetDisplayBars, 50, 200);
    }

    public static bool TryParse(string? label, out ChartTimeframe tf)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            tf = ChartTimeframe.분봉1;
            return false;
        }

        var s = label.Trim();
        if (Enum.TryParse(s, ignoreCase: false, out tf) && Enum.IsDefined(tf))
        {
            return true;
        }

        // UiLabel / aliases
        switch (s.ToLowerInvariant())
        {
            case "1m":
            case "1분":
            case "분봉1":
            case "분봉":
                tf = ChartTimeframe.분봉1;
                return true;
            case "5m":
            case "5분":
            case "분봉5":
                tf = ChartTimeframe.분봉5;
                return true;
            case "10m":
            case "10분":
            case "분봉10":
                tf = ChartTimeframe.분봉10;
                return true;
            case "15m":
            case "15분":
            case "분봉15":
                tf = ChartTimeframe.분봉15;
                return true;
            case "30m":
            case "30분":
            case "분봉30":
                tf = ChartTimeframe.분봉30;
                return true;
            case "60m":
            case "1h":
            case "60분":
            case "분봉60":
                tf = ChartTimeframe.분봉60;
                return true;
            case "240m":
            case "4h":
            case "240분":
            case "분봉240":
                tf = ChartTimeframe.분봉240;
                return true;
            case "1d":
            case "d":
            case "일":
            case "일봉":
                tf = ChartTimeframe.일봉;
                return true;
            case "1w":
            case "w":
            case "주":
            case "주봉":
                tf = ChartTimeframe.주봉;
                return true;
            default:
                tf = ChartTimeframe.분봉1;
                return false;
        }
    }

    /// <summary>하위 호환: 예전 ToTossInterval — 소스 interval 만 반환.</summary>
    public static string ToTossInterval(ChartTimeframe tf) => SourceTossInterval(tf);
}
