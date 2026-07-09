namespace TradingBot.Domain;

public sealed record QuoteSnapshot(
    string Symbol,
    decimal? LastPrice,
    string Currency,
    DateTimeOffset? TimestampUtc);
