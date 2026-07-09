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
        AutoTradeSessionService session)
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
    }

    public AutoTradeSessionService Session => _session;

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
        var audit = new InMemoryAuditLog();
        var dryLedger = new InMemoryDryRunLedger();
        var dryRun = new DryRunOrderRouter(dryLedger);
        var paperLedger = new InMemoryPaperLedger();
        var paper = new PaperOrderRouter(paperLedger);
        return new AppHarness(
            settings,
            ReadOnlyPortfolioService.CreateMock(),
            new OrderCandidatePipeline(),
            new LiveOrderGate(),
            dryLedger,
            dryRun,
            paperLedger,
            paper,
            audit,
            new AutoTradeSessionService());
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

    public (IReadOnlyList<CandlePoint> Candles, IReadOnlyList<TradeMarker> Markers) GetChartData()
    {
        var symbol = _session.ResolveWatchSymbols().FirstOrDefault() ?? "AAPL";
        var candles = MockCandleSeriesFactory.CreateSeries(symbol, 120, DateTimeOffset.UtcNow);
        var paperMarkers = MockCandleSeriesFactory.MarkersFromPaperFills(_paperLedger.GetSnapshot());
        if (paperMarkers.Count > 0)
        {
            return (candles, paperMarkers);
        }

        return (candles, MockCandleSeriesFactory.CreateDemoMarkers(candles));
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
        var snapshot = CockpitReadOnlyProjector.Project(portfolio, _settings);

        var now = DateTimeOffset.UtcNow;
        var usSession = UsMarketSessionGuard.Evaluate(portfolio.UsMarket, now);
        var qty = _session.Strategy == TradingStrategyKind.관망만 ? 0m : 1m;
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
                usMarket: portfolio.UsMarket);

            foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
            {
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
        (_dryRunLedger.Count, _paperLedger.Count, true);
}
