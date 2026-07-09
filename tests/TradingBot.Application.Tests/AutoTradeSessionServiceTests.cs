using TradingBot.Application;
using TradingBot.Domain;

namespace TradingBot.Application.Tests;

public class AutoTradeSessionServiceTests
{
    [Fact]
    public void Start_and_stop_practice_session()
    {
        var s = new AutoTradeSessionService();
        Assert.True(s.TryStart(out var msg));
        Assert.Contains("실주문", msg, StringComparison.Ordinal);
        Assert.Equal(AutoTradeSessionStatus.실행중, s.Status);
        Assert.True(s.TryStop(out _));
        Assert.Equal(AutoTradeSessionStatus.중지, s.Status);
    }

    [Fact]
    public void Panel_has_required_auto_trade_fields()
    {
        var s = new AutoTradeSessionService();
        s.StockKind = StockMarketKind.나스닥;
        s.Strategy = TradingStrategyKind.단순연습전략;
        var p = s.ToPanelSnapshot();
        Assert.Equal("나스닥", p.StockKindLabel);
        Assert.Equal("단순연습전략", p.StrategyLabel);
        Assert.Contains("잔액", p.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("연습", p.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("%", p.ReturnRateLabel, StringComparison.Ordinal);
        Assert.True(p.CanStart);
    }

    [Fact]
    public void ApplyExternalBalance_updates_panel_and_optional_starting()
    {
        var s = new AutoTradeSessionService();
        Assert.Equal(AutoTradeSessionService.DefaultPracticeStartingBalance, s.StartingBalance);
        Assert.Equal(AutoTradeSessionService.DefaultPracticeStartingBalance, s.Balance);

        s.SetDataSourceLabel("실계좌 읽기");
        s.ApplyExternalBalance(12_345.67m, setStartingIfUnset: true);

        Assert.Equal(12_345.67m, s.Balance);
        Assert.Equal(12_345.67m, s.StartingBalance);

        var p = s.ToPanelSnapshot();
        Assert.Equal(12_345.67m, p.Balance);
        Assert.Contains("12,345.67", p.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("실계좌 읽기", p.BalanceLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("(연습)", p.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("실주문", p.SafetyNote, StringComparison.Ordinal);

        // Starting only set once from default practice
        s.ApplyExternalBalance(99_000m, setStartingIfUnset: true);
        Assert.Equal(99_000m, s.Balance);
        Assert.Equal(12_345.67m, s.StartingBalance);
    }

    [Fact]
    public void ApplyExternalWatchSymbols_overrides_catalog_until_stock_kind_changes()
    {
        var s = new AutoTradeSessionService { StockKind = StockMarketKind.나스닥 };
        s.ApplyExternalWatchSymbols(["TSLA", "aapl", "TSLA"]);
        var watch = s.ResolveWatchSymbols();
        Assert.Equal(2, watch.Length);
        Assert.Contains("TSLA", watch);
        Assert.Contains("AAPL", watch);

        s.FocusSymbol = "TSLA";
        Assert.Equal("TSLA", s.ResolveFocusSymbol());
        Assert.Contains("TSLA", s.ToPanelSnapshot().WatchSymbolsText, StringComparison.Ordinal);

        // Stock kind change clears external watch override
        s.StockKind = StockMarketKind.나스닥코어3;
        var core = s.ResolveWatchSymbols();
        Assert.Equal(3, core.Length);
        Assert.Contains("QQQ", core);
    }

    [Fact]
    public void Candle_factory_creates_ohlc_series()
    {
        var candles = MockCandleSeriesFactory.CreateSeries("AAPL", 50, DateTimeOffset.UtcNow);
        Assert.Equal(50, candles.Count);
        Assert.True(candles[0].High >= candles[0].Low);
        var markers = MockCandleSeriesFactory.CreateDemoMarkers(candles);
        Assert.NotEmpty(markers);
        Assert.Contains(markers, m => m.Side == TradeMarkerSide.매수);
        Assert.Contains(markers, m => m.Side == TradeMarkerSide.매도);
    }

    [Fact]
    public void Bubble_markers_have_variable_size_weights()
    {
        var candles = MockCandleSeriesFactory.CreateSeries("NQ", 80, DateTimeOffset.UtcNow, seedPrice: 21950);
        var markers = MockCandleSeriesFactory.CreateDemoMarkers(candles);
        Assert.True(markers.Count >= candles.Count);
        Assert.Contains(markers, m => m.SizeWeight > 1.5);
        Assert.Contains(markers, m => m.Side == TradeMarkerSide.매수 && m.SizeWeight > 0);
        Assert.Contains(markers, m => m.Side == TradeMarkerSide.매도 && m.SizeWeight > 0);
        Assert.True(markers.Max(m => m.SizeWeight) > markers.Min(m => m.SizeWeight));
    }
}
