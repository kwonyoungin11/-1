using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

namespace TradingBot.App.Services;

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
    private readonly IAuditLog _audit;
    private readonly AutoTradeSessionService _session;
    private readonly TossOptions _tossOptions;
    private string _connectionLabel = "연결 확인 전";
    private string _connectionModeLabel = "mock";

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
        TossOptions? tossOptions = null)
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
    }

    public AutoTradeSessionService Session => _session;

    public string ConnectionLabel => _connectionLabel;

    public string ConnectionModeLabel => _connectionModeLabel;

    public static AppHarness CreateDefault()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = false,
            KillSwitch = true,
            OrderMode = OrderMode.DryRun,
            MaxOrderNotional = 50_000m,
            MarketDataMaxStalenessSeconds = TradingSafetyDefaults.MarketDataMaxStalenessSeconds,
        };
        var toss = TossReadOnlyFactory.LoadOptionsFromEnvironment();
        var portfolio = TossReadOnlyFactory.CreatePortfolioService(toss);
        var audit = new InMemoryAuditLog();
        var dryLedger = new InMemoryDryRunLedger();
        var dryRun = new DryRunOrderRouter(dryLedger);
        var paperLedger = new InMemoryPaperLedger();
        var paper = new PaperOrderRouter(paperLedger);
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
            new AutoTradeSessionService(),
            toss);
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

    public void SetStockKind(StockMarketKind kind) => _session.StockKind = kind;

    public void SetStrategy(TradingStrategyKind strategy) => _session.Strategy = strategy;

    public void SetFocusSymbol(string symbol) => _session.FocusSymbol = symbol;

    public (IReadOnlyList<CandlePoint> Candles, IReadOnlyList<TradeMarker> Markers) GetChartData()
    {
        var symbol = _session.ResolveFocusSymbol();
        var seed = WatchlistCatalog.ChartSeedPrice(symbol);
        var candles = MockCandleSeriesFactory.CreateSeries(symbol, 160, DateTimeOffset.UtcNow, seed);
        var paperAll = MockCandleSeriesFactory.MarkersFromPaperFills(_paperLedger.GetSnapshot());
        // 포커스 종목 paper 체결만 강조 + 데모 버블(거래대금 규모)
        var paperFocus = paperAll
            .Where(m => true) // paper fill에 symbol 없음 → 전체 반영; 규모는 Notional
            .ToList();
        var demo = MockCandleSeriesFactory.CreateDemoMarkers(candles);
        if (paperFocus.Count == 0)
        {
            return (candles, demo);
        }

        var merged = new List<TradeMarker>(demo.Count + paperFocus.Count);
        merged.AddRange(demo);
        // paper 규모를 차트 Weight 스케일로 정규화 (로그 스케일 근사)
        var maxPaper = paperFocus.Max(m => m.SizeWeight);
        foreach (var p in paperFocus)
        {
            var w = maxPaper <= 0 ? 1 : 1.2 + 4.0 * (p.SizeWeight / maxPaper);
            merged.Add(p with { SizeWeight = w, Label = $"{p.Label}(연습체결)" });
        }

        return (candles, merged);
    }

    public async Task<CockpitDashboardModel> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        _audit.Append(new AuditEntry(
            DateTimeOffset.UtcNow,
            "system",
            "데스크톱 콕핏 갱신 — 실거래 차단 · 연습만",
            "app_boot"));

        var liveDecision = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());
        var symbols = _session.ResolveWatchSymbols();
        var portfolio = await _portfolio
            .GetSnapshotAsync(symbols, cancellationToken)
            .ConfigureAwait(false);
        _connectionLabel = portfolio.ConnectionOwnerMessage;
        _connectionModeLabel = TossReadOnlyFactory.DescribeMode(_tossOptions);
        var snapshot = CockpitReadOnlyProjector.Project(portfolio, _settings);

        var now = DateTimeOffset.UtcNow;
        var usSession = UsMarketSessionGuard.Evaluate(portfolio.UsMarket, now);
        var strategy = _session.Strategy;
        var qty = StrategyCatalog.BaseQuantity(strategy);
        IReadOnlyList<EvaluatedOrderCandidate> evaluated = Array.Empty<EvaluatedOrderCandidate>();

        if (_session.Status == AutoTradeSessionStatus.실행중)
        {
            evaluated = _pipeline.BuildCandidates(
                portfolio.Quotes,
                _settings,
                defaultOrderQuantity: qty,
                nowUtc: now,
                marketSessionOpen: usSession.IsOpenForOrders,
                marketSessionKnown: usSession.IsKnown,
                usMarket: portfolio.UsMarket,
                strategy: strategy);

            foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
            {
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
}
