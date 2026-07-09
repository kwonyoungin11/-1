using System.Net.Http;
using System.Reflection;
using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Evidence: dry-run routes append ledger entries and never issue Toss order HTTP.
/// </summary>
public class DryRunLedgerTests
{
    private static OrderCandidate Sample(
        string clientOrderId = "test-client-order-1",
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
    public async Task DryRun_with_ledger_appends_entry_on_accept()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        var candidate = Sample();

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.DryRun.ToString(), result.Mode);
        Assert.Equal(1, ledger.Count);

        var entry = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(candidate.ClientOrderId, entry.Candidate.ClientOrderId);
        Assert.Equal(candidate.Symbol, entry.Candidate.Symbol);
        Assert.Equal(candidate.Side, entry.Candidate.Side);
        Assert.Equal(candidate.Quantity, entry.Candidate.Quantity);
        Assert.Equal(candidate.LimitPrice, entry.Candidate.LimitPrice);
        Assert.Equal(candidate.OrderType, entry.Candidate.OrderType);
        Assert.True(entry.Accepted);
        Assert.Equal(OrderMode.DryRun.ToString(), entry.Mode);
        Assert.Contains("No live order", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, entry.EntryId);
        Assert.True(entry.RecordedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task DryRun_records_full_candidate_quantity_and_limit_price_on_ledger()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        var candidate = Sample(
            clientOrderId: "qty-price-1",
            symbol: "MSFT",
            side: "SELL",
            quantity: 12.5m,
            limitPrice: 401.25m);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.False(router.IsLiveSubmissionEnabled);

        var entry = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(12.5m, entry.Candidate.Quantity);
        Assert.Equal(401.25m, entry.Candidate.LimitPrice);
        Assert.Equal("MSFT", entry.Candidate.Symbol);
        Assert.Equal("SELL", entry.Candidate.Side);
        Assert.Equal("qty-price-1", entry.Candidate.ClientOrderId);
        Assert.Equal(OrderMode.DryRun.ToString(), entry.Mode);
        Assert.NotEqual(OrderMode.Live.ToString(), entry.Mode);
    }

    [Fact]
    public async Task DryRun_without_ledger_still_accepts_no_live_no_http()
    {
        var router = new DryRunOrderRouter();
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.DryRun.ToString(), result.Mode);
        Assert.Empty(result.Blocks);
        Assert.Contains("No live order", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
    }

    [Fact]
    public async Task DryRun_ledger_appends_multiple_routes_in_order()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);

        await router.RouteAsync(Sample("id-1", quantity: 1m, limitPrice: 10m), CancellationToken.None);
        await router.RouteAsync(Sample("id-2", quantity: 3m, limitPrice: 20m), CancellationToken.None);

        Assert.Equal(2, ledger.Count);
        var snap = ledger.GetSnapshot();
        Assert.Equal("id-1", snap[0].Candidate.ClientOrderId);
        Assert.Equal(1m, snap[0].Candidate.Quantity);
        Assert.Equal(10m, snap[0].Candidate.LimitPrice);
        Assert.Equal("id-2", snap[1].Candidate.ClientOrderId);
        Assert.Equal(3m, snap[1].Candidate.Quantity);
        Assert.Equal(20m, snap[1].Candidate.LimitPrice);
        Assert.All(snap, e => Assert.Equal(OrderMode.DryRun.ToString(), e.Mode));
        Assert.All(snap, e => Assert.True(e.Accepted));
        Assert.All(snap, e => Assert.NotEqual(OrderMode.Live.ToString(), e.Mode));
    }

    [Fact]
    public void InMemoryLedger_append_null_throws()
    {
        var ledger = new InMemoryDryRunLedger();
        Assert.Throws<ArgumentNullException>(() => ledger.Append(null!));
    }

    [Fact]
    public async Task DryRun_never_uses_live_mode()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.False(router.IsLiveSubmissionEnabled);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.NotEqual("live", result.Mode, StringComparer.OrdinalIgnoreCase);
        var entry = Assert.Single(ledger.GetSnapshot());
        Assert.NotEqual(OrderMode.Live.ToString(), entry.Mode);
        Assert.Equal(OrderMode.DryRun.ToString(), entry.Mode);
    }

    [Fact]
    public async Task DryRun_null_candidate_throws_and_does_not_append()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => router.RouteAsync(null!, CancellationToken.None));

        Assert.Equal(0, ledger.Count);
    }

    [Fact]
    public async Task DryRun_canceled_token_throws_and_does_not_append()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.RouteAsync(Sample(), cts.Token));

        Assert.Equal(0, ledger.Count);
    }

    [Fact]
    public async Task DryRun_route_does_not_require_or_invoke_HttpClient()
    {
        // Architectural + runtime evidence: dry-run completes with zero HTTP surface.
        Assert.False(TypeDependsOnHttp(typeof(DryRunOrderRouter)));

        var ledger = new InMemoryDryRunLedger();
        IOrderRouter router = new DryRunOrderRouter(ledger);
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, ledger.Count);
        Assert.Contains("No live order", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DryRunOrderRouter_constructors_do_not_accept_HttpClient()
    {
        var ctors = typeof(DryRunOrderRouter).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        Assert.NotEmpty(ctors);
        foreach (var ctor in ctors)
        {
            foreach (var p in ctor.GetParameters())
            {
                Assert.False(
                    IsHttpRelated(p.ParameterType),
                    $"DryRunOrderRouter must not take HTTP types; found {p.ParameterType.FullName}");
            }
        }
    }

    [Fact]
    public async Task DryRun_concurrent_appends_preserve_all_entries()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        const int n = 32;

        var tasks = Enumerable.Range(0, n)
            .Select(i => router.RouteAsync(Sample($"concurrent-{i}"), CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(n, ledger.Count);
        Assert.All(results, r => Assert.True(r.Accepted));
        Assert.All(results, r => Assert.Equal(OrderMode.DryRun.ToString(), r.Mode));
        Assert.All(ledger.GetSnapshot(), e => Assert.True(e.Accepted));
        Assert.Equal(n, ledger.GetSnapshot().Select(e => e.Candidate.ClientOrderId).Distinct().Count());
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
