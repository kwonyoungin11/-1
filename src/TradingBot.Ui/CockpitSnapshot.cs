using TradingBot.Domain;

namespace TradingBot.Ui;

/// <summary>
/// Owner-facing home projection. Framework-agnostic (Blazor/Avalonia/etc. bind later).
/// Never carries raw secrets or unmasked full account numbers.
/// </summary>
public sealed class CockpitSnapshot
{
    public required BotLifecycleState BotState { get; init; }
    public required string BotStateOwnerMessage { get; init; }
    public required LiveLockState LiveLock { get; init; }
    public required bool KillSwitchActive { get; init; }
    public required OrderMode OrderMode { get; init; }
    public required bool AllowLiveOrders { get; init; }
    public required string ConnectionSummary { get; init; }
    public required string MarketSessionSummary { get; init; }
    public required string AccountMaskedSummary { get; init; }
    public required int OrderCandidateCount { get; init; }
    public required string NextActionOwnerMessage { get; init; }
    public required IReadOnlyList<string> RecentBlockMessages { get; init; }
    public required IReadOnlyList<string> RecentAuditLines { get; init; }

    /// <summary>Safe defaults for harness / Phase 1 — live path closed.</summary>
    public static CockpitSnapshot CreateSafeDefault() => new()
    {
        BotState = BotLifecycleState.HarnessReady,
        BotStateOwnerMessage = "기본 준비 완료. 토스 읽기 연결과 화면 구조 승인 단계입니다.",
        LiveLock = LiveLockState.Locked,
        KillSwitchActive = true,
        OrderMode = OrderMode.DryRun,
        AllowLiveOrders = false,
        ConnectionSummary = "API 미연결 (읽기 전용 연결 전)",
        MarketSessionSummary = "시장 정보 없음 — 연결 후 표시",
        AccountMaskedSummary = "계좌 미연결",
        OrderCandidateCount = 0,
        NextActionOwnerMessage = "화면 구조를 확인·승인한 뒤, 토스 읽기 연결(Phase 2)로 진행하세요.",
        RecentBlockMessages = new[]
        {
            "실거래 잠금 활성 — 실제 주문 불가",
            "긴급 정지 ON — 실거래 차단",
            "주문 모드 dry_run — 연습만 가능",
        },
        RecentAuditLines = new[]
        {
            "시스템 — cockpit 안전 기본값 로드",
            "시스템 — live order 경로 비활성",
        },
    };

    public bool IsLiveTradingVisuallyOpen =>
        LiveLock == LiveLockState.Unlocked
        && !KillSwitchActive
        && AllowLiveOrders
        && OrderMode == OrderMode.Live;

    public string SafetyHeadline =>
        IsLiveTradingVisuallyOpen
            ? "주의: 실거래 관련 설정이 열린 상태입니다. 게이트·승인·한도를 확인하세요."
            : "실거래 잠김 — 지금 화면에서는 실제 주문이 나가지 않습니다.";
}
