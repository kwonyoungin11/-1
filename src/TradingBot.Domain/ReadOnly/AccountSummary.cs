namespace TradingBot.Domain;

public sealed record AccountSummary(
    string AccountSeq,
    string AccountNoMasked,
    string AccountType);
