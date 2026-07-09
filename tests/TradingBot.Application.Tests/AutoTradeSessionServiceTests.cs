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
        Assert.Contains("%", p.ReturnRateLabel, StringComparison.Ordinal);
        Assert.True(p.CanStart);
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
}
