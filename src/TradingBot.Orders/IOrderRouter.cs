using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Routes order candidates. Live path must remain gated.</summary>
public interface IOrderRouter
{
    Task<OrderRouteResult> RouteAsync(OrderCandidate candidate, CancellationToken cancellationToken);
}

public sealed record OrderRouteResult(bool Accepted, string Mode, string Message, IReadOnlyList<BlockedReason> Blocks);
