using TradingBot.Domain;

namespace TradingBot.Risk;

/// <summary>
/// Fail-closed evaluation of US (NASDAQ) market session for order-candidate gating.
/// Does not place orders. Unknown / missing / holiday → not open for orders.
/// </summary>
public static class UsMarketSessionGuard
{
    public const string UnknownOwnerMessage =
        "미국 시장 세션 정보 없음 — 세션을 확인할 수 없어 주문을 차단합니다 (fail-closed).";

    public const string HolidayOwnerMessageFallback =
        "미국 시장 휴장/정규장 없음 — 주문 후보를 차단합니다.";

    public const string OpenOwnerMessageFallback =
        "미국 시장 정규장 세션 확인됨 — 주문 후보 세션 게이트 통과.";

    /// <summary>
    /// Evaluates a US market calendar snapshot for risk gating.
    /// <paramref name="wallClockUtc"/> is reserved for future intraday open/close checks;
    /// calendar holiday/closed flag is authoritative today.
    /// </summary>
    public static UsMarketSessionEvaluation Evaluate(
        UsMarketSessionSnapshot? snapshot,
        DateTimeOffset? wallClockUtc = null)
    {
        _ = wallClockUtc; // reserved: future regular-hours window validation

        if (snapshot is null)
        {
            return new UsMarketSessionEvaluation(
                IsKnown: false,
                IsOpenForOrders: false,
                OwnerMessage: UnknownOwnerMessage);
        }

        if (snapshot.IsHolidayOrClosed)
        {
            var msg = string.IsNullOrWhiteSpace(snapshot.OwnerMessage)
                ? HolidayOwnerMessageFallback
                : snapshot.OwnerMessage;
            return new UsMarketSessionEvaluation(
                IsKnown: true,
                IsOpenForOrders: false,
                OwnerMessage: msg);
        }

        // Not holiday/closed and snapshot present → session known and open for candidates.
        var openMsg = string.IsNullOrWhiteSpace(snapshot.OwnerMessage)
            ? OpenOwnerMessageFallback
            : snapshot.OwnerMessage;
        return new UsMarketSessionEvaluation(
            IsKnown: true,
            IsOpenForOrders: true,
            OwnerMessage: openMsg);
    }

    /// <summary>
    /// Maps snapshot evaluation into <see cref="CandidateRiskContext"/> session flags.
    /// Pipeline should call this (or set flags equivalently) before <see cref="RiskGate.EvaluateOrderCandidate"/>.
    /// </summary>
    public static CandidateRiskContext ApplyToContext(
        CandidateRiskContext context,
        UsMarketSessionSnapshot? snapshot,
        DateTimeOffset? wallClockUtc = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evaluation = Evaluate(snapshot, wallClockUtc);
        return context with
        {
            MarketSessionKnown = evaluation.IsKnown,
            MarketSessionOpen = evaluation.IsOpenForOrders,
            MarketSessionOwnerMessage = evaluation.OwnerMessage,
        };
    }
}

/// <summary>Result of US market session evaluation for risk gating.</summary>
public sealed record UsMarketSessionEvaluation(
    bool IsKnown,
    bool IsOpenForOrders,
    string OwnerMessage);
