namespace TradingBot.Domain;

/// <summary>A proposed order that is NOT an execution. Candidates never submit live by themselves.</summary>
public sealed record OrderCandidate(
    string Symbol,
    string Side,
    string OrderType,
    decimal Quantity,
    decimal? LimitPrice,
    string ClientOrderId,
    DateTimeOffset CreatedAtUtc);
