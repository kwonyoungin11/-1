using TradingBot.Backtesting;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Infrastructure.Toss.Http;

namespace TradingBot.Runner;

/// <summary>
/// CLI: <c>backtest --symbol VMAR --interval 1m --target-bars 40000</c>
/// Fetches/caches candles, runs <see cref="StrategySuite.AllDefault"/> via
/// <see cref="BacktestEngine.Run"/>, writes markdown+JSON report.
/// Simulation only — never places live orders. Not investment advice.
/// </summary>
public static class BacktestCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        var opts = BacktestCliOptions.Parse(args);
        if (opts.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var repoRoot = TossReadOnlyFactory.ResolveRepoRoot()
                       ?? Directory.GetCurrentDirectory();
        Console.WriteLine("TradingBot backtest host (simulation only · live orders blocked)");
        Console.WriteLine($"Repo: {repoRoot}");
        Console.WriteLine($"Symbol={opts.Symbol} Interval={opts.Interval} TargetBars={opts.TargetBars}");

        var cachePath = string.IsNullOrWhiteSpace(opts.CachePath)
            ? CandleJsonStore.DefaultCachePath(repoRoot, opts.Symbol, opts.Interval)
            : opts.CachePath!;

        var (candles, dataSource) = await LoadCandlesAsync(
                opts, cachePath, repoRoot, cancellationToken)
            .ConfigureAwait(false);

        if (candles.Count == 0)
        {
            Console.Error.WriteLine(
                "No candles available. Enable TOSS_ALLOW_LIVE_HTTP + credentials, " +
                "or place a cache JSON at: " + cachePath);
            return 2;
        }

        Console.WriteLine($"Candles: {candles.Count:N0} from {dataSource}");
        if (candles.Count > 0)
        {
            Console.WriteLine(
                $"Range: {KoreaTime.FormatFull(candles[0].Time)} → {KoreaTime.FormatFull(candles[^1].Time)}");
        }

        var config = new BacktestConfig(
            InitialCash: opts.InitialCash,
            FeeRatePerSide: opts.FeeRate,
            SlippageRatePerSide: opts.SlippageRate,
            CooldownBarsAfterExit: 3,
            RegularSessionOnly: opts.RegularSessionOnly,
            MaxHoldBars: opts.MaxHoldBars);

        Func<CandlePoint, bool>? includeBar = config.RegularSessionOnly
            ? UsRegularSessionFilter.IsRegularSession
            : null;

        var strategies = StrategySuite.AllDefault();
        Console.WriteLine($"Strategies: {strategies.Count}");

        var results = new List<BacktestResult>(strategies.Count);
        foreach (var strategy in strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Fresh instance semantics: suite already returns new instances.
            var result = BacktestEngine.Run(candles, strategy, config, includeBar);
            results.Add(result);
            Console.WriteLine(
                $"  {result.StrategyName,-24} ret={result.TotalReturnPct,8:F2}%  " +
                $"mdd={result.MaxDrawdownPct,6:F2}%  trades={result.TradeCount,5}  sharpe={result.Sharpe,6:F2}");
        }

        var reportStem = string.IsNullOrWhiteSpace(opts.ReportStem)
            ? BacktestReportWriter.DefaultVmarCers6mStem(repoRoot)
            : opts.ReportStem!;

        // Prefer symbol-specific stem when not VMAR default.
        if (string.IsNullOrWhiteSpace(opts.ReportStem)
            && !opts.Symbol.Equals("VMAR", StringComparison.OrdinalIgnoreCase))
        {
            reportStem = Path.Combine(
                repoRoot,
                "artifacts",
                $"{opts.Symbol.ToLowerInvariant()}_{opts.Interval}_backtest_report");
        }

        var document = BacktestReportWriter.BuildDocument(
            symbol: opts.Symbol,
            interval: opts.Interval,
            dataSource: dataSource,
            candles: candles,
            config: config,
            results: results,
            title: $"{opts.Symbol} {opts.Interval} CERS multi-strategy backtest",
            notes: "simulation · not investment advice · live orders blocked");

        var paths = BacktestReportWriter.Write(reportStem, document);
        Console.WriteLine($"Report MD:   {paths.MarkdownPath}");
        Console.WriteLine($"Report JSON: {paths.JsonPath}");
        Console.WriteLine("Done. No Toss order API was called.");
        return 0;
    }

    private static async Task<(IReadOnlyList<CandlePoint> Candles, string Source)> LoadCandlesAsync(
        BacktestCliOptions opts,
        string cachePath,
        string repoRoot,
        CancellationToken cancellationToken)
    {
        // 1) Live fetch when allowed
        if (!opts.CacheOnly)
        {
            var loader = HistoricalCandleLoader.TryCreateFromEnvironment(repoRoot);
            if (loader is not null)
            {
                try
                {
                    Console.WriteLine("Fetching historical candles via Toss (paged, delayed)...");
                    var progress = new Progress<HistoricalCandleProgress>(p =>
                    {
                        if (p.PagesFetched % 10 == 0 || p.Truncated)
                        {
                            Console.WriteLine(
                                $"  page={p.PagesFetched} bars={p.BarsSoFar:N0}" +
                                (p.Truncated ? " (truncated on HTTP error)" : string.Empty));
                        }
                    });

                    var live = await loader.LoadAsync(
                            opts.Symbol,
                            opts.Interval,
                            opts.TargetBars,
                            maxPages: opts.MaxPages,
                            countPerPage: HistoricalCandleLoader.DefaultCountPerPage,
                            progress: progress,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    if (live.Count > 0)
                    {
                        await CandleJsonStore.SaveAsync(
                                cachePath,
                                opts.Symbol,
                                opts.Interval,
                                live,
                                source: "toss-openapi-live-page",
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        Console.WriteLine($"Cache written: {cachePath} ({live.Count:N0} bars)");
                        return (live, "toss live GET /api/v1/candles (paged)");
                    }

                    Console.WriteLine("Live fetch returned 0 bars; trying cache...");
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
                {
                    // Never print secrets; message is already redacted by client.
                    Console.WriteLine($"Live fetch failed: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine("Falling back to candle cache JSON...");
                }
            }
            else
            {
                Console.WriteLine(
                    "Live HTTP unavailable (TOSS_ALLOW_LIVE_HTTP=false or missing credentials). Using cache.");
            }
        }

        // 2) Cache
        var cached = await CandleJsonStore.TryLoadAsync(cachePath, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null && cached.Candles.Count > 0)
        {
            return (cached.Candles, $"cache:{cachePath} (source={cached.Source ?? "file"})");
        }

        return (Array.Empty<CandlePoint>(), "none");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            TradingBot backtest host — offline multi-strategy simulation (not investment advice).

            Usage:
              dotnet run --project src/TradingBot.Runner -- backtest [options]

            Options:
              --symbol <SYM>         Default: VMAR
              --interval <1m|1d>     Default: 1m
              --target-bars <N>      Default: 40000
              --max-pages <N>        Default: 300 (historical loader only)
              --cache-only           Skip live fetch; load artifacts/candles only
              --cache-path <path>    Override candle JSON cache path
              --report-stem <path>   Override report path stem (writes .md + .json)
              --no-rth-filter        Allow signals outside US regular session
              --initial-cash <N>     Default: 10000
              --fee <rate>           Per-side fee rate (default 0.001)
              --slippage <rate>      Per-side slippage (default 0.0005)
              --max-hold-bars <N>    Default: 60
              --help                 Show this help

            Safety:
              Live orders remain blocked. This command never calls order APIs.
              Requires TOSS_ALLOW_LIVE_HTTP=true + credentials only for live candle fetch.
            """);
    }
}

internal sealed class BacktestCliOptions
{
    public string Symbol { get; init; } = "VMAR";
    public string Interval { get; init; } = "1m";
    public int TargetBars { get; init; } = 40_000;
    public int MaxPages { get; init; } = HistoricalCandleLoader.DefaultMaxPages;
    public bool CacheOnly { get; init; }
    public string? CachePath { get; init; }
    public string? ReportStem { get; init; }
    public bool RegularSessionOnly { get; init; } = true;
    public decimal InitialCash { get; init; } = 10_000m;
    public decimal FeeRate { get; init; } = 0.001m;
    public decimal SlippageRate { get; init; } = 0.0005m;
    public int MaxHoldBars { get; init; } = 60;
    public bool ShowHelp { get; init; }

    public static BacktestCliOptions Parse(string[] args)
    {
        var symbol = "VMAR";
        var interval = "1m";
        var targetBars = 40_000;
        var maxPages = HistoricalCandleLoader.DefaultMaxPages;
        var cacheOnly = false;
        string? cachePath = null;
        string? reportStem = null;
        var rth = true;
        var cash = 10_000m;
        var fee = 0.001m;
        var slip = 0.0005m;
        var maxHold = 60;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "-h" or "--help" or "help")
            {
                help = true;
                continue;
            }

            if (a == "--symbol" && i + 1 < args.Length)
            {
                symbol = args[++i].Trim().ToUpperInvariant();
                continue;
            }

            if (a == "--interval" && i + 1 < args.Length)
            {
                interval = args[++i].Trim().ToLowerInvariant();
                continue;
            }

            if (a == "--target-bars" && i + 1 < args.Length
                && int.TryParse(args[++i], out var tb) && tb > 0)
            {
                targetBars = tb;
                continue;
            }

            if (a == "--max-pages" && i + 1 < args.Length
                && int.TryParse(args[++i], out var mp) && mp > 0)
            {
                maxPages = mp;
                continue;
            }

            if (a == "--cache-only")
            {
                cacheOnly = true;
                continue;
            }

            if (a == "--cache-path" && i + 1 < args.Length)
            {
                cachePath = args[++i];
                continue;
            }

            if (a == "--report-stem" && i + 1 < args.Length)
            {
                reportStem = args[++i];
                continue;
            }

            if (a == "--no-rth-filter")
            {
                rth = false;
                continue;
            }

            if (a == "--initial-cash" && i + 1 < args.Length
                && decimal.TryParse(args[++i], out var ic) && ic > 0)
            {
                cash = ic;
                continue;
            }

            if (a == "--fee" && i + 1 < args.Length
                && decimal.TryParse(args[++i], out var f) && f >= 0)
            {
                fee = f;
                continue;
            }

            if (a == "--slippage" && i + 1 < args.Length
                && decimal.TryParse(args[++i], out var s) && s >= 0)
            {
                slip = s;
                continue;
            }

            if (a == "--max-hold-bars" && i + 1 < args.Length
                && int.TryParse(args[++i], out var mh) && mh > 0)
            {
                maxHold = mh;
            }
        }

        return new BacktestCliOptions
        {
            Symbol = symbol,
            Interval = interval,
            TargetBars = targetBars,
            MaxPages = maxPages,
            CacheOnly = cacheOnly,
            CachePath = cachePath,
            ReportStem = reportStem,
            RegularSessionOnly = rth,
            InitialCash = cash,
            FeeRate = fee,
            SlippageRate = slip,
            MaxHoldBars = maxHold,
            ShowHelp = help,
        };
    }
}
