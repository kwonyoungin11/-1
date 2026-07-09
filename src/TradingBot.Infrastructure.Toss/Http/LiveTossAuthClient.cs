using System.Net.Http.Headers;
using TradingBot.Infrastructure.Toss.Dto;

namespace TradingBot.Infrastructure.Toss.Http;

/// <summary>
/// OAuth2 Client Credentials. 토큰은 메모리 캐시. 시크릿/토큰 원문 로그 금지.
/// </summary>
public sealed class LiveTossAuthClient : ITossAuthClient
{
    private readonly HttpClient _http;
    private readonly TossOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TossAccessToken? _cached;
    private DateTimeOffset _expiresAtUtc;

    public LiveTossAuthClient(HttpClient http, TossOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<TossAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        LiveHttpGuard.EnsureAllowed(_options);
        if (!_options.HasClientCredentials)
        {
            throw new InvalidOperationException("TOSS_CLIENT_ID/SECRET missing — cannot call live auth.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow < _expiresAtUtc)
            {
                return _cached;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId!,
                ["client_secret"] = _options.ClientSecret!,
            });

            using var response = await _http
                .PostAsync(new Uri("oauth2/token", UriKind.Relative), content, cancellationToken)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Toss OAuth failed HTTP {(int)response.StatusCode} (body redacted).");
            }

            var dto = TossJson.DeserializeRequired<OAuth2TokenResponseDto>(body);
            var token = TossDtoMapper.MapToken(dto);
            // 만료 60초 전 갱신
            var skew = TimeSpan.FromSeconds(Math.Min(60, Math.Max(5, token.ExpiresInSeconds / 20.0)));
            _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresInSeconds) - skew;
            _cached = token;
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplyBearerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType,
            token.AccessToken);
    }
}
