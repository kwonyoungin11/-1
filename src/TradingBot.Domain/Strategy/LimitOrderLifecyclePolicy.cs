namespace TradingBot.Domain;

/// <summary>What to do with a working LIMIT order / plan.</summary>
public enum WorkingOrderAction
{
    Hold = 0,
    Cancel = 1,
    Reprice = 2,
    CancelAndStandDown = 3,
    BlockNewEntries = 4,
}

/// <summary>Why a lifecycle decision was made (owner-safe, no secrets).</summary>
public enum WorkingOrderReason
{
    None = 0,
    TtlExpired = 1,
    AdversePriceMove = 2,
    FavorableCanReprice = 3,
    SignalReversed = 4,
    KillSwitchOrDailyLoss = 5,
    StaleData = 6,
    PartialFillRecalc = 7,
    NewsDayReduce = 8,
    SymbolWarning = 9,
    SessionClosed = 10,
    StillValid = 11,
}

/// <summary>
/// Decision for an unfilled or partially filled LIMIT order.
/// Simulation / paper / future live working-order manager.
/// Not investment advice. Does not place live orders by itself.
/// </summary>
public sealed record WorkingOrderDecision(
    WorkingOrderAction Action,
    WorkingOrderReason Reason,
    decimal? NewLimitPrice,
    string OwnerMessage);

/// <summary>
/// Professional default policy for LIMIT entry lifecycle (SPCX-oriented).
/// Fail-closed: ambiguous → cancel / stand down.
/// </summary>
public static class LimitOrderLifecyclePolicy
{
    /// <summary>Default TTL = 2 bars of official 15m timeframe.</summary>
    public static TimeSpan DefaultTtl { get; } =
        TimeSpan.FromTicks(ChartTimeframeCatalog.BarDuration(ChartTimeframe.분봉15).Ticks * 2);

    /// <summary>Max chase toward market as fraction of ATR (long: raise limit up to this).</summary>
    public const decimal MaxChaseAtrFraction = 0.25m;

    /// <summary>
    /// Evaluate unfilled long LIMIT entry.
    /// </summary>
    /// <param name="side">BUY or SELL</param>
    /// <param name="limitPrice">Working limit</param>
    /// <param name="lastPrice">Latest trade/mid</param>
    /// <param name="submittedAtUtc">Order submit time</param>
    /// <param name="nowUtc">Now</param>
    /// <param name="ttl">Time-to-live</param>
    /// <param name="atr">Optional ATR for chase cap</param>
    /// <param name="signalStillValid">Strategy still wants this side</param>
    /// <param name="killOrDailyLoss">Hard risk stop</param>
    /// <param name="dataStale">Quotes/candles too old</param>
    /// <param name="sessionOpen">US regular session open for entries</param>
    /// <param name="symbolWarningActive">Toss warnings / halt flags</param>
    /// <param name="newsDay">Owner news-day: no chase, prefer cancel</param>
    /// <param name="partialFilledQty">Already filled quantity</param>
    /// <param name="remainingQty">Leaves quantity</param>
    public static WorkingOrderDecision EvaluateUnfilledLongLimit(
        decimal limitPrice,
        decimal lastPrice,
        DateTimeOffset submittedAtUtc,
        DateTimeOffset nowUtc,
        TimeSpan? ttl = null,
        double? atr = null,
        bool signalStillValid = true,
        bool killOrDailyLoss = false,
        bool dataStale = false,
        bool sessionOpen = true,
        bool symbolWarningActive = false,
        bool newsDay = false,
        decimal partialFilledQty = 0m,
        decimal remainingQty = 0m)
    {
        if (killOrDailyLoss)
        {
            return Decide(
                WorkingOrderAction.CancelAndStandDown,
                WorkingOrderReason.KillSwitchOrDailyLoss,
                "킬스위치/일손실 — 미체결 취소 · 신규 금지 · 실주문 경로와 무관한 정책 평가");
        }

        if (dataStale)
        {
            return Decide(
                WorkingOrderAction.CancelAndStandDown,
                WorkingOrderReason.StaleData,
                "시세 지연/단절 — 미체결 취소 · 장님 매매 금지");
        }

        if (symbolWarningActive)
        {
            return Decide(
                WorkingOrderAction.CancelAndStandDown,
                WorkingOrderReason.SymbolWarning,
                "종목 경고/주의 플래그 — 미체결 취소 · 신규 금지");
        }

        if (!sessionOpen)
        {
            return Decide(
                WorkingOrderAction.Cancel,
                WorkingOrderReason.SessionClosed,
                "정규 세션 아님 — 미체결 취소 권고");
        }

        if (!signalStillValid)
        {
            return Decide(
                WorkingOrderAction.Cancel,
                WorkingOrderReason.SignalReversed,
                "신호 무효/반전 — 미체결 진입 취소");
        }

        var life = ttl ?? DefaultTtl;
        if (nowUtc - submittedAtUtc >= life)
        {
            return Decide(
                WorkingOrderAction.Cancel,
                WorkingOrderReason.TtlExpired,
                $"TTL {life.TotalMinutes:0}분 만료 — 취소 후 재평가 · 시장가 추격 금지");
        }

        if (partialFilledQty > 0m && remainingQty > 0m)
        {
            return Decide(
                WorkingOrderAction.Cancel,
                WorkingOrderReason.PartialFillRecalc,
                "부분 체결 — 잔량 취소 후 체결분에 SL/TP 재계산 권고");
        }

        if (limitPrice <= 0m || lastPrice <= 0m)
        {
            return Decide(
                WorkingOrderAction.CancelAndStandDown,
                WorkingOrderReason.StaleData,
                "가격 데이터 비정상 — 취소");
        }

        // Long limit: want last <= limit to fill. If last >> limit → adverse (price ran away up).
        var adverse = lastPrice > limitPrice;
        var favorable = lastPrice < limitPrice;

        if (adverse)
        {
            var distance = lastPrice - limitPrice;
            var maxChase = atr is double a && a > 0
                ? (decimal)a * MaxChaseAtrFraction
                : limitPrice * 0.005m;

            // If only slightly above, allow single reprice toward market within chase cap
            if (!newsDay && distance <= maxChase)
            {
                var newLimit = Math.Round(Math.Min(lastPrice, limitPrice + maxChase), 2, MidpointRounding.AwayFromZero);
                if (newLimit > limitPrice)
                {
                    return new WorkingOrderDecision(
                        WorkingOrderAction.Reprice,
                        WorkingOrderReason.FavorableCanReprice,
                        newLimit,
                        $"소폭 불리 — LIMIT 재호가 ≤ ATR×{MaxChaseAtrFraction:0.##} 한도 · {newLimit:N2} · 시장가 금지");
                }
            }

            return Decide(
                WorkingOrderAction.Cancel,
                WorkingOrderReason.AdversePriceMove,
                "가격이 지정가 위로 이탈 — 취소 · 추격 시장가 금지 · 다음 신호 대기");
        }

        if (newsDay && favorable)
        {
            // On news day: do not extend life with reprice; hold only if already favorable
            return Decide(
                WorkingOrderAction.Hold,
                WorkingOrderReason.NewsDayReduce,
                "뉴스데이 — 재호가 없이 유지 또는 TTL 대기 · 사이즈 축소 정책 병행");
        }

        if (favorable || lastPrice == limitPrice)
        {
            return Decide(
                WorkingOrderAction.Hold,
                WorkingOrderReason.StillValid,
                "지정가 유효 — 유지 (체결 대기)");
        }

        return Decide(
            WorkingOrderAction.Hold,
            WorkingOrderReason.StillValid,
            "유지");
    }

    /// <summary>
    /// Aggregate contingency for "should we allow new entries right now?"
    /// </summary>
    public static WorkingOrderDecision EvaluateNewEntryGate(
        bool killOrDailyLoss,
        bool dataStale,
        bool sessionOpen,
        bool symbolWarningActive,
        bool newsDay,
        bool trendFilterOk)
    {
        if (killOrDailyLoss)
        {
            return Decide(
                WorkingOrderAction.BlockNewEntries,
                WorkingOrderReason.KillSwitchOrDailyLoss,
                "신규 진입 차단 — 킬스위치/일손실");
        }

        if (dataStale)
        {
            return Decide(
                WorkingOrderAction.BlockNewEntries,
                WorkingOrderReason.StaleData,
                "신규 진입 차단 — 데이터 불량");
        }

        if (!sessionOpen)
        {
            return Decide(
                WorkingOrderAction.BlockNewEntries,
                WorkingOrderReason.SessionClosed,
                "신규 진입 차단 — 세션");
        }

        if (symbolWarningActive)
        {
            return Decide(
                WorkingOrderAction.BlockNewEntries,
                WorkingOrderReason.SymbolWarning,
                "신규 진입 차단 — 종목 경고");
        }

        if (!trendFilterOk)
        {
            return Decide(
                WorkingOrderAction.BlockNewEntries,
                WorkingOrderReason.SignalReversed,
                "신규 진입 차단 — 추세/필터 미충족");
        }

        if (newsDay)
        {
            return Decide(
                WorkingOrderAction.Hold,
                WorkingOrderReason.NewsDayReduce,
                "뉴스데이 — 신규 허용 시 사이즈 50% 권고 · 자동 방향 결정에 뉴스 감성 사용 금지");
        }

        return Decide(
            WorkingOrderAction.Hold,
            WorkingOrderReason.StillValid,
            "신규 진입 게이트 통과 (정책 평가 · 실주문 아님)");
    }

    /// <summary>News-day size multiplier (owner contingency).</summary>
    public static decimal SizeMultiplier(bool newsDay, bool symbolWarningActive)
    {
        if (symbolWarningActive || false)
        {
            return 0m;
        }

        return newsDay ? 0.5m : 1.0m;
    }

    private static WorkingOrderDecision Decide(
        WorkingOrderAction action,
        WorkingOrderReason reason,
        string message) =>
        new(action, reason, NewLimitPrice: null, OwnerMessage: message);
}
