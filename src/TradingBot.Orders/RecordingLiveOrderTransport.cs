using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>
/// Test / harness transport: records submit calls without any network I/O.
/// Default result is success so gated-router tests can assert call counts independently of brokers.
/// </summary>
public sealed class RecordingLiveOrderTransport : ILiveOrderTransport
{
    private readonly object _sync = new();
    private readonly List<OrderCandidate> _calls = new();

    /// <summary>Optional factory to control success/failure per candidate. Null → success.</summary>
    public Func<OrderCandidate, LiveTransportResult>? ResultFactory { get; set; }

    /// <summary>Number of times <see cref="SubmitCandidateAsync"/> was invoked.</summary>
    public int CallCount
    {
        get
        {
            lock (_sync)
            {
                return _calls.Count;
            }
        }
    }

    /// <summary>Snapshot of candidates passed to the transport (order of calls).</summary>
    public IReadOnlyList<OrderCandidate> Calls
    {
        get
        {
            lock (_sync)
            {
                return _calls.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public Task<LiveTransportResult> SubmitCandidateAsync(
        OrderCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _calls.Add(candidate);
        }

        var result = ResultFactory?.Invoke(candidate)
            ?? new LiveTransportResult(
                Success: true,
                Message: "Recording transport accepted (no network). Not a real broker submit.",
                BrokerReference: null);

        return Task.FromResult(result);
    }
}
