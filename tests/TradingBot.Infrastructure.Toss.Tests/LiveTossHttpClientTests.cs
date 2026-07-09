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
        Assert.Equal(3500.5m, snap.CashBuyingPower);
        Assert.Equal("USD", snap.CashCurrency);
        Assert.Equal(100m, snap.MarketValueUsdDecimal);
        Assert.DoesNotContain(handler.Paths, p => p.Contains("orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(handler.Paths, p => p.Contains("oauth2/token", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("api/v1/accounts", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("api/v1/prices", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("api/v1/buying-power", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("currency=USD", StringComparison.Ordinal));
        Assert.True(handler.AccountHeaderSeenOnBuyingPower);
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

    [Fact]
    public async Task GetCandlesAsync_calls_api_v1_candles_with_1m_interval_and_maps_ohlcv()
    {
        var handler = new StubHandler();
        var options = new TossOptions
        {
            BaseUrl = "https://openapi.tossinvest.com/",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            AllowLiveHttp = true,
        };
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl),
        };
        var auth = new LiveTossAuthClient(http, options);
        var market = new LiveTossMarketDataClient(http, options, auth);

        var candles = await market.GetCandlesAsync("AAPL", "1m", 160, CancellationToken.None);

        Assert.Equal(2, candles.Count);
        Assert.True(candles[0].Time < candles[1].Time);
        Assert.Equal(185.70, candles[0].Close, precision: 4);
        Assert.Equal(186.00, candles[1].Close, precision: 4);

        var candlePath = Assert.Single(handler.Paths, p => p.Contains("api/v1/candles", StringComparison.Ordinal));
        Assert.Contains("symbol=AAPL", candlePath, StringComparison.Ordinal);
        Assert.Contains("interval=1m", candlePath, StringComparison.Ordinal);
        Assert.Contains("count=160", candlePath, StringComparison.Ordinal);
        Assert.DoesNotContain(handler.Paths, p => p.Contains("orders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCandlesAsync_rejects_invalid_interval()
    {
        var http = new HttpClient(new StubHandler())
        {
            BaseAddress = new Uri("https://openapi.tossinvest.com/"),
        };
        var options = new TossOptions
        {
            ClientId = "test-id",
            ClientSecret = "test-secret",
            AllowLiveHttp = true,
        };
        var market = new LiveTossMarketDataClient(http, options, new LiveTossAuthClient(http, options));

        await Assert.ThrowsAsync<ArgumentException>(
            () => market.GetCandlesAsync("AAPL", "5m", 100, CancellationToken.None));
    }

    [Fact]
    public async Task Mock_GetCandlesAsync_returns_synthetic_series()
    {
        var mock = new Mock.MockTossMarketDataClient();
        var candles = await mock.GetCandlesAsync("AAPL", "1m", 40, CancellationToken.None);
        Assert.Equal(40, candles.Count);
        Assert.True(candles[0].High >= candles[0].Low);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        public bool AccountHeaderSeenOnBuyingPower { get; private set; }

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
else if (path.Contains("api/v1/buying-power", StringComparison.Ordinal))
            {
                AccountHeaderSeenOnBuyingPower =
                    request.Headers.TryGetValues("X-Tossinvest-Account", out var vals)
                    && vals.Any(v => !string.IsNullOrWhiteSpace(v));
                json = """{"result":{"currency":"USD","cashBuyingPower":"3500.5"}}""";
            }
            else if (path.Contains("api/v1/candles", StringComparison.Ordinal))
            {
                json = """{"result":{"candles":[{"timestamp":"2026-03-25T09:32:00+09:00","openPrice":"185.70","highPrice":"186.10","lowPrice":"185.50","closePrice":"186.00","volume":"15200","currency":"USD"},{"timestamp":"2026-03-25T09:31:00+09:00","openPrice":"185.50","highPrice":"185.80","lowPrice":"185.40","closePrice":"185.70","volume":"18400","currency":"USD"}],"nextBefore":"2026-03-25T09:31:00+09:00"}}""";

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
