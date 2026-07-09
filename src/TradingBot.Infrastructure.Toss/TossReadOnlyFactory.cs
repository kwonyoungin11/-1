using TradingBot.Infrastructure.Toss.Http;
using TradingBot.Infrastructure.Toss.Mock;
using TradingBot.Orders;

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

        return "실 HTTP 모드 (읽기·주문 API 사용 가능)";
    }

    /// <summary>
    /// Creates a gated live order transport when HTTP and credentials are enabled.
    /// </summary>
    public static ILiveOrderTransport CreateLiveOrderTransport(
        TossOptions options,
        HttpMessageHandler? httpHandler = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        LiveHttpGuard.EnsureAllowed(options);

        if (!options.HasClientCredentials)
        {
            throw new InvalidOperationException("TOSS_CLIENT_ID/SECRET missing — cannot create live order transport.");
        }

        var baseUri = new Uri(EnsureTrailingSlash(options.BaseUrl));
        var http = httpHandler is null
            ? new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) }
            : new HttpClient(httpHandler, disposeHandler: false)
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(30),
            };

        var auth = new LiveTossAuthClient(http, options);
        var accounts = new LiveTossAccountClient(http, options, auth, new DomainTossRedactor());
        return new TossLiveOrderTransport(http, options, auth, accounts);
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";

    /// <summary>
    /// Prefer directory with TradingBot.sln + .env; then any parent with .env;
    /// then cwd. Worktree bin paths often lack .env — walk up to main repo.
    /// </summary>
    private static string? FindRepoRoot()
    {
        // 1) App base → sln
        var fromBase = WalkFor(AppContext.BaseDirectory, requireSln: true, requireEnv: false);
        if (fromBase is not null && File.Exists(Path.Combine(fromBase, ".env")))
        {
            return fromBase;
        }

        // 2) cwd → sln with .env
        var fromCwd = WalkFor(Directory.GetCurrentDirectory(), requireSln: true, requireEnv: true);
        if (fromCwd is not null)
        {
            return fromCwd;
        }

        // 3) Any parent of base with .env (main repo when running from worktree bin)
        var envOnly = WalkFor(AppContext.BaseDirectory, requireSln: false, requireEnv: true)
                      ?? WalkFor(Directory.GetCurrentDirectory(), requireSln: false, requireEnv: true);
        if (envOnly is not null)
        {
            return envOnly;
        }

        // 4) sln without .env (mock fallback still works)
        return fromBase ?? WalkFor(Directory.GetCurrentDirectory(), requireSln: true, requireEnv: false)
               ?? Directory.GetCurrentDirectory();
    }

    private static string? WalkFor(string start, bool requireSln, bool requireEnv)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        DirectoryInfo? dir;
        try
        {
            dir = new DirectoryInfo(start);
        }
        catch
        {
            return null;
        }

        while (dir is not null)
        {
            var sln = File.Exists(Path.Combine(dir.FullName, "TradingBot.sln"));
            var env = File.Exists(Path.Combine(dir.FullName, ".env"));
            if ((!requireSln || sln) && (!requireEnv || env))
            {
                if (requireSln || requireEnv)
                {
                    return dir.FullName;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }
}
