using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

namespace TradingBot.Runner;

/// <summary>
/// Composition root for the fail-closed harness.
/// Mock portfolio, signal pipeline, dry-run + paper ledgers, audit.
/// Never calls Toss order HTTP; live submission stays blocked.
/// </summary>
public sealed class TradingBotHarness
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

    public TradingBotHarness(
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
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _liveOrderGate = liveOrderGate ?? throw new ArgumentNullException(nameof(liveOrderGate));
        _dryRunLedger = dryRunLedger ?? throw new ArgumentNullException(nameof(dryRunLedger));
        _dryRun = dryRun ?? throw new ArgumentNullException(nameof(dryRun));
        _paperLedger = paperLedger ?? throw new ArgumentNullException(nameof(paperLedger));
        _paper = paper ?? throw new ArgumentNullException(nameof(paper));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public TradingSafetySettings Settings => _settings;
    public IAuditLog Audit => _audit;
    public IDryRunLedger Ledger => _dryRunLedger;
    public IPaperLedger PaperLedger => _paperLedger;

    public static TradingBotHarness CreateDefault()
    {
        var settings = CreateHarnessSafetySettings();
        var audit = new InMemoryAuditLog();
        var dryLedger = new InMemoryDryRunLedger();
        var dryRun = new DryRunOrderRouter(dryLedger);
        var paperLedger = new InMemoryPaperLedger();
        var paper = new PaperOrderRouter(paperLedger);
        var portfolio = ReadOnlyPortfolioService.CreateMock();
        var pipeline = new OrderCandidatePipeline();
        var liveGate = new LiveOrderGate();
        return new TradingBotHarness(
            settings, portfolio, pipeline, liveGate, dryLedger, dryRun, paperLedger, paper, audit);
    }

    public static TradingSafetySettings CreateHarnessSafetySettings() =>
        new()
        {
            AllowLiveOrders = false,
            KillSwitch = true,
            OrderMode = OrderMode.DryRun,
            MaxOrderNotional = 50_000m,
            MarketDataMaxStalenessSeconds = 120,
        };

    public async Task<IHarnessRunResult> RunOnceAsync(
        IReadOnlyList<string>? watchSymbols = null,
        decimal defaultOrderQuantity = 1m,
        CancellationToken cancellationToken = default)
    {
        var symbols = watchSymbols is { Count: > 0 } ? watchSymbols : DefaultWatchSymbols;

        _audit.Append(new AuditEntry(
            DateTimeOffset.UtcNow,
            "system",
            "Harness start — live blocked; wave02 paper+session+di",
            "boot"));

        var liveDecision = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());
        var portfolio = await _portfolio
            .GetSnapshotAsync(symbols, cancellationToken)
            .ConfigureAwait(false);
        var snapshot = CockpitReadOnlyProjector.Project(portfolio, _settings);

        var now = DateTimeOffset.UtcNow;
        var session = UsMarketSessionGuard.Evaluate(portfolio.UsMarket, now);
        _audit.Append(new AuditEntry(
            now,
            "session",
            session.OwnerMessage,
            "us_market"));

        var evaluated = _pipeline.BuildCandidates(
            portfolio.Quotes,
            _settings,
            defaultOrderQuantity: defaultOrderQuantity,
            nowUtc: now,
            marketSessionOpen: session.IsOpenForOrders,
            marketSessionKnown: session.IsKnown,
            usMarket: portfolio.UsMarket);

        foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
        {
            _ = await _dryRun.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
            _ = await _paper.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
            _audit.Append(new AuditEntry(
                DateTimeOffset.UtcNow,
                "dry_run_paper",
                $"Accepted candidate {item.Candidate.Symbol} (dry-run+paper virtual)",
                item.Candidate.ClientOrderId));
        }

        var dashboard = CockpitDashboardMapper.Compose(snapshot, _settings, candidates: evaluated);

        return new HarnessRunResult(
            SafetyHeadline: dashboard.Snapshot.SafetyHeadline,
            ConnectionSummary: dashboard.Snapshot.ConnectionSummary,
            CandidateCount: evaluated.Count,
            DryRunLedgerCount: _dryRunLedger.Count,
            RiskGateRowCount: dashboard.RiskGates.Count,
            UiCandidateCount: dashboard.OrderCandidates.Count,
            AuditEntryCount: _audit.Count,
            LiveSubmissionBlocked: liveDecision.IsBlocked,
            IsLiveTradingVisuallyOpen: dashboard.Snapshot.IsLiveTradingVisuallyOpen);
    }
}
