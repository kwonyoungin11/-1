namespace TradingBot.Domain;

/// <summary>매매 전략 목록·설명·기본 수량 정책 (투자 조언 아님).</summary>
public static class StrategyCatalog
{
    public static IReadOnlyList<TradingStrategyKind> All { get; } =
    [
        TradingStrategyKind.관망만,
        TradingStrategyKind.단순연습전략,
        TradingStrategyKind.추세추종,
        TradingStrategyKind.평균회귀,
        TradingStrategyKind.모멘텀돌파,
    ];

    public static IReadOnlyList<string> Labels { get; } =
        All.Select(s => s.ToString()).ToArray();

    /// <summary>SPCX 최종 확정 전략 (see <see cref="SpacexOfficialStrategyPreset"/>).</summary>
    public static TradingStrategyKind RecommendedForSpacex => SpacexOfficialStrategyPreset.Strategy;

    public static string Describe(TradingStrategyKind kind) => kind switch
    {
        TradingStrategyKind.관망만 =>
            "신호 없음 · 차트·잔액만 · 수수료 0 · 데이터 불안정 시 안전",
        TradingStrategyKind.단순연습전략 =>
            "파이프라인 검증용 · SPCX 실전 1순위 아님",
        TradingStrategyKind.추세추종 =>
            "★ SPCX 권장 · 추세 방향 + LIMIT·ATR손절·2R익절 · 15m/60m 권장 · 투자 조언 아님",
        TradingStrategyKind.평균회귀 =>
            "과열 반대 매매 · SPCX 고변동·강한 추세에선 칼날 위험 · 주의",
        TradingStrategyKind.모멘텀돌파 =>
            "강한 돌파 구간 보조 · 수량↑ · 거짓 돌파·수수료 주의 · 투자 조언 아님",
        _ => "전략",
    };

    /// <summary>관망이 아니면 기본 주문 수량 시드 (전략이 재조정).</summary>
    public static decimal BaseQuantity(TradingStrategyKind kind) => kind switch
    {
        TradingStrategyKind.관망만 => 0m,
        TradingStrategyKind.단순연습전략 => 1m,
        TradingStrategyKind.추세추종 => 2m,
        TradingStrategyKind.평균회귀 => 2m,
        TradingStrategyKind.모멘텀돌파 => 3m,
        _ => 1m,
    };
}
