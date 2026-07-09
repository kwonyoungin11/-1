namespace TradingBot.Domain;

public enum SignalSide
{
    None = 0,
    Buy = 1,
    Sell = 2,
    Hold = 3,
}

/// <summary>Computed strategy intent. Not an order. Not execution.</summary>
public sealed record StrategySignal(
    string Symbol,
    SignalSide Side,
    decimal? SuggestedQuantity,
    decimal? ReferencePrice,
    string StrategyName,
    string OwnerMessage,
    DateTimeOffset CreatedAtUtc,
    bool IsActionable);
