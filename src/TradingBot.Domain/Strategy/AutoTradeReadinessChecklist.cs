namespace TradingBot.Domain;

/// <summary>
/// What is required vs optional vs rejected for safe auto-trading (owner education + gates).
/// Not investment advice.
/// </summary>
public static class AutoTradeReadinessChecklist
{
    public static IReadOnlyList<string> Required { get; } =
    [
        "신선 시세·봉",
        "재현 가능 신호 규칙",
        "1회 리스크·일손실·사이징",
        "LIMIT+SL/TP 계획",
        "미체결 TTL/취소/재호가 한도",
        "체결 후 청산 관리",
        "킬스위치·fail-closed",
        "감사 로그(시크릿 없음)",
        "수수료 인지",
        "세션 필터",
    ];

    public static IReadOnlyList<string> OptionalUseful { get; } =
    [
        "거래량/버블 필터",
        "횡보(ADX) 필터",
        "호가 스프레드 가드",
        "warnings 하드 블록",
        "뉴스데이 수동 토글",
        "이벤트 캘린더",
    ];

    public static IReadOnlyList<string> NotRequiredOrHarmful { get; } =
    [
        "뉴스 감성으로 자동 매수/매도",
        "지표 과다 스택",
        "시장가 기본 진입",
        "소셜 루머 피드",
        "장식 전용 UI",
        "TTL 없는 좀비 지정가",
    ];

    public static string OwnerSummary =>
        "필수=데이터·규칙·리스크·주문수명·청산·킬스위치. " +
        "뉴스는 차단/축소만. 감성 자동매매 금지. 실주문 잠금.";
}
