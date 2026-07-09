using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

public class BacktestReportWriterTests
{
    [Fact]
    public void Write_emits_md_and_json_with_disclaimer()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tb-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var stem = Path.Combine(dir, "vmar_cers_6m_backtest_report");
            var candles = new[]
            {
                new CandlePoint(DateTimeOffset.Parse("2026-01-01T15:00:00Z"), 10, 11, 9, 10.5, 1),
                new CandlePoint(DateTimeOffset.Parse("2026-01-01T15:01:00Z"), 10.5, 11, 10, 10.8, 1),
            };
            var config = new BacktestConfig();
            var result = new BacktestResult(
                StrategyName: "Cers",
                InitialCash: 10_000m,
                FinalEquity: 10_100m,
                TotalReturnPct: 1m,
                MaxDrawdownPct: 0.5m,
                Sharpe: 1.2,
                TradeCount: 1,
                WinRatePct: 100,
                ProfitFactor: 2,
                AvgHoldBars: 3,
                Trades: Array.Empty<BacktestTrade>(),
                EquityCurve: Array.Empty<EquityPoint>(),
                Notes: "simulation");

            var doc = BacktestReportWriter.BuildDocument(
                "VMAR", "1m", "test", candles, config, new[] { result });
            var paths = BacktestReportWriter.Write(stem, doc);

            Assert.True(File.Exists(paths.MarkdownPath));
            Assert.True(File.Exists(paths.JsonPath));
            var md = File.ReadAllText(paths.MarkdownPath);
            Assert.Contains("투자 조언 아님", md, StringComparison.Ordinal);
            Assert.Contains("Cers", md, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
