using TradingBot.Application;
using TradingBot.Domain;

namespace TradingBot.Application.Tests;

public class OneMinuteSplitScalpTests
{
    [Fact]
    public void Generator_produces_actionable_buy_for_vmar_quote()
    {
        var gen = new OneMinuteSplitScalpSignalGenerator();
        Assert.Equal(TradingStrategyKind.일분분할스캘프, gen.Kind);

        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quote = new QuoteSnapshot(WatchlistCatalog.VmarSymbol, 3.50m, "USD", now);
        var signal = gen.Generate(quote, baseOrderQuantity: 6m, nowUtc: now);

        Assert.True(signal.IsActionable);
        Assert.Equal(SignalSide.Buy, signal.Side);
        Assert.Equal(6m, signal.SuggestedQuantity);
        Assert.Equal(3.50m, signal.ReferencePrice);
        Assert.Equal("VMAR", signal.Symbol);
        Assert.Contains("분할", signal.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("투자 조언 아님", signal.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("실주문 아님", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_invalid_quote_is_not_actionable()
    {
        var gen = new OneMinuteSplitScalpSignalGenerator();
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");

        var noPrice = gen.Generate(new QuoteSnapshot("VMAR", null, "USD", now), 6m, now);
        Assert.False(noPrice.IsActionable);

        var zeroPrice = gen.Generate(new QuoteSnapshot("VMAR", 0m, "USD", now), 6m, now);
        Assert.False(zeroPrice.IsActionable);

        var zeroQty = gen.Generate(new QuoteSnapshot("VMAR", 3.5m, "USD", now), 0m, now);
        Assert.False(zeroQty.IsActionable);
    }

    [Fact]
    public void Router_wires_one_minute_split_scalp_generator()
    {
        var router = new StrategySignalRouter();
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var q = new QuoteSnapshot("VMAR", 3.5m, "USD", now);
        var s = router.Generate(TradingStrategyKind.일분분할스캘프, q, 6m, now);

        Assert.True(s.IsActionable);
        Assert.Equal(SignalSide.Buy, s.Side);
        Assert.Equal(6m, s.SuggestedQuantity);
        Assert.Contains("분할", s.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipeline_split_scalp_produces_three_evaluated_candidates()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("VMAR", 10m, "USD", now),
        };
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 100_000m,
            MarketDataMaxStalenessSeconds = 60,
        };

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 6m,
            nowUtc: now,
            strategy: TradingStrategyKind.일분분할스캘프);

        Assert.Equal(3, result.Count);
        Assert.Equal(6m, result.Sum(r => r.Candidate.Quantity));
        Assert.All(result, r => Assert.Equal("BUY", r.Candidate.Side));
        Assert.All(result, r => Assert.Equal("VMAR", r.Candidate.Symbol));
        Assert.All(result, r => Assert.Equal("LIMIT", r.Candidate.OrderType));
        Assert.All(result, r => ClientOrderIdFactory.Validate(r.Candidate.ClientOrderId));

        var ids = result.Select(r => r.Candidate.ClientOrderId).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());

        // Stepped prices: 10, 9.99, 9.98
        var prices = result.Select(r => r.Candidate.LimitPrice).OrderByDescending(p => p).ToArray();
        Assert.Equal(10m, prices[0]);
        Assert.Equal(9.99m, prices[1]);
        Assert.Equal(9.98m, prices[2]);
    }

    [Fact]
    public void Pipeline_other_strategies_produce_at_most_one_candidate_per_quote()
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
        var simple = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.단순연습전략);

        Assert.Single(simple);
        Assert.Equal(2m, simple[0].Candidate.Quantity);
    }

    [Fact]
    public void Pipeline_split_scalp_fail_closed_when_qty_too_small_for_legs()
    {
        // qty 2 < LegCount 3 → planner empty → no candidates
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[]
        {
            new QuoteSnapshot("VMAR", 10m, "USD", now),
        };
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 100_000m,
            MarketDataMaxStalenessSeconds = 60,
        };

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.일분분할스캘프);

        Assert.Empty(result);
    }
}
