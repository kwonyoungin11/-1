using TradingBot.Domain;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

// Harness runner: prints safety status only. Never submits orders.
var settings = TradingSafetySettings.CreateSafeDefaults();
var gate = new LiveOrderGate();
var decision = gate.Evaluate(settings, new LiveOrderContext());
var cockpit = CockpitState.CreateSafeDefault();
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
Console.WriteLine($"Cockpit: {cockpit.SafetySummary}");
Console.WriteLine($"Live allowed by settings: {settings.AllowLiveOrders}");
Console.WriteLine($"Kill switch: {settings.KillSwitch}");
Console.WriteLine($"Order mode: {settings.OrderMode}");
Console.WriteLine($"Live gate blocked: {decision.IsBlocked}");
Console.WriteLine($"Dry-run accepted (no live call): {dryResult.Accepted}");
Console.WriteLine("No Toss order API was called.");

return decision.IsBlocked ? 0 : 1; // exit 0 only if still safely blocked
