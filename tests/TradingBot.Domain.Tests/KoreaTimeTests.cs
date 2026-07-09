using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class KoreaTimeTests
{
    [Fact]
    public void Utc_converts_to_kst_plus_nine()
    {
        var utc = new DateTimeOffset(2026, 7, 9, 6, 30, 0, TimeSpan.Zero);
        var kst = KoreaTime.ToKst(utc);
        Assert.Equal(15, kst.Hour); // 06:30 UTC → 15:30 KST
        Assert.Equal(30, kst.Minute);
        Assert.Contains("KST", KoreaTime.FormatFull(utc), StringComparison.Ordinal);
    }

    [Fact]
    public void FormatAxis_includes_date_when_requested()
    {
        var utc = new DateTimeOffset(2026, 7, 9, 1, 5, 0, TimeSpan.Zero);
        var s = KoreaTime.FormatAxis(utc, includeDate: true);
        Assert.Contains("10:05", s, StringComparison.Ordinal); // 01:05 UTC → 10:05 KST
    }
}
