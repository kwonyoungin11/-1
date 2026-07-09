using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>
/// Narrow transport for gated live submission of a single <see cref="OrderCandidate"/>.
/// Not a free-for-all HTTP client — call only after <see cref="TradingBot.Risk.LiveOrderGate"/> allows.
/// </summary>
public interface ILiveOrderTransport
{
    /// <summary>
    /// Submits one candidate. Implementations must not be invoked when the live gate blocks.
    /// Test/recording stubs must not perform real network I/O.
    /// </summary>
    Task<LiveTransportResult> SubmitCandidateAsync(OrderCandidate candidate, CancellationToken cancellationToken);
}

/// <summary>Outcome of a live transport submit (no secrets; no raw HTTP).</summary>
public sealed record LiveTransportResult(
    bool Success,
    string Message,
    string? BrokerReference = null);
