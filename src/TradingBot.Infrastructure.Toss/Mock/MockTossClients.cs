using TradingBot.Domain;
using TradingBot.Infrastructure.Toss.Dto;

namespace TradingBot.Infrastructure.Toss.Mock;

/// <summary>In-memory Toss read-only data for Phase 2 (no network).</summary>
public sealed class MockTossAuthClient : ITossAuthClient
{
    public Task<TossAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Fake token shape only — never a real credential.
        return Task.FromResult(new TossAccessToken("mock-access-token-not-real", "Bearer", 3600));
    }
}

public sealed class MockTossAccountClient : ITossAccountClient
{
    private readonly ITossRedactor _redactor;

    public MockTossAccountClient(ITossRedactor redactor)
    {
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
    }

    public Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dto = new AccountsResponseDto
        {
            Result = new List<AccountDto>
            {
                new() { AccountNo = "1234567890", AccountSeq = "1", AccountType = "위탁" },
            },
        };
        return Task.FromResult(TossDtoMapper.MapAccounts(dto, _redactor));
    }

    public Task<HoldingsReadModel> GetHoldingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dto = new HoldingsResponseDto
        {
            Result = new HoldingsOverviewDto
            {
                MarketValue = new OverviewMarketValueDto
                {
                    Amount = new CurrencyAmountDto { Krw = "0", Usd = "1500.25" },
                },
                Items = new List<HoldingsItemDto>
                {
                    new()
                    {
                        Symbol = "AAPL",
                        Name = "Apple Inc.",
                        Currency = "USD",
                        Quantity = "10",
                        LastPrice = "190.50",
                    },
                },
            },
        };
        return Task.FromResult(TossDtoMapper.MapHoldings(dto));
    }
}

public sealed class MockTossMarketDataClient : ITossMarketDataClient
{
    public Task<IReadOnlyList<QuoteSnapshot>> GetPricesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(symbols);
        var list = symbols
            .Select(s => new QuoteSnapshot(s, 100m, "USD", DateTimeOffset.UtcNow))
            .ToList();
        return Task.FromResult<IReadOnlyList<QuoteSnapshot>>(list);
    }

    public Task<UsMarketSessionSnapshot> GetUsMarketCalendarAsync(
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var d = (date ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");
        return Task.FromResult(new UsMarketSessionSnapshot(
            d,
            IsHolidayOrClosed: false,
            OwnerMessage: $"미국 시장 {d}: mock 정규장 세션 있음"));
    }
}
