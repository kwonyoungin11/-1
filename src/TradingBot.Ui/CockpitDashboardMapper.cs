using TradingBot.Domain;

namespace TradingBot.Ui;

/// <summary>
/// Maps domain risk / evaluated candidates into owner-facing cockpit rows.
/// Fail-closed: null or empty inputs never invent a "live open" state.
/// Does not call order routers or Toss order APIs.
/// </summary>
public static class CockpitDashboardMapper
{
    /// <summary>Default home risk rows when only safe defaults are known (live path blocked).</summary>
    public static IReadOnlyList<RiskGateRowViewModel> MapDefaultSafetyGateRows() =>
        MapSafetyGateRows(TradingSafetySettings.CreateSafeDefaults());

    /// <summary>
    /// Safety strip gates from settings. Passed = clear for live path.
    /// Defaults and missing settings keep Passed=false for live-critical rows (fail-closed).
    /// </summary>
    public static IReadOnlyList<RiskGateRowViewModel> MapSafetyGateRows(TradingSafetySettings? safety)
    {
        safety ??= TradingSafetySettings.CreateSafeDefaults();

        return new[]
        {
            new RiskGateRowViewModel
            {
                Code = BlockedReason.KillSwitchActive.Code,
                Title = "긴급 정지",
                OwnerMessage = safety.KillSwitch
                    ? "긴급 정지가 켜져 있어 실거래가 막혀 있습니다."
                    : "긴급 정지가 꺼져 있습니다. (다른 게이트도 확인하세요.)",
                Passed = !safety.KillSwitch,
                Severity = safety.KillSwitch ? "block" : "info",
            },
            new RiskGateRowViewModel
            {
                Code = BlockedReason.LiveOrdersNotAllowed.Code,
                Title = "실거래 허용 플래그",
                OwnerMessage = safety.AllowLiveOrders
                    ? "실거래 허용 플래그가 켜져 있습니다. (주의 — 다른 잠금도 확인)"
                    : "실거래 허용 플래그가 꺼져 있어 실제 주문이 불가합니다.",
                Passed = safety.AllowLiveOrders,
                Severity = safety.AllowLiveOrders ? "warning" : "block",
            },
            new RiskGateRowViewModel
            {
                Code = BlockedReason.OrderModeNotLive.Code,
                Title = "주문 모드",
                OwnerMessage = safety.OrderMode == OrderMode.Live
                    ? "주문 모드가 live 입니다. (실주문 경로는 별도 잠금·승인 필요)"
                    : $"주문 모드가 {FormatOrderMode(safety.OrderMode)} 입니다 — 연습/가상만 가능.",
                Passed = safety.OrderMode == OrderMode.Live,
                Severity = safety.OrderMode == OrderMode.Live ? "warning" : "block",
            },
            new RiskGateRowViewModel
            {
                Code = "live_lock",
                Title = "실거래 잠금",
                OwnerMessage = "실거래 잠금이 기본으로 켜져 있습니다. 홈에서는 실제 주문이 나가지 않습니다.",
                Passed = false,
                Severity = "block",
            },
        };
    }

    /// <summary>
    /// Maps a risk decision into rows. Allowed with no blocks → single info row.
    /// Null decision → fail-closed unknown block row.
    /// </summary>
    public static IReadOnlyList<RiskGateRowViewModel> MapRiskDecision(RiskDecision? decision)
    {
        if (decision is null)
        {
            return new[]
            {
                FromBlockedReason(BlockedReason.UnknownState, passed: false),
            };
        }

        if (decision.Allowed && decision.Blocks.Count == 0)
        {
            return new[]
            {
                new RiskGateRowViewModel
                {
                    Code = "risk_clear",
                    Title = "리스크 검사",
                    OwnerMessage = "현재 검사 기준에서는 후보 경로가 허용됩니다. (실주문 아님)",
                    Passed = true,
                    Severity = "info",
                },
            };
        }

        if (decision.Blocks.Count == 0)
        {
            // Fail-closed: blocked without reasons still shows unknown.
            return new[]
            {
                FromBlockedReason(BlockedReason.UnknownState, passed: false),
            };
        }

        return decision.Blocks.Select(b => FromBlockedReason(b, passed: false)).ToArray();
    }

    public static RiskGateRowViewModel FromBlockedReason(BlockedReason reason, bool passed = false)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return new RiskGateRowViewModel
        {
            Code = reason.Code,
            Title = TitleForCode(reason.Code),
            OwnerMessage = OwnerMessageFor(reason),
            Passed = passed,
            Severity = passed ? "info" : "block",
        };
    }

    /// <summary>Maps evaluated candidates. Null/empty → empty list (not live).</summary>
    public static IReadOnlyList<OrderCandidateRowViewModel> MapCandidates(
        IReadOnlyList<EvaluatedOrderCandidate>? candidates)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return Array.Empty<OrderCandidateRowViewModel>();
        }

        return candidates.Select(FromEvaluated).ToArray();
    }

    public static OrderCandidateRowViewModel FromEvaluated(EvaluatedOrderCandidate evaluated)
    {
        ArgumentNullException.ThrowIfNull(evaluated);

        var c = evaluated.Candidate;
        return new OrderCandidateRowViewModel
        {
            Symbol = string.IsNullOrWhiteSpace(c.Symbol) ? "(종목 없음)" : c.Symbol,
            Side = string.IsNullOrWhiteSpace(c.Side) ? "?" : c.Side,
            Quantity = c.Quantity,
            LimitPrice = c.LimitPrice,
            ClientOrderId = c.ClientOrderId,
            Status = string.IsNullOrWhiteSpace(evaluated.OwnerStatusMessage)
                ? (evaluated.Risk.Allowed ? "dry-run 후보 (실주문 아님)" : "risk 차단 — 상세 없음")
                : evaluated.OwnerStatusMessage,
        };
    }

    /// <summary>
    /// Compose home dashboard. Keeps snapshot live-lock as provided (caller should keep Locked).
    /// Null risk/candidates → safe empty/default rows; never marks candidates live.
    /// </summary>
    public static CockpitDashboardModel Compose(
        CockpitSnapshot snapshot,
        TradingSafetySettings? safety = null,
        IReadOnlyList<EvaluatedOrderCandidate>? candidates = null,
        RiskDecision? extraRiskDecision = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var safetyRows = MapSafetyGateRows(safety);
        var riskRows = new List<RiskGateRowViewModel>(safetyRows);
        if (extraRiskDecision is not null)
        {
            foreach (var row in MapRiskDecision(extraRiskDecision))
            {
                if (riskRows.Any(r => r.Code == row.Code))
                {
                    continue;
                }

                riskRows.Add(row);
            }
        }

        var candidateRows = MapCandidates(candidates);
        var orderCount = candidateRows.Count;

        // Keep count in sync when caller did not already set it from the same list.
        var projected = snapshot;
        if (snapshot.OrderCandidateCount != orderCount && candidates is not null)
        {
            projected = CloneSnapshotWithCandidateCount(snapshot, orderCount);
        }

        return new CockpitDashboardModel
        {
            Snapshot = projected,
            RiskGates = riskRows,
            OrderCandidates = candidateRows,
        };
    }

    private static CockpitSnapshot CloneSnapshotWithCandidateCount(CockpitSnapshot source, int count) =>
        new()
        {
            BotState = source.BotState,
            BotStateOwnerMessage = source.BotStateOwnerMessage,
            LiveLock = source.LiveLock,
            KillSwitchActive = source.KillSwitchActive,
            OrderMode = source.OrderMode,
            AllowLiveOrders = source.AllowLiveOrders,
            ConnectionSummary = source.ConnectionSummary,
            MarketSessionSummary = source.MarketSessionSummary,
            AccountMaskedSummary = source.AccountMaskedSummary,
            OrderCandidateCount = count,
            NextActionOwnerMessage = source.NextActionOwnerMessage,
            RecentBlockMessages = source.RecentBlockMessages,
            RecentAuditLines = source.RecentAuditLines,
        };

    private static string FormatOrderMode(OrderMode mode) => mode switch
    {
        OrderMode.DryRun => "dry_run",
        OrderMode.Paper => "paper",
        OrderMode.Live => "live",
        _ => mode.ToString().ToLowerInvariant(),
    };

    private static string TitleForCode(string code) => code switch
    {
        "kill_switch_active" => "긴급 정지",
        "live_orders_not_allowed" => "실거래 허용 플래그",
        "order_mode_not_live" => "주문 모드",
        "manual_approval_missing" => "수동 승인",
        "unknown_state" => "상태 불명",
        "missing_data" => "데이터 부족",
        "stale_market_data" => "시세 지연",
        "api_error" => "API 오류",
        "live_implementation_disabled" => "실주문 구현 비활성",
        "max_order_notional_exceeded" => "주문 금액 한도",
        "max_position_size_exceeded" => "포지션 한도",
        "market_session_closed" => "장 시간",
        "candidate_blocked_by_risk" => "후보 리스크 차단",
        "invalid_signal" => "신호 무효",
        _ => code,
    };

    private static string OwnerMessageFor(BlockedReason reason) => reason.Code switch
    {
        "kill_switch_active" => "긴급 정지가 켜져 있어 실거래가 막혀 있습니다.",
        "live_orders_not_allowed" => "실거래 허용이 꺼져 있어 실제 주문이 불가합니다.",
        "order_mode_not_live" => "주문 모드가 live가 아니어서 실주문이 막혀 있습니다.",
        "manual_approval_missing" => "수동 승인이 없어 실거래 경로가 막혀 있습니다.",
        "unknown_state" => "시스템 상태를 알 수 없어 안전하게 차단했습니다.",
        "missing_data" => "필요한 데이터가 없어 차단했습니다.",
        "stale_market_data" => "시세가 오래되어 차단했습니다.",
        "api_error" => "API 오류로 차단했습니다. 실주문은 하지 않습니다.",
        "live_implementation_disabled" => "이 단계에서는 실주문 구현이 꺼져 있습니다.",
        "max_order_notional_exceeded" => "주문 금액이 한도를 넘어 차단했습니다.",
        "max_position_size_exceeded" => "포지션 한도를 넘어 차단했습니다.",
        "market_session_closed" => "장 시간이 아니거나 알 수 없어 차단했습니다.",
        "candidate_blocked_by_risk" => "주문 후보가 리스크 검사에서 막혔습니다.",
        "invalid_signal" => "전략 신호가 불완전하여 차단했습니다.",
        _ => string.IsNullOrWhiteSpace(reason.Message)
            ? "차단됨 — 상세 없음 (fail-closed)"
            : reason.Message,
    };
}
