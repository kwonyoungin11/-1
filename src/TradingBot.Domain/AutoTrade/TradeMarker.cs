namespace TradingBot.Domain;

public enum TradeMarkerSide
{
    매수 = 0,
    매도 = 1,
}

/// <summary>차트에 표시할 매수/매도 마커 (연습 체결).</summary>
public sealed record TradeMarker(
    DateTimeOffset Time,
    double Price,
    TradeMarkerSide Side,
    string Label);
