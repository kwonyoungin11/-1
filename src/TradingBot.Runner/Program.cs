using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
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

var liveDecision = new LiveOrderGate().Evaluate(settings, new LiveOrderContext());
var portfolio = await ReadOnlyPortfolioService.CreateMock()
    .GetSnapshotAsync(new[] { "AAPL" }, CancellationToken.None);
var cockpit = CockpitReadOnlyProjector.Project(portfolio, settings);

var now = DateTimeOffset.UtcNow;
var pipeline = new OrderCandidatePipeline();
var evaluated = pipeline.BuildCandidates(
    portfolio.Quotes,
    settings,
    defaultOrderQuantity: 1m,
    nowUtc: now,
    marketSessionOpen: portfolio.UsMarket is { IsHolidayOrClosed: false });

var dryRun = new DryRunOrderRouter();
foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
{
    _ = await dryRun.RouteAsync(item.Candidate, CancellationToken.None);
}

Console.WriteLine("TradingBot harness runner");
Console.WriteLine($"Safety: {cockpit.SafetyHeadline}");
Console.WriteLine($"Connection: {cockpit.ConnectionSummary}");
Console.WriteLine($"Candidates: {evaluated.Count} (accepted dry-run: {evaluated.Count(e => e.IsAcceptedForDryRun)})");
foreach (var e in evaluated)
{
    Console.WriteLine($"  - {e.Candidate.Symbol} {e.Candidate.Side} x{e.Candidate.Quantity} :: {e.OwnerStatusMessage}");
}

Console.WriteLine($"Live submission blocked: {liveDecision.IsBlocked}");
Console.WriteLine("No Toss order API was called. Signals are not investment advice.");

return liveDecision.IsBlocked ? 0 : 1;
