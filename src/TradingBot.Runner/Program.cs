using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

// Harness runner: safety + mock read-only snapshot. Never submits orders.
var settings = TradingSafetySettings.CreateSafeDefaults();
var gate = new LiveOrderGate();
var decision = gate.Evaluate(settings, new LiveOrderContext());

var portfolio = await ReadOnlyPortfolioService.CreateMock()
    .GetSnapshotAsync(new[] { "AAPL" }, CancellationToken.None);
var cockpit = CockpitReadOnlyProjector.Project(portfolio, settings);

var dryRun = new DryRunOrderRouter();
var candidate = new OrderCandidate(
    Symbol: "AAPL",
    Side: "BUY",
    OrderType: "LIMIT",
    Quantity: 0,
    LimitPrice: null,
    ClientOrderId: "harness-noop",
    CreatedAtUtc: DateTimeOffset.UtcNow);
var dryResult = await dryRun.RouteAsync(candidate, CancellationToken.None);

Console.WriteLine("TradingBot harness runner");
Console.WriteLine($"Safety: {cockpit.SafetyHeadline}");
Console.WriteLine($"Bot: {cockpit.BotStateOwnerMessage}");
Console.WriteLine($"Connection: {cockpit.ConnectionSummary}");
Console.WriteLine($"Account: {cockpit.AccountMaskedSummary}");
Console.WriteLine($"Market: {cockpit.MarketSessionSummary}");
Console.WriteLine($"Live gate blocked: {decision.IsBlocked}");
Console.WriteLine($"Dry-run accepted (no live call): {dryResult.Accepted}");
Console.WriteLine("No Toss order API was called.");

return decision.IsBlocked && !cockpit.IsLiveTradingVisuallyOpen ? 0 : 1;
