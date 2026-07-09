namespace TradingBot.Domain;

/// <summary>대상 주식 종류. 스페이스X(SPCX) 단일 유니버스만 사용.</summary>
public enum StockMarketKind
{
    /// <summary>스페이스X 상장 티커 SPCX 전용 (그 외 종목 제거).</summary>
    스페이스X = 0,
}
