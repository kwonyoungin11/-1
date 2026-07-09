namespace TradingBot.Domain;

/// <summary>
/// 차트 시간봉. 토스 OpenAPI candles interval enum 과 1:1 (1m, 1d).
/// </summary>
public enum ChartTimeframe
{
    /// <summary>1분봉 (Toss interval=1m).</summary>
    분봉1 = 0,

    /// <summary>일봉 (Toss interval=1d).</summary>
    일봉 = 1,
}

/// <summary>시간봉 → Toss API interval 문자열.</summary>
public static class ChartTimeframeCatalog
{
    public static IReadOnlyList<ChartTimeframe> All { get; } =
    [
        ChartTimeframe.분봉1,
        ChartTimeframe.일봉,
    ];

    public static IReadOnlyList<string> Labels { get; } =
        All.Select(t => t.ToString()).ToArray();

    public static string ToTossInterval(ChartTimeframe tf) => tf switch
    {
        ChartTimeframe.분봉1 => "1m",
        ChartTimeframe.일봉 => "1d",
        _ => "1m",
    };

    public static string Describe(ChartTimeframe tf) => tf switch
    {
        ChartTimeframe.분봉1 => "1분 봉 (토스 interval=1m)",
        ChartTimeframe.일봉 => "일 봉 (토스 interval=1d)",
        _ => "시간봉",
    };

    public static bool TryParse(string? label, out ChartTimeframe tf)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            tf = ChartTimeframe.분봉1;
            return false;
        }

        if (Enum.TryParse(label.Trim(), ignoreCase: false, out tf) && Enum.IsDefined(tf))
        {
            return true;
        }

        // 별칭
        switch (label.Trim().ToLowerInvariant())
        {
            case "1m":
            case "1분":
            case "분봉":
                tf = ChartTimeframe.분봉1;
                return true;
            case "1d":
            case "일":
            case "일봉":
                tf = ChartTimeframe.일봉;
                return true;
            default:
                tf = ChartTimeframe.분봉1;
                return false;
        }
    }
}
