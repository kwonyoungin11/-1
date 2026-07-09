using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

namespace TradingBot.App.Services;

/// <summary>
/// Desktop app composition root (Avalonia). Mock read-only + dry-run/paper.
/// Never calls Toss order HTTP. Live submission blocked by defaults.
/// </summary>
public sealed class AppHarness
{
    private static readonly string[] DefaultWatchSymbols = ["AAPL"];

    private readonly TradingSafetySettings _settings;
    private readonly IReadOnlyPortfolioService _portfolio;
    private readonly OrderCandidatePipeline _pipeline;
    private readonly LiveOrderGate _liveOrderGate;
    private readonly IDryRunLedger _dryRunLedger;
    private readonly DryRunOrderRouter _dryRun;
    private readonly IPaperLedger _paperLedger;
    private readonly PaperOrderRouter _paper;
    private readonly IAuditLog _audit;

    public AppHarness(
        TradingSafetySettings settings,
        IReadOnlyPortfolioService portfolio,
        OrderCandidatePipeline pipeline,
        LiveOrderGate liveOrderGate,
        IDryRunLedger dryRunLedger,
        DryRunOrderRouter dryRun,
        IPaperLedger paperLedger,
        PaperOrderRouter paper,
        IAuditLog audit)
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
    }

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
            audit);
    }

    public async Task<CockpitDashboardModel> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        _audit.Append(new AuditEntry(
            DateTimeOffset.UtcNow,
            "system",
            "Desktop cockpit refresh — live blocked; mock read-only",
            "app_boot"));

        var liveDecision = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());
        var portfolio = await _portfolio
            .GetSnapshotAsync(DefaultWatchSymbols, cancellationToken)
            .ConfigureAwait(false);
        var snapshot = CockpitReadOnlyProjector.Project(portfolio, _settings);

        var now = DateTimeOffset.UtcNow;
        var session = UsMarketSessionGuard.Evaluate(portfolio.UsMarket, now);

        var evaluated = _pipeline.BuildCandidates(
            portfolio.Quotes,
            _settings,
            defaultOrderQuantity: 1m,
            nowUtc: now,
            marketSessionOpen: session.IsOpenForOrders,
            marketSessionKnown: session.IsKnown,
            usMarket: portfolio.UsMarket);

        foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
        {
            _ = await _dryRun.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
            _ = await _paper.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
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
