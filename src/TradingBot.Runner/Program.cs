using TradingBot.Runner;

// Thin entry: default harness, or offline backtest host.
// Live order path stays blocked in both modes.

if (args.Length > 0
    && args[0].Equals("backtest", StringComparison.OrdinalIgnoreCase))
{
    var backtestArgs = args.Skip(1).ToArray();
    return await BacktestCommand.RunAsync(backtestArgs, CancellationToken.None);
}

var harness = TradingBotHarness.CreateDefault();
var result = await harness.RunOnceAsync(cancellationToken: CancellationToken.None);

Console.WriteLine("TradingBot harness runner (composition root)");
Console.WriteLine($"Safety: {result.SafetyHeadline}");
Console.WriteLine($"Connection: {result.ConnectionSummary}");
Console.WriteLine($"Candidates: {result.CandidateCount} (dry-run ledger: {result.DryRunLedgerCount})");
Console.WriteLine($"Risk rows: {result.RiskGateRowCount}; UI candidates: {result.UiCandidateCount}");
Console.WriteLine($"Audit entries: {result.AuditEntryCount}");
Console.WriteLine($"Live submission blocked: {result.LiveSubmissionBlocked}");
Console.WriteLine("No Toss order API was called.");

return result.ExitCode;
