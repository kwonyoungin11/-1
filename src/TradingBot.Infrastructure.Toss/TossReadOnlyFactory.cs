using TradingBot.Infrastructure.Toss.Http;
using TradingBot.Infrastructure.Toss.Mock;

namespace TradingBot.Infrastructure.Toss;

/// <summary>
/// mock vs live read-only 조립. 주문 클라이언트는 항상 차단.
/// AllowLiveHttp=false 또는 credentials 없으면 mock.
/// </summary>
public static class TossReadOnlyFactory
{
    public static ReadOnlyPortfolioService CreatePortfolioService(
        TossOptions options,
        HttpMessageHandler? httpHandler = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.AllowLiveHttp || !options.HasClientCredentials)
        {
            return ReadOnlyPortfolioService.CreateMock();
        }

        var baseUri = new Uri(EnsureTrailingSlash(options.BaseUrl));
        var http = httpHandler is null
            ? new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) }
            : new HttpClient(httpHandler, disposeHandler: false)
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(30),
            };

        var redactor = new DomainTossRedactor();
        var auth = new LiveTossAuthClient(http, options);
        var accounts = new LiveTossAccountClient(http, options, auth, redactor);
        var market = new LiveTossMarketDataClient(http, options, auth);

        return new ReadOnlyPortfolioService(
            auth,
            accounts,
            market,
            new SystemTossClock(),
            redactor,
            options,
            isMock: false);
    }

    public static TossOptions LoadOptionsFromEnvironment()
    {
        var env = EnvFile.LoadMergedWithProcess(FindRepoRoot());
        return TossOptions.FromEnvironment(env);
    }

    public static string DescribeMode(TossOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.AllowLiveHttp)
        {
            return "mock 모드 (TOSS_ALLOW_LIVE_HTTP=false)";
        }

        if (!options.HasClientCredentials)
        {
            return "mock 모드 (클라이언트 키 없음 — 실 HTTP 요청 안 함)";
        }

        return "실 HTTP 읽기 전용 모드 (주문 API 없음)";
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TradingBot.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
