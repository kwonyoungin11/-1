namespace TradingBot.Domain;

/// <summary>
/// Korea Standard Time (UTC+9) helpers for chart axes and owner-facing timestamps.
/// </summary>
public static class KoreaTime
{
    public static TimeZoneInfo TimeZone { get; } = ResolveKst();

    private static TimeZoneInfo ResolveKst()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            }

            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "KST",
                TimeSpan.FromHours(9),
                "Korea Standard Time",
                "KST");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "KST",
                TimeSpan.FromHours(9),
                "Korea Standard Time",
                "KST");
        }
    }

    public static DateTimeOffset ToKst(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, TimeZone);

    public static DateTime ToKstDateTime(DateTimeOffset value) =>
        ToKst(value).DateTime;

    /// <summary>Chart axis / tooltip: KST without offset suffix clutter.</summary>
    public static string FormatAxis(DateTimeOffset utc, bool includeDate)
    {
        var k = ToKstDateTime(utc);
        return includeDate ? k.ToString("MM-dd HH:mm") : k.ToString("HH:mm");
    }

    public static string FormatAxisFromTicks(long ticks, bool includeDate)
    {
        if (ticks <= 0)
        {
            return string.Empty;
        }

        // LiveCharts DateTimePoint uses local DateTime ticks; we store KST wall times as DateTime.Unspecified.
        var dt = new DateTime(ticks, DateTimeKind.Unspecified);
        return includeDate ? dt.ToString("MM-dd HH:mm") : dt.ToString("HH:mm");
    }

    public static string FormatFull(DateTimeOffset utc) =>
        ToKstDateTime(utc).ToString("yyyy-MM-dd HH:mm:ss") + " KST";
}
