namespace TradingBot.Domain;

public enum TradeMarkerSide
{
    매수 = 0,
    매도 = 1,
}

/// <summary>
/// 차트 버블 마커 (연습 체결·주문흐름 표시).
/// <para>
/// <b>SizeWeight = 체결 규모</b> (거래대금 개념): 대략 <c>수량 × 가격</c> 또는 봉 거래대금.
/// 원이 클수록 그날/그 순간의 매수·매도 규모가 큼 (ChartFanatics 스타일).
/// </para>
/// </summary>
public sealed record TradeMarker(
    DateTimeOffset Time,
    double Price,
    TradeMarkerSide Side,
    string Label,
    double SizeWeight = 1.0)
{
    /// <summary>버블 크기용 규모 = max(0.01, 수량 × 가격).</summary>
    public static double NotionalSize(decimal quantity, decimal price) =>
        Math.Max(0.01, (double)(quantity * price));

    /// <summary>버블 크기용 규모 = 봉 거래대금 근사 (거래량 × 종가).</summary>
    public static double VolumeNotionalSize(double volume, double closePrice) =>
        Math.Max(0.01, volume * Math.Max(0.01, closePrice));
}
