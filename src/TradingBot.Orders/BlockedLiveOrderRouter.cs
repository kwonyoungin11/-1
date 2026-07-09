using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Orders;

/// <summary>
/// Live order router stub. Always evaluates the live gate and blocks unless every condition passes.
/// Does not call Toss order create/modify/cancel endpoints.
/// </summary>
public sealed class BlockedLiveOrderRouter : IOrderRouter
{
    private readonly LiveOrderGate _gate = new();
    private readonly TradingSafetySettings _settings;
    private readonly LiveOrderContext _context;

    public BlockedLiveOrderRouter(TradingSafetySettings settings, LiveOrderContext? context = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _context = context ?? new LiveOrderContext();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always false. Even when every live gate flag is open, this router still refuses submission
    /// (<c>live_not_implemented</c>) and never calls Toss order create/modify/cancel HTTP.
    /// </remarks>
    public bool IsLiveSubmissionEnabled => false;

    public Task<OrderRouteResult> RouteAsync(OrderCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var decision = _gate.Evaluate(_settings, _context);
        if (decision.IsBlocked)
        {
            return Task.FromResult(new OrderRouteResult(
                Accepted: false,
                Mode: "live_blocked",
                Message: "Live order blocked by fail-closed gate. No API call was made.",
                Blocks: decision.Blocks));
        }

        // Even if all flags open, harness phase must not submit until explicit live implementation is built and approved.
        return Task.FromResult(new OrderRouteResult(
            Accepted: false,
            Mode: "live_not_implemented",
            Message: "Live path is not implemented. No API call was made.",
            Blocks: new[] { BlockedReason.LiveImplementationDisabled }));
    }
}
