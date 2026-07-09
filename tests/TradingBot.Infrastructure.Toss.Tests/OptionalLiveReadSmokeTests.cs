using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;

namespace TradingBot.Infrastructure.Toss.Tests;

/// <summary>
/// Optional real HTTP read-only smoke. Skipped unless both:
///   TOSS_ALLOW_LIVE_HTTP=true AND client credentials are present.
/// Default CI path is no-op (pass). Never prints secret values.
/// Does not call order endpoints.
/// </summary>
public class OptionalLiveReadSmokeTests
{
    /// <summary>
    /// Gate for optional live read smoke. Does not log ClientId/ClientSecret.
    /// </summary>
    public static bool IsLiveReadSmokeEnabled(TossOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AllowLiveHttp && options.HasClientCredentials;
    }

    [Fact]
    public void Gate_disabled_when_allow_live_http_false()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = false,
            ClientId = "synthetic-id",
            ClientSecret = "synthetic-secret",
        };

        Assert.False(IsLiveReadSmokeEnabled(options));
        Assert.Contains("mock", TossReadOnlyFactory.DescribeMode(options), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gate_disabled_when_credentials_missing()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = true,
            ClientId = null,
            ClientSecret = null,
        };

        Assert.False(IsLiveReadSmokeEnabled(options));
    }

    [Fact]
    public void Gate_enabled_only_with_flag_and_credentials()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = true,
            ClientId = "synthetic-id",
            ClientSecret = "synthetic-secret",
        };

        Assert.True(IsLiveReadSmokeEnabled(options));
    }

    [Fact]
    public async Task Optional_live_http_read_smoke_skipped_unless_flag_and_credentials()
    {
        // Load env (process + optional .env). Never dump values to test output.
        var options = TossReadOnlyFactory.LoadOptionsFromEnvironment();

        if (!IsLiveReadSmokeEnabled(options))
        {
            // Default / CI: intentional skip (not a failure). TOSS_ALLOW_LIVE_HTTP remains false by default.
            Assert.True(
                !options.AllowLiveHttp || !options.HasClientCredentials,
                "Smoke gate off requires AllowLiveHttp=false or missing credentials.");
            return;
        }

        // Owner-enabled path: live HTTP for read + gated order transport when safety flags allow.
        var mode = TossReadOnlyFactory.DescribeMode(options);
        Assert.Contains("실 HTTP", mode, StringComparison.Ordinal);
        Assert.Contains("주문", mode, StringComparison.Ordinal);

        var svc = TossReadOnlyFactory.CreatePortfolioService(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var snap = await svc.GetSnapshotAsync([], cts.Token);

        // Live path must not silently fall back to mock when gate was enabled.
        Assert.NotEqual(ConnectionStatus.MockConnected, snap.ConnectionStatus);
        Assert.True(
            snap.ConnectionStatus is ConnectionStatus.LiveReadOnlyConnected or ConnectionStatus.Error,
            "Expected live-read or error status; never log credentials on failure.");
    }
}
