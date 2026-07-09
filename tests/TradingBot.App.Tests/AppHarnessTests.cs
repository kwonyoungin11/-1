using TradingBot.App.Services;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.Tests;

public class AppHarnessTests
{
    [Fact]
    public void CreateDefault_is_spacex_only()
    {
        var harness = AppHarness.CreateDefault();
        Assert.Equal(StockMarketKind.스페이스X, harness.Session.StockKind);
        Assert.Equal(new[] { WatchlistCatalog.SpaceXSymbol }, harness.Session.ResolveWatchSymbols());
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

    [Fact]
    public void Watchlist_kind_labels_are_spacex_only()
    {
        Assert.Single(WatchlistCatalog.KindLabels);
        Assert.Equal("스페이스X", WatchlistCatalog.KindLabels[0]);
    }

    [Fact]
    public async Task GetDashboardAsync_populates_ConnectionLabel_and_keeps_live_locked()
    {
        var harness = AppHarness.CreateDefault();
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionLabel));
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionModeLabel));

        var dash = await harness.GetDashboardAsync();
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionLabel));
        Assert.False(string.IsNullOrWhiteSpace(harness.ConnectionModeLabel));
        Assert.Contains("실주문", harness.ConnectionLabel, StringComparison.Ordinal);
        AssertConnectionModeIsReadOnly(harness.ConnectionModeLabel);
        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

    [Fact]
    public async Task Chart_returns_candles_markers_and_indicators()
    {
        var harness = AppHarness.CreateDefault();
        harness.SetStrategy(TradingStrategyKind.추세추종);
        _ = await harness.GetDashboardAsync();
        var (candles, markers, indicators) = harness.GetChartData();
        Assert.True(candles.Count >= 10);
        Assert.NotEmpty(indicators);
        Assert.Contains(indicators, i => i.Name.Contains("SMA", StringComparison.Ordinal));
        _ = markers; // may be empty when live-connected without fills
    }

    [Fact]
    public async Task Start_and_stop_do_not_open_live_orders()
    {
        var harness = AppHarness.CreateDefault();
        harness.SetStockKind(StockMarketKind.스페이스X);
        harness.SetStrategy(TradingStrategyKind.추세추종);

        var startMsg = harness.StartAutoTrade();
        Assert.Contains("실주문", startMsg, StringComparison.Ordinal);
        Assert.Contains("SPCX", startMsg, StringComparison.Ordinal);

        var panelRunning = harness.GetAutoTradePanel();
        Assert.Equal(AutoTradeSessionStatus.실행중, panelRunning.SessionStatus);
        Assert.Contains("실주문", panelRunning.SafetyNote, StringComparison.Ordinal);

        var dashWhileRunning = await harness.GetDashboardAsync();
        Assert.Equal(LiveLockState.Locked, dashWhileRunning.Snapshot.LiveLock);
        Assert.False(dashWhileRunning.IsLiveTradingVisuallyOpen);
        Assert.Equal(OrderMode.DryRun, dashWhileRunning.Snapshot.OrderMode);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);

        var stopMsg = harness.StopAutoTrade();
        Assert.Contains("종료", stopMsg, StringComparison.Ordinal);
        Assert.Equal(AutoTradeSessionStatus.중지, harness.GetAutoTradePanel().SessionStatus);
    }

    [Fact]
    public async Task Start_exposes_balance_return_and_evidence()
    {
        var harness = AppHarness.CreateDefault();
        harness.SetStrategy(TradingStrategyKind.단순연습전략);
        Assert.False(harness.IsLiveSubmissionEnabled);
        var startMsg = harness.StartAutoTrade();
        Assert.Contains("실주문", startMsg, StringComparison.Ordinal);

        var dash = await harness.GetDashboardAsync();
        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);

        var evidence = harness.GetTradingEvidenceSummary();
        Assert.True(evidence.LiveBlocked);
        Assert.False(evidence.IsLiveSubmissionEnabled);
        Assert.NotNull(evidence.ExportText);
        Assert.Contains("live_orders=false", evidence.ExportText, StringComparison.Ordinal);

        var panel = harness.GetAutoTradePanel();
        Assert.Contains("잔액", panel.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("%", panel.ReturnRateLabel, StringComparison.Ordinal);

        _ = harness.StopAutoTrade();
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void GetTradingEvidenceSummary_before_session_is_empty_and_live_blocked()
    {
        var harness = AppHarness.CreateDefault();
        var evidence = harness.GetTradingEvidenceSummary();
        Assert.Equal(0, evidence.DryRunCount);
        Assert.Equal(0, evidence.PaperCount);
        Assert.True(evidence.LiveBlocked);
        Assert.False(evidence.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void CreateDefault_keeps_IsLiveSubmissionEnabled_false()
    {
        var harness = AppHarness.CreateDefault();
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.False(harness.SettingsWouldAllowLiveRouting);
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
        Assert.Contains("LIVE_READY=false", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDefault_wires_practice_equity_context()
    {
        var harness = AppHarness.CreateDefault();
        var practice = harness.BuildPracticeContext();
        Assert.Equal(AppHarness.DefaultPracticeStartingBalance, practice.DayStartEquity);
        Assert.Equal(harness.Session.Balance, practice.CurrentEquity);
        Assert.Equal(AppHarness.DefaultPracticeMaxDailyLossAbsolute, 3_000m);
    }

    [Fact]
    public void SetTimeframe_invalidates_and_uses_session()
    {
        var harness = AppHarness.CreateDefault();
        harness.SetTimeframe(ChartTimeframe.일봉);
        Assert.Equal(ChartTimeframe.일봉, harness.Timeframe);
        harness.SetTimeframe(ChartTimeframe.분봉1);
        Assert.Equal(ChartTimeframe.분봉1, harness.Timeframe);
    }

    [Fact]
    public void ApplyRealPortfolio_live_snapshot_binds_spcx_only()
    {
        var harness = AppHarness.CreateDefault();

        var portfolio = new ReadOnlyPortfolioSnapshot
        {
            ConnectionStatus = ConnectionStatus.LiveReadOnlyConnected,
            ConnectionOwnerMessage = "토스 실 HTTP 읽기 연결됨 (실주문 없음)",
            Accounts = Array.Empty<AccountSummary>(),
            Holdings =
            [
                new HoldingSummary("TSLA", "Tesla", "USD", 2m, 250m),
                new HoldingSummary("SPCX", "SpaceX", "USD", 1m, 90m),
            ],
            Quotes =
            [
                new QuoteSnapshot("SPCX", 90m, "USD", DateTimeOffset.UtcNow),
            ],
            UsMarket = null,
            MarketValueUsdSummary = "90.00",
            CashBuyingPower = 12_000m,
            CashCurrency = "USD",
            AsOfUtc = DateTimeOffset.UtcNow,
            BlockMessages = ["주문 API 미사용"],
        };

        harness.ApplyRealPortfolio(portfolio);

        Assert.Equal(12_000m, harness.Session.Balance);
        Assert.Equal("토스 실계좌", harness.Session.DataSourceLabel);
        Assert.Equal(new[] { "SPCX" }, harness.Session.ResolveWatchSymbols());
        Assert.Equal("SPCX", harness.Session.ResolveFocusSymbol());
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void TryResolveRealBalance_prefers_cash_then_market_value()
    {
        var withCash = new ReadOnlyPortfolioSnapshot
        {
            ConnectionStatus = ConnectionStatus.LiveReadOnlyConnected,
            ConnectionOwnerMessage = "test",
            Accounts = Array.Empty<AccountSummary>(),
            Holdings = Array.Empty<HoldingSummary>(),
            Quotes = Array.Empty<QuoteSnapshot>(),
            UsMarket = null,
            MarketValueUsdSummary = "1500.25",
            CashBuyingPower = 99m,
            AsOfUtc = DateTimeOffset.UtcNow,
            BlockMessages = Array.Empty<string>(),
        };
        Assert.Equal(99m, AppHarness.TryResolveRealBalance(withCash));

        var mvOnly = new ReadOnlyPortfolioSnapshot
        {
            ConnectionStatus = ConnectionStatus.LiveReadOnlyConnected,
            ConnectionOwnerMessage = "test",
            Accounts = Array.Empty<AccountSummary>(),
            Holdings = Array.Empty<HoldingSummary>(),
            Quotes = Array.Empty<QuoteSnapshot>(),
            UsMarket = null,
            MarketValueUsdSummary = "1500.25",
            CashBuyingPower = null,
            AsOfUtc = DateTimeOffset.UtcNow,
            BlockMessages = Array.Empty<string>(),
        };
        Assert.Equal(1500.25m, AppHarness.TryResolveRealBalance(mvOnly));
    }

    [Fact]
    public void ApplyRealPortfolio_ignores_mock_connection()
    {
        var harness = AppHarness.CreateDefault();
        var before = harness.Session.Balance;

        var portfolio = new ReadOnlyPortfolioSnapshot
        {
            ConnectionStatus = ConnectionStatus.MockConnected,
            ConnectionOwnerMessage = "mock",
            Accounts = Array.Empty<AccountSummary>(),
            Holdings = [new HoldingSummary("SPCX", "SpaceX", "USD", 1m, 90m)],
            Quotes = Array.Empty<QuoteSnapshot>(),
            UsMarket = null,
            MarketValueUsdSummary = "9999",
            AsOfUtc = DateTimeOffset.UtcNow,
            BlockMessages = Array.Empty<string>(),
        };

        harness.ApplyRealPortfolio(portfolio);
        Assert.Equal(before, harness.Session.Balance);
        Assert.Equal("연습", harness.Session.DataSourceLabel);
    }

    private static void AssertConnectionModeIsReadOnly(string modeLabel)
    {
        Assert.False(string.IsNullOrWhiteSpace(modeLabel));
        var ok = modeLabel.Contains("mock", StringComparison.OrdinalIgnoreCase)
                 || modeLabel.Contains("실 HTTP", StringComparison.Ordinal);
        Assert.True(ok, $"Expected mock or live-read-only mode label, got: {modeLabel}");
        Assert.DoesNotContain("주문 실행", modeLabel, StringComparison.Ordinal);
    }
}
