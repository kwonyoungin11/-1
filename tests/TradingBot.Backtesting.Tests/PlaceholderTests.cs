namespace TradingBot.Backtesting.Tests;

public class PlaceholderTests
{
    [Fact]
    public void Module_is_engine_or_host()
    {
        // Host branch may carry engine Status after merge of pw13-bt-engine.
        Assert.False(string.IsNullOrWhiteSpace(TradingBot.Backtesting.BacktestingModule.Status));
        Assert.NotEqual("scaffold", TradingBot.Backtesting.BacktestingModule.Status);
    }
}
