namespace TradingBot.Domain;

/// <summary>
/// 매매 전략 모드 (연습 시스템 설정). 투자 조언이 아닙니다.
/// </summary>
public enum TradingStrategyKind
{
    /// <summary>매수/매도 후보를 만들지 않음.</summary>
    관망만 = 0,

    /// <summary>파이프라인 검증용 단순 매수 후보 (연습).</summary>
    단순연습전략 = 1,

    /// <summary>단기 추세 방향에 맞춰 매수/매도 후보 (연습).</summary>
    추세추종 = 2,

    /// <summary>단기 과열/과매도 구간 반대 방향 후보 (연습).</summary>
    평균회귀 = 3,

    /// <summary>변동 큰 구간에서 규모↑ 돌파 후보 (연습 · 버블 크게).</summary>
    모멘텀돌파 = 4,
}
