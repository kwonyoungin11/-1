using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Orders;

/// <summary>
/// Live-capable order router that remains fail-closed: evaluates real <see cref="LiveOrderGate"/>,
/// registers <c>ClientOrderId</c> for idempotency, then calls <see cref="ILiveOrderTransport"/> only when allowed.
/// Under <see cref="TradingSafetySettings.CreateSafeDefaults"/>, <see cref="IsLiveSubmissionEnabled"/> is false
/// and every route is blocked with no transport call.
/// </summary>
public sealed class GatedLiveOrderRouter : IOrderRouter
{
    private readonly TradingSafetySettings _settings;
    private readonly Func<LiveOrderContext> _contextFactory;
    private readonly LiveOrderGate _gate;
    private readonly ILiveOrderTransport _transport;
    private readonly ClientOrderIdIndex _clientOrderIdIndex;

    public GatedLiveOrderRouter(
        TradingSafetySettings settings,
        LiveOrderContext context,
        ILiveOrderTransport transport,
        LiveOrderGate? gate = null,
        ClientOrderIdIndex? clientOrderIdIndex = null)
        : this(
            settings,
            contextFactory: () => context ?? throw new ArgumentNullException(nameof(context)),
            transport,
            gate,
            clientOrderIdIndex)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public GatedLiveOrderRouter(
        TradingSafetySettings settings,
        Func<LiveOrderContext> contextFactory,
        ILiveOrderTransport transport,
        LiveOrderGate? gate = null,
        ClientOrderIdIndex? clientOrderIdIndex = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        // Always use a real LiveOrderGate when not injected (tests should inject or rely on this).
        _gate = gate ?? new LiveOrderGate();
        _clientOrderIdIndex = clientOrderIdIndex ?? new ClientOrderIdIndex();
    }

    /// <inheritdoc />
    /// <remarks>
    /// True only when settings allow live, kill switch is off, and mode is Live.
    /// Safe defaults always yield false. Data freshness and API health are still enforced on
    /// <see cref="RouteAsync"/>.
    /// </remarks>
    public bool IsLiveSubmissionEnabled
    {
        get
        {
            var context = _contextFactory()
                ?? throw new InvalidOperationException("LiveOrderContext factory returned null.");

            return _settings.AllowLiveOrders
                && !_settings.KillSwitch
                && _settings.OrderMode == OrderMode.Live;
        }
    }

    /// <inheritdoc />
    public async Task<OrderRouteResult> RouteAsync(
        OrderCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var context = _contextFactory()
            ?? throw new InvalidOperationException("LiveOrderContext factory returned null.");

        // Real fail-closed gate — any missing approval / unsafe default blocks; no transport.
        var decision = _gate.Evaluate(_settings, context);
        if (decision.IsBlocked)
        {
            return new OrderRouteResult(
                Accepted: false,
                Mode: "live_blocked",
                Message: "Live order blocked by fail-closed gate. No transport call was made.",
                Blocks: decision.Blocks);
        }

        // Case-sensitive (ordinal) idempotency before any broker-facing transport.
        if (!_clientOrderIdIndex.TryRegister(candidate.ClientOrderId))
        {
            return new OrderRouteResult(
                Accepted: false,
                Mode: OrderMode.Live.ToString(),
                Message: "Duplicate client order id. Live submit rejected; no second transport call.",
                Blocks: Array.Empty<BlockedReason>());
        }

        var transportResult = await _transport
            .SubmitCandidateAsync(candidate, cancellationToken)
            .ConfigureAwait(false);

        return new OrderRouteResult(
            Accepted: transportResult.Success,
            Mode: OrderMode.Live.ToString(),
            Message: transportResult.Message,
            Blocks: Array.Empty<BlockedReason>());
    }
}
