using TradingBot.App.Services;
using TradingBot.App.ViewModels;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.Tests;

public class AppHarnessTests
{
    /// <summary>
    /// Combobox data source is catalog-driven: Domain 나스닥코어3 병합 시 KindLabels에 자동 포함.
    /// </summary>
    [Fact]
    public void StockKindOptions_follow_WatchlistCatalog_KindLabels()
    {
        Assert.NotEmpty(WatchlistCatalog.KindLabels);
        Assert.Equal(WatchlistCatalog.AllKinds.Count, WatchlistCatalog.KindLabels.Count);
        Assert.Contains(StockMarketKind.나스닥.ToString(), WatchlistCatalog.KindLabels);

        // When Domain ships 나스닥코어3, labels must surface it (no Avalonia hardcode).
        if (MainWindowViewModel.TryGetCore3Kind(out var core3))
        {
            Assert.Contains(MainWindowViewModel.Core3KindName, WatchlistCatalog.KindLabels);
            Assert.Contains(core3, WatchlistCatalog.AllKinds);
            Assert.Equal(MainWindowViewModel.Core3KindName, core3.ToString());
        }
        else
        {
            // Pre-merge: UI still uses KindLabels; core name reserved for merge.
            Assert.Equal("나스닥코어3", MainWindowViewModel.Core3KindName);
            Assert.DoesNotContain(MainWindowViewModel.Core3KindName, WatchlistCatalog.KindLabels);
        }
    }

    [Fact]
    public void ResolveDefaultStockKind_prefers_core3_when_catalog_lists_it()
    {
        var def = MainWindowViewModel.ResolveDefaultStockKind();
        if (MainWindowViewModel.TryGetCore3Kind(out var core3)
            && WatchlistCatalog.KindLabels.Contains(core3.ToString(), StringComparer.Ordinal))
        {
            Assert.Equal(core3, def);
        }
        else
        {
            Assert.Equal(StockMarketKind.나스닥, def);
        }
    }

    /// <summary>
    /// Selecting core preset resolves exactly 3 symbols when Domain enum exists; otherwise merge-ready skip path.
    /// </summary>
    [Fact]
    public void Selecting_core3_preset_resolves_three_symbols_when_enum_present()
    {
        if (!MainWindowViewModel.TryGetCore3Kind(out var core3))
        {
            // Prepare for merge: harness + catalog path still practice-safe on 나스닥.
            var pre = AppHarness.CreateDefault();
            pre.SetStockKind(StockMarketKind.나스닥);
            var symbolsPre = pre.Session.ResolveWatchSymbols();
            Assert.NotEmpty(symbolsPre);
            Assert.True(pre.GetEvidenceCounts().LiveBlocked);
            return;
        }

        var harness = AppHarness.CreateDefault();
        harness.SetStockKind(core3);

        var symbols = WatchlistCatalog.ResolveSymbols(core3);
        Assert.Equal(3, symbols.Count);
        Assert.Contains("QQQ", symbols);
        Assert.Contains("NVDA", symbols);
        Assert.Contains("AAPL", symbols);

        var fromSession = harness.Session.ResolveWatchSymbols();
        Assert.Equal(3, fromSession.Length);
        Assert.Equal(symbols.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            fromSession.OrderBy(s => s, StringComparer.Ordinal).ToArray());

        var panel = harness.GetAutoTradePanel();
        Assert.Equal(core3, panel.StockKind);
        Assert.Equal(MainWindowViewModel.Core3KindName, panel.StockKindLabel);
        var watchParts = panel.WatchSymbolsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(3, watchParts.Length);
        Assert.Contains("연습", panel.StockKindDescription + panel.SafetyNote, StringComparison.Ordinal);

        // Core preset selection never unlocks live.
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);

        var startMsg = harness.StartAutoTrade();
        Assert.Contains("실주문", startMsg, StringComparison.Ordinal);
        Assert.Contains("연습", startMsg, StringComparison.Ordinal);
        var panelRunning = harness.GetAutoTradePanel();
        Assert.Contains("연습", panelRunning.SafetyNote, StringComparison.Ordinal);
        Assert.Equal(AutoTradeSessionStatus.실행중, panelRunning.SessionStatus);
        _ = harness.StopAutoTrade();
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void Practice_safety_copy_mentions_practice_not_advice()
    {
        var harness = AppHarness.CreateDefault();
        var panel = harness.GetAutoTradePanel();
        Assert.Contains("연습", panel.SafetyNote, StringComparison.Ordinal);
        Assert.Contains("실주문", panel.SafetyNote, StringComparison.Ordinal);

        // ViewModel enriches SafetyHeadline / SafetyNote with non-advice wording on ApplyPanel.
        Assert.Equal(
            MainWindowViewModel.ResolveDefaultStockKind().ToString(),
            MainWindowViewModel.TryGetCore3Kind(out _)
                && WatchlistCatalog.KindLabels.Contains(MainWindowViewModel.Core3KindName, StringComparer.Ordinal)
                ? MainWindowViewModel.Core3KindName
                : StockMarketKind.나스닥.ToString());
    }

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

    [Fact]
    public void CreateDefault_keeps_IsLiveSubmissionEnabled_false_and_settings_fail_closed()
    {
        var harness = AppHarness.CreateDefault();

        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.False(harness.SettingsWouldAllowLiveRouting);
        // Gated live router is registered for readiness, never used under CreateDefault practice.
        Assert.True(harness.IsGatedLiveRouterRegistered);
    }

    [Fact]
    public void GetLiveReadinessReport_never_enables_live()
    {
        var harness = AppHarness.CreateDefault();
        var report = harness.GetLiveReadinessReport();

        Assert.True(report.LiveBlocked);
        Assert.False(report.IsLiveSubmissionEnabled);
        Assert.False(report.EnablesLive);
        Assert.False(report.GatedLiveRouterUsedInPractice);
        Assert.True(report.GatedLiveRouterRegistered);
        Assert.False(report.SettingsAllowLiveOrders);
        Assert.True(report.SettingsKillSwitch);
        Assert.Equal(nameof(OrderMode.DryRun), report.SettingsOrderMode);
        Assert.False(string.IsNullOrWhiteSpace(report.OwnerUnlockStatus));
        Assert.DoesNotContain("unlocked", report.OwnerUnlockStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LIVE_READY=true", report.Summary, StringComparison.Ordinal);
        Assert.Contains("LIVE_READY=false", report.Summary, StringComparison.Ordinal);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task CreateDefault_practice_works_while_live_readiness_stays_blocked()
    {
        var harness = AppHarness.CreateDefault();
        Assert.False(harness.IsLiveSubmissionEnabled);

        harness.SetStockKind(StockMarketKind.나스닥);
        harness.SetStrategy(TradingStrategyKind.단순연습전략);
        var startMsg = harness.StartAutoTrade();
        Assert.Contains("실주문", startMsg, StringComparison.Ordinal);

        var dash = await harness.GetDashboardAsync();
        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.Equal(OrderMode.DryRun, dash.Snapshot.OrderMode);

        var report = harness.GetLiveReadinessReport();
        Assert.True(report.LiveBlocked);
        Assert.False(report.IsLiveSubmissionEnabled);
        Assert.False(report.EnablesLive);
        Assert.False(report.GatedLiveRouterUsedInPractice);
        Assert.False(harness.SettingsWouldAllowLiveRouting);

        // Practice session still running after readiness read.
        Assert.Equal(AutoTradeSessionStatus.실행중, harness.GetAutoTradePanel().SessionStatus);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);

        _ = harness.StopAutoTrade();
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetLiveReadinessReport().LiveBlocked);
    }

    [Fact]
    public void GetLiveReadinessReport_scans_repo_docs_when_available()
    {
        var harness = AppHarness.CreateDefault();
        var report = harness.GetLiveReadinessReport();

        // In this worktree the checklist/evidence docs and automation script exist.
        // Report may still miss them if repo root is not discoverable; never enable live.
        Assert.True(report.LiveBlocked);
        Assert.False(report.EnablesLive);
        Assert.False(report.IsLiveSubmissionEnabled);

        var root = AppHarness.TryFindRepoRoot();
        if (root is not null)
        {
            Assert.True(report.ChecklistPresent);
            Assert.True(report.EvidenceDocPresent);
            Assert.True(report.AutomationScriptPresent);
            // Shipped evaluator token; with artifacts present → ready_for_owner_unlock.
            Assert.Equal("ready_for_owner_unlock", report.OwnerUnlockStatus);
            Assert.True(report.LiveBlocked);
            Assert.False(report.EnablesLive);
            Assert.True(harness.IsGatedLiveRouterRegistered);
            Assert.False(harness.IsLiveSubmissionEnabled);
        }
    }

    [Fact]
    public void CreateDefault_registers_real_GatedLiveOrderRouter_not_blocked_stub()
    {
        var harness = AppHarness.CreateDefault();
        Assert.True(harness.IsGatedLiveRouterRegistered);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.False(harness.SettingsWouldAllowLiveRouting);

        // Drive shipped LiveReadinessEvaluator via harness on real repo root.
        var report = harness.GetLiveReadinessReport();
        Assert.Equal("ready_for_owner_unlock", report.OwnerUnlockStatus);
        Assert.Contains("ready_for_owner_unlock", report.Summary, StringComparison.Ordinal);
        Assert.Contains("GatedLiveOrderRouter", report.Summary, StringComparison.Ordinal);
        Assert.True(report.LiveBlocked);
        Assert.False(report.IsLiveSubmissionEnabled);
    }
}
