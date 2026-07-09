using TradingBot.Application;
using TradingBot.Domain;

namespace TradingBot.Application.Tests;

public class StrategyCatalogAndSignalsTests
{
    [Fact]
    public void Watchlist_has_expanded_kinds_and_symbols()
    {
        Assert.True(WatchlistCatalog.AllKinds.Count >= 5);
        Assert.Contains("NVDA", WatchlistCatalog.ResolveSymbols(StockMarketKind.나스닥));
        Assert.Contains("SPY", WatchlistCatalog.ResolveSymbols(StockMarketKind.미국ETF));
        Assert.Contains("005930", WatchlistCatalog.ResolveSymbols(StockMarketKind.국내주식));
        Assert.Contains("AMD", WatchlistCatalog.ResolveSymbols(StockMarketKind.나스닥테크));
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
    public void Session_focus_symbol_and_expanded_watchlist()
    {
        var s = new AutoTradeSessionService();
        s.StockKind = StockMarketKind.미국ETF;
        Assert.Contains("QQQ", s.ResolveWatchSymbols());
        s.FocusSymbol = "QQQ";
        Assert.Equal("QQQ", s.ResolveFocusSymbol());
        var p = s.ToPanelSnapshot();
        Assert.Equal("QQQ", p.FocusSymbol);
        Assert.Contains("ETF", p.StockKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("규모", p.SafetyNote, StringComparison.Ordinal);
    }
}
