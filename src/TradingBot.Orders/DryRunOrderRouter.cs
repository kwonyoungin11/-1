using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Default router: accepts candidates for simulation only. Never calls Toss order APIs.</summary>
public sealed class DryRunOrderRouter : IOrderRouter
{
    private readonly IDryRunLedger? _ledger;

    public DryRunOrderRouter(IDryRunLedger? ledger = null)
    {
        _ledger = ledger;
    }

    /// <inheritdoc />
    /// <remarks>Always false — dry-run never issues Toss order HTTP.</remarks>
    public bool IsLiveSubmissionEnabled => false;

    public Task<OrderRouteResult> RouteAsync(OrderCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var result = new OrderRouteResult(
            Accepted: true,
            Mode: OrderMode.DryRun.ToString(),
            Message: "Dry-run accepted. No live order was submitted.",
            Blocks: Array.Empty<BlockedReason>());

        if (_ledger is not null)
        {
            _ledger.Append(new DryRunLedgerEntry(
                EntryId: Guid.CreateVersion7(),
                RecordedAtUtc: DateTimeOffset.UtcNow,
                Candidate: candidate,
                Accepted: result.Accepted,
                Mode: result.Mode,
                Message: result.Message));
        }

        return Task.FromResult(result);
    }
}
