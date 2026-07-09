using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

namespace TradingBot.App.Services;

/// <summary>
/// Owner-facing practice evidence from dry-run + paper ledgers.
/// Live is always blocked in the desktop app host — no secrets, no account numbers.
/// </summary>
public sealed record AppTradingEvidenceSummary(
    int DryRunCount,
    int PaperCount,
    bool LiveBlocked,
    string? ExportText = null,
    TradingEvidenceSnapshot? Snapshot = null)
{
    /// <summary>
    /// Always false in AppHarness — dry-run and paper routers never enable live submission.
    /// </summary>
    public bool IsLiveSubmissionEnabled => false;
}

/// <summary>
/// Mac 앱 조합 루트. 차트·자동매매 연습 세션. 실주문 HTTP 없음.
/// 버블 크기 = 체결 규모(수량×가격).
/// </summary>
public sealed class AppHarness
{
    private readonly TradingSafetySettings _settings;
    private readonly IReadOnlyPortfolioService _portfolio;
    private readonly OrderCandidatePipeline _pipeline;
    private readonly LiveOrderGate _liveOrderGate;
    private readonly IDryRunLedger _dryRunLedger;
    private readonly DryRunOrderRouter _dryRun;
    private readonly IPaperLedger _paperLedger;
    private readonly PaperOrderRouter _paper;
    private readonly IOrderRouter? _gatedLiveRouter;
    private readonly IAuditLog _audit;
    private readonly AutoTradeSessionService _session;
    private readonly TossOptions _tossOptions;
    private readonly EvidenceBuilder _evidenceBuilder;
    private string _connectionLabel = "연결 확인 전";
    private string _connectionModeLabel = "mock";
    private bool _isLiveReadOnlyConnected;
    private IReadOnlyList<QuoteSnapshot> _lastQuotes = Array.Empty<QuoteSnapshot>();
    private string? _cachedCandlesSymbol;
    private IReadOnlyList<CandlePoint>? _cachedRealCandles;
    private bool _newsDay;
    private bool _symbolWarningActive;

    public AppHarness(
        TradingSafetySettings settings,
        IReadOnlyPortfolioService portfolio,
        OrderCandidatePipeline pipeline,
        LiveOrderGate liveOrderGate,
        IDryRunLedger dryRunLedger,
        DryRunOrderRouter dryRun,
        IPaperLedger paperLedger,
        PaperOrderRouter paper,
        IAuditLog audit,
        AutoTradeSessionService session,
        TossOptions? tossOptions = null,
        IOrderRouter? gatedLiveRouter = null)
    {
        _settings = settings;
        _portfolio = portfolio;
        _pipeline = pipeline;
        _liveOrderGate = liveOrderGate;
        _dryRunLedger = dryRunLedger;
        _dryRun = dryRun;
        _paperLedger = paperLedger;
        _paper = paper;
        _audit = audit;
        _session = session;
        _tossOptions = tossOptions ?? new TossOptions { AllowLiveHttp = false };
        _connectionModeLabel = TossReadOnlyFactory.DescribeMode(_tossOptions);
        _evidenceBuilder = new EvidenceBuilder(_dryRunLedger, _paperLedger);
        // Optional gated live router (BlockedLiveOrderRouter today; GatedLiveOrderRouter when merged).
        // Registered for readiness / future wiring only — practice loop never routes here under defaults.
        _gatedLiveRouter = gatedLiveRouter;
    }

    public AutoTradeSessionService Session => _session;

    public string ConnectionLabel => _connectionLabel;

    public string ConnectionModeLabel => _connectionModeLabel;

    /// <summary>
    /// True when a gated live router instance is held (still not used by CreateDefault practice loop).
    /// </summary>
    /// <summary>True when host holds a real <see cref="GatedLiveOrderRouter"/> (still unused in practice loop).</summary>
    public bool IsGatedLiveRouterRegistered => _gatedLiveRouter is GatedLiveOrderRouter;

    /// <summary>
    /// Practice routers never enable live Toss order HTTP. Gated live router (if registered)
    /// also reports false until a future approved live implementation.
    /// </summary>
    public bool IsLiveSubmissionEnabled =>
        _dryRun.IsLiveSubmissionEnabled
        || _paper.IsLiveSubmissionEnabled
        || (_gatedLiveRouter?.IsLiveSubmissionEnabled ?? false);

    /// <summary>
    /// Settings that would theoretically select a live path. CreateDefault is always fail-closed
    /// (all three false / non-live), so practice never uses the gated live router.
    /// </summary>
    public bool SettingsWouldAllowLiveRouting =>
        _settings.AllowLiveOrders
        && !_settings.KillSwitch
        && _settings.OrderMode == OrderMode.Live;

    /// <summary>Default practice notional for day-start equity and MaxDailyLoss base.</summary>
    public const decimal DefaultPracticeStartingBalance = 100_000m;

    /// <summary>
    /// Absolute MaxDailyLoss for CreateDefault (3% of <see cref="DefaultPracticeStartingBalance"/>).
    /// RiskGate uses <see cref="DailyLossGuard.EvaluateAbsolute"/> — not a percent field.
    /// </summary>
    public const decimal DefaultPracticeMaxDailyLossAbsolute = 3_000m;

    public static AppHarness CreateDefault()
    {
        // MaxDailyLoss is absolute USD (not %). 3% of 100k practice notional = 3_000.
        // Equity inputs come from BuildPracticeContext so RiskGate can evaluate fail-closed correctly.
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = false,
            KillSwitch = true,
            OrderMode = OrderMode.DryRun,
            MaxOrderNotional = 50_000m,
            MaxDailyLoss = DefaultPracticeMaxDailyLossAbsolute,
            MarketDataMaxStalenessSeconds = TradingSafetyDefaults.MarketDataMaxStalenessSeconds,
        };
        var toss = TossReadOnlyFactory.LoadOptionsFromEnvironment();
        var portfolio = TossReadOnlyFactory.CreatePortfolioService(toss);
        var audit = new InMemoryAuditLog();
        // Shared index: same ClientOrderId cannot pass dry-run then paper twice
        var clientOrderIds = new ClientOrderIdIndex();
        var dryLedger = new InMemoryDryRunLedger();
        var dryRun = new DryRunOrderRouter(dryLedger, clientOrderIds);
        var paperLedger = new InMemoryPaperLedger();
        // Paper uses its own index for paper-only dups; dry-run already registered ids
        // stay unique per pipeline step via unique factory. Keep separate paper index.
        var paper = new PaperOrderRouter(paperLedger);
        // Gated live stub: registered for readiness evidence, never used by practice loop
        // under CreateDefault fail-closed settings. Prefer GatedLiveOrderRouter when present
        // in Orders assembly; fall back to BlockedLiveOrderRouter (always IsLiveSubmissionEnabled=false).
        var gatedLive = CreateGatedLiveRouterOrBlocked(settings);

        // Official final SPCX preset (pro + research lock-in).
        var session = new AutoTradeSessionService
        {
            StockKind = StockMarketKind.스페이스X,
            FocusSymbol = WatchlistCatalog.SpaceXSymbol,
            Strategy = SpacexOfficialStrategyPreset.Strategy,
            Timeframe = SpacexOfficialStrategyPreset.Timeframe,
        };

        return new AppHarness(
            settings,
            portfolio,
            new OrderCandidatePipeline(),
            new LiveOrderGate(),
            dryLedger,
            dryRun,
            paperLedger,
            paper,
            audit,
            session,
            toss,
            gatedLiveRouter: gatedLive);
    }

    /// <summary>
    /// Practice equity for RiskGate daily loss: session Balance as current,
    /// session StartingBalance (or <see cref="DefaultPracticeStartingBalance"/>) as day start.
    /// </summary>
    /// <summary>
    /// Practice risk context: equity sizing + daily loss % + optional trend params.
    /// Uses session balances — not live account money. Not investment advice.
    /// </summary>
    public PracticeStrategyContext BuildPracticeContext()
    {
        var dayStart = _session.StartingBalance > 0m
            ? _session.StartingBalance
            : DefaultPracticeStartingBalance;
        var equity = _session.Balance > 0m ? _session.Balance : dayStart;
        return new PracticeStrategyContext(
            Equity: equity,
            RiskPercentPerTrade: 1m,
            StopLossPercent: 2m,
            MaxDailyLossPercent: 3m,
            DayStartEquity: dayStart,
            CurrentEquity: equity,
            TrendFollow: TrendFollowParameters.CreateSafeDefaults(),
            NewsDay: _newsDay,
            SymbolWarningActive: _symbolWarningActive);
    }

    /// <summary>Owner toggle: news/event day — size 50%, no aggressive limit chase.</summary>
    public bool NewsDay
    {
        get => _newsDay;
        set => _newsDay = value;
    }

    /// <summary>Set from Toss warnings (or tests). Blocks new entries when true.</summary>
    public bool SymbolWarningActive
    {
        get => _symbolWarningActive;
        set => _symbolWarningActive = value;
    }

    /// <summary>Policy evaluation for new entries (does not place orders).</summary>
    public WorkingOrderDecision EvaluateEntryGate(bool sessionOpen = true, bool trendFilterOk = true) =>
        LimitOrderLifecyclePolicy.EvaluateNewEntryGate(
            killOrDailyLoss: _settings.KillSwitch,
            dataStale: false,
            sessionOpen: sessionOpen,
            symbolWarningActive: _symbolWarningActive,
            newsDay: _newsDay,
            trendFilterOk: trendFilterOk);

    /// <summary>
    /// Real <see cref="GatedLiveOrderRouter"/> with fail-closed context and no-op transport.
    /// Practice loop never calls this router; it is registered for capability/tests only.
    /// Under CreateDefault settings, <see cref="GatedLiveOrderRouter.IsLiveSubmissionEnabled"/> is false.
    /// </summary>
    private static IOrderRouter CreateGatedLiveRouterOrBlocked(TradingSafetySettings settings)
    {
        // Fail-closed context: LiveImplementationEnabled stays false for CreateDefault host.
        var failClosedContext = new LiveOrderContext
        {
            ManualApprovalPresent = false,
            LiveImplementationEnabled = false,
        };
        // Recording transport never hits network; CallCount stays 0 when gate blocks.
        var transport = new RecordingLiveOrderTransport();
        return new GatedLiveOrderRouter(settings, failClosedContext, transport);
    }

    public AutoTradePanelSnapshot GetAutoTradePanel() => _session.ToPanelSnapshot();

    public string StartAutoTrade()
    {
        _ = _session.TryStart(out var msg);
        _audit.Append(new AuditEntry(DateTimeOffset.UtcNow, "session", msg, "auto_start"));
        return msg;
    }

    public string StopAutoTrade()
    {
        _ = _session.TryStop(out var msg);
        _audit.Append(new AuditEntry(DateTimeOffset.UtcNow, "session", msg, "auto_stop"));
        return msg;
    }

    public void SetStockKind(StockMarketKind kind) => _session.StockKind = StockMarketKind.스페이스X;

    public void SetStrategy(TradingStrategyKind strategy) => _session.Strategy = strategy;

    public void SetFocusSymbol(string symbol) => _session.FocusSymbol = WatchlistCatalog.SpaceXSymbol;

    public void SetTimeframe(ChartTimeframe timeframe)
    {
        _session.Timeframe = timeframe;
        // 시간봉 변경 시 실봉 캐시 무효화
        _cachedRealCandles = null;
        _cachedCandlesSymbol = null;
    }

    public ChartTimeframe Timeframe => _session.Timeframe;

    /// <summary>
    /// SPCX long LIMIT bracket plan from last price + ATR (or %). Display / dry-run only.
    /// Never places live orders.
    /// </summary>
    public TradeBracketPlan GetActiveBracketPlan()
    {
        var (candles, _, _) = GetChartData();
        var atr = AtrCalculator.Compute(candles, SpacexRiskParameters.CreateSafeDefaults().AtrPeriod);
        var last = ResolveLastPrice(candles);
        var practice = BuildPracticeContext();
        var risk = SpacexRiskParameters.CreateSafeDefaults() with
        {
            RiskPercentPerTrade = practice.EffectiveRiskPercentPerTrade,
        };
        if (practice.SymbolWarningActive || practice.EffectiveRiskPercentPerTrade <= 0m)
        {
            return TradeBracketPlan.Invalid(
                WatchlistCatalog.SpaceXSymbol,
                "종목 경고 또는 리스크 0 — 지정가 계획 중단 · 실주문 없음");
        }
        var trend = practice.TrendFollow ?? TrendFollowParameters.CreateSafeDefaults();
        return TradeBracketPlanner.PlanLongLimit(
            WatchlistCatalog.SpaceXSymbol,
            last,
            practice.Equity > 0m ? practice.Equity : DefaultPracticeStartingBalance,
            risk,
            atr,
            trend);
    }

    private decimal ResolveLastPrice(IReadOnlyList<CandlePoint> candles)
    {
        var quote = _lastQuotes.FirstOrDefault(q =>
            q.Symbol.Equals(WatchlistCatalog.SpaceXSymbol, StringComparison.OrdinalIgnoreCase));
        if (quote?.LastPrice is decimal px && px > 0m)
        {
            return px;
        }

        if (candles is { Count: > 0 } && candles[^1].Close > 0)
        {
            return (decimal)candles[^1].Close;
        }

        return (decimal)WatchlistCatalog.ChartSeedPrice(WatchlistCatalog.SpaceXSymbol);
    }

    public (IReadOnlyList<CandlePoint> Candles, IReadOnlyList<TradeMarker> Markers, IReadOnlyList<ChartIndicatorLine> Indicators) GetChartData()
    {
        var symbol = WatchlistCatalog.SpaceXSymbol;
        var tf = _session.Timeframe;
        IReadOnlyList<CandlePoint> candles;
        var cacheKey = $"{symbol}:{ChartTimeframeCatalog.UiLabel(tf)}:{ChartTimeframeCatalog.SourceTossInterval(tf)}";
        if (_cachedRealCandles is { Count: > 0 }
            && string.Equals(_cachedCandlesSymbol, cacheKey, StringComparison.OrdinalIgnoreCase))
        {
            candles = _cachedRealCandles;
        }
        else
        {
            // Mock / offline: 1m dense then aggregate; 1d/1w use day steps
            var seed = TryResolveChartSeed(symbol);
            if (ChartTimeframeCatalog.SourceTossInterval(tf) == "1d"
                && !ChartTimeframeCatalog.IsWeeklyAggregation(tf))
            {
                candles = MockCandleSeriesFactory.CreateSeries(
                    symbol, 160, DateTimeOffset.UtcNow, seed, TimeSpan.FromDays(1));
            }
            else
            {
                var rawCount = ChartTimeframeCatalog.PreferredRawBarCount(tf, 160);
                var raw = MockCandleSeriesFactory.CreateSeries(
                    symbol, Math.Min(rawCount, 800), DateTimeOffset.UtcNow, seed, TimeSpan.FromMinutes(1));
                candles = ChartTimeframeCatalog.NeedsAggregation(tf)
                    ? CandleAggregator.Aggregate(raw, tf)
                    : raw;
            }

            if (candles.Count > 200)
            {
                candles = candles.Skip(candles.Count - 200).ToArray();
            }
        }

        // 실데이터·mock 공통: 봉 거래대금 버블 (ChartFanatics). 실연결에서 버블 실종 방지.
        var volumeBubbles = MockCandleSeriesFactory.CreateVolumeBubbles(candles);
        var paperAll = MockCandleSeriesFactory.MarkersFromPaperFills(_paperLedger.GetSnapshot());
        IReadOnlyList<TradeMarker> markers;
        if (paperAll.Count == 0)
        {
            markers = volumeBubbles;
        }
        else
        {
            var maxPaper = paperAll.Max(m => m.SizeWeight);
            var merged = new List<TradeMarker>(volumeBubbles.Count + paperAll.Count);
            merged.AddRange(volumeBubbles);
            foreach (var p in paperAll)
            {
                var w = maxPaper <= 0 ? 2.5 : 2.0 + 3.5 * (p.SizeWeight / maxPaper);
                merged.Add(p with { SizeWeight = w, Label = $"{p.Label}(체결)" });
            }

            markers = merged;
        }

        var indicators = ChartIndicatorCalculator.ForStrategy(candles, _session.Strategy);
        return (candles, markers, indicators);
    }

    /// <summary>
    /// Live 읽기 전용 스냅샷을 UI 세션에 바인딩: 잔액·SPCX 워치.
    /// mock / 오류 / 끊김에서는 호출하지 않음. 실주문 없음.
    /// </summary>
    public void ApplyRealPortfolio(ReadOnlyPortfolioSnapshot portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        if (portfolio.ConnectionStatus != ConnectionStatus.LiveReadOnlyConnected)
        {
            return;
        }

        _isLiveReadOnlyConnected = true;
        _lastQuotes = portfolio.Quotes ?? Array.Empty<QuoteSnapshot>();

        var balance = TryResolveRealBalance(portfolio);
        if (balance is decimal bal)
        {
            _session.ApplyExternalBalance(bal, setStartingIfUnset: true);
        }

        _session.SetDataSourceLabel("토스 실계좌");
        _session.StockKind = StockMarketKind.스페이스X;
        _session.ApplyExternalWatchSymbols([WatchlistCatalog.SpaceXSymbol]);
        _session.FocusSymbol = WatchlistCatalog.SpaceXSymbol;
    }

    /// <summary>
    /// CashBuyingPower(USD) 우선 — 병합 전 필드는 reflection-safe.
    /// 없으면 MarketValueUsdSummary 파싱, 그것도 없으면 null(연습 유지).
    /// </summary>
    public static decimal? TryResolveRealBalance(ReadOnlyPortfolioSnapshot portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);

        // Prefer USD cash buying power from Toss GET /api/v1/buying-power.
        if (portfolio.CashBuyingPower is decimal cash && cash > 0m)
        {
            return cash;
        }

        if (portfolio.MarketValueUsdDecimal is decimal mvd && mvd > 0m)
        {
            return mvd;
        }

        if (!string.IsNullOrWhiteSpace(portfolio.MarketValueUsdSummary)
            && decimal.TryParse(
                portfolio.MarketValueUsdSummary.Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var mv)
            && mv > 0m)
        {
            return mv;
        }

        return null;
    }

    private static decimal? TryReadOptionalDecimalProperty(object target, string propertyName)
    {
        var prop = target.GetType().GetProperty(propertyName);
        if (prop is null || !prop.CanRead)
        {
            return null;
        }

        var raw = prop.GetValue(target);
        return raw switch
        {
            null => null,
            decimal d => d,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            int i => i,
            long l => l,
            string s when decimal.TryParse(
                s.Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };
    }

    private double TryResolveChartSeed(string symbol)
    {
        var quote = _lastQuotes.FirstOrDefault(q =>
            q.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        if (quote?.LastPrice is decimal px && px > 0m)
        {
            return (double)px;
        }

        return WatchlistCatalog.ChartSeedPrice(symbol);
    }

    /// <summary>
    /// Cache real Toss candles for chart when live read-only is connected.
    /// </summary>
    private async Task TryCacheRealCandlesAsync(string symbol, CancellationToken cancellationToken)
    {
        if (!_isLiveReadOnlyConnected)
        {
            return;
        }

        var sym = WatchlistCatalog.SpaceXSymbol;
        var tf = _session.Timeframe;
        var sourceInterval = ChartTimeframeCatalog.SourceTossInterval(tf);
        var cacheKey = $"{sym}:{ChartTimeframeCatalog.UiLabel(tf)}:{sourceInterval}";

        try
        {
            var rawTarget = ChartTimeframeCatalog.PreferredRawBarCount(tf, targetDisplayBars: 160);
            var maxPages = Math.Clamp((rawTarget + 199) / 200, 1, 5);
            var raw = await _portfolio
                .GetCandlesPagedAsync(
                    sym,
                    sourceInterval,
                    countPerPage: 200,
                    maxPages: maxPages,
                    targetTotal: rawTarget,
                    cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<CandlePoint> candles = ChartTimeframeCatalog.NeedsAggregation(tf)
                ? CandleAggregator.Aggregate(raw, tf)
                : raw;

            // Cap display bars for UI density
            if (candles.Count > 200)
            {
                candles = candles.Skip(candles.Count - 200).ToArray();
            }

            if (candles.Count > 0)
            {
                _cachedRealCandles = candles;
                _cachedCandlesSymbol = cacheKey;
            }
        }
        catch
        {
            // Fail closed for chart: keep seed path. Never affect orders.
            _cachedRealCandles = null;
            _cachedCandlesSymbol = null;
        }
    }

    public async Task<CockpitDashboardModel> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        _audit.Append(new AuditEntry(
            DateTimeOffset.UtcNow,
            "system",
            "데스크톱 콕핏 갱신 — 토스 읽기 · SPCX · 실주문 게이트 잠금",
            "app_boot"));

        var liveDecision = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());
        var symbols = new[] { WatchlistCatalog.SpaceXSymbol };
        var portfolio = await _portfolio
            .GetSnapshotAsync(symbols, cancellationToken)
            .ConfigureAwait(false);
        _connectionLabel = portfolio.ConnectionOwnerMessage;
        _connectionModeLabel = TossReadOnlyFactory.DescribeMode(_tossOptions);

        // Live 읽기 전용일 때만 실 포트폴리오를 세션에 바인딩 (mock은 연습 유지)
        if (portfolio.ConnectionStatus == ConnectionStatus.LiveReadOnlyConnected)
        {
            ApplyRealPortfolio(portfolio);
            await TryCacheRealCandlesAsync(WatchlistCatalog.SpaceXSymbol, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            _isLiveReadOnlyConnected = false;
            // mock/오류: 시세는 차트 seed로만 캐시 (라벨·잔액은 연습 유지)
            _lastQuotes = portfolio.Quotes ?? Array.Empty<QuoteSnapshot>();
            _cachedRealCandles = null;
            _cachedCandlesSymbol = null;
        }

        var snapshot = CockpitReadOnlyProjector.Project(portfolio, _settings);

        var now = DateTimeOffset.UtcNow;
        var usSession = UsMarketSessionGuard.Evaluate(portfolio.UsMarket, now);
        var strategy = _session.Strategy;
        var qty = StrategyCatalog.BaseQuantity(strategy);
        IReadOnlyList<EvaluatedOrderCandidate> evaluated = Array.Empty<EvaluatedOrderCandidate>();

        if (_session.Status == AutoTradeSessionStatus.실행중)
        {
            // End-to-end practice strategy: sizer + daily halt % + trend params + session window.
            var practice = BuildPracticeContext();
            evaluated = _pipeline.BuildCandidates(
                portfolio.Quotes ?? Array.Empty<QuoteSnapshot>(),
                _settings,
                defaultOrderQuantity: qty,
                nowUtc: now,
                marketSessionOpen: usSession.IsOpenForOrders,
                marketSessionKnown: usSession.IsKnown,
                usMarket: portfolio.UsMarket,
                strategy: strategy,
                practice: practice);

            foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
            {
                // CreateDefault practice loop: dry-run + paper only.
                // Gated live router is intentionally NOT used here even if registered.
                // Live path remains fail-closed unless settings would allow AND a future
                // approved implementation is wired (SettingsWouldAllowLiveRouting is false
                // under CreateDefault).
                _ = await _dryRun.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
                var paperResult = await _paper.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
                if (paperResult.Accepted && item.Candidate.LimitPrice is decimal px)
                {
                    _session.ApplyVirtualFill(item.Candidate.Side, item.Candidate.Quantity, px);
                    // 규모(수량×가격)에 비례한 소액 평가손익 연습
                    _session.ApplyScaffoldMarkToMarket(item.Candidate.Quantity * px * 0.0005m);
                }
            }
        }

        return CockpitDashboardMapper.Compose(
            snapshot,
            _settings,
            candidates: evaluated,
            extraRiskDecision: liveDecision);
    }

    public (int DryRun, int Paper, bool LiveBlocked) GetEvidenceCounts() =>
        (_dryRunLedger.Count, _paperLedger.Count, true);

    /// <summary>
    /// Practice evidence summary: dry-run/paper ledger counts, live always blocked,
    /// optional export text built from <see cref="EvidenceBuilder"/> / UI owner message.
    /// Never enables live orders or touches Toss order HTTP.
    /// </summary>
    public AppTradingEvidenceSummary GetTradingEvidenceSummary()
    {
        // Exercise fail-closed live gate; desktop host always reports blocked.
        _ = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());

        var snapshot = _evidenceBuilder.Build();
        var dry = snapshot.Summary.DryRunEntryCount;
        var paper = snapshot.Summary.PaperFillCount;

        // Real exporter over live ledger snapshots (shipped path).
        var exporter = new TradingEvidenceExporter(_dryRunLedger, _paperLedger);
        var exportText = exporter.ExportAsText();
        if (!exportText.Contains("live_orders=false", StringComparison.Ordinal)
            && !exportText.Contains("LiveSubmissionEnabled=false", StringComparison.OrdinalIgnoreCase))
        {
            exportText += "\nlive_orders=false\nLiveSubmissionEnabled=false";
        }

        return new AppTradingEvidenceSummary(
            DryRunCount: dry,
            PaperCount: paper,
            LiveBlocked: true,
            ExportText: exportText,
            Snapshot: snapshot);
    }

    /// <summary>
    /// Live readiness / owner-unlock status via shipped <see cref="LiveReadinessEvaluator"/>.
    /// Never enables live submission — <see cref="LiveReadinessEvaluation.LiveReady"/> is always false.
    /// </summary>
    public AppLiveReadinessReport GetLiveReadinessReport()
    {
        // Fail-closed gate exercise (defaults block).
        _ = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());

        var root = TryFindRepoRoot() ?? Directory.GetCurrentDirectory();
        var evaluation = LiveReadinessEvaluator.Evaluate(root);

        var checklistPresent = File.Exists(Path.Combine(root, "docs", "LIVE_READINESS_CHECKLIST.md"));
        var evidencePresent = File.Exists(Path.Combine(root, "docs", "plans", "LIVE_READINESS_EVIDENCE.md"));
        var scriptPresent = File.Exists(Path.Combine(root, "scripts", "grok", "check-live-readiness.sh"));
        var artifactDirPresent = Directory.Exists(
            Path.Combine(root, LiveReadinessEvaluator.RelativeArtifactDirectory));

        var ownerFiles = evaluation.PresentArtifacts
            .Where(n => n.Contains("owner", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("signoff", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("unlock", StringComparison.OrdinalIgnoreCase))
            .Select(n => Path.Combine(LiveReadinessEvaluator.RelativeArtifactDirectory, n))
            .ToList();

        var ownerStatus = evaluation.ToOwnerUnlockStatusToken();
        var summary =
            $"LIVE_READY=false; LIVE_OWNER_UNLOCK_STATUS={ownerStatus}; "
            + $"status={evaluation.Status}; "
            + $"gatedLiveRouterType={(_gatedLiveRouter?.GetType().Name ?? "none")}; "
            + "practiceLoop=dry_run_paper_only";

        return new AppLiveReadinessReport(
            LiveBlocked: true,
            IsLiveSubmissionEnabled: IsLiveSubmissionEnabled,
            OwnerUnlockStatus: ownerStatus,
            ChecklistPresent: checklistPresent,
            EvidenceDocPresent: evidencePresent,
            LiveReadinessArtifactDirPresent: artifactDirPresent,
            AutomationScriptPresent: scriptPresent,
            GatedLiveRouterRegistered: IsGatedLiveRouterRegistered,
            GatedLiveRouterUsedInPractice: false,
            SettingsAllowLiveOrders: _settings.AllowLiveOrders,
            SettingsKillSwitch: _settings.KillSwitch,
            SettingsOrderMode: _settings.OrderMode.ToString(),
            OwnerUnlockArtifactFiles: ownerFiles,
            Summary: summary);
    }

    /// <summary>
    /// Walk up from <see cref="AppContext.BaseDirectory"/> to find repo root (TradingBot.sln).
    /// Returns null when not found (e.g. packaged deploy without solution file).
    /// </summary>
    public static string? TryFindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TradingBot.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

}
