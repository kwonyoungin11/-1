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
}
