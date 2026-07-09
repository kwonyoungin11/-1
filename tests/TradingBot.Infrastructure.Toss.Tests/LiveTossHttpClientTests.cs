using System.Net;
using System.Text;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Infrastructure.Toss.Http;

namespace TradingBot.Infrastructure.Toss.Tests;

public class LiveTossHttpClientTests
{
    [Fact]
    public async Task Live_read_path_uses_oauth_accounts_prices_without_orders()
    {
        var handler = new StubHandler();
        var options = new TossOptions
        {
            BaseUrl = "https://openapi.tossinvest.com/",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            AccountSeq = "1",
            AllowLiveHttp = true,
        };

        var svc = TossReadOnlyFactory.CreatePortfolioService(options, handler);
        var snap = await svc.GetSnapshotAsync(new[] { "AAPL" }, CancellationToken.None);

        Assert.Equal(ConnectionStatus.LiveReadOnlyConnected, snap.ConnectionStatus);
        Assert.NotEmpty(snap.Accounts);
        Assert.NotEmpty(snap.Quotes);
        Assert.DoesNotContain(handler.Paths, p => p.Contains("orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(handler.Paths, p => p.Contains("oauth2/token", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("api/v1/accounts", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("api/v1/prices", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Factory_falls_back_to_mock_when_live_http_disallowed()
    {
        var options = new TossOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            AllowLiveHttp = false,
        };
        var svc = TossReadOnlyFactory.CreatePortfolioService(options);
        Assert.Contains("mock", TossReadOnlyFactory.DescribeMode(options), StringComparison.OrdinalIgnoreCase);
        var snap = await svc.GetSnapshotAsync(new[] { "AAPL" }, CancellationToken.None);
        Assert.Equal(ConnectionStatus.MockConnected, snap.ConnectionStatus);
    }

    [Fact]
    public async Task Auth_client_blocked_when_flag_false()
    {
        var http = new HttpClient(new StubHandler())
        {
            BaseAddress = new Uri("https://openapi.tossinvest.com/"),
        };
        var client = new LiveTossAuthClient(http, new TossOptions { AllowLiveHttp = false, ClientId = "a", ClientSecret = "b" });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAccessTokenAsync(CancellationToken.None));
        Assert.Contains("TOSS_ALLOW_LIVE_HTTP", ex.Message, StringComparison.Ordinal);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? request.RequestUri?.ToString() ?? "";
            Paths.Add(path);

            string json;
            if (path.Contains("oauth2/token", StringComparison.Ordinal))
            {
                json = """{"access_token":"example-not-a-real-token","token_type":"Bearer","expires_in":3600}""";
            }
            else if (path.Contains("api/v1/accounts", StringComparison.Ordinal))
            {
                json = """{"result":[{"accountNo":"1234567890","accountSeq":1,"accountType":"BROKERAGE"}]}""";
            }
            else if (path.Contains("api/v1/holdings", StringComparison.Ordinal))
            {
                json = """{"result":{"marketValue":{"amount":{"krw":"0","usd":"100"}},"items":[{"symbol":"AAPL","name":"Apple","currency":"USD","quantity":"1","lastPrice":"190"}]}}""";
            }
            else if (path.Contains("api/v1/prices", StringComparison.Ordinal))
            {
                json = """{"result":[{"symbol":"AAPL","timestamp":"2026-07-09T15:00:00Z","lastPrice":"190.00","currency":"USD"}]}""";
            }
            else if (path.Contains("market-calendar", StringComparison.Ordinal))
            {
                json = """{"result":{"today":{"date":"2026-07-09","regularMarket":{}}}}""";
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
