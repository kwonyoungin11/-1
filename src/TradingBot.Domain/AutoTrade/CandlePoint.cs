namespace TradingBot.Domain;

/// <summary>차트용 봉 데이터.</summary>
public sealed record CandlePoint(
    DateTimeOffset Time,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume);
