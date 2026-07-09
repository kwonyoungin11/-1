using TradingBot.Domain;
using TradingBot.Orders;
using TradingBot.Risk;

namespace TradingBot.Orders.Tests;

public class OrderRouterTests
{
    private static OrderCandidate Sample() => new(
        "AAPL", "BUY", "LIMIT", 1m, 1m, "test-client-order-1", DateTimeOffset.UtcNow);

    [Fact]
    public async Task DryRun_accepts_without_live_submission()
    {
        var router = new DryRunOrderRouter();
        var result = await router.RouteAsync(Sample(), CancellationToken.None);
        Assert.True(result.Accepted);
        Assert.Equal("DryRun", result.Mode);
    }

    [Fact]
    public async Task BlockedLiveRouter_blocks_with_default_settings()
    {
        var router = new BlockedLiveOrderRouter(TradingSafetySettings.CreateSafeDefaults());
        var result = await router.RouteAsync(Sample(), CancellationToken.None);
        Assert.False(result.Accepted);
        Assert.NotEmpty(result.Blocks);
    }
}
