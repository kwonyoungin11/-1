using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class TradeMarkerTests
{
    [Fact]
    public void NotionalSize_is_quantity_times_price()
    {
        var size = TradeMarker.NotionalSize(10m, 190m);
        Assert.Equal(1900d, size, precision: 5);
    }

    [Theory]
    [InlineData(1, 100, 100)]
    [InlineData(2.5, 40, 100)]
    [InlineData(0.1, 250, 25)]
    [InlineData(100, 1.5, 150)]
    public void NotionalSize_math_matches_qty_times_price(
        double quantity,
        double price,
        double expected)
    {
        var size = TradeMarker.NotionalSize((decimal)quantity, (decimal)price);
        Assert.Equal(expected, size, precision: 5);
    }

    [Fact]
    public void NotionalSize_floors_at_minimum_bubble_weight()
    {
        // 0 * anything → floor 0.01 so chart bubble stays visible
        Assert.Equal(0.01d, TradeMarker.NotionalSize(0m, 100m), precision: 8);
        Assert.Equal(0.01d, TradeMarker.NotionalSize(0m, 0m), precision: 8);
    }

    [Fact]
    public void VolumeNotionalSize_is_volume_times_close()
    {
        var vol = TradeMarker.VolumeNotionalSize(1_000_000, 200);
        Assert.Equal(200_000_000d, vol, precision: 1);
    }

    [Fact]
    public void VolumeNotionalSize_floors_at_minimum()
    {
        Assert.Equal(0.01d, TradeMarker.VolumeNotionalSize(0, 100), precision: 8);
        // closePrice floor at 0.01 before multiply
        var tiny = TradeMarker.VolumeNotionalSize(1, 0);
        Assert.Equal(0.01d, tiny, precision: 8);
    }

    [Fact]
    public void TradeMarker_record_holds_side_and_size()
    {
        var t = new DateTimeOffset(2026, 7, 9, 16, 0, 0, TimeSpan.Zero);
        var marker = new TradeMarker(t, 190d, TradeMarkerSide.매수, "매수", 1900d);
        Assert.Equal(TradeMarkerSide.매수, marker.Side);
        Assert.Equal(1900d, marker.SizeWeight);
        Assert.Equal("매수", marker.Label);
        Assert.Equal(t, marker.Time);
    }
}
