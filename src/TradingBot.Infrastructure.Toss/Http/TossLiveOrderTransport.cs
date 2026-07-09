using System.Net.Http.Json;
using System.Text.Json;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss.Dto;
using TradingBot.Orders;

namespace TradingBot.Infrastructure.Toss.Http;

/// <summary>
/// Gated Toss order POST (<c>api/v1/orders</c>). Invoked only after <see cref="LiveOrderGate"/> allows.
/// Never logs secrets, tokens, or account numbers.
/// </summary>
public sealed class TossLiveOrderTransport : ILiveOrderTransport
{
    private readonly HttpClient _http;
    private readonly TossOptions _options;
    private readonly LiveTossAuthClient _auth;
    private readonly LiveTossAccountClient _accounts;

    public TossLiveOrderTransport(
        HttpClient http,
        TossOptions options,
        LiveTossAuthClient auth,
        LiveTossAccountClient accounts)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    public async Task<LiveTransportResult> SubmitCandidateAsync(
        OrderCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        LiveHttpGuard.EnsureAllowed(_options);

        if (!_options.HasClientCredentials)
        {
            return new LiveTransportResult(
                Success: false,
                Message: "TOSS_CLIENT_ID/SECRET missing — live order not sent.");
        }

        if (candidate.LimitPrice is null && string.Equals(candidate.OrderType, "LIMIT", StringComparison.OrdinalIgnoreCase))
        {
            return new LiveTransportResult(
                Success: false,
                Message: "LIMIT order requires LimitPrice — live order not sent.");
        }

        var body = new OrderCreateRequestDto
        {
            ClientOrderId = candidate.ClientOrderId,
            Symbol = candidate.Symbol,
            Side = candidate.Side.ToUpperInvariant(),
            OrderType = candidate.OrderType.ToUpperInvariant(),
            Quantity = candidate.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Price = candidate.LimitPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        var accounts = await _accounts.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
        var accountSeq = _options.AccountSeq
            ?? accounts.FirstOrDefault()?.AccountSeq;
        if (string.IsNullOrWhiteSpace(accountSeq))
        {
            return new LiveTransportResult(
                Success: false,
                Message: "TOSS_ACCOUNT_SEQ missing and accounts list empty — live order not sent.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("api/v1/orders", UriKind.Relative))
        {
            Content = JsonContent.Create(body, options: TossJson.Options),
        };
        await _auth.ApplyBearerAsync(request, cancellationToken).ConfigureAwait(false);
        request.Headers.TryAddWithoutValidation("X-Tossinvest-Account", accountSeq);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new LiveTransportResult(
                Success: false,
                Message: $"Toss order POST failed HTTP {(int)response.StatusCode} (body redacted).");
        }

        OrderCreateResponseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<OrderCreateResponseDto>(responseBody, TossJson.Options);
        }
        catch (JsonException)
        {
            return new LiveTransportResult(
                Success: false,
                Message: "Toss order response could not be parsed (redacted).");
        }

        var orderId = dto?.Result?.OrderId;
        return new LiveTransportResult(
            Success: !string.IsNullOrWhiteSpace(orderId),
            Message: string.IsNullOrWhiteSpace(orderId)
                ? "Toss order accepted but orderId missing in response."
                : "Live order submitted to Toss.",
            BrokerReference: orderId);
    }
}