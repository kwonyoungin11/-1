namespace TradingBot.Domain;

/// <summary>자동매매 필수 표시 항목.</summary>
public sealed class AutoTradePanelSnapshot
{
    public required StockMarketKind StockKind { get; init; }
    public required TradingStrategyKind Strategy { get; init; }
    public required AutoTradeSessionStatus SessionStatus { get; init; }
    public required string StockKindLabel { get; init; }
    public required string StrategyLabel { get; init; }
    public required string SessionStatusLabel { get; init; }
    public required string WatchSymbolsText { get; init; }
    public required decimal Balance { get; init; }
    public required string BalanceLabel { get; init; }
    public required decimal ReturnRatePercent { get; init; }
    public required string ReturnRateLabel { get; init; }
    public required bool CanStart { get; init; }
    public required bool CanStop { get; init; }
    public required string SafetyNote { get; init; }
}
