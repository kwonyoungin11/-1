using TradingBot.Domain;

namespace TradingBot.Risk;

/// <summary>Inputs for evaluating whether an order *candidate* may proceed to dry-run/paper.</summary>
public sealed record CandidateRiskContext
{
    public required string Symbol { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal? LimitPrice { get; init; }
    public decimal? CurrentPositionQuantity { get; init; }
    public DateTimeOffset? QuoteTimestampUtc { get; init; }
    public DateTimeOffset NowUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool HasMissingData { get; init; }
    public bool HasUnknownState { get; init; }
    public bool HasApiError { get; init; }
    public bool MarketSessionOpen { get; init; } = true;
    public bool MarketSessionKnown { get; init; } = true;

    /// <summary>
    /// Optional owner-facing session detail from <see cref="UsMarketSessionGuard"/>.
    /// Used to enrich <see cref="BlockedReason.MarketSessionClosed"/> messages.
    /// </summary>
    public string? MarketSessionOwnerMessage { get; init; }

    /// <summary>
    /// Builds a context with US market session flags set from a calendar snapshot (fail-closed),
    /// including new-entry open/close buffers when open/close are known.
    /// Prefer this (or <see cref="UsMarketSessionGuard.ApplyToContext"/>) in the pipeline.
    /// </summary>
    public static CandidateRiskContext BuildContextFromUsSnapshot(
        CandidateRiskContext baseContext,
        UsMarketSessionSnapshot? snapshot,
        DateTimeOffset? wallClockUtc = null,
        TradingSessionWindow? sessionWindow = null)
        => UsMarketSessionGuard.ApplyToContext(baseContext, snapshot, wallClockUtc, sessionWindow);
}
