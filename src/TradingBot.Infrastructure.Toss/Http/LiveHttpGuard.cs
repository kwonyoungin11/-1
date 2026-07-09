namespace TradingBot.Infrastructure.Toss.Http;

/// <summary>Blocks outbound Toss HTTP unless AllowLiveHttp is explicitly true.</summary>
public static class LiveHttpGuard
{
    public static void EnsureAllowed(TossOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.AllowLiveHttp)
        {
            throw new InvalidOperationException(
                "TOSS_ALLOW_LIVE_HTTP is false. Outbound Toss HTTP is blocked. Use mock clients.");
        }
    }
}

/// <summary>
/// Placeholder live read client. Refuses all calls unless AllowLiveHttp is true,
/// and even then Phase 2 does not implement order endpoints.
/// Full HttpClient wiring is intentionally deferred until owner enables HTTP.
/// </summary>
public sealed class GatedLiveTossAuthClient : ITossAuthClient
{
    private readonly TossOptions _options;

    public GatedLiveTossAuthClient(TossOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<TossAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        LiveHttpGuard.EnsureAllowed(_options);
        throw new NotImplementedException(
            "Live HTTP auth is gated and not fully implemented in this phase. Prefer mock.");
    }
}
