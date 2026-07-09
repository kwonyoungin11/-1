using System.Text.Json;
using TradingBot.Backtesting;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;

namespace TradingBot.Runner;

/// <summary>
/// CLI: <c>backtest-split --cache-only</c> — grid-search split buy/sell on VMAR 1m.
/// Simulation only — never places live orders. Not investment advice.
/// </summary>
public static class SplitBacktestCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var cacheOnly = args.Any(a => a is "--cache-only");
        var repoRoot = TossReadOnlyFactory.ResolveRepoRoot()
                       ?? Directory.GetCurrentDirectory();
        var cachePath = Path.Combine(repoRoot, "artifacts", "candles", "VMAR_1m.json");
        var reportStem = Path.Combine(repoRoot, "artifacts", "vmar_split_ladder_6m_backtest_report");

        Console.WriteLine("Split ladder backtest (simulation · live orders blocked · not advice)");
        Console.WriteLine($"Repo: {repoRoot}");

        IReadOnlyList<CandlePoint> candles;
        string source;
        if (!cacheOnly)
        {
            // Prefer cache; live optional via main backtest host.
            Console.WriteLine("Prefer cache; use `backtest --symbol VMAR` first if missing.");
        }

        var cached = await CandleJsonStore.TryLoadAsync(cachePath, cancellationToken).ConfigureAwait(false);
        if (cached is null || cached.Candles.Count == 0)
        {
            Console.Error.WriteLine($"No candle cache at {cachePath}");
            Console.Error.WriteLine("Run: CACHE_ONLY=0 bash scripts/grok/run-vmar-6m-backtest.sh  (or copy VMAR_1m.json)");
            return 2;
        }

        candles = cached.Candles;
        source = $"cache:{cachePath} ({cached.Source})";
        Console.WriteLine($"Candles: {candles.Count:N0} · {source}");
        Console.WriteLine(
            $"Range: {KoreaTime.FormatFull(candles[0].Time)} → {KoreaTime.FormatFull(candles[^1].Time)}");

        var config = new BacktestConfig(
            InitialCash: 10_000m,
            FeeRatePerSide: 0.001m,
            SlippageRatePerSide: 0.0005m,
            CooldownBarsAfterExit: 3,
            MaxHoldBars: 0);

        Console.WriteLine("Grid-searching split ladder params (CERS entry thr=0.006)...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = SplitLadderOptimizer.GridSearch(
            candles,
            config,
            includeBar: null,
            minTrades: 15);
        sw.Stop();
        Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s · candidates ranked: {ranked.Count}");

        if (ranked.Count == 0)
        {
            Console.Error.WriteLine("No ranked results.");
            return 3;
        }

        var best = ranked[0];
        Console.WriteLine(
            $"BEST score={best.Score:F2} ret={best.Result.TotalReturnPct:F2}% mdd={best.Result.MaxDrawdownPct:F2}% " +
            $"trades={best.Result.TradeCount} · {best.Params.Name}");

        var md = SplitLadderOptimizer.RenderMarkdownReport(
            ranked,
            symbol: "VMAR",
            interval: "1m",
            barCount: candles.Count,
            dataSource: source,
            first: candles[0].Time,
            last: candles[^1].Time,
            firstClose: candles[0].Close,
            lastClose: candles[^1].Close,
            topN: 30);

        Directory.CreateDirectory(Path.GetDirectoryName(reportStem)!);
        var mdPath = reportStem + ".md";
        var jsonPath = reportStem + ".json";
        await File.WriteAllTextAsync(mdPath, md, cancellationToken).ConfigureAwait(false);

        var slim = new
        {
            title = "VMAR split ladder grid backtest",
            disclaimer = "simulation · not investment advice · past ≠ future",
            barCount = candles.Count,
            dataSource = source,
            best = new
            {
                best.Params.BuyLegs,
                best.Params.BuyStepPercent,
                best.Params.SellLegs,
                best.Params.SellStepPercent,
                best.Params.StopLossFromAvgPercent,
                best.Params.TakeProfitFromAvgPercent,
                best.Params.EntryThreshold,
                best.Params.MaxHoldBars,
                best.Score,
                best.Result.TotalReturnPct,
                best.Result.MaxDrawdownPct,
                best.Result.Sharpe,
                best.Result.TradeCount,
                best.Result.WinRatePct,
                best.Result.ProfitFactor,
            },
            top = ranked.Take(30).Select(r => new
            {
                r.Params.Name,
                r.Params.BuyLegs,
                r.Params.BuyStepPercent,
                r.Params.SellStepPercent,
                r.Params.StopLossFromAvgPercent,
                r.Params.TakeProfitFromAvgPercent,
                r.Params.MaxHoldBars,
                r.Score,
                r.Result.TotalReturnPct,
                r.Result.MaxDrawdownPct,
                r.Result.TradeCount,
                r.Result.WinRatePct,
                r.Result.ProfitFactor,
                r.Result.Sharpe,
            }),
            projectDefault = ranked
                .Select((r, i) => (r, i))
                .Where(x => x.r.Params.BuyLegs == 3 && Math.Abs(x.r.Params.BuyStepPercent - 0.10) < 1e-9)
                .Select(x => new { rank = x.i + 1, x.r.Score, x.r.Result.TotalReturnPct, x.r.Result.MaxDrawdownPct, x.r.Result.TradeCount })
                .FirstOrDefault(),
            generatedAtUtc = DateTimeOffset.UtcNow,
        };
        await File.WriteAllTextAsync(
                jsonPath,
                JsonSerializer.Serialize(slim, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Report MD:   {mdPath}");
        Console.WriteLine($"Report JSON: {jsonPath}");
        Console.WriteLine("No Toss order API was called.");
        return 0;
    }
}
