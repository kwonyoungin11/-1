using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>Routes order candidates. Live path must remain gated.</summary>
public interface IOrderRouter
{
    /// <summary>
    /// Whether this router may submit live broker (Toss order HTTP) requests.
    /// Must remain <c>false</c> until live readiness is evidenced and approved.
    /// </summary>
    bool IsLiveSubmissionEnabled { get; }

    Task<OrderRouteResult> RouteAsync(OrderCandidate candidate, CancellationToken cancellationToken);
}

public sealed record OrderRouteResult(bool Accepted, string Mode, string Message, IReadOnlyList<BlockedReason> Blocks);
