using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Default router: accepts candidates for simulation only. Never calls Toss order APIs.</summary>
public sealed class DryRunOrderRouter : IOrderRouter
{
    public Task<OrderRouteResult> RouteAsync(OrderCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var result = new OrderRouteResult(
            Accepted: true,
            Mode: OrderMode.DryRun.ToString(),
            Message: "Dry-run accepted. No live order was submitted.",
            Blocks: Array.Empty<BlockedReason>());

        return Task.FromResult(result);
    }
}
