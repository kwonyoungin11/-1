using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

namespace TradingBot.Web.Services;

/// <summary>
/// Web composition root for mock read-only cockpit.
/// Does not reference TradingBot.Runner. Never calls Toss order HTTP.
/// Live submission stays blocked via safe defaults + LiveOrderGate.
/// </summary>
public sealed class WebHarness
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

    public WebHarness(
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
    public IDryRunLedger DryRunLedger => _dryRunLedger;
    public IPaperLedger PaperLedger => _paperLedger;

    /// <summary>
    /// Builds owner-facing cockpit dashboard from mock portfolio + dry-run path only.
    /// </summary>
    public async Task<CockpitDashboardModel> GetDashboardAsync(
        IReadOnlyList<string>? watchSymbols = null,
        decimal defaultOrderQuantity = 1m,
        CancellationToken cancellationToken = default)
    {
        var symbols = watchSymbols is { Count: > 0 } ? watchSymbols : DefaultWatchSymbols;

        _audit.Append(new AuditEntry(
            DateTimeOffset.UtcNow,
            "system",
            "Web cockpit refresh — live blocked; mock read-only",
            "web_boot"));

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

        if (liveDecision.IsBlocked)
        {
            _audit.Append(new AuditEntry(
                DateTimeOffset.UtcNow,
                "live_gate",
                "Live submission blocked (fail-closed)",
                "live_gate"));
        }

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

        return CockpitDashboardMapper.Compose(
            snapshot,
            _settings,
            candidates: evaluated,
            extraRiskDecision: liveDecision);
    }

    /// <summary>Safe empty dashboard without portfolio I/O (fallback).</summary>
    public CockpitDashboardModel GetSafeDefaultDashboard() =>
        CockpitDashboardModel.CreateSafeDefault();

    /// <summary>
    /// Evidence summary for cockpit pages: ledger counts, configured modes, live always blocked.
    /// Uses <see cref="IDryRunLedger"/>, <see cref="IPaperLedger"/>, and <see cref="LiveOrderGate"/>;
    /// does not touch Toss order HTTP or secrets.
    /// </summary>
    public Task<WebEvidenceSummary> GetEvidenceSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Exercise LiveOrderGate with fail-closed context; web host always reports blocked.
        _ = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());

        var summary = new WebEvidenceSummary
        {
            DryRunCount = _dryRunLedger.Count,
            PaperFillCount = _paperLedger.Count,
            LastModes = BuildLastModes(_settings.OrderMode),
            // Absolute host policy: live never enabled in TradingBot.Web.
            LiveBlocked = true,
        };

        return Task.FromResult(summary);
    }

    private static IReadOnlyList<string> BuildLastModes(OrderMode configured)
    {
        // Web host only produces dry-run + paper virtual evidence; live never appears as executable.
        var modes = new List<string>(capacity: 3)
        {
            FormatOrderMode(configured),
        };

        if (configured != OrderMode.DryRun)
        {
            modes.Add(FormatOrderMode(OrderMode.DryRun));
        }

        if (configured != OrderMode.Paper)
        {
            modes.Add(FormatOrderMode(OrderMode.Paper));
        }

        // Never advertise live as a last/executable mode on this host.
        return modes
            .Where(m => !string.Equals(m, "live", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FormatOrderMode(OrderMode mode) => mode switch
    {
        OrderMode.DryRun => "dry_run",
        OrderMode.Paper => "paper",
        OrderMode.Live => "live",
        _ => mode.ToString().ToLowerInvariant(),
    };
}
