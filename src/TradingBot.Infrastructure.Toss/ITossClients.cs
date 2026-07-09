namespace TradingBot.Infrastructure.Toss;

/// <summary>Toss adapter contracts. Order client is a gated stub in harness phase.</summary>
public interface ITossAuthClient
{
    // Token acquisition will be implemented after SDK install + owner credentials.
}

public interface ITossMarketDataClient
{
}

public interface ITossAccountClient
{
}

/// <summary>Order client interface exists for architecture; live implementation is blocked.</summary>
public interface ITossOrderClient
{
    bool IsLiveSubmissionEnabled { get; }
}

public interface ITossClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ITossRateLimitPolicy
{
}

public interface ITossRedactor
{
    string MaskToken(string? token);
    string MaskAccount(string? account);
}

/// <summary>Harness stub: live submission always disabled.</summary>
public sealed class BlockedTossOrderClient : ITossOrderClient
{
    public bool IsLiveSubmissionEnabled => false;
}

public sealed class SystemTossClock : ITossClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class DomainTossRedactor : ITossRedactor
{
    public string MaskToken(string? token) => TradingBot.Domain.SecretRedactor.MaskToken(token);

    public string MaskAccount(string? account) => TradingBot.Domain.SecretRedactor.MaskAccount(account);
}
