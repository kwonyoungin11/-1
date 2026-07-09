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
        ClientOrderIdFactory.Validate(result[0].Candidate.ClientOrderId);
        Assert.True(
            result[0].Candidate.ClientOrderId.StartsWith(ClientOrderIdFactory.Prefix + "-", StringComparison.Ordinal) ||
            ClientOrderIdFactory.IsValid(result[0].Candidate.ClientOrderId));
    }

    [Fact]
    public void Pipeline_assigns_unique_client_order_ids_per_candidate()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("AAPL", 190m, "USD", now),
            new QuoteSnapshot("MSFT", 400m, "USD", now),
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
            defaultOrderQuantity: 1m,
            nowUtc: now,
            strategy: TradingStrategyKind.단순연습전략);

        Assert.Equal(2, result.Count);
        var ids = result.Select(r => r.Candidate.ClientOrderId).ToArray();
        Assert.All(ids, id => ClientOrderIdFactory.Validate(id));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
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

    [Fact]
    public void Practice_context_sizes_quantity_via_PositionRiskSizer()
    {
        // equity 100k · risk 1% · stop 2% · price 100 → qty floor(1000/2) = 500
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("AAPL", 100m, "USD", now),
        };
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 100_000m,
            MarketDataMaxStalenessSeconds = 60,
        };
        var practice = new PracticeStrategyContext(
            Equity: 100_000m,
            RiskPercentPerTrade: 1m,
            StopLossPercent: 2m);

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.단순연습전략,
            practice: practice);

        Assert.Single(result);
        Assert.True(result[0].IsAcceptedForDryRun);
        Assert.Equal(500m, result[0].Candidate.Quantity);
        Assert.Equal(100m, result[0].Candidate.LimitPrice);
        Assert.Equal("BUY", result[0].Candidate.Side);
    }

    [Fact]
    public void Practice_daily_loss_halt_returns_no_candidates()
    {
        // day start 100k · current 96k · max daily loss 3% → loss 4% ≥ 3% → halt
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("AAPL", 100m, "USD", now),
        };
        var practice = new PracticeStrategyContext(
            Equity: 100_000m,
            RiskPercentPerTrade: 1m,
            StopLossPercent: 2m,
            MaxDailyLossPercent: 3m,
            DayStartEquity: 100_000m,
            CurrentEquity: 96_000m);

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            TradingSafetySettings.CreateSafeDefaults(),
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.단순연습전략,
            practice: practice);

        Assert.Empty(result);
        Assert.DoesNotContain(result, r => r.IsAcceptedForDryRun);
    }

    [Fact]
    public void Holiday_us_market_session_produces_no_accepted_candidates()
    {
        var now = DateTimeOffset.Parse("2026-07-04T17:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("AAPL", 100m, "USD", now),
        };
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 100_000m,
            MarketDataMaxStalenessSeconds = 60,
        };
        var holiday = new UsMarketSessionSnapshot(
            Date: "2026-07-04",
            IsHolidayOrClosed: true,
            OwnerMessage: "미국 독립기념일 휴장");

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 2m,
            nowUtc: now,
            usMarket: holiday,
            strategy: TradingStrategyKind.단순연습전략);

        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.False(r.IsAcceptedForDryRun));
        Assert.Contains(
            result[0].Risk.Blocks,
            b => b.Code == BlockedReason.MarketSessionClosed.Code);
    }

    [Fact]
    public void Practice_trend_follow_params_are_wired_through_router()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("AAPL", 100m, "USD", now),
        };
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 100_000m,
            MarketDataMaxStalenessSeconds = 60,
        };
        // Threshold above any |MomentumScore| → hold, so no accepted candidates
        var practice = new PracticeStrategyContext(
            Equity: 100_000m,
            RiskPercentPerTrade: 1m,
            StopLossPercent: 2m,
            TrendFollow: new TrendFollowParameters(
                StopLossR: 1.0m,
                TakeProfitR: 2.0m,
                CooldownBars: 3,
                MinMomentumScore: 10m));

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.추세추종,
            practice: practice);

        Assert.Empty(result);
    }
}
