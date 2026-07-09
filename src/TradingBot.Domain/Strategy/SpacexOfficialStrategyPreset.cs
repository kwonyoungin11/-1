namespace TradingBot.Domain;

/// <summary>
/// Final locked-in SPCX automated-cockpit strategy (pro reasoning + post-IPO research).
/// Not investment advice. Live orders remain gated.
/// </summary>
public static class SpacexOfficialStrategyPreset
{
    public const string Version = "2026.07-final-v1";

    public static TradingStrategyKind Strategy => TradingStrategyKind.추세추종;

    public static ChartTimeframe Timeframe => ChartTimeframe.분봉15;

    public static ChartTimeframe AlternateTimeframe => ChartTimeframe.분봉60;

    public static SpacexRiskParameters Risk => SpacexRiskParameters.CreateSafeDefaults();

    public static TrendFollowParameters Trend => TrendFollowParameters.CreateSafeDefaults();

    /// <summary>Owner-facing one-liner (Korean).</summary>
    public static string OwnerSummary =>
        "최종 전략: 추세추종 · 15m(대안 60m) · 지정가 LIMIT · ATR×1.5 손절 · 2R 익절 · 1% 리스크 · " +
        "수수료 반영 · 1m 스캘핑·평균회귀 기본 비권장 · 실주문 잠금 · 투자 조언 아님";

    public static string RationaleBullets =>
        "SPCX 포스트IPO 고변동·조정/박스 혼재 → 추세 프레임+넓은 손절. " +
        "왕복 수수료~0.2% → 스캘핑 불리. 평균회귀는 이벤트 역방향 위험. " +
        "LIMIT+사이징은 실행·생존 우선.";
}
