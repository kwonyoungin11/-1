using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;

namespace TradingBot.Infrastructure.Toss.Tests;

/// <summary>
/// Factory mode labels and assembly rules. Never asserts secret values.
/// </summary>
public class TossReadOnlyFactoryTests
{
    [Fact]
    public void DescribeMode_when_live_http_false_is_mock_flag_message()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = false,
            ClientId = "synthetic-id",
            ClientSecret = "synthetic-secret",
        };

        var mode = TossReadOnlyFactory.DescribeMode(options);

        Assert.Contains("mock", mode, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TOSS_ALLOW_LIVE_HTTP=false", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-id", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-secret", mode, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeMode_when_live_true_but_missing_credentials_is_mock_no_key()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = true,
            ClientId = null,
            ClientSecret = null,
        };

        var mode = TossReadOnlyFactory.DescribeMode(options);

        Assert.Contains("mock", mode, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("키 없음", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("실 HTTP 읽기 전용 모드", mode, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeMode_when_live_true_and_credentials_present_describes_live_http()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = true,
            ClientId = "synthetic-id",
            ClientSecret = "synthetic-secret",
        };

        var mode = TossReadOnlyFactory.DescribeMode(options);

        Assert.Contains("실 HTTP", mode, StringComparison.Ordinal);
        Assert.Contains("주문", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-id", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-secret", mode, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeMode_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => TossReadOnlyFactory.DescribeMode(null!));
    }

    [Fact]
    public async Task CreatePortfolioService_mock_when_live_false_even_with_credentials()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = false,
            ClientId = "synthetic-id",
            ClientSecret = "synthetic-secret",
        };

        var svc = TossReadOnlyFactory.CreatePortfolioService(options);
        var snap = await svc.GetSnapshotAsync(["AAPL"], CancellationToken.None);

        Assert.Equal(ConnectionStatus.MockConnected, snap.ConnectionStatus);
        Assert.Equal("mock 모드 (TOSS_ALLOW_LIVE_HTTP=false)", TossReadOnlyFactory.DescribeMode(options));
    }

    [Fact]
    public async Task CreatePortfolioService_mock_when_live_true_but_no_credentials()
    {
        var options = new TossOptions
        {
            AllowLiveHttp = true,
            ClientId = "only-id",
            ClientSecret = null,
        };

        var svc = TossReadOnlyFactory.CreatePortfolioService(options);
        var snap = await svc.GetSnapshotAsync(["AAPL"], CancellationToken.None);

        Assert.Equal(ConnectionStatus.MockConnected, snap.ConnectionStatus);
        Assert.Contains("키 없음", TossReadOnlyFactory.DescribeMode(options), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    public void TossOptions_AllowLiveHttp_parses_bool_tokens(string? raw, bool expected)
    {
        var env = new Dictionary<string, string?>
        {
            ["TOSS_ALLOW_LIVE_HTTP"] = raw,
        };
        var opt = TossOptions.FromEnvironment(env);
        Assert.Equal(expected, opt.AllowLiveHttp);
    }

    [Fact]
    public void TossOptions_default_disallows_live_http_when_key_missing()
    {
        var opt = TossOptions.FromEnvironment(new Dictionary<string, string?>());
        Assert.False(opt.AllowLiveHttp);
        Assert.False(opt.HasClientCredentials);
    }
}
