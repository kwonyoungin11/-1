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
                new()
                {
                    AccountNo = "1234567890",
                    AccountSeqElement = System.Text.Json.JsonSerializer.SerializeToElement("1"),
                    AccountType = "위탁",
                },
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
                        Symbol = "SPCX",
                        Name = "SpaceX",
                        Currency = "USD",
                        Quantity = "10",
                        LastPrice = "85.50",
                    },
                },
            },
        };
        return Task.FromResult(TossDtoMapper.MapHoldings(dto));
    }

    public Task<BuyingPowerSnapshot> GetBuyingPowerAsync(
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var ccy = currency.Trim().ToUpperInvariant();
        var cash = ccy switch
        {
            "KRW" => "5000000",
            _ => "3500.50",
        };
        var dto = new BuyingPowerResponseDto
        {
            Result = new BuyingPowerResultDto
            {
                Currency = ccy is "KRW" or "USD" ? ccy : "USD",
                CashBuyingPower = cash,
            },
        };
        return Task.FromResult(TossDtoMapper.MapBuyingPower(dto));
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
        // 심볼별 서로 다른 연습 시세 (전략 모멘텀 점수 다양화)
        var list = symbols
            .Select(s =>
            {
                var seed = (decimal)WatchlistCatalog.ChartSeedPrice(s);
                var jitter = (Math.Abs(HashCode.Combine(s, DateTime.UtcNow.Minute / 5)) % 100) / 50m; // 0..2
                var price = Math.Max(1m, seed * (0.98m + jitter * 0.02m));
                var ccy = s.Length == 6 && s.All(char.IsDigit) ? "KRW" : "USD";
                return new QuoteSnapshot(s, Math.Round(price, 2), ccy, DateTimeOffset.UtcNow);
            })
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

    public Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return Task.FromResult<IReadOnlyList<CandlePoint>>(Array.Empty<CandlePoint>());
        }

        // Synthetic series for cockpit chart when live HTTP is off (no network).
        var clamped = Math.Clamp(count <= 0 ? 160 : count, 1, 200);
        _ = interval; // mock ignores interval; live client validates 1m|1d
        var series = TradingBot.Application.MockCandleSeriesFactory.CreateSeries(
            symbol.Trim(),
            clamped,
            DateTimeOffset.UtcNow);
        return Task.FromResult(series);
    }
}
