using TradingBot.Application;
using TradingBot.Domain;

namespace TradingBot.Application.Tests;

public class AutoTradeSessionServiceTests
{
    [Fact]
    public void Start_and_stop_session()
    {
        var s = new AutoTradeSessionService();
        Assert.True(s.TryStart(out var msg));
        Assert.Contains("실주문", msg, StringComparison.Ordinal);
        Assert.Contains("SPCX", msg, StringComparison.Ordinal);
        Assert.Equal(AutoTradeSessionStatus.실행중, s.Status);
        Assert.True(s.TryStop(out _));
        Assert.Equal(AutoTradeSessionStatus.중지, s.Status);
    }

    [Fact]
    public void Panel_has_spacex_fields()
    {
        var s = new AutoTradeSessionService();
        s.StockKind = StockMarketKind.스페이스X;
        s.Strategy = TradingStrategyKind.추세추종;
        s.Timeframe = ChartTimeframe.분봉1;
        var p = s.ToPanelSnapshot();
        Assert.Equal("스페이스X", p.StockKindLabel);
        Assert.Equal("추세추종", p.StrategyLabel);
        Assert.Contains("잔액", p.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("%", p.ReturnRateLabel, StringComparison.Ordinal);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, p.FocusSymbol);
        Assert.True(p.CanStart);
    }

    [Fact]
    public void ApplyExternalBalance_updates_panel_and_optional_starting()
    {
        var s = new AutoTradeSessionService();
        Assert.Equal(AutoTradeSessionService.DefaultPracticeStartingBalance, s.StartingBalance);
        Assert.Equal(AutoTradeSessionService.DefaultPracticeStartingBalance, s.Balance);

        s.SetDataSourceLabel("토스 실계좌");
        s.ApplyExternalBalance(12_345.67m, setStartingIfUnset: true);

        Assert.Equal(12_345.67m, s.Balance);
        Assert.Equal(12_345.67m, s.StartingBalance);

        var p = s.ToPanelSnapshot();
        Assert.Equal(12_345.67m, p.Balance);
        Assert.Contains("12,345.67", p.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("토스 실계좌", p.BalanceLabel, StringComparison.Ordinal);
        Assert.Contains("실주문", p.SafetyNote, StringComparison.Ordinal);

        s.ApplyExternalBalance(99_000m, setStartingIfUnset: true);
        Assert.Equal(99_000m, s.Balance);
        Assert.Equal(12_345.67m, s.StartingBalance);
    }

    [Fact]
    public void Watch_and_focus_are_always_spcx()
    {
        var s = new AutoTradeSessionService { StockKind = StockMarketKind.스페이스X };
        s.ApplyExternalWatchSymbols(["TSLA", "AAPL", "SPCX"]);
        var watch = s.ResolveWatchSymbols();
        Assert.Single(watch);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, watch[0]);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, s.ResolveFocusSymbol());

        s.FocusSymbol = "TSLA";
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, s.ResolveFocusSymbol());
    }

    [Fact]
    public void Candle_factory_creates_ohlc_series()
    {
        var candles = MockCandleSeriesFactory.CreateSeries("SPCX", 50, DateTimeOffset.UtcNow);
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
        var candles = MockCandleSeriesFactory.CreateSeries("SPCX", 80, DateTimeOffset.UtcNow, seedPrice: 85);
        var markers = MockCandleSeriesFactory.CreateDemoMarkers(candles);
        Assert.True(markers.Count >= candles.Count);
        Assert.Contains(markers, m => m.SizeWeight > 1.5);
        Assert.Contains(markers, m => m.Side == TradeMarkerSide.매수 && m.SizeWeight > 0);
    }
}
