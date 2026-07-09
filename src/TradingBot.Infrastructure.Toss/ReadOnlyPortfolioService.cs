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

        // Known universe only (SPCX / VMAR). Unknown tickers dropped; empty → PrimarySymbol.
        var symbols = watchSymbols
            .Select(WatchlistCatalog.NormalizeKnownSymbol)
            .Where(s => s is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (symbols.Length == 0)
        {
            symbols = [WatchlistCatalog.PrimarySymbol];
        }

        try
        {
            var token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            _ = _redactor.MaskToken(token.AccessToken);

            // Partial success: account/auth hard; holdings/bp/quotes/calendar soft.
            // 이전에는 buying-power 등 하나 실패 시 전체가 Error → UI가 연습 잔액으로 떨어짐.
            var blocks = new List<string>
            {
                "주문 API 미사용 — 읽기 경로",
                _options.AllowLiveHttp
                    ? "실 HTTP 읽기 허용 상태 (주문 아님)"
                    : "실 HTTP 차단 — mock 경로",
            };

            IReadOnlyList<AccountSummary> accounts;
            try
            {
                accounts = await _accounts.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ErrorSnapshot(ex, "계좌 목록 조회 실패");
            }

            HoldingsReadModel? holdingsModel = null;
            try
            {
                holdingsModel = await _accounts.GetHoldingsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                blocks.Add($"보유종목 부분실패:{SafeErrorHint(ex)}");
            }

            BuyingPowerSnapshot? buyingPower = null;
            try
            {
                buyingPower = await _accounts
                    .GetBuyingPowerAsync("USD", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // USD 실패 시 KRW 한 번 더 시도
                try
                {
                    buyingPower = await _accounts
                        .GetBuyingPowerAsync("KRW", cancellationToken)
                        .ConfigureAwait(false);
                    blocks.Add("매수가능:KRW 폴백");
                }
                catch (Exception ex2) when (ex2 is not OperationCanceledException)
                {
                    blocks.Add($"매수가능 부분실패:{SafeErrorHint(ex)}");
                }
            }

            IReadOnlyList<QuoteSnapshot> quotes = Array.Empty<QuoteSnapshot>();
            try
            {
                quotes = await _market.GetPricesAsync(symbols, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                blocks.Add($"시세 부분실패:{SafeErrorHint(ex)}");
            }

            UsMarketSessionSnapshot? us = null;
            try
            {
                us = await _market.GetUsMarketCalendarAsync(null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                blocks.Add($"미국장 캘린더 부분실패:{SafeErrorHint(ex)}");
            }

            var holdings = holdingsModel?.Items ?? Array.Empty<HoldingSummary>();
            var mvSummary = holdingsModel?.MarketValueUsd;
            var mvDec = TossDtoMapper.ParseMarketValueUsd(mvSummary);

            var status = _isMock ? ConnectionStatus.MockConnected : ConnectionStatus.LiveReadOnlyConnected;
            var msg = _isMock
                ? "mock 읽기 전용 연결됨 (실 HTTP 없음, 실주문 없음)"
                : "토스 실 HTTP 읽기 연결됨 (잔액·시세 · 실주문은 잠금 유지)";

            return new ReadOnlyPortfolioSnapshot
            {
                ConnectionStatus = status,
                ConnectionOwnerMessage = msg,
                Accounts = accounts,
                Holdings = holdings,
                Quotes = quotes,
                UsMarket = us,
                MarketValueUsdSummary = mvSummary,
                MarketValueUsdDecimal = mvDec,
                CashBuyingPower = buyingPower?.CashBuyingPower,
                CashCurrency = buyingPower?.Currency,
                AsOfUtc = _clock.UtcNow,
                BlockMessages = blocks,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorSnapshot(ex, "읽기 연결 오류");
        }
    }

    private ReadOnlyPortfolioSnapshot ErrorSnapshot(Exception ex, string headline)
    {
        var hint = SafeErrorHint(ex);
        return new ReadOnlyPortfolioSnapshot
        {
            ConnectionStatus = ConnectionStatus.Error,
            ConnectionOwnerMessage = $"{headline} — {hint} · 실주문은 하지 않습니다.",
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
                hint,
            },
        };
    }

    /// <summary>
    /// Owner-safe error hint: type + HTTP status if present. Never includes tokens/secrets/body.
    /// </summary>
    public static string SafeErrorHint(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (ex is HttpRequestException http)
        {
            // Messages are already redacted in live clients ("body redacted").
            var msg = http.Message;
            if (msg.Length > 160)
            {
                msg = msg[..160];
            }

            // Strip anything that looks like a bearer/token fragment.
            if (msg.Contains("Bearer", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                return $"HttpRequestException HTTP 오류 (상세 숨김)";
            }

            return msg;
        }

        if (ex is InvalidOperationException ioe)
        {
            var m = ioe.Message;
            if (m.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
                || m.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                return "설정 오류 (키/토큰 관련 — 값 숨김)";
            }

            return m.Length > 120 ? m[..120] : m;
        }

        return ex.GetType().Name;
    }

    public Task<IReadOnlyList<CandlePoint>> GetCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken) =>
        GetCandlesPagedAsync(symbol, interval, count, maxPages: 1, targetTotal: count, cancellationToken);

    public Task<IReadOnlyList<CandlePoint>> GetCandlesPagedAsync(
        string symbol,
        string interval,
        int countPerPage,
        int maxPages,
        int targetTotal,
        CancellationToken cancellationToken)
    {
        var sym = ResolveSpcx(symbol);
        return _market.GetCandlesPagedAsync(
            sym,
            interval,
            countPerPage,
            maxPages,
            targetTotal,
            cancellationToken);
    }

    private static string ResolveSpcx(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return WatchlistCatalog.SpaceXSymbol;
        }

        var sym = symbol.Trim().ToUpperInvariant();
        return sym.Equals(WatchlistCatalog.SpaceXSymbol, StringComparison.Ordinal)
            ? sym
            : WatchlistCatalog.SpaceXSymbol;
    }
}
