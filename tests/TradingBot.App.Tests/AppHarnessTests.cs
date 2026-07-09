using TradingBot.App.Services;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.Tests;

public class AppHarnessTests
{
    [Fact]
    public async Task GetDashboardAsync_populates_ConnectionLabel_and_ConnectionModeLabel()
    {
        var harness = AppHarness.CreateDefault();

        // Pre-dashboard defaults are non-empty placeholders (not blank).
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionLabel));
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionModeLabel));

        var dash = await harness.GetDashboardAsync();

        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionLabel));
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionModeLabel));

        // Mock / read-only path: owner-facing messages never imply live order capability.
        Assert.Contains("실주문", harness.ConnectionLabel, StringComparison.Ordinal);
        Assert.Contains("mock", harness.ConnectionModeLabel, StringComparison.OrdinalIgnoreCase);

        // Dashboard connection summary is aligned with harness label after refresh.
        Assert.Equal(harness.ConnectionLabel, dash.Snapshot.ConnectionSummary);
    }

    [Fact]
    public async Task GetDashboardAsync_keeps_live_locked()
    {
        var harness = AppHarness.CreateDefault();
        var dash = await harness.GetDashboardAsync();

        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);
        Assert.Equal(LiveLockState.Locked, dash.LiveLock);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.False(dash.Snapshot.IsLiveTradingVisuallyOpen);
        Assert.False(dash.Snapshot.AllowLiveOrders);
        Assert.True(dash.Snapshot.KillSwitchActive);
        Assert.Equal(OrderMode.DryRun, dash.Snapshot.OrderMode);
        Assert.Contains("실거래 잠김", dash.SafetyHeadline, StringComparison.Ordinal);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

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
    public async Task Start_and_stop_practice_do_not_open_live_orders()
    {
        var harness = AppHarness.CreateDefault();
        harness.SetStockKind(StockMarketKind.나스닥);
        harness.SetStrategy(TradingStrategyKind.단순연습전략);

        var startMsg = harness.StartAutoTrade();
        Assert.Contains("실주문", startMsg, StringComparison.Ordinal);
        Assert.Contains("나가지", startMsg, StringComparison.Ordinal);

        var panelRunning = harness.GetAutoTradePanel();
        Assert.Equal(AutoTradeSessionStatus.실행중, panelRunning.SessionStatus);
        Assert.Contains("연습", panelRunning.SafetyNote, StringComparison.Ordinal);
        Assert.Contains("실주문", panelRunning.SafetyNote, StringComparison.Ordinal);

        var dashWhileRunning = await harness.GetDashboardAsync();
        Assert.Equal(LiveLockState.Locked, dashWhileRunning.Snapshot.LiveLock);
        Assert.False(dashWhileRunning.IsLiveTradingVisuallyOpen);
        Assert.False(dashWhileRunning.Snapshot.AllowLiveOrders);
        Assert.Equal(OrderMode.DryRun, dashWhileRunning.Snapshot.OrderMode);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);

        // Connection labels remain populated after a practice-cycle dashboard refresh.
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionLabel));
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionModeLabel));
        Assert.Contains("mock", harness.ConnectionModeLabel, StringComparison.OrdinalIgnoreCase);

        var stopMsg = harness.StopAutoTrade();
        Assert.Contains("종료", stopMsg, StringComparison.Ordinal);

        var panelStopped = harness.GetAutoTradePanel();
        Assert.Equal(AutoTradeSessionStatus.중지, panelStopped.SessionStatus);

        var dashAfterStop = await harness.GetDashboardAsync();
        Assert.Equal(LiveLockState.Locked, dashAfterStop.Snapshot.LiveLock);
        Assert.False(dashAfterStop.IsLiveTradingVisuallyOpen);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
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
    public async Task Start_and_GetDashboard_exposes_trading_evidence_with_live_blocked()
    {
        var harness = AppHarness.CreateDefault();
        harness.SetStockKind(StockMarketKind.나스닥);
        harness.SetStrategy(TradingStrategyKind.단순연습전략);

        // Practice start must never unlock live submission.
        Assert.False(harness.IsLiveSubmissionEnabled);
        var startMsg = harness.StartAutoTrade();
        Assert.Contains("실주문", startMsg, StringComparison.Ordinal);
        Assert.False(harness.IsLiveSubmissionEnabled);

        var dash = await harness.GetDashboardAsync();
        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.False(dash.Snapshot.AllowLiveOrders);
        Assert.Equal(OrderMode.DryRun, dash.Snapshot.OrderMode);

        var evidence = harness.GetTradingEvidenceSummary();
        Assert.True(evidence.LiveBlocked);
        Assert.False(evidence.IsLiveSubmissionEnabled);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(evidence.DryRunCount >= 0);
        Assert.True(evidence.PaperCount >= 0);
        Assert.Equal(harness.GetEvidenceCounts().DryRun, evidence.DryRunCount);
        Assert.Equal(harness.GetEvidenceCounts().Paper, evidence.PaperCount);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);

        // Evidence types are merged: snapshot + export text available.
        Assert.NotNull(evidence.Snapshot);
        Assert.False(evidence.Snapshot!.Summary.LiveModePresent);
        Assert.False(string.IsNullOrWhiteSpace(evidence.ExportText));
        Assert.Contains("live_orders=false", evidence.ExportText, StringComparison.Ordinal);
        Assert.Contains("LiveSubmissionEnabled=false", evidence.ExportText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            evidence.Snapshot.Summary.ModesPresent,
            m => m.Equals("Live", StringComparison.OrdinalIgnoreCase));

        // Practice session still works after evidence read.
        var panel = harness.GetAutoTradePanel();
        Assert.Equal(AutoTradeSessionStatus.실행중, panel.SessionStatus);
        Assert.Contains("연습", panel.SafetyNote, StringComparison.Ordinal);

        _ = harness.StopAutoTrade();
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetTradingEvidenceSummary().LiveBlocked);
    }

    [Fact]
    public void GetTradingEvidenceSummary_before_practice_is_empty_and_live_blocked()
    {
        var harness = AppHarness.CreateDefault();
        var evidence = harness.GetTradingEvidenceSummary();

        Assert.Equal(0, evidence.DryRunCount);
        Assert.Equal(0, evidence.PaperCount);
        Assert.True(evidence.LiveBlocked);
        Assert.False(evidence.IsLiveSubmissionEnabled);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.NotNull(evidence.ExportText);
        Assert.Contains("live_orders=false", evidence.ExportText, StringComparison.Ordinal);
        Assert.Contains("LiveSubmissionEnabled=false", evidence.ExportText, StringComparison.Ordinal);
        Assert.NotNull(evidence.Snapshot);
        Assert.False(evidence.Snapshot!.Summary.LiveModePresent);
    }

    [Fact]
    public void Stop_without_start_stays_stopped_and_does_not_unlock_live()
    {
        var harness = AppHarness.CreateDefault();

        var stopMsg = harness.StopAutoTrade();
        Assert.Contains("중지", stopMsg, StringComparison.Ordinal);

        var panel = harness.GetAutoTradePanel();
        Assert.Equal(AutoTradeSessionStatus.중지, panel.SessionStatus);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
        Assert.True(harness.GetTradingEvidenceSummary().LiveBlocked);
        Assert.False(harness.IsLiveSubmissionEnabled);
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
