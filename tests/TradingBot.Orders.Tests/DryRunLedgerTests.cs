using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

public class DryRunLedgerTests
{
    private static OrderCandidate Sample(string clientOrderId = "test-client-order-1") => new(
        "AAPL", "BUY", "LIMIT", 1m, 190.5m, clientOrderId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task DryRun_with_ledger_appends_entry_on_accept()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);
        var candidate = Sample();

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("DryRun", result.Mode);
        Assert.Equal(1, ledger.Count);

        var entry = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(candidate.ClientOrderId, entry.Candidate.ClientOrderId);
        Assert.Equal(candidate.Symbol, entry.Candidate.Symbol);
        Assert.Equal(candidate.Side, entry.Candidate.Side);
        Assert.Equal(candidate.Quantity, entry.Candidate.Quantity);
        Assert.True(entry.Accepted);
        Assert.Equal("DryRun", entry.Mode);
        Assert.Contains("No live order", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, entry.EntryId);
    }

    [Fact]
    public async Task DryRun_without_ledger_still_accepts_no_live()
    {
        var router = new DryRunOrderRouter();
        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("DryRun", result.Mode);
        Assert.Empty(result.Blocks);
    }

    [Fact]
    public async Task DryRun_ledger_appends_multiple_routes_in_order()
    {
        var ledger = new InMemoryDryRunLedger();
        var router = new DryRunOrderRouter(ledger);

        await router.RouteAsync(Sample("id-1"), CancellationToken.None);
        await router.RouteAsync(Sample("id-2"), CancellationToken.None);

        Assert.Equal(2, ledger.Count);
        var snap = ledger.GetSnapshot();
        Assert.Equal("id-1", snap[0].Candidate.ClientOrderId);
        Assert.Equal("id-2", snap[1].Candidate.ClientOrderId);
        Assert.All(snap, e => Assert.Equal("DryRun", e.Mode));
        Assert.All(snap, e => Assert.True(e.Accepted));
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

        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.NotEqual("live", result.Mode, StringComparer.OrdinalIgnoreCase);
        var entry = Assert.Single(ledger.GetSnapshot());
        Assert.NotEqual(OrderMode.Live.ToString(), entry.Mode);
    }
}
