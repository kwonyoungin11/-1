namespace TradingBot.Domain;

public sealed record UsMarketSessionSnapshot(
    string Date,
    bool IsHolidayOrClosed,
    string OwnerMessage);
