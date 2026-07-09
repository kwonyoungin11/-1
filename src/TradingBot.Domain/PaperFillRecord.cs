namespace TradingBot.Domain;

/// <summary>
/// One virtual paper fill for simulation / paper ledger.
/// Never represents a live broker fill. No network, no secrets.
/// </summary>
public sealed record PaperFillRecord(
    Guid FillId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset FilledAtUtc,
    string ClientOrderId,
    string Note);
