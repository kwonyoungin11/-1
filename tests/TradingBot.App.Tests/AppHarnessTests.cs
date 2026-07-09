using TradingBot.App.Services;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.Tests;

public class AppHarnessTests
{
    [Fact]
    public async Task Dashboard_and_chart_keep_live_locked()
    {
        var harness = AppHarness.CreateDefault();
        var dash = await harness.GetDashboardAsync();
        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionLabel));
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionModeLabel));

        var (candles, markers) = harness.GetChartData();
        Assert.True(candles.Count >= 10);
        Assert.NotEmpty(markers);
    }

    [Fact]
    public async Task Start_exposes_balance_return_and_runs_practice()
    {
        var harness = AppHarness.CreateDefault();
        harness.SetStockKind(StockMarketKind.나스닥);
        harness.SetStrategy(TradingStrategyKind.단순연습전략);
        var msg = harness.StartAutoTrade();
        Assert.Contains("실주문", msg, StringComparison.Ordinal);
        var panel = harness.GetAutoTradePanel();
        Assert.Equal(AutoTradeSessionStatus.실행중, panel.SessionStatus);
        Assert.Contains("잔액", panel.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("%", panel.ReturnRateLabel, StringComparison.Ordinal);
        _ = await harness.GetDashboardAsync();
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
        _ = harness.StopAutoTrade();
    }

    [Fact]
    public void Project_is_desktop_app()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TradingBot.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var csproj = File.ReadAllText(Path.Combine(dir!.FullName, "src", "TradingBot.App", "TradingBot.App.csproj"));
        Assert.Contains("WinExe", csproj, StringComparison.Ordinal);
        Assert.Contains("Avalonia", csproj, StringComparison.Ordinal);
        Assert.Contains("LiveChartsCore", csproj, StringComparison.Ordinal);
    }
}
