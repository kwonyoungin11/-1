using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>
/// Paper trading router: accepts candidates as virtual fills only.
/// Uses limit price or optional reference-price resolver. Never calls Toss order APIs.
/// Live mode is never enabled.
/// </summary>
public sealed class PaperOrderRouter : IOrderRouter
{
    private readonly IPaperLedger _ledger;
    private readonly Func<OrderCandidate, decimal?>? _referencePriceResolver;

    public PaperOrderRouter(
        IPaperLedger ledger,
        Func<OrderCandidate, decimal?>? referencePriceResolver = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _referencePriceResolver = referencePriceResolver;
    }

    public Task<OrderRouteResult> RouteAsync(OrderCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var fillPrice = ResolveFillPrice(candidate);
        if (fillPrice is null || fillPrice <= 0m)
        {
            return Task.FromResult(new OrderRouteResult(
                Accepted: false,
                Mode: OrderMode.Paper.ToString(),
                Message: "Paper fill rejected: no limit or reference price available. No live order was submitted.",
                Blocks: Array.Empty<BlockedReason>()));
        }

        var note = "Paper virtual fill. No live order was submitted.";
        _ledger.Append(new PaperFillRecord(
            FillId: Guid.CreateVersion7(),
            Symbol: candidate.Symbol,
            Side: candidate.Side,
            Quantity: candidate.Quantity,
            Price: fillPrice.Value,
            FilledAtUtc: DateTimeOffset.UtcNow,
            ClientOrderId: candidate.ClientOrderId,
            Note: note));

        return Task.FromResult(new OrderRouteResult(
            Accepted: true,
            Mode: OrderMode.Paper.ToString(),
            Message: note,
            Blocks: Array.Empty<BlockedReason>()));
    }

    private decimal? ResolveFillPrice(OrderCandidate candidate)
    {
        if (candidate.LimitPrice is > 0m)
        {
            return candidate.LimitPrice;
        }

        var reference = _referencePriceResolver?.Invoke(candidate);
        if (reference is > 0m)
        {
            return reference;
        }

        return null;
    }
}
