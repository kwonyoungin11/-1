using System.Globalization;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss.Dto;

namespace TradingBot.Infrastructure.Toss;

public static class TossDtoMapper
{
    public static IReadOnlyList<AccountSummary> MapAccounts(
        AccountsResponseDto dto,
        ITossRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(redactor);

        return (dto.Result ?? new List<AccountDto>())
            .Select(a => new AccountSummary(
                AccountSeq: a.AccountSeq ?? string.Empty,
                AccountNoMasked: redactor.MaskAccount(a.AccountNo),
                AccountType: a.AccountType ?? "unknown"))
            .ToList();
    }

    public static HoldingsReadModel MapHoldings(HoldingsResponseDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var items = (dto.Result?.Items ?? new List<HoldingsItemDto>())
            .Select(i => new HoldingSummary(
                Symbol: i.Symbol ?? string.Empty,
                Name: i.Name ?? string.Empty,
                Currency: i.Currency ?? string.Empty,
                Quantity: ParseDecimal(i.Quantity) ?? 0m,
                LastPrice: ParseDecimal(i.LastPrice)))
            .ToList();

        var usd = dto.Result?.MarketValue?.Amount?.Usd;
        return new HoldingsReadModel(usd, items);
    }

    public static IReadOnlyList<QuoteSnapshot> MapPrices(PricesResponseDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return (dto.Result ?? new List<PriceDto>())
            .Select(p => new QuoteSnapshot(
                Symbol: p.Symbol ?? string.Empty,
                LastPrice: ParseDecimal(p.LastPrice),
                Currency: p.Currency ?? string.Empty,
                TimestampUtc: ParseTimestamp(p.Timestamp)))
            .ToList();
    }

    public static UsMarketSessionSnapshot MapUsCalendar(UsMarketCalendarResponseDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var today = dto.Result?.Today;
        var date = today?.Date ?? "unknown";
        var closed = today?.RegularMarket is null;
        var msg = closed
            ? $"미국 시장 {date}: 정규장 세션 없음(휴장 또는 장외 가능성)"
            : $"미국 시장 {date}: 정규장 세션 정보 있음";
        return new UsMarketSessionSnapshot(date, closed, msg);
    }

    public static TossAccessToken MapToken(OAuth2TokenResponseDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.AccessToken))
        {
            throw new InvalidOperationException("Token response missing access_token.");
        }

        return new TossAccessToken(
            dto.AccessToken,
            dto.TokenType ?? "Bearer",
            dto.ExpiresIn);
    }

    private static decimal? ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    private static DateTimeOffset? ParseTimestamp(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts)
            ? ts
            : null;
    }
}
