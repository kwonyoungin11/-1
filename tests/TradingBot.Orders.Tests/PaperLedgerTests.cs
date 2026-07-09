using System.Net.Http;
using System.Reflection;
using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Evidence: paper fills record quantity and price; never live; no Toss order HTTP.
/// </summary>
public class PaperLedgerTests
{
    private static OrderCandidate Sample(
        string clientOrderId = "paper-client-order-1",
        string symbol = "AAPL",
        string side = "BUY",
        decimal quantity = 1m,
        decimal? limitPrice = 190.5m) => new(
        symbol,
        side,
        "LIMIT",
        quantity,
        limitPrice,
        clientOrderId,
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task Paper_router_appends_fill_on_accept()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        var candidate = Sample();

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.Equal(1, ledger.Count);
        Assert.False(router.IsLiveSubmissionEnabled);

        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(candidate.ClientOrderId, fill.ClientOrderId);
        Assert.Equal(candidate.Symbol, fill.Symbol);
        Assert.Equal(candidate.Side, fill.Side);
        Assert.Equal(candidate.Quantity, fill.Quantity);
        Assert.Equal(candidate.LimitPrice, fill.Price);
        Assert.Contains("No live order", fill.Note, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, fill.FillId);
        Assert.True(fill.FilledAtUtc <= DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Theory]
    [InlineData("AAPL", "BUY", 1, 190.5)]
    [InlineData("MSFT", "SELL", 2.5, 400)]
    [InlineData("NVDA", "BUY", 10, 99.99)]
    [InlineData("TSLA", "SELL", 100, 250.01)]
    public async Task Paper_fill_records_exact_quantity_and_price(
        string symbol,
        string side,
        decimal quantity,
        decimal limitPrice)
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        var candidate = Sample(
            clientOrderId: $"qp-{symbol}-{quantity}",
            symbol: symbol,
            side: side,
            quantity: quantity,
            limitPrice: limitPrice);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.False(router.IsLiveSubmissionEnabled);

        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(symbol, fill.Symbol);
        Assert.Equal(side, fill.Side);
        Assert.Equal(quantity, fill.Quantity);
        Assert.Equal(limitPrice, fill.Price);
        Assert.Equal(candidate.ClientOrderId, fill.ClientOrderId);
        Assert.Contains("No live order", fill.Note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("virtual", fill.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Paper_ledger_appends_multiple_fills_in_order_with_qty_price()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);

        await router.RouteAsync(
            Sample("id-1", quantity: 1m, limitPrice: 190.5m),
            CancellationToken.None);
        await router.RouteAsync(
            Sample("id-2", quantity: 5m, limitPrice: 200m),
            CancellationToken.None);

        Assert.Equal(2, ledger.Count);
        var snap = ledger.GetSnapshot();
        Assert.Equal("id-1", snap[0].ClientOrderId);
        Assert.Equal(1m, snap[0].Quantity);
        Assert.Equal(190.5m, snap[0].Price);
        Assert.Equal("id-2", snap[1].ClientOrderId);
        Assert.Equal(5m, snap[1].Quantity);
        Assert.Equal(200m, snap[1].Price);
        Assert.All(snap, f => Assert.Equal("AAPL", f.Symbol));
        Assert.All(snap, f => Assert.True(f.Quantity > 0m));
        Assert.All(snap, f => Assert.True(f.Price > 0m));
    }

    [Fact]
    public async Task Paper_router_accepted_mode_is_paper_never_live()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.Equal("Paper", result.Mode);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.NotEqual("live", result.Mode, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(result.Blocks);
        Assert.Contains("No live order", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(router.IsLiveSubmissionEnabled);

        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Contains("No live order", fill.Note, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("Live", result.Mode);
    }

    [Fact]
    public async Task Paper_uses_reference_price_when_limit_missing_records_quantity()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger, _ => 175.25m);
        var candidate = Sample(quantity: 7m, limitPrice: null);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(7m, fill.Quantity);
        Assert.Equal(175.25m, fill.Price);
    }

    [Fact]
    public async Task Paper_prefers_limit_price_over_reference_price()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger, _ => 999m);
        var candidate = Sample(quantity: 3m, limitPrice: 111.11m);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(3m, fill.Quantity);
        Assert.Equal(111.11m, fill.Price);
        Assert.NotEqual(999m, fill.Price);
    }

    [Fact]
    public async Task Paper_rejects_without_price_still_not_live_and_no_fill()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        var candidate = Sample(limitPrice: null);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.Equal(0, ledger.Count);
        Assert.Contains("No live order", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(router.IsLiveSubmissionEnabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task Paper_rejects_non_positive_limit_price_without_fill(decimal badPrice)
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        var candidate = Sample(limitPrice: badPrice);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.Equal(0, ledger.Count);
        Assert.False(router.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Paper_rejects_non_positive_reference_price_without_fill()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger, _ => 0m);
        var candidate = Sample(limitPrice: null);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(0, ledger.Count);
    }

    [Fact]
    public void InMemoryPaperLedger_append_null_throws()
    {
        var ledger = new InMemoryPaperLedger();
        Assert.Throws<ArgumentNullException>(() => ledger.Append(null!));
    }

    [Fact]
    public void PaperOrderRouter_null_ledger_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PaperOrderRouter(null!));
    }

    [Fact]
    public void PaperFillRecord_can_be_appended_directly_with_quantity_price()
    {
        var ledger = new InMemoryPaperLedger();
        var fill = new PaperFillRecord(
            FillId: Guid.CreateVersion7(),
            Symbol: "MSFT",
            Side: "SELL",
            Quantity: 2m,
            Price: 400m,
            FilledAtUtc: DateTimeOffset.UtcNow,
            ClientOrderId: "direct-1",
            Note: "manual paper fill");

        ledger.Append(fill);

        Assert.Equal(1, ledger.Count);
        var snap = Assert.Single(ledger.GetSnapshot());
        Assert.Equal("MSFT", snap.Symbol);
        Assert.Equal("SELL", snap.Side);
        Assert.Equal(2m, snap.Quantity);
        Assert.Equal(400m, snap.Price);
        Assert.Equal("direct-1", snap.ClientOrderId);
    }

    [Fact]
    public async Task Paper_route_does_not_require_or_invoke_HttpClient()
    {
        Assert.False(TypeDependsOnHttp(typeof(PaperOrderRouter)));

        var ledger = new InMemoryPaperLedger();
        IOrderRouter router = new PaperOrderRouter(ledger);
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(quantity: 4m, limitPrice: 50m), CancellationToken.None);

        Assert.True(result.Accepted);
        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(4m, fill.Quantity);
        Assert.Equal(50m, fill.Price);
        Assert.Contains("No live order", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaperOrderRouter_constructors_do_not_accept_HttpClient()
    {
        var ctors = typeof(PaperOrderRouter).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        Assert.NotEmpty(ctors);
        foreach (var ctor in ctors)
        {
            foreach (var p in ctor.GetParameters())
            {
                Assert.False(
                    IsHttpRelated(p.ParameterType),
                    $"PaperOrderRouter must not take HTTP types; found {p.ParameterType.FullName}");
            }
        }
    }

    [Fact]
    public async Task Paper_null_candidate_throws_and_does_not_append()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => router.RouteAsync(null!, CancellationToken.None));

        Assert.Equal(0, ledger.Count);
    }

    [Fact]
    public async Task Paper_canceled_token_throws_and_does_not_append()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.RouteAsync(Sample(), cts.Token));

        Assert.Equal(0, ledger.Count);
    }

    private static bool TypeDependsOnHttp(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var field in type.GetFields(flags))
        {
            if (IsHttpRelated(field.FieldType))
            {
                return true;
            }
        }

        foreach (var prop in type.GetProperties(flags))
        {
            if (IsHttpRelated(prop.PropertyType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHttpRelated(Type type) =>
        type == typeof(HttpClient)
        || type == typeof(HttpMessageHandler)
        || typeof(HttpMessageHandler).IsAssignableFrom(type)
        || (type.FullName?.Contains("HttpClient", StringComparison.Ordinal) == true);
}
