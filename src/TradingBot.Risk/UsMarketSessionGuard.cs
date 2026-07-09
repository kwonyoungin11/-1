using TradingBot.Domain;

namespace TradingBot.Risk;

/// <summary>
/// Fail-closed evaluation of US (NASDAQ) market session for order-candidate gating.
/// Does not place orders. Unknown / missing / holiday / unknown open-close / outside
/// new-entry window → not open for orders.
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
    /// Evaluates a US market calendar snapshot for risk gating, including the new-entry
    /// session window (first N minutes after open / last M minutes before close).
    /// </summary>
    /// <param name="snapshot">Calendar day snapshot; null → unknown, fail-closed.</param>
    /// <param name="wallClockUtc">
    /// Wall clock used for intraday open/close and entry buffers. Required when the calendar
    /// day is open; missing clock → fail-closed for the entry window.
    /// </param>
    /// <param name="sessionWindow">
    /// Known regular open/close window. When null, standard US RTH is derived from
    /// <see cref="UsMarketSessionSnapshot.Date"/>; if that also fails → fail-closed.
    /// </param>
    public static UsMarketSessionEvaluation Evaluate(
        UsMarketSessionSnapshot? snapshot,
        DateTimeOffset? wallClockUtc = null,
        TradingSessionWindow? sessionWindow = null)
    {
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

        // Calendar says trading day → resolve open/close for entry-window filters.
        var window = sessionWindow;
        if (window is null)
        {
            if (!TradingSessionWindow.TryCreateUsRegularHours(snapshot.Date, out window) || window is null)
            {
                return new UsMarketSessionEvaluation(
                    IsKnown: false,
                    IsOpenForOrders: false,
                    OwnerMessage: TradingSessionWindow.UnknownOpenCloseOwnerMessage);
            }
        }

        if (wallClockUtc is null)
        {
            return new UsMarketSessionEvaluation(
                IsKnown: false,
                IsOpenForOrders: false,
                OwnerMessage: TradingSessionWindow.MissingWallClockOwnerMessage);
        }

        var entry = window.EvaluateNewEntry(wallClockUtc.Value);
        if (!entry.AllowsNewEntry)
        {
            return new UsMarketSessionEvaluation(
                IsKnown: entry.IsKnown,
                IsOpenForOrders: false,
                OwnerMessage: entry.OwnerMessage);
        }

        // Entry window pass — prefer calendar owner message when present.
        var openMsg = string.IsNullOrWhiteSpace(snapshot.OwnerMessage)
            ? entry.OwnerMessage
            : $"{snapshot.OwnerMessage} | {entry.OwnerMessage}";
        if (string.IsNullOrWhiteSpace(snapshot.OwnerMessage) && string.IsNullOrWhiteSpace(entry.OwnerMessage))
        {
            openMsg = OpenOwnerMessageFallback;
        }

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
        DateTimeOffset? wallClockUtc = null,
        TradingSessionWindow? sessionWindow = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evaluation = Evaluate(snapshot, wallClockUtc, sessionWindow);
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
