namespace TradingBot.Domain;

/// <summary>
/// Cash-only buying power from Toss GET /api/v1/buying-power (read-only).
/// Values are never secrets; account identity stays masked elsewhere.
/// </summary>
public sealed record BuyingPowerSnapshot(
    string? Currency,
    decimal? CashBuyingPower);
