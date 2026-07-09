using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

var settings = new TradingSafetySettings
{
    AllowLiveOrders = false,
    KillSwitch = true,
    OrderMode = OrderMode.DryRun,
    MaxOrderNotional = 50_000m,
    MarketDataMaxStalenessSeconds = 120,
};

var audit = new InMemoryAuditLog();
audit.Append(new AuditEntry(DateTimeOffset.UtcNow, "system", "Harness start — live blocked", "boot"));

var liveDecision = new LiveOrderGate().Evaluate(settings, new LiveOrderContext());
var portfolio = await ReadOnlyPortfolioService.CreateMock()
    .GetSnapshotAsync(new[] { "AAPL" }, CancellationToken.None);
var snapshot = CockpitReadOnlyProjector.Project(portfolio, settings);

var now = DateTimeOffset.UtcNow;
var pipeline = new OrderCandidatePipeline();
var evaluated = pipeline.BuildCandidates(
    portfolio.Quotes,
    settings,
    defaultOrderQuantity: 1m,
    nowUtc: now,
    marketSessionOpen: portfolio.UsMarket is { IsHolidayOrClosed: false });

var ledger = new InMemoryDryRunLedger();
var dryRun = new DryRunOrderRouter(ledger);
foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
{
    _ = await dryRun.RouteAsync(item.Candidate, CancellationToken.None);
    audit.Append(new AuditEntry(DateTimeOffset.UtcNow, "dry_run", $"Accepted candidate {item.Candidate.Symbol}", item.Candidate.ClientOrderId));
}

var dashboard = CockpitDashboardMapper.Compose(snapshot, settings, candidates: evaluated);

Console.WriteLine("TradingBot harness runner (parallel wave merge)");
Console.WriteLine($"Safety: {dashboard.Snapshot.SafetyHeadline}");
Console.WriteLine($"Connection: {dashboard.Snapshot.ConnectionSummary}");
Console.WriteLine($"Candidates: {evaluated.Count} (dry-run ledger: {ledger.Count})");
Console.WriteLine($"Risk rows: {dashboard.RiskGates.Count}; UI candidates: {dashboard.OrderCandidates.Count}");
Console.WriteLine($"Audit entries: {audit.Count}");
Console.WriteLine($"Live submission blocked: {liveDecision.IsBlocked}");
Console.WriteLine("No Toss order API was called.");

return liveDecision.IsBlocked && !dashboard.Snapshot.IsLiveTradingVisuallyOpen ? 0 : 1;
