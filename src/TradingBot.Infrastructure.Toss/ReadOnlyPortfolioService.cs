using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss.Mock;

namespace TradingBot.Infrastructure.Toss;

public sealed class ReadOnlyPortfolioService : IReadOnlyPortfolioService
{
    private readonly ITossAuthClient _auth;
    private readonly ITossAccountClient _accounts;
    private readonly ITossMarketDataClient _market;
    private readonly ITossClock _clock;
    private readonly ITossRedactor _redactor;
    private readonly TossOptions _options;
    private readonly bool _isMock;

    public ReadOnlyPortfolioService(
        ITossAuthClient auth,
        ITossAccountClient accounts,
        ITossMarketDataClient market,
        ITossClock clock,
        ITossRedactor redactor,
        TossOptions options,
        bool isMock)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _market = market ?? throw new ArgumentNullException(nameof(market));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _isMock = isMock;
    }

    public static ReadOnlyPortfolioService CreateMock()
    {
        var redactor = new DomainTossRedactor();
        var options = new TossOptions { AllowLiveHttp = false };
        return new ReadOnlyPortfolioService(
            new MockTossAuthClient(),
            new MockTossAccountClient(redactor),
            new MockTossMarketDataClient(),
            new SystemTossClock(),
            redactor,
            options,
            isMock: true);
    }

    public async Task<ReadOnlyPortfolioSnapshot> GetSnapshotAsync(
        IReadOnlyList<string> watchSymbols,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(watchSymbols);

        try
        {
            var token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            _ = _redactor.MaskToken(token.AccessToken); // ensure redaction path exercised; never log raw

            var accounts = await _accounts.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
            var holdings = await _accounts.GetHoldingsAsync(cancellationToken).ConfigureAwait(false);
            // NASDAQ path defaults to USD cash buying power (OpenAPI: currency query required).
            var buyingPower = await _accounts
                .GetBuyingPowerAsync("USD", cancellationToken)
                .ConfigureAwait(false);
            var quotes = await _market.GetPricesAsync(watchSymbols, cancellationToken).ConfigureAwait(false);
            var us = await _market.GetUsMarketCalendarAsync(null, cancellationToken).ConfigureAwait(false);

            var status = _isMock ? ConnectionStatus.MockConnected : ConnectionStatus.LiveReadOnlyConnected;
            var msg = _isMock
                ? "mock 읽기 전용 연결됨 (실 HTTP 없음, 실주문 없음)"
                : "실 HTTP 읽기 전용 연결됨 (실주문 없음)";

            return new ReadOnlyPortfolioSnapshot
            {
                ConnectionStatus = status,
                ConnectionOwnerMessage = msg,
                Accounts = accounts,
                Holdings = holdings.Items,
                Quotes = quotes,
                UsMarket = us,
                MarketValueUsdSummary = holdings.MarketValueUsd,
                MarketValueUsdDecimal = TossDtoMapper.ParseMarketValueUsd(holdings.MarketValueUsd),
                CashBuyingPower = buyingPower.CashBuyingPower,
                CashCurrency = buyingPower.Currency,
                AsOfUtc = _clock.UtcNow,
                BlockMessages = new[]
                {
                    "주문 API 미사용 — read-only 단계",
                    _options.AllowLiveHttp
                        ? "실 HTTP 읽기 허용 상태 (주문 아님)"
                        : "실 HTTP 차단 — mock 경로",
                },
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ReadOnlyPortfolioSnapshot
            {
                ConnectionStatus = ConnectionStatus.Error,
                ConnectionOwnerMessage = "읽기 연결 오류 — 실주문은 하지 않습니다.",
                Accounts = Array.Empty<AccountSummary>(),
                Holdings = Array.Empty<HoldingSummary>(),
                Quotes = Array.Empty<QuoteSnapshot>(),
                UsMarket = null,
                MarketValueUsdSummary = null,
                MarketValueUsdDecimal = null,
                CashBuyingPower = null,
                CashCurrency = null,
                AsOfUtc = _clock.UtcNow,
                BlockMessages = new[]
                {
                    "read-only 오류로 데이터 없음 (fail-closed)",
                    ex.GetType().Name,
                },
            };
        }
    }

    public Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken) =>
        _market.GetCandlesAsync(symbol, interval, count, cancellationToken);
}
