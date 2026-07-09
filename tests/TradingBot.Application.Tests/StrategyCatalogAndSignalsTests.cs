using TradingBot.Application;
using TradingBot.Domain;

namespace TradingBot.Application.Tests;

public class StrategyCatalogAndSignalsTests
{
    [Fact]
    public void Watchlist_is_spacex_only()
    {
        Assert.Single(WatchlistCatalog.AllKinds);
        Assert.Equal(StockMarketKind.스페이스X, WatchlistCatalog.AllKinds[0]);
        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.스페이스X);
        Assert.Single(symbols);
        Assert.Equal("SPCX", symbols[0]);
    }

    [Fact]
    public void Strategies_include_trend_mean_reversion_momentum()
    {
        Assert.Contains(TradingStrategyKind.추세추종, StrategyCatalog.All);
        Assert.Contains(TradingStrategyKind.평균회귀, StrategyCatalog.All);
        Assert.Contains(TradingStrategyKind.모멘텀돌파, StrategyCatalog.All);
        Assert.Equal(0m, StrategyCatalog.BaseQuantity(TradingStrategyKind.관망만));
        Assert.True(StrategyCatalog.BaseQuantity(TradingStrategyKind.모멘텀돌파) >= 3m);
    }

    [Fact]
    public void TrendFollow_generator_defaults_to_safe_parameters()
    {
        var gen = new TrendFollowSignalGenerator();
        var defaults = TrendFollowParameters.CreateSafeDefaults();
        Assert.Equal(defaults, gen.Parameters);
        Assert.Equal(0.15m, gen.Parameters.MinMomentumScore);
        Assert.Equal(1.0m, gen.Parameters.StopLossR);
        Assert.Equal(2.0m, gen.Parameters.TakeProfitR);
        Assert.Equal(3, gen.Parameters.CooldownBars);
    }

    [Fact]
    public void TrendFollow_uses_MinMomentumScore_threshold()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var q = new QuoteSnapshot("AAPL", 190m, "USD", now);

        // Threshold above any possible |MomentumScore| (clamped to 1.5) → always hold
        var tight = new TrendFollowSignalGenerator(
            new TrendFollowParameters(
                StopLossR: 1.0m,
                TakeProfitR: 2.0m,
                CooldownBars: 3,
                MinMomentumScore: 10m));
        var hold = tight.Generate(q, 2m, now);
        Assert.False(hold.IsActionable);
        Assert.Equal(SignalSide.Hold, hold.Side);
        Assert.Contains("임계", hold.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("투자 조언 아님", hold.OwnerMessage, StringComparison.Ordinal);

        // Zero threshold → never hold for momentum weakness (always acts when quote valid)
        var loose = new TrendFollowSignalGenerator(
            new TrendFollowParameters(
                StopLossR: 1.0m,
                TakeProfitR: 2.0m,
                CooldownBars: 3,
                MinMomentumScore: 0m));
        var action = loose.Generate(q, 2m, now);
        Assert.True(action.IsActionable);
        Assert.True(action.Side is SignalSide.Buy or SignalSide.Sell);
        Assert.Contains("SL", action.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("TP", action.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("투자 조언 아님", action.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TrendFollow_Describe_is_honest_not_advice()
    {
        var description = StrategyCatalog.Describe(TradingStrategyKind.추세추종);
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains("투자 조언 아님", description, StringComparison.Ordinal);
        Assert.DoesNotContain("보장", description, StringComparison.Ordinal);
        Assert.DoesNotContain("수익", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Bubble_size_is_notional_quantity_times_price()
    {
        var size = TradeMarker.NotionalSize(10m, 190m);
        Assert.Equal(1900d, size, precision: 5);
        var vol = TradeMarker.VolumeNotionalSize(1_000_000, 200);
        Assert.Equal(200_000_000d, vol, precision: 1);
    }

    [Fact]
    public void Router_hold_produces_no_actionable()
    {
        var router = new StrategySignalRouter();
        var q = new QuoteSnapshot("AAPL", 190m, "USD", DateTimeOffset.UtcNow);
        var s = router.Generate(TradingStrategyKind.관망만, q, 2m, DateTimeOffset.UtcNow);
        Assert.False(s.IsActionable);
    }

    [Fact]
    public void Router_simple_practice_buys()
    {
        var router = new StrategySignalRouter();
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var q = new QuoteSnapshot("AAPL", 190m, "USD", now);
        var s = router.Generate(TradingStrategyKind.단순연습전략, q, 2m, now);
        Assert.True(s.IsActionable);
        Assert.Equal(SignalSide.Buy, s.Side);
        Assert.Equal(2m, s.SuggestedQuantity);
    }

    [Fact]
    public void Momentum_breakout_uses_larger_quantity_when_actionable()
    {
        var router = new StrategySignalRouter();
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        // try several symbols — at least one should be actionable or all hold
        var anyLarge = false;
        foreach (var sym in new[] { "AAPL", "NVDA", "TSLA", "MSFT", "AMD", "SPY", "META" })
        {
            var q = new QuoteSnapshot(sym, (decimal)WatchlistCatalog.ChartSeedPrice(sym), "USD", now);
            var s = router.Generate(TradingStrategyKind.모멘텀돌파, q, 3m, now);
            if (s.IsActionable && s.SuggestedQuantity is decimal qty && qty >= 5m)
            {
                anyLarge = true;
                break;
            }
        }

        // If none break out this minute, still verify hold message path is clean
        if (!anyLarge)
        {
            var q = new QuoteSnapshot("AAPL", 190m, "USD", now);
            var s = router.Generate(TradingStrategyKind.모멘텀돌파, q, 3m, now);
            Assert.False(s.IsActionable);
            Assert.Contains("모멘텀", s.OwnerMessage, StringComparison.Ordinal);
        }
        else
        {
            Assert.True(anyLarge);
        }
    }

    [Fact]
    public void Pipeline_respects_strategy_hold()
    {
        var now = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
        var quotes = new[] { new QuoteSnapshot("AAPL", 190m, "USD", now) };
        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            TradingSafetySettings.CreateSafeDefaults(),
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.관망만);
        Assert.Empty(result);
    }

    [Fact]
    public void Session_focus_symbol_is_always_spcx()
    {
        var s = new AutoTradeSessionService();
        s.StockKind = StockMarketKind.스페이스X;
        Assert.Equal(new[] { "SPCX" }, s.ResolveWatchSymbols());
        s.FocusSymbol = "AAPL";
        Assert.Equal("SPCX", s.ResolveFocusSymbol());
        var p = s.ToPanelSnapshot();
        Assert.Equal("SPCX", p.FocusSymbol);
        Assert.Contains("SPCX", p.StockKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("실주문", p.SafetyNote, StringComparison.Ordinal);
    }
}
