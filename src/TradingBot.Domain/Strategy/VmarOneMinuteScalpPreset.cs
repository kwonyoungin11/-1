namespace TradingBot.Domain;

/// <summary>
/// VMAR 15분봉 분할매수·분할매도 연습 프리셋.
/// Not investment advice. Live orders remain gated.
/// </summary>
public static class VmarOneMinuteScalpPreset
{
    public const string Symbol = WatchlistCatalog.VmarSymbol;

    public static TradingStrategyKind Strategy => TradingStrategyKind.일분분할스캘프;

    public static ChartTimeframe Timeframe => ChartTimeframe.분봉15;

    /// <summary>분할 레그 수 (매수/매도 각각).</summary>
    public const int LegCount = 3;

    /// <summary>레그 간 가격 간격 (%). 0.10 = 0.1%.</summary>
    public const decimal PriceStepPercent = 0.10m;

    /// <summary>스캘프 연습용 타이트 리스크 (1%보다 작음).</summary>
    public const decimal RiskPercentPerTrade = 0.5m;

    /// <summary>Owner-facing one-liner (Korean). Practice only · live locked · not advice.</summary>
    public static string OwnerSummary =>
        "VMAR 15분봉 분할매수·분할매도 연습 · 3레그 · 0.1% 간격 · 0.5% 리스크 · 실주문 잠금 · 투자 조언 아님";
}
