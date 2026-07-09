namespace TradingBot.Domain;

/// <summary>대상 주식 종류 (관심 종목 묶음).</summary>
public enum StockMarketKind
{
    나스닥 = 0,
    미국주식 = 1,
    국내주식 = 2,
    나스닥테크 = 3,
    미국ETF = 4,
    /// <summary>나스닥 코어 3종 프리셋 (연습 유니버스, 투자 권유 아님).</summary>
    나스닥코어3 = 5,
}
