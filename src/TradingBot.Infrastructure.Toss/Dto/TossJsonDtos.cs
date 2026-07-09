using System.Text.Json.Serialization;

namespace TradingBot.Infrastructure.Toss.Dto;

// Minimal DTOs aligned with official OpenAPI 1.2.2 field names (read-only).

public sealed class OAuth2TokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }
}

public sealed class AccountsResponseDto
{
    [JsonPropertyName("result")]
    public List<AccountDto>? Result { get; set; }
}

public sealed class AccountDto
{
    [JsonPropertyName("accountNo")]
    public string? AccountNo { get; set; }

    [JsonPropertyName("accountSeq")]
    public string? AccountSeq { get; set; }

    [JsonPropertyName("accountType")]
    public string? AccountType { get; set; }
}

public sealed class HoldingsResponseDto
{
    [JsonPropertyName("result")]
    public HoldingsOverviewDto? Result { get; set; }
}

public sealed class HoldingsOverviewDto
{
    [JsonPropertyName("marketValue")]
    public OverviewMarketValueDto? MarketValue { get; set; }

    [JsonPropertyName("items")]
    public List<HoldingsItemDto>? Items { get; set; }
}

public sealed class OverviewMarketValueDto
{
    [JsonPropertyName("amount")]
    public CurrencyAmountDto? Amount { get; set; }
}

public sealed class CurrencyAmountDto
{
    [JsonPropertyName("krw")]
    public string? Krw { get; set; }

    [JsonPropertyName("usd")]
    public string? Usd { get; set; }
}

public sealed class HoldingsItemDto
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("quantity")]
    public string? Quantity { get; set; }

    [JsonPropertyName("lastPrice")]
    public string? LastPrice { get; set; }
}

public sealed class PricesResponseDto
{
    [JsonPropertyName("result")]
    public List<PriceDto>? Result { get; set; }
}

public sealed class PriceDto
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("lastPrice")]
    public string? LastPrice { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

public sealed class UsMarketCalendarResponseDto
{
    [JsonPropertyName("result")]
    public UsMarketCalendarResultDto? Result { get; set; }
}

public sealed class UsMarketCalendarResultDto
{
    [JsonPropertyName("today")]
    public UsMarketDayDto? Today { get; set; }
}

public sealed class UsMarketDayDto
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("regularMarket")]
    public object? RegularMarket { get; set; }
}
