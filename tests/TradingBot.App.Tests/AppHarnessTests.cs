using TradingBot.App.Services;
using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;
using TradingBot.Observability;

namespace TradingBot.App.Tests;

public class AppHarnessTests
{
    /// <summary>Process env overrides repo .env so unit tests stay fail-closed.</summary>
    private static AppHarness CreateTestHarness()
    {
        Environment.SetEnvironmentVariable("ALLOW_LIVE_ORDERS", "false");
        Environment.SetEnvironmentVariable("KILL_SWITCH", "true");
        Environment.SetEnvironmentVariable("ORDER_MODE", "dry_run");
        Environment.SetEnvironmentVariable("TOSS_ALLOW_LIVE_HTTP", "false");
        return AppHarness.CreateDefault();
    }

    [Fact]
    public void CreateDefault_is_vmar_default_live_locked()
    {
        var harness = CreateTestHarness();
        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);
        Assert.Equal(new[] { WatchlistCatalog.VmarSymbol }, harness.Session.ResolveWatchSymbols());
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(VmarOneMinuteScalpPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(ChartTimeframe.분봉15, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

    [Fact]
    public void SetStockKind_vmar_applies_scalp_preset_and_keeps_live_locked()
    {
        var harness = CreateTestHarness();
        harness.SetStockKind(StockMarketKind.비전마린);
        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(new[] { WatchlistCatalog.VmarSymbol }, harness.Session.ResolveWatchSymbols());
        Assert.Equal(VmarOneMinuteScalpPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(VmarOneMinuteScalpPreset.Timeframe, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);

        harness.SetStockKind(StockMarketKind.스페이스X);
        Assert.Equal(StockMarketKind.스페이스X, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(SpacexOfficialStrategyPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(SpacexOfficialStrategyPreset.Timeframe, harness.Session.Timeframe);
    }

    [Fact]
    public void SetFocusSymbol_accepts_vmar_rejects_unknown()
    {
        var harness = CreateTestHarness();
        harness.SetFocusSymbol("VMAR");
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        harness.SetFocusSymbol("AAPL");
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        // Catalog still knows SPCX, but cockpit default focus stays VMAR after reject of unknown.
        harness.SetFocusSymbol("spcx");
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());
        harness.SetFocusSymbol("VMAR");
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
    }

    [Fact]
    public void Watchlist_kind_labels_include_spacex_and_vmar()
    {
        Assert.Equal(2, WatchlistCatalog.KindLabels.Count);
        Assert.Contains("스페이스X", WatchlistCatalog.KindLabels);
        Assert.Contains("비전마린", WatchlistCatalog.KindLabels);
    }

    [Fact]
    public async Task GetDashboardAsync_populates_ConnectionLabel_and_keeps_live_locked()
    {
        var harness = CreateTestHarness();
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
        var harness = CreateTestHarness();
        harness.SetStrategy(TradingStrategyKind.추세추종);
        _ = await harness.GetDashboardAsync();
        var (candles, markers, indicators) = harness.GetChartData();
        Assert.True(candles.Count >= 10);
        // Volume bubbles always present (live or mock) — one per candle baseline
        Assert.NotEmpty(markers);
        Assert.True(markers.Count >= candles.Count);
        Assert.NotEmpty(indicators);
        Assert.Contains(indicators, i => i.Name.Contains("SMA", StringComparison.Ordinal));
    }

    [Fact]
    public void GetActiveBracketPlan_is_limit_with_sl_tp_and_live_locked()
    {
        var harness = CreateTestHarness();
        var plan = harness.GetActiveBracketPlan();
        Assert.Equal(WatchlistCatalog.VmarSymbol, plan.Symbol);
        Assert.Equal("LIMIT", plan.OrderType);
        Assert.True(plan.EntryLimit > 0m);
        Assert.True(plan.StopPrice < plan.EntryLimit);
        Assert.True(plan.TakeProfitPrice > plan.EntryLimit);
        Assert.Contains("실주문 잠금", plan.OwnerMessage, StringComparison.Ordinal);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Start_and_stop_do_not_open_live_orders()
    {
        var harness = CreateTestHarness();
        harness.SetStockKind(StockMarketKind.비전마린);
        harness.SetStrategy(TradingStrategyKind.일분분할스캘프);

        var startMsg = harness.StartAutoTrade();
        Assert.Contains("실주문", startMsg, StringComparison.Ordinal);
        Assert.Contains("VMAR", startMsg, StringComparison.Ordinal);

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
        var harness = CreateTestHarness();
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
        var harness = CreateTestHarness();
        var evidence = harness.GetTradingEvidenceSummary();
        Assert.Equal(0, evidence.DryRunCount);
        Assert.Equal(0, evidence.PaperCount);
        Assert.True(evidence.LiveBlocked);
        Assert.False(evidence.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void CreateDefault_keeps_IsLiveSubmissionEnabled_false()
    {
        var harness = CreateTestHarness();
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.False(harness.SettingsWouldAllowLiveRouting);
        Assert.True(harness.IsGatedLiveRouterRegistered);
    }

    [Fact]
    public void GetLiveReadinessReport_never_enables_live()
    {
        var harness = CreateTestHarness();
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
        var harness = CreateTestHarness();
        var practice = harness.BuildPracticeContext();
        Assert.Equal(AppHarness.DefaultPracticeStartingBalance, practice.DayStartEquity);
        Assert.Equal(harness.Session.Balance, practice.CurrentEquity);
        Assert.Equal(AppHarness.DefaultPracticeMaxDailyLossAbsolute, 3_000m);
    }

    [Fact]
    public void SetTimeframe_invalidates_and_uses_session()
    {
        var harness = CreateTestHarness();
        harness.SetTimeframe(ChartTimeframe.일봉);
        Assert.Equal(ChartTimeframe.일봉, harness.Timeframe);
        harness.SetTimeframe(ChartTimeframe.분봉1);
        Assert.Equal(ChartTimeframe.분봉1, harness.Timeframe);
    }

    [Fact]
    public void ApplyRealPortfolio_live_snapshot_binds_known_symbols_only()
    {
        var harness = CreateTestHarness();

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
        Assert.Contains("SPCX", harness.Session.ResolveWatchSymbols());
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void ApplyRealPortfolio_preserves_vmar_focus_when_vmar_in_quotes()
    {
        var harness = CreateTestHarness();
        harness.SetStockKind(StockMarketKind.비전마린);

        var portfolio = new ReadOnlyPortfolioSnapshot
        {
            ConnectionStatus = ConnectionStatus.LiveReadOnlyConnected,
            ConnectionOwnerMessage = "토스 실 HTTP 읽기 연결됨 (실주문 없음)",
            Accounts = Array.Empty<AccountSummary>(),
            Holdings = Array.Empty<HoldingSummary>(),
            Quotes =
            [
                new QuoteSnapshot("VMAR", 3.5m, "USD", DateTimeOffset.UtcNow),
            ],
            UsMarket = null,
            MarketValueUsdSummary = "0",
            CashBuyingPower = 5_000m,
            CashCurrency = "USD",
            AsOfUtc = DateTimeOffset.UtcNow,
            BlockMessages = ["주문 API 미사용"],
        };

        harness.ApplyRealPortfolio(portfolio);
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Contains(WatchlistCatalog.VmarSymbol, harness.Session.ResolveWatchSymbols());
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
        var harness = CreateTestHarness();
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

    [Fact]
    public void Chart_honesty_defaults_are_mock_practice_not_silent_production()
    {
        var harness = CreateTestHarness();
        Assert.False(harness.ChartUsesRealCandles);
        Assert.Equal("연습 데이터 · 실봉 아님", harness.ChartWatermark);
        Assert.Contains("mock", harness.ChartDataSourceLabel, StringComparison.OrdinalIgnoreCase);
        Assert.False(harness.IsChartDataErrorOrStale);

        var (candles, _, _) = harness.GetChartData();
        Assert.True(candles.Count >= 10);
        Assert.True(candles.Count <= AppHarness.ChartDisplayBarCap);
        // Still practice after GetChartData — mock path must keep watermark.
        Assert.False(harness.ChartUsesRealCandles);
        Assert.Equal("연습 데이터 · 실봉 아님", harness.ChartWatermark);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Chart_watermark_error_when_connection_error()
    {
        var portfolio = new StubPortfolioService(
            ConnectionStatus.Error,
            "오류 · 읽기 실패",
            candles: Array.Empty<CandlePoint>());
        var harness = CreateHarness(portfolio);

        _ = await harness.GetDashboardAsync();

        Assert.False(harness.ChartUsesRealCandles);
        Assert.True(harness.IsChartDataErrorOrStale);
        Assert.Equal("데이터 오류 · 주문 차단", harness.ChartWatermark);
        Assert.Contains("오류", harness.ChartDataSourceLabel, StringComparison.Ordinal);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Chart_watermark_error_when_live_connected_but_candles_empty()
    {
        var portfolio = new StubPortfolioService(
            ConnectionStatus.LiveReadOnlyConnected,
            "토스 실 HTTP 읽기 연결됨 (실주문 없음)",
            candles: Array.Empty<CandlePoint>(),
            cashBuyingPower: 5_000m);
        var harness = CreateHarness(portfolio);

        _ = await harness.GetDashboardAsync();
        _ = harness.GetChartData();

        Assert.False(harness.ChartUsesRealCandles);
        Assert.True(harness.IsChartDataErrorOrStale);
        Assert.Equal("데이터 오류 · 주문 차단", harness.ChartWatermark);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Chart_uses_real_candles_and_watermark_when_toss_bars_cached()
    {
        var now = DateTimeOffset.UtcNow;
        // 1m TF: no client aggregation — bar count matches Toss raw series.
        var realBars = Enumerable.Range(0, 40)
            .Select(i => new CandlePoint(
                now.AddMinutes(-40 + i),
                100 + i * 0.1,
                101 + i * 0.1,
                99 + i * 0.1,
                100.5 + i * 0.1,
                10_000 + i))
            .ToArray();
        var portfolio = new StubPortfolioService(
            ConnectionStatus.LiveReadOnlyConnected,
            "토스 실 HTTP 읽기 연결됨 (실주문 없음)",
            candles: realBars,
            cashBuyingPower: 8_000m);
        var harness = CreateHarness(portfolio, ChartTimeframe.분봉1);

        _ = await harness.GetDashboardAsync();
        var (candles, _, _) = harness.GetChartData();

        Assert.True(harness.ChartUsesRealCandles);
        Assert.False(harness.IsChartDataErrorOrStale);
        Assert.Equal("토스 실봉", harness.ChartWatermark);
        Assert.Equal("토스 실봉", harness.ChartDataSourceLabel);
        Assert.Equal(realBars.Length, candles.Count);
        Assert.Equal(realBars[0].Time, candles[0].Time);
        Assert.True(candles.Count <= AppHarness.ChartDisplayBarCap);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Chart_display_bar_cap_is_300_when_enough_real_candles()
    {
        var now = DateTimeOffset.UtcNow;
        var realBars = Enumerable.Range(0, 350)
            .Select(i => new CandlePoint(
                now.AddMinutes(-350 + i),
                90,
                91,
                89,
                90.5,
                1_000))
            .ToArray();
        var portfolio = new StubPortfolioService(
            ConnectionStatus.LiveReadOnlyConnected,
            "토스 실 HTTP 읽기 연결됨 (실주문 없음)",
            candles: realBars,
            cashBuyingPower: 1_000m);
        var harness = CreateHarness(portfolio, ChartTimeframe.분봉1);

        _ = await harness.GetDashboardAsync();
        var (candles, _, _) = harness.GetChartData();

        Assert.True(harness.ChartUsesRealCandles);
        Assert.Equal(AppHarness.ChartDisplayBarCap, candles.Count);
        Assert.Equal(300, AppHarness.ChartDisplayBarCap);
        Assert.Equal(200, AppHarness.TossCandlePageSize);
        Assert.Equal("토스 실봉", harness.ChartWatermark);
    }

    private static AppHarness CreateHarness(
        IReadOnlyPortfolioService portfolio,
        ChartTimeframe timeframe = ChartTimeframe.분봉15)
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = false,
            KillSwitch = true,
            OrderMode = OrderMode.DryRun,
            MaxOrderNotional = 50_000m,
            MaxDailyLoss = AppHarness.DefaultPracticeMaxDailyLossAbsolute,
            MarketDataMaxStalenessSeconds = TradingSafetyDefaults.MarketDataMaxStalenessSeconds,
        };
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        return new AppHarness(
            settings,
            portfolio,
            new OrderCandidatePipeline(),
            new LiveOrderGate(),
            dryLedger,
            new DryRunOrderRouter(dryLedger),
            paperLedger,
            new PaperOrderRouter(paperLedger),
            new InMemoryAuditLog(),
            new AutoTradeSessionService
            {
                StockKind = StockMarketKind.비전마린,
                FocusSymbol = WatchlistCatalog.VmarSymbol,
                Strategy = VmarOneMinuteScalpPreset.Strategy,
                Timeframe = timeframe,
            });
    }

    private static void AssertConnectionModeIsReadOnly(string modeLabel)
    {
        Assert.False(string.IsNullOrWhiteSpace(modeLabel));
        var ok = modeLabel.Contains("mock", StringComparison.OrdinalIgnoreCase)
                 || modeLabel.Contains("실 HTTP", StringComparison.Ordinal);
        Assert.True(ok, $"Expected mock or live-read-only mode label, got: {modeLabel}");
        Assert.DoesNotContain("주문 실행", modeLabel, StringComparison.Ordinal);
    }

    /// <summary>Test double: fixed connection + optional candle series. No network, no orders.</summary>
    private sealed class StubPortfolioService : IReadOnlyPortfolioService
    {
        private readonly ConnectionStatus _status;
        private readonly string _ownerMessage;
        private readonly IReadOnlyList<CandlePoint> _candles;
        private readonly decimal? _cashBuyingPower;

        public StubPortfolioService(
            ConnectionStatus status,
            string ownerMessage,
            IReadOnlyList<CandlePoint> candles,
            decimal? cashBuyingPower = null)
        {
            _status = status;
            _ownerMessage = ownerMessage;
            _candles = candles;
            _cashBuyingPower = cashBuyingPower;
        }

        public Task<ReadOnlyPortfolioSnapshot> GetSnapshotAsync(
            IReadOnlyList<string> watchSymbols,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReadOnlyPortfolioSnapshot
            {
                ConnectionStatus = _status,
                ConnectionOwnerMessage = _ownerMessage,
                Accounts = Array.Empty<AccountSummary>(),
                Holdings = Array.Empty<HoldingSummary>(),
                Quotes =
                [
                    new QuoteSnapshot(WatchlistCatalog.VmarSymbol, 3.5m, "USD", DateTimeOffset.UtcNow),
                ],
                UsMarket = null,
                MarketValueUsdSummary = "0",
                CashBuyingPower = _cashBuyingPower,
                CashCurrency = "USD",
                AsOfUtc = DateTimeOffset.UtcNow,
                BlockMessages = ["주문 API 미사용"],
            });
        }

        public Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
            string symbol,
            string interval,
            int count,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<CandlePoint> slice = _candles.Count <= count
                ? _candles
                : _candles.Skip(_candles.Count - count).ToArray();
            return Task.FromResult(slice);
        }

        public Task<IReadOnlyList<CandlePoint>> GetCandlesPagedAsync(
            string symbol,
            string interval,
            int countPerPage,
            int maxPages,
            int targetTotal,
            CancellationToken cancellationToken)
        {
            var take = Math.Min(targetTotal, _candles.Count);
            IReadOnlyList<CandlePoint> slice = take >= _candles.Count
                ? _candles
                : _candles.Skip(_candles.Count - take).ToArray();
            return Task.FromResult(slice);
        }
    }
}
