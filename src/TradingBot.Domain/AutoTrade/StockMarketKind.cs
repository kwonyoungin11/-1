namespace TradingBot.Domain;

/// <summary>대상 주식 종류. 스페이스X(SPCX) + 비전마린(VMAR) 연습 유니버스.</summary>
public enum StockMarketKind
{
    /// <summary>스페이스X 상장 티커 SPCX.</summary>
    스페이스X = 0,

    /// <summary>비전 마린 테크놀로지 티커 VMAR — 1분봉 분할매매 연습.</summary>
    비전마린 = 1,
}
