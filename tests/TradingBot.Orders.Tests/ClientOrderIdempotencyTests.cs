using System.Net.Http;
using System.Reflection;
using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Evidence: ClientOrderId duplicate guard on real dry-run / paper routers + in-memory ledgers.
/// No Toss order HTTP. Matching is ordinal case-sensitive.
/// </summary>
public class ClientOrderIdempotencyTests
{
    private static OrderCandidate Sample(
        string clientOrderId,
        decimal quantity = 1m,
        decimal? limitPrice = 100m) => new(
        "AAPL",
        "BUY",
        "LIMIT",
        quantity,
        limitPrice,
        clientOrderId,
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task DryRun_duplicate_ClientOrderId_rejects_second_ledger_stays_one()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        Assert.False(router.IsLiveSubmissionEnabled);

        var first = await router.RouteAsync(Sample("idemp-dry-1", quantity: 2m, limitPrice: 10m), CancellationToken.None);
        var second = await router.RouteAsync(Sample("idemp-dry-1", quantity: 99m, limitPrice: 999m), CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.Equal(OrderMode.DryRun.ToString(), first.Mode);
        Assert.False(second.Accepted);
        Assert.Equal(OrderMode.DryRun.ToString(), second.Mode);
        Assert.Contains("duplicate client order id", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No live order", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, ledger.Count);

        var entry = Assert.Single(ledger.GetSnapshot());
        Assert.Equal("idemp-dry-1", entry.Candidate.ClientOrderId);
        Assert.Equal(2m, entry.Candidate.Quantity);
        Assert.Equal(10m, entry.Candidate.LimitPrice);
        Assert.True(entry.Accepted);
    }

    [Fact]
    public async Task DryRun_different_ClientOrderIds_both_accepted()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);

        var a = await router.RouteAsync(Sample("dry-a"), CancellationToken.None);
        var b = await router.RouteAsync(Sample("dry-b"), CancellationToken.None);

        Assert.True(a.Accepted);
        Assert.True(b.Accepted);
        Assert.Equal(2, ledger.Count);
        var snap = ledger.GetSnapshot();
        Assert.Equal("dry-a", snap[0].Candidate.ClientOrderId);
        Assert.Equal("dry-b", snap[1].Candidate.ClientOrderId);
        Assert.All(snap, e => Assert.True(e.Accepted));
        Assert.All(snap, e => Assert.Equal(OrderMode.DryRun.ToString(), e.Mode));
        Assert.False(router.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Paper_duplicate_ClientOrderId_rejects_second_ledger_stays_one()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        Assert.False(router.IsLiveSubmissionEnabled);

        var first = await router.RouteAsync(Sample("idemp-paper-1", quantity: 3m, limitPrice: 50.5m), CancellationToken.None);
        var second = await router.RouteAsync(Sample("idemp-paper-1", quantity: 100m, limitPrice: 1m), CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), first.Mode);
        Assert.False(second.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), second.Mode);
        Assert.Contains("duplicate client order id", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No live order", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, ledger.Count);

        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal("idemp-paper-1", fill.ClientOrderId);
        Assert.Equal(3m, fill.Quantity);
        Assert.Equal(50.5m, fill.Price);
    }

    [Fact]
    public async Task Paper_different_ClientOrderIds_both_accepted()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);

        var a = await router.RouteAsync(Sample("paper-a", quantity: 1m, limitPrice: 10m), CancellationToken.None);
        var b = await router.RouteAsync(Sample("paper-b", quantity: 2m, limitPrice: 20m), CancellationToken.None);

        Assert.True(a.Accepted);
        Assert.True(b.Accepted);
        Assert.Equal(2, ledger.Count);
        var snap = ledger.GetSnapshot();
        Assert.Equal("paper-a", snap[0].ClientOrderId);
        Assert.Equal(1m, snap[0].Quantity);
        Assert.Equal(10m, snap[0].Price);
        Assert.Equal("paper-b", snap[1].ClientOrderId);
        Assert.Equal(2m, snap[1].Quantity);
        Assert.Equal(20m, snap[1].Price);
        Assert.False(router.IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task DryRun_ClientOrderId_match_is_case_sensitive()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);

        var lower = await router.RouteAsync(Sample("CaseId"), CancellationToken.None);
        var upper = await router.RouteAsync(Sample("caseid"), CancellationToken.None);

        Assert.True(lower.Accepted);
        Assert.True(upper.Accepted);
        Assert.Equal(2, ledger.Count);
    }

    [Fact]
    public async Task Paper_price_reject_does_not_consume_ClientOrderId()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);

        var rejected = await router.RouteAsync(Sample("retry-id", limitPrice: null), CancellationToken.None);
        Assert.False(rejected.Accepted);
        Assert.Equal(0, ledger.Count);

        var accepted = await router.RouteAsync(Sample("retry-id", quantity: 4m, limitPrice: 12m), CancellationToken.None);
        Assert.True(accepted.Accepted);
        Assert.Equal(1, ledger.Count);
        Assert.Equal(4m, Assert.Single(ledger.GetSnapshot()).Quantity);
    }

    [Fact]
    public async Task Shared_index_blocks_duplicate_across_router_instance_same_index()
    {
        var index = new ClientOrderIdIndex();
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var dry = new DryRunOrderRouter(dryLedger, index);
        var paper = new PaperOrderRouter(paperLedger, clientOrderIdIndex: index);

        var first = await dry.RouteAsync(Sample("shared-1"), CancellationToken.None);
        var second = await paper.RouteAsync(Sample("shared-1", limitPrice: 5m), CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Contains("duplicate client order id", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, dryLedger.Count);
        Assert.Equal(0, paperLedger.Count);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void ClientOrderIdIndex_TryRegister_null_throws()
    {
        var index = new ClientOrderIdIndex();
        Assert.Throws<ArgumentNullException>(() => index.TryRegister(null!));
        Assert.Throws<ArgumentNullException>(() => index.Contains(null!));
    }

    [Fact]
    public void Routers_and_index_have_no_HttpClient_fields()
    {
        Assert.False(TypeDependsOnHttp(typeof(ClientOrderIdIndex)));
        Assert.False(TypeDependsOnHttp(typeof(DryRunOrderRouter)));
        Assert.False(TypeDependsOnHttp(typeof(PaperOrderRouter)));
        Assert.False(TypeDependsOnHttp(typeof(InMemoryDryRunLedger)));
        Assert.False(TypeDependsOnHttp(typeof(InMemoryPaperLedger)));
    }

    [Fact]
    public void Router_constructors_do_not_accept_HttpClient()
    {
        foreach (var type in new[] { typeof(DryRunOrderRouter), typeof(PaperOrderRouter), typeof(ClientOrderIdIndex) })
        {
            foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            {
                foreach (var p in ctor.GetParameters())
                {
                    Assert.False(
                        IsHttpRelated(p.ParameterType),
                        $"{type.Name} must not take HTTP types; found {p.ParameterType.FullName}");
                }
            }
        }
    }

    [Fact]
    public async Task Concurrent_same_ClientOrderId_only_one_ledger_entry_dry_run()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        const int n = 24;

        var results = await Task.WhenAll(
            Enumerable.Range(0, n)
                .Select(_ => router.RouteAsync(Sample("race-dry"), CancellationToken.None)));

        Assert.Equal(1, results.Count(r => r.Accepted));
        Assert.Equal(n - 1, results.Count(r => !r.Accepted));
        Assert.Equal(1, ledger.Count);
        Assert.All(results.Where(r => !r.Accepted), r =>
            Assert.Contains("duplicate client order id", r.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Concurrent_same_ClientOrderId_only_one_ledger_entry_paper()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        const int n = 24;

        var results = await Task.WhenAll(
            Enumerable.Range(0, n)
                .Select(_ => router.RouteAsync(Sample("race-paper", limitPrice: 9m), CancellationToken.None)));

        Assert.Equal(1, results.Count(r => r.Accepted));
        Assert.Equal(n - 1, results.Count(r => !r.Accepted));
        Assert.Equal(1, ledger.Count);
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
