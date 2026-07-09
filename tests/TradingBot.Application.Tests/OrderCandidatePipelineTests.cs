using TradingBot.Application;
using TradingBot.Domain;

namespace TradingBot.Application.Tests;

public class OrderCandidatePipelineTests
{
    [Fact]
    public void Builds_dry_run_candidates_from_fresh_quotes()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("AAPL", 190m, "USD", now),
        };
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 10_000m,
            MarketDataMaxStalenessSeconds = 60,
        };

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.단순연습전략);

        Assert.Single(result);
        Assert.True(result[0].IsAcceptedForDryRun);
        Assert.Equal("BUY", result[0].Candidate.Side);
        Assert.Equal(2m, result[0].Candidate.Quantity);
        Assert.True(result[0].Candidate.ClientOrderId.StartsWith("cand-", StringComparison.Ordinal) || result[0].Candidate.ClientOrderId.StartsWith("dry-", StringComparison.Ordinal));
    }

    [Fact]
    public void Stale_quotes_produce_blocked_candidates()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("AAPL", 190m, "USD", now.AddMinutes(-10)),
        };
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 10_000m,
            MarketDataMaxStalenessSeconds = 5,
        };

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(quotes, settings, defaultOrderQuantity: 1m, nowUtc: now);

        Assert.Single(result);
        Assert.False(result[0].IsAcceptedForDryRun);
        Assert.Contains(result[0].Risk.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
    }

    [Fact]
    public void No_actionable_signal_without_quantity_policy()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[] { new QuoteSnapshot("AAPL", 190m, "USD", now) };
        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            TradingSafetySettings.CreateSafeDefaults(),
            defaultOrderQuantity: 0m,
            nowUtc: now);
        Assert.Empty(result);
    }
}
