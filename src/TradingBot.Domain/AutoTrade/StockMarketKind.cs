namespace TradingBot.Domain;

/// <summary>대상 주식 종류 (관심 종목 묶음).</summary>
public enum StockMarketKind
{
    나스닥 = 0,
    미국주식 = 1,
    국내주식 = 2,
    나스닥테크 = 3,
    미국ETF = 4,
}
