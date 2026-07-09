namespace TradingBot.Domain;

public sealed record HoldingSummary(
    string Symbol,
    string Name,
    string Currency,
    decimal Quantity,
    decimal? LastPrice);
