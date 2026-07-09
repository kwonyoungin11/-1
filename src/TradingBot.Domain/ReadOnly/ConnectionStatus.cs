namespace TradingBot.Domain;

public enum ConnectionStatus
{
    Disconnected = 0,
    MockConnected = 1,
    LiveReadOnlyConnected = 2,
    Error = 3,
    Blocked = 4,
}
