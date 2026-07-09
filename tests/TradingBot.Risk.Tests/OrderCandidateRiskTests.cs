using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class OrderCandidateRiskTests
{
    private static TradingSafetySettings Settings(
        decimal? maxNotional = null,
        decimal? maxPos = null,
        int stale = 5) => new()
    {
        AllowLiveOrders = false,
        KillSwitch = true,
        OrderMode = OrderMode.DryRun,
        MaxOrderNotional = maxNotional,
        MaxPositionSize = maxPos,
        MarketDataMaxStalenessSeconds = stale,
    };

    private static CandidateRiskContext OkContext(DateTimeOffset now) => new()
    {
        Symbol = "AAPL",
        Quantity = 1,
        LimitPrice = 100m,
        QuoteTimestampUtc = now,
        NowUtc = now,
        MarketSessionOpen = true,
        MarketSessionKnown = true,
    };

    [Fact]
    public void Fresh_quote_within_limits_allows_candidate()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: 1000m), OkContext(now));
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Stale_quote_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { QuoteTimestampUtc = now.AddSeconds(-30) };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(stale: 5), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
    }

    [Fact]
    public void Missing_price_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { LimitPrice = null, HasMissingData = true };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
    }

    [Fact]
    public void Notional_limit_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { Quantity = 10, LimitPrice = 100m }; // 1000
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: 500m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MaxOrderNotionalExceeded.Code);
    }

    [Fact]
    public void Closed_market_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { MarketSessionOpen = false };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
    }

    [Fact]
    public void Live_submission_still_blocked_by_defaults()
    {
        var decision = new LiveOrderGate().Evaluate(
            TradingSafetySettings.CreateSafeDefaults(),
            new LiveOrderContext());
        Assert.True(decision.IsBlocked);
    }
}
