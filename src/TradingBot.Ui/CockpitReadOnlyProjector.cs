using TradingBot.Domain;

namespace TradingBot.Ui;

/// <summary>Maps read-only portfolio data into owner cockpit home projection.</summary>
public static class CockpitReadOnlyProjector
{
    public static CockpitSnapshot Project(ReadOnlyPortfolioSnapshot portfolio, TradingSafetySettings safety)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(safety);

        var baseSnap = CockpitSnapshot.CreateSafeDefault();
        var botState = portfolio.ConnectionStatus switch
        {
            ConnectionStatus.MockConnected => BotLifecycleState.ReadOnlyConnected,
            ConnectionStatus.LiveReadOnlyConnected => BotLifecycleState.ReadOnlyConnected,
            ConnectionStatus.Error => BotLifecycleState.Error,
            ConnectionStatus.Blocked => BotLifecycleState.Degraded,
            _ => BotLifecycleState.AwaitingReadOnlyConnect,
        };

        var accountSummary = portfolio.Accounts.Count == 0
            ? "계좌 미연결"
            : string.Join(", ", portfolio.Accounts.Select(a => $"{a.AccountNoMasked}({a.AccountType})"));

        if (!string.IsNullOrEmpty(portfolio.MarketValueUsdSummary))
        {
            accountSummary += $" · 평가(USD) {portfolio.MarketValueUsdSummary}";
        }

        var market = portfolio.UsMarket?.OwnerMessage ?? "시장 정보 없음";

        var blocks = new List<string>
        {
            safety.KillSwitch ? "긴급 정지 ON — 실거래 차단" : "긴급 정지 OFF",
            safety.AllowLiveOrders ? "실거래 허용 플래그가 켜져 있음 (주의)" : "실거래 허용 플래그 꺼짐",
            $"주문 모드 {safety.OrderMode}",
        };
        blocks.AddRange(portfolio.BlockMessages);

        var next = portfolio.ConnectionStatus switch
        {
            ConnectionStatus.Disconnected => "토스 읽기 연결(mock 또는 승인된 실 HTTP)을 진행하세요.",
            ConnectionStatus.Error => "연결 오류를 확인하세요. 실주문은 하지 않습니다.",
            ConnectionStatus.MockConnected => "mock 읽기 정상. 실 read-only는 별도 승인 후.",
            ConnectionStatus.LiveReadOnlyConnected => "읽기 전용 연결됨. 주문 후보는 아직 다음 단계.",
            _ => baseSnap.NextActionOwnerMessage,
        };

        var audit = new List<string>
        {
            $"시스템 — read-only 스냅샷 {portfolio.AsOfUtc:u}",
            $"시스템 — 연결 상태 {portfolio.ConnectionStatus}",
        };

        return new CockpitSnapshot
        {
            BotState = botState,
            BotStateOwnerMessage = portfolio.ConnectionOwnerMessage,
            LiveLock = LiveLockState.Locked,
            KillSwitchActive = safety.KillSwitch,
            OrderMode = safety.OrderMode,
            AllowLiveOrders = safety.AllowLiveOrders,
            ConnectionSummary = portfolio.ConnectionOwnerMessage,
            MarketSessionSummary = market,
            AccountMaskedSummary = accountSummary,
            OrderCandidateCount = 0,
            NextActionOwnerMessage = next,
            RecentBlockMessages = blocks,
            RecentAuditLines = audit,
        };
    }
}
