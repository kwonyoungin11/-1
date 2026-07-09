using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Infrastructure.Toss.News;
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
    public bool IsLiveSubmissionEnabled => !LiveBlocked;
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
    private ConnectionStatus _lastConnectionStatus = ConnectionStatus.Disconnected;
    /// <summary>
    /// True when live read-only was connected but Toss candle fetch failed, returned empty,
    /// or cache was cleared — chart is showing MockCandleSeriesFactory fallback, not production bars.
    /// </summary>
    private bool _chartCandleFetchFailedOrEmpty;
    private IReadOnlyList<QuoteSnapshot> _lastQuotes = Array.Empty<QuoteSnapshot>();
    private string? _cachedCandlesSymbol;
    private IReadOnlyList<CandlePoint>? _cachedRealCandles;
    private bool _newsDay;
    private bool _symbolWarningActive;
    private readonly INewsFeed _newsFeed;
    private IReadOnlyList<NewsHeadline> _lastNews = Array.Empty<NewsHeadline>();
    private string _newsStatus = "뉴스 대기";

    /// <summary>
    /// Max candles shown on the chart (UI density). Toss still pages at
    /// <see cref="TossCandlePageSize"/> bars per HTTP request — this cap only trims after fetch/aggregate.
    /// </summary>
    public const int ChartDisplayBarCap = 300;

    /// <summary>Preferred display bar count when requesting/aggregating series (capped by <see cref="ChartDisplayBarCap"/>).</summary>
    public const int ChartPreferredDisplayBars = 300;

    /// <summary>Toss OpenAPI candle page size (official list endpoint page limit).</summary>
    public const int TossCandlePageSize = 200;

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
        IOrderRouter? gatedLiveRouter = null,
        INewsFeed? newsFeed = null)
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
        _newsFeed = newsFeed ?? CompositeSpaceXNewsFeed.FromEnvironment();
    }

    public AutoTradeSessionService Session => _session;

    public string ConnectionLabel => _connectionLabel;

    public string ConnectionModeLabel => _connectionModeLabel;

    public bool IsLiveReadOnlyConnected => _isLiveReadOnlyConnected;

    /// <summary>
    /// True only when the chart series is the cached Toss candle response
    /// (not <see cref="MockCandleSeriesFactory"/>). Requires live read-only connection
    /// and a non-empty cache keyed to the current symbol/timeframe.
    /// </summary>
    public bool ChartUsesRealCandles =>
        _isLiveReadOnlyConnected
        && _cachedRealCandles is { Count: > 0 }
        && !string.IsNullOrEmpty(_cachedCandlesSymbol)
        && !_chartCandleFetchFailedOrEmpty;

    /// <summary>
    /// Diagnostic / pill label for chart data provenance. Prefer <see cref="ChartWatermark"/> for on-chart UI.
    /// </summary>
    public string ChartDataSourceLabel =>
        ChartUsesRealCandles
            ? "토스 실봉"
            : IsChartDataErrorOrStale
                ? "데이터 오류/폴백 · 실봉 아님 · 주문 차단"
                : _isLiveReadOnlyConnected
                    ? "토스 연결 · 봉 대기/실패 → 표시용 폴백"
                    : "mock/오프라인 폴백 · 실 HTTP 아님";

    /// <summary>
    /// On-chart honesty watermark for ViewModel binding. Never silent production look on mock/fallback.
    /// <list type="bullet">
    /// <item><description>Real Toss candles: <c>토스 실봉</c></description></item>
    /// <item><description>Mock / offline practice: <c>연습 데이터 · 실봉 아님</c></description></item>
    /// <item><description>Error, stale, or live-connected mock fallback: <c>데이터 오류 · 주문 차단</c></description></item>
    /// </list>
    /// </summary>
    public string ChartWatermark =>
        ChartUsesRealCandles
            ? "토스 실봉"
            : IsChartDataErrorOrStale
                ? "데이터 오류 · 주문 차단"
                : "연습 데이터 · 실봉 아님";

    /// <summary>
    /// Fail-closed chart honesty: connection error, blocked, or live path that could not load real candles.
    /// </summary>
    public bool IsChartDataErrorOrStale =>
        _lastConnectionStatus is ConnectionStatus.Error or ConnectionStatus.Blocked
        || _chartCandleFetchFailedOrEmpty
        || (_isLiveReadOnlyConnected && !ChartUsesRealCandles);

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
        var root = TryFindRepoRoot();
        var env = EnvFile.LoadMergedWithProcess(root);
        var loaded = TradingSafetySettings.FromEnvironment(env);
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = loaded.AllowLiveOrders,
            KillSwitch = loaded.KillSwitch,
            OrderMode = loaded.OrderMode,
            MarketDataMaxStalenessSeconds = loaded.MarketDataMaxStalenessSeconds,
            MaxOrderNotional = loaded.MaxOrderNotional ?? 50_000m,
            MaxDailyLoss = loaded.MaxDailyLoss ?? DefaultPracticeMaxDailyLossAbsolute,
            MaxPositionSize = loaded.MaxPositionSize,
            MaxSymbolPositionRatio = loaded.MaxSymbolPositionRatio,
            MaxOpenOrders = loaded.MaxOpenOrders,
        };

        var toss = TossOptions.FromEnvironment(env);
        var portfolio = TossReadOnlyFactory.CreatePortfolioService(toss);
        var audit = new InMemoryAuditLog();
        var clientOrderIds = new ClientOrderIdIndex();
        var dryLedger = new InMemoryDryRunLedger();
        var dryRun = new DryRunOrderRouter(dryLedger, clientOrderIds);
        var paperLedger = new InMemoryPaperLedger();
        var paper = new PaperOrderRouter(paperLedger);
        var transport = CreateLiveTransport(settings, toss);
        AppHarness? self = null;
        var gatedLive = new GatedLiveOrderRouter(
            settings,
            () => self!.BuildLiveOrderContext(),
            transport);

        var session = new AutoTradeSessionService
        {
            StockKind = StockMarketKind.비전마린,
            FocusSymbol = WatchlistCatalog.VmarSymbol,
            Strategy = VmarOneMinuteScalpPreset.Strategy,
            Timeframe = VmarOneMinuteScalpPreset.Timeframe,
        };

        self = new AppHarness(
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
            gatedLiveRouter: gatedLive,
            newsFeed: CompositeSpaceXNewsFeed.FromEnvironment());
        return self;
    }

    public IReadOnlyList<NewsHeadline> LastNews => _lastNews;

    public string NewsStatus => _newsStatus;

    /// <summary>
    /// Poll focus-symbol news (Finnhub if key present, else mock). Display/gate only.
    /// </summary>
    public async Task RefreshNewsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var newsSymbol = _session.ResolveFocusSymbol();
            var items = await _newsFeed
                .GetHeadlinesAsync(newsSymbol, maxCount: 2, cancellationToken)
                .ConfigureAwait(false);
            _lastNews = items;
            var material = items.Count(i => i.IsMaterialEvent);
            var sourceNote = _newsFeed is CompositeSpaceXNewsFeed c
                ? c.LastSourceNote
                : "feed";
            _newsStatus = items.Count == 0
                ? sourceNote
                : $"{sourceNote} · 중요 {material} · {KoreaTime.FormatFull(DateTimeOffset.UtcNow)}";
            if (material > 0 && !_newsDay)
            {
                _newsStatus += " · 뉴스데이 수동 ON 권장";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastNews = Array.Empty<NewsHeadline>();
            _newsStatus = $"실뉴스 오류 · {ex.GetType().Name} · 모의 없음";
        }
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

    private static ILiveOrderTransport CreateLiveTransport(TradingSafetySettings settings, TossOptions toss)
    {
        if (settings.OrderMode == OrderMode.Live
            && settings.AllowLiveOrders
            && !settings.KillSwitch
            && toss.AllowLiveHttp
            && toss.HasClientCredentials)
        {
            return TossReadOnlyFactory.CreateLiveOrderTransport(toss);
        }

        return new RecordingLiveOrderTransport();
    }

    private LiveOrderContext BuildLiveOrderContext()
    {
        var stale = false;
        if (_lastQuotes.Count > 0 && _settings.MarketDataMaxStalenessSeconds > 0)
        {
            var timestamps = _lastQuotes
                .Select(q => q.TimestampUtc)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToList();
            if (timestamps.Count > 0)
            {
                var newest = timestamps.Max();
                stale = DateTimeOffset.UtcNow - newest
                    > TimeSpan.FromSeconds(_settings.MarketDataMaxStalenessSeconds);
            }
        }

        return new LiveOrderContext
        {
            HasUnknownState = false,
            HasMissingData = _settings.OrderMode == OrderMode.Live
                && _lastConnectionStatus != ConnectionStatus.LiveReadOnlyConnected,
            HasStaleMarketData = stale,
            HasApiError = _lastConnectionStatus == ConnectionStatus.Error,
        };
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

    public void SetStockKind(StockMarketKind kind)
    {
        if (kind != StockMarketKind.비전마린)
        {
            kind = StockMarketKind.비전마린;
        }

        _session.StockKind = kind;
        var symbols = WatchlistCatalog.ResolveSymbols(kind);
        var focus = symbols.Count > 0 ? symbols[0] : WatchlistCatalog.VmarSymbol;
        _session.FocusSymbol = focus;

        _session.Strategy = VmarOneMinuteScalpPreset.Strategy;
        SetTimeframe(VmarOneMinuteScalpPreset.Timeframe);

        _cachedRealCandles = null;
        _cachedCandlesSymbol = null;
        if (_isLiveReadOnlyConnected)
        {
            _chartCandleFetchFailedOrEmpty = true;
        }
    }

    public void SetStrategy(TradingStrategyKind strategy) => _session.Strategy = strategy;

    public void SetFocusSymbol(string symbol)
    {
        if (WatchlistCatalog.IsKnownSymbol(symbol))
        {
            _session.FocusSymbol = symbol;
            _cachedRealCandles = null;
            _cachedCandlesSymbol = null;
            if (_isLiveReadOnlyConnected)
            {
                _chartCandleFetchFailedOrEmpty = true;
            }
        }
    }

    public void SetTimeframe(ChartTimeframe timeframe)
    {
        _session.Timeframe = timeframe;
        // 시간봉 변경 시 실봉 캐시 무효화 — 다음 대시보드 갱신 전까지 폴백 라벨 유지
        _cachedRealCandles = null;
        _cachedCandlesSymbol = null;
        if (_isLiveReadOnlyConnected)
        {
            _chartCandleFetchFailedOrEmpty = true;
        }
    }

    public ChartTimeframe Timeframe => _session.Timeframe;

    /// <summary>
    /// Focus-symbol long LIMIT bracket plan from last price + ATR (or %). Display / dry-run only.
    /// Never places live orders.
    /// </summary>
    public TradeBracketPlan GetActiveBracketPlan()
    {
        var symbol = _session.ResolveFocusSymbol();
        var (candles, _, _) = GetChartData();
        var atr = AtrCalculator.Compute(candles, SpacexRiskParameters.CreateSafeDefaults().AtrPeriod);
        var last = ResolveLastPrice(candles, symbol);
        var practice = BuildPracticeContext();
        var riskPercent = practice.EffectiveRiskPercentPerTrade;
        if (_session.StockKind == StockMarketKind.비전마린
            || string.Equals(symbol, WatchlistCatalog.VmarSymbol, StringComparison.OrdinalIgnoreCase))
        {
            riskPercent = Math.Min(riskPercent, VmarOneMinuteScalpPreset.RiskPercentPerTrade);
        }

        var isVmar = _session.StockKind == StockMarketKind.비전마린
            || string.Equals(symbol, WatchlistCatalog.VmarSymbol, StringComparison.OrdinalIgnoreCase);
        var risk = SpacexRiskParameters.CreateSafeDefaults() with
        {
            RiskPercentPerTrade = riskPercent,
            UseAtrStops = !isVmar,
            FallbackStopLossPercent = isVmar ? 2.0m : SpacexRiskParameters.CreateSafeDefaults().FallbackStopLossPercent,
        };
        if (practice.SymbolWarningActive || riskPercent <= 0m)
        {
            return TradeBracketPlan.Invalid(
                symbol,
                "종목 경고 또는 리스크 0 — 지정가 계획 중단 · 실주문 없음");
        }
        var trend = practice.TrendFollow ?? TrendFollowParameters.CreateSafeDefaults();
        return TradeBracketPlanner.PlanLongLimit(
            symbol,
            last,
            practice.Equity > 0m ? practice.Equity : DefaultPracticeStartingBalance,
            risk,
            isVmar ? null : atr,
            trend);
    }

    private decimal ResolveLastPrice(IReadOnlyList<CandlePoint> candles, string symbol)
    {
        var quote = _lastQuotes.FirstOrDefault(q =>
            q.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        if (quote?.LastPrice is decimal px && px > 0m)
        {
            return px;
        }

        if (candles is { Count: > 0 } && candles[^1].Close > 0)
        {
            return (decimal)candles[^1].Close;
        }

        return (decimal)WatchlistCatalog.ChartSeedPrice(symbol);
    }

    /// <summary>
    /// Chart series for UI. Uses cached Toss candles when <see cref="ChartUsesRealCandles"/>;
    /// otherwise <see cref="MockCandleSeriesFactory"/> — never silent production look
    /// (bind <see cref="ChartWatermark"/> / <see cref="ChartDataSourceLabel"/>).
    /// Display capped at <see cref="ChartDisplayBarCap"/>; Toss page size remains <see cref="TossCandlePageSize"/>.
    /// </summary>
    public (IReadOnlyList<CandlePoint> Candles, IReadOnlyList<TradeMarker> Markers, IReadOnlyList<ChartIndicatorLine> Indicators) GetChartData()
    {
        var symbol = _session.ResolveFocusSymbol();
        var tf = _session.Timeframe;
        IReadOnlyList<CandlePoint> candles;
        var cacheKey = $"{symbol}:{ChartTimeframeCatalog.UiLabel(tf)}:{ChartTimeframeCatalog.SourceTossInterval(tf)}";
        if (ChartUsesRealCandles
            && _cachedRealCandles is { Count: > 0 }
            && string.Equals(_cachedCandlesSymbol, cacheKey, StringComparison.OrdinalIgnoreCase))
        {
            candles = _cachedRealCandles;
        }
        else
        {
            // Mock / fallback path — ChartWatermark must not look like live production bars.
            if (_isLiveReadOnlyConnected)
            {
                _chartCandleFetchFailedOrEmpty = true;
            }

            var seed = TryResolveChartSeed(symbol);
            if (ChartTimeframeCatalog.SourceTossInterval(tf) == "1d"
                && !ChartTimeframeCatalog.IsWeeklyAggregation(tf))
            {
                candles = MockCandleSeriesFactory.CreateSeries(
                    symbol, ChartPreferredDisplayBars, DateTimeOffset.UtcNow, seed, TimeSpan.FromDays(1));
            }
            else
            {
                var rawCount = ChartTimeframeCatalog.PreferredRawBarCount(tf, ChartPreferredDisplayBars);
                var raw = MockCandleSeriesFactory.CreateSeries(
                    symbol, Math.Min(rawCount, 800), DateTimeOffset.UtcNow, seed, TimeSpan.FromMinutes(1));
                candles = ChartTimeframeCatalog.NeedsAggregation(tf)
                    ? CandleAggregator.Aggregate(raw, tf)
                    : raw;
            }

            candles = CapDisplayBars(candles);
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

    private static IReadOnlyList<CandlePoint> CapDisplayBars(IReadOnlyList<CandlePoint> candles)
    {
        if (candles.Count <= ChartDisplayBarCap)
        {
            return candles;
        }

        return candles.Skip(candles.Count - ChartDisplayBarCap).ToArray();
    }

    /// <summary>
    /// Live 읽기 전용 스냅샷을 UI 세션에 바인딩: 잔액·워치(알려진 심볼).
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
            _session.SetDataSourceLabel("토스 실계좌");
        }
        else
        {
            _session.SetDataSourceLabel("토스 실연결 · 잔액 미확인");
        }

        var fromHoldings = (portfolio.Holdings ?? Array.Empty<HoldingSummary>())
            .Select(h => h.Symbol)
            .Where(WatchlistCatalog.IsKnownSymbol)
            .ToArray();
        var fromQuotes = (portfolio.Quotes ?? Array.Empty<QuoteSnapshot>())
            .Select(q => q.Symbol)
            .Where(WatchlistCatalog.IsKnownSymbol)
            .ToArray();
        var catalog = WatchlistCatalog.ResolveSymbols(_session.StockKind);
        var merged = catalog
            .Concat(fromHoldings)
            .Concat(fromQuotes)
            .Select(WatchlistCatalog.NormalizeKnownSymbol)
            .Where(s => s is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _session.ApplyExternalWatchSymbols(
            merged.Length > 0 ? merged : [WatchlistCatalog.VmarSymbol]);

        var focus = _session.ResolveFocusSymbol();
        if (!WatchlistCatalog.IsKnownSymbol(focus))
        {
            _session.FocusSymbol = merged.Length > 0 ? merged[0] : WatchlistCatalog.VmarSymbol;
        }
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
    /// Cache real Toss candles when live read-only is connected.
    /// On empty/error: clear cache and set <see cref="_chartCandleFetchFailedOrEmpty"/> so
    /// <see cref="GetChartData"/> mock fallback is labeled (never silent production look).
    /// Toss page size = <see cref="TossCandlePageSize"/>; display cap = <see cref="ChartDisplayBarCap"/>.
    /// </summary>
    private async Task TryCacheRealCandlesAsync(string symbol, CancellationToken cancellationToken)
    {
        if (!_isLiveReadOnlyConnected)
        {
            return;
        }

        var sym = string.IsNullOrWhiteSpace(symbol)
            ? _session.ResolveFocusSymbol()
            : (WatchlistCatalog.NormalizeKnownSymbol(symbol) ?? _session.ResolveFocusSymbol());
        var tf = _session.Timeframe;
        var sourceInterval = ChartTimeframeCatalog.SourceTossInterval(tf);
        var cacheKey = $"{sym}:{ChartTimeframeCatalog.UiLabel(tf)}:{sourceInterval}";

        try
        {
            var rawTarget = ChartTimeframeCatalog.PreferredRawBarCount(tf, targetDisplayBars: ChartPreferredDisplayBars);
            // Catalog still clamps direct 1m/1d raw to Toss page size (200); multi-page fetch can fill ChartDisplayBarCap.
            if (!ChartTimeframeCatalog.NeedsAggregation(tf))
            {
                rawTarget = Math.Max(rawTarget, ChartPreferredDisplayBars);
            }

            var maxPages = Math.Clamp((rawTarget + TossCandlePageSize - 1) / TossCandlePageSize, 1, 5);
            var raw = await _portfolio
                .GetCandlesPagedAsync(
                    sym,
                    sourceInterval,
                    countPerPage: TossCandlePageSize,
                    maxPages: maxPages,
                    targetTotal: rawTarget,
                    cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<CandlePoint> candles = ChartTimeframeCatalog.NeedsAggregation(tf)
                ? CandleAggregator.Aggregate(raw, tf)
                : raw;

            candles = CapDisplayBars(candles);

            if (candles.Count > 0)
            {
                _cachedRealCandles = candles;
                _cachedCandlesSymbol = cacheKey;
                _chartCandleFetchFailedOrEmpty = false;
            }
            else
            {
                // Empty Toss response → mock fallback must show error/practice watermark, not "실봉".
                _cachedRealCandles = null;
                _cachedCandlesSymbol = null;
                _chartCandleFetchFailedOrEmpty = true;
            }
        }
        catch
        {
            // Fail closed for chart honesty: mock path + error watermark. Never affect orders.
            _cachedRealCandles = null;
            _cachedCandlesSymbol = null;
            _chartCandleFetchFailedOrEmpty = true;
        }
    }

    public async Task<CockpitDashboardModel> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var focus = _session.ResolveFocusSymbol();
        _audit.Append(new AuditEntry(
            DateTimeOffset.UtcNow,
            "system",
            $"데스크톱 콕핏 갱신 — 토스 읽기 · {focus} · 실주문 게이트 잠금",
            "app_boot"));

        var liveDecision = _liveOrderGate.Evaluate(_settings, BuildLiveOrderContext());
        var symbols = _session.ResolveWatchSymbols();
        if (symbols.Length == 0)
        {
            symbols = [focus];
        }

        var portfolio = await _portfolio
            .GetSnapshotAsync(symbols, cancellationToken)
            .ConfigureAwait(false);
        _connectionLabel = portfolio.ConnectionOwnerMessage;
        _connectionModeLabel = TossReadOnlyFactory.DescribeMode(_tossOptions);
        await RefreshNewsAsync(cancellationToken).ConfigureAwait(false);

        _lastConnectionStatus = portfolio.ConnectionStatus;

        // Live 읽기 전용일 때만 실 포트폴리오를 세션에 바인딩 (mock/오류는 연습·폴백 라벨)
        if (portfolio.ConnectionStatus == ConnectionStatus.LiveReadOnlyConnected)
        {
            ApplyRealPortfolio(portfolio);
            await TryCacheRealCandlesAsync(_session.ResolveFocusSymbol(), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            _isLiveReadOnlyConnected = false;
            _lastQuotes = portfolio.Quotes ?? Array.Empty<QuoteSnapshot>();
            _cachedRealCandles = null;
            _cachedCandlesSymbol = null;
            // Offline mock is practice; Error/Blocked is fail-closed chart honesty (orders already blocked).
            _chartCandleFetchFailedOrEmpty =
                portfolio.ConnectionStatus is ConnectionStatus.Error or ConnectionStatus.Blocked;
            _session.SetDataSourceLabel(
                portfolio.ConnectionStatus == ConnectionStatus.Error
                    ? "오류 · 연습 폴백"
                    : portfolio.ConnectionStatus == ConnectionStatus.Blocked
                        ? "차단 · 연습 폴백"
                        : "mock/오프라인");
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
                if (SettingsWouldAllowLiveRouting && _gatedLiveRouter is not null)
                {
                    var liveResult = await _gatedLiveRouter
                        .RouteAsync(item.Candidate, cancellationToken)
                        .ConfigureAwait(false);
                    _audit.Append(new AuditEntry(
                        DateTimeOffset.UtcNow,
                        "orders",
                        liveResult.Message,
                        liveResult.Accepted ? "live_accepted" : "live_blocked"));
                    continue;
                }

                _ = await _dryRun.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
                var paperResult = await _paper.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
                if (paperResult.Accepted && item.Candidate.LimitPrice is decimal px)
                {
                    _session.ApplyVirtualFill(item.Candidate.Side, item.Candidate.Quantity, px);
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
        (_dryRunLedger.Count, _paperLedger.Count, !IsLiveSubmissionEnabled);

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
            LiveBlocked: !IsLiveSubmissionEnabled,
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
            + (SettingsWouldAllowLiveRouting ? "practiceLoop=live" : "practiceLoop=dry_run_paper_only");

        return new AppLiveReadinessReport(
            LiveBlocked: !IsLiveSubmissionEnabled,
            IsLiveSubmissionEnabled: IsLiveSubmissionEnabled,
            OwnerUnlockStatus: ownerStatus,
            ChecklistPresent: checklistPresent,
            EvidenceDocPresent: evidencePresent,
            LiveReadinessArtifactDirPresent: artifactDirPresent,
            AutomationScriptPresent: scriptPresent,
            GatedLiveRouterRegistered: IsGatedLiveRouterRegistered,
            GatedLiveRouterUsedInPractice: SettingsWouldAllowLiveRouting,
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
