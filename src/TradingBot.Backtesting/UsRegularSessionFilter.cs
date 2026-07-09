using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// US equity regular trading hours (09:30–16:00 America/New_York) bar filter.
/// Used as <c>includeBar</c> for long-only backtests. Simulation only.
/// </summary>
public static class UsRegularSessionFilter
{
    private static readonly TimeZoneInfo Eastern = ResolveEastern();

    public static bool IsRegularSession(CandlePoint bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        return IsRegularSession(bar.Time);
    }

    public static bool IsRegularSession(DateTimeOffset time)
    {
        var et = TimeZoneInfo.ConvertTime(time, Eastern);
        if (et.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var tod = et.TimeOfDay;
        // [09:30, 16:00) Eastern
        return tod >= new TimeSpan(9, 30, 0) && tod < new TimeSpan(16, 0, 0);
    }

    private static TimeZoneInfo ResolveEastern()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }

            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "US-Eastern-Approx",
                TimeSpan.FromHours(-5),
                "US Eastern",
                "EST");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "US-Eastern-Approx",
                TimeSpan.FromHours(-5),
                "US Eastern",
                "EST");
        }
    }
}
