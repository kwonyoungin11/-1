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

    public static string Describe(TradingStrategyKind kind) => kind switch
    {
        TradingStrategyKind.관망만 => "신호·후보 없음 · 차트·잔액만 관찰",
        TradingStrategyKind.단순연습전략 => "연습용 단순 매수 후보 (파이프라인 검증)",
        TradingStrategyKind.추세추종 => "단기 상승 추세 → 매수, 하락 추세 → 매도 후보 (연습 파라미터 · 투자 조언 아님)",
        TradingStrategyKind.평균회귀 => "단기 과열 → 매도, 과매도 → 매수 후보 (반대 매매)",
        TradingStrategyKind.모멘텀돌파 => "변동 큰 구간 돌파 후보 · 규모(버블) 확대",
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
