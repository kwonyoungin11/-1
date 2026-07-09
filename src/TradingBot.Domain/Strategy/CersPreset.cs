namespace TradingBot.Domain;

/// <summary>
/// CERS (Cost-aware Edge Reversion Score) domain preset.
/// Matches backtest <c>CersStrategy</c> / <c>CustomMathIndicators.Cers</c> defaults.
/// Practice signals only · live orders remain gated. Not investment advice.
/// </summary>
public static class CersPreset
{
    public const int EmaPeriod = 21;
    public const int RsiPeriod = 14;
    public const int AtrPeriod = 14;
    public const int VolSmaPeriod = 20;
    public const int AutocorrWindow = 30;

    public const decimal FeeRatePerSide = 0.001m;
    public const decimal SlippageRatePerSide = 0.0005m;

    /// <summary>Round-trip cost fraction: 2 × (fee + slippage) ≈ 0.003.</summary>
    public const double RoundTripCost = 0.003;

    public const double ThresholdMultiple = 2.0;

    /// <summary>Entry when expected edge &gt; ThresholdMultiple × RoundTripCost (= 0.006).</summary>
    public const double EntryThreshold = 0.006;

    public const double StopLossPct = 0.012;
    public const double TakeProfitExpectedMultiple = 1.5;
    public const int MaxHoldBars = 40;

    public static ChartTimeframe Timeframe => ChartTimeframe.분봉1;

    public static TradingStrategyKind Strategy => TradingStrategyKind.CERS비용회귀;

    /// <summary>Owner-facing one-liner (Korean). Practice · live gated · not advice.</summary>
    public static string OwnerSummary =>
        "CERS 비용인식 평균회귀 · 1분봉 · 진입=엣지>2×왕복비용 · SL 1.2% · 실주문 게이트 잠금 · 투자 조언 아님";
}
