using System.Net;
using System.Text;
using TradingBot.Infrastructure.Toss.Http;

namespace TradingBot.Infrastructure.Toss.Tests;

public class HistoricalCandleLoaderTests
{
    [Fact]
    public async Task LoadAsync_pages_beyond_production_clamp_and_merges_ascending()
    {
        var handler = new MultiPageHandler(pages: 3, barsPerPage: 2);
        var options = new TossOptions
        {
            BaseUrl = "https://openapi.tossinvest.com/",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            AllowLiveHttp = true,
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        var auth = new LiveTossAuthClient(http, options);
        // Zero delay for tests
        var loader = new HistoricalCandleLoader(http, options, auth, delayMsMin: 0, delayMsMax: 0);

        var candles = await loader.LoadAsync(
            "VMAR",
            "1m",
            targetBars: 10,
            maxPages: 10,
            countPerPage: 2,
            cancellationToken: CancellationToken.None);

        Assert.True(candles.Count >= 4);
        for (var i = 1; i < candles.Count; i++)
        {
            Assert.True(candles[i - 1].Time <= candles[i].Time);
        }

        Assert.True(handler.CandleRequests >= 2);
        Assert.DoesNotContain(handler.Paths, p => p.Contains("orders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadAsync_blocks_when_allow_live_http_false()
    {
        var options = new TossOptions
        {
            ClientId = "test-id",
            ClientSecret = "test-secret",
            AllowLiveHttp = false,
        };
        var http = new HttpClient(new MultiPageHandler(1, 1))
        {
            BaseAddress = new Uri("https://openapi.tossinvest.com/"),
        };
        var loader = new HistoricalCandleLoader(
            http, options, new LiveTossAuthClient(http, options), delayMsMin: 0, delayMsMax: 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            loader.LoadAsync("VMAR", "1m", 100, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_rejects_invalid_interval()
    {
        var options = new TossOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            AllowLiveHttp = true,
        };
        var http = new HttpClient(new MultiPageHandler(1, 1))
        {
            BaseAddress = new Uri("https://openapi.tossinvest.com/"),
        };
        var loader = new HistoricalCandleLoader(
            http, options, new LiveTossAuthClient(http, options), delayMsMin: 0, delayMsMax: 0);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            loader.LoadAsync("VMAR", "5m", 10, cancellationToken: CancellationToken.None));
    }

    private sealed class MultiPageHandler : HttpMessageHandler
    {
        private readonly int _pages;
        private readonly int _barsPerPage;
        private int _candlePage;

        public List<string> Paths { get; } = new();
        public int CandleRequests { get; private set; }

        public MultiPageHandler(int pages, int barsPerPage)
        {
            _pages = pages;
            _barsPerPage = barsPerPage;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? "";
            Paths.Add(path);

            string json;
            if (path.Contains("oauth2/token", StringComparison.Ordinal))
            {
                json = """{"access_token":"example-not-a-real-token","token_type":"Bearer","expires_in":3600}""";
            }
            else if (path.Contains("api/v1/candles", StringComparison.Ordinal))
            {
                CandleRequests++;
                var pageIndex = _candlePage++;
                if (pageIndex >= _pages)
                {
                    json = """{"result":{"candles":[],"nextBefore":null}}""";
                }
                else
                {
                    // Newest-first page; older pages use earlier timestamps (valid clock times).
                    // Global newest offset: page0 bar0 = base, then decreases by 1 minute.
                    var sb = new StringBuilder();
                    sb.Append("{\"result\":{\"candles\":[");
                    for (var i = 0; i < _barsPerPage; i++)
                    {
                        var ageMinutes = (pageIndex * _barsPerPage) + i;
                        var ts = new DateTimeOffset(2026, 3, 25, 15, 0, 0, TimeSpan.FromHours(9))
                            .AddMinutes(-ageMinutes);
                        var px = 50.0 + ageMinutes * 0.01;
                        if (i > 0)
                        {
                            sb.Append(',');
                        }

                        sb.Append(
                            System.Globalization.CultureInfo.InvariantCulture,
                            $"{{\"timestamp\":\"{ts:yyyy-MM-ddTHH:mm:sszzz}\"," +
                            $"\"openPrice\":\"{px:F2}\",\"highPrice\":\"{px + 0.1:F2}\"," +
                            $"\"lowPrice\":\"{px - 0.1:F2}\",\"closePrice\":\"{px:F2}\"," +
                            $"\"volume\":\"100\",\"currency\":\"USD\"}}");
                    }

                    string? next = null;
                    if (pageIndex + 1 < _pages)
                    {
                        var nextTs = new DateTimeOffset(2026, 3, 25, 15, 0, 0, TimeSpan.FromHours(9))
                            .AddMinutes(-((pageIndex + 1) * _barsPerPage));
                        next = nextTs.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                    }

                    if (next is null)
                    {
                        sb.Append("],\"nextBefore\":null}}");
                    }
                    else
                    {
                        sb.Append("],\"nextBefore\":\"").Append(next).Append("\"}}");
                    }

                    json = sb.ToString();
                }
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
