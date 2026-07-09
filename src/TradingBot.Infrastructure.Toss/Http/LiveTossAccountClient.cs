using TradingBot.Domain;
using TradingBot.Infrastructure.Toss.Dto;

namespace TradingBot.Infrastructure.Toss.Http;

/// <summary>Read-only accounts + holdings. Never posts orders.</summary>
public sealed class LiveTossAccountClient : ITossAccountClient
{
    private readonly HttpClient _http;
    private readonly TossOptions _options;
    private readonly LiveTossAuthClient _auth;
    private readonly ITossRedactor _redactor;

    public LiveTossAccountClient(
        HttpClient http,
        TossOptions options,
        LiveTossAuthClient auth,
        ITossRedactor redactor)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
    }

    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        LiveHttpGuard.EnsureAllowed(_options);
        var dto = await GetJsonAsync<AccountsResponseDto>(
                new Uri("api/v1/accounts", UriKind.Relative),
                accountSeqHeader: null,
                cancellationToken)
            .ConfigureAwait(false);
        return TossDtoMapper.MapAccounts(dto, _redactor);
    }

    public async Task<HoldingsReadModel> GetHoldingsAsync(CancellationToken cancellationToken)
    {
        LiveHttpGuard.EnsureAllowed(_options);
        var seq = await ResolveAccountSeqAsync(cancellationToken).ConfigureAwait(false);
        var dto = await GetJsonAsync<HoldingsResponseDto>(
                new Uri("api/v1/holdings", UriKind.Relative),
                accountSeqHeader: seq,
                cancellationToken)
            .ConfigureAwait(false);
        return TossDtoMapper.MapHoldings(dto);
    }

    private async Task<string> ResolveAccountSeqAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.AccountSeq))
        {
            return _options.AccountSeq!;
        }

        var accounts = await GetAccountsAsync(cancellationToken).ConfigureAwait(false);
        var first = accounts.FirstOrDefault()?.AccountSeq;
        if (string.IsNullOrWhiteSpace(first))
        {
            throw new InvalidOperationException(
                "TOSS_ACCOUNT_SEQ missing and accounts list empty — cannot read holdings.");
        }

        return first;
    }

    private async Task<T> GetJsonAsync<T>(
        Uri relative,
        string? accountSeqHeader,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relative);
        await _auth.ApplyBearerAsync(request, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(accountSeqHeader))
        {
            request.Headers.TryAddWithoutValidation("X-Tossinvest-Account", accountSeqHeader);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Toss GET {relative} failed HTTP {(int)response.StatusCode} (body redacted).");
        }

        return TossJson.DeserializeRequired<T>(body);
    }
}
