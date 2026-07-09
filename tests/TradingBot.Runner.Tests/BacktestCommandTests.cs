namespace TradingBot.Runner.Tests;

public class BacktestCommandTests
{
    [Fact]
    public async Task Help_exits_zero()
    {
        var code = await BacktestCommand.RunAsync(new[] { "--help" }, CancellationToken.None);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Cache_only_without_cache_exits_two()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-cache-" + Guid.NewGuid().ToString("N") + ".json");
        var code = await BacktestCommand.RunAsync(
            new[]
            {
                "--cache-only",
                "--cache-path", missing,
                "--symbol", "VMAR",
                "--interval", "1m",
            },
            CancellationToken.None);
        Assert.Equal(2, code);
    }
}
