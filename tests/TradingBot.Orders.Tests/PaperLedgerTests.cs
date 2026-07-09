using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

public class PaperLedgerTests
{
    private static OrderCandidate Sample(
        string clientOrderId = "paper-client-order-1",
        decimal? limitPrice = 190.5m) => new(
        "AAPL", "BUY", "LIMIT", 1m, limitPrice, clientOrderId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Paper_router_appends_fill_on_accept()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        var candidate = Sample();

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("Paper", result.Mode);
        Assert.Equal(1, ledger.Count);

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

    [Fact]
    public async Task Paper_ledger_appends_multiple_fills_in_order()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);

        await router.RouteAsync(Sample("id-1"), CancellationToken.None);
        await router.RouteAsync(Sample("id-2", limitPrice: 200m), CancellationToken.None);

        Assert.Equal(2, ledger.Count);
        var snap = ledger.GetSnapshot();
        Assert.Equal("id-1", snap[0].ClientOrderId);
        Assert.Equal(190.5m, snap[0].Price);
        Assert.Equal("id-2", snap[1].ClientOrderId);
        Assert.Equal(200m, snap[1].Price);
        Assert.All(snap, f => Assert.Equal("AAPL", f.Symbol));
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

        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Contains("No live order", fill.Note, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("Live", result.Mode);
    }

    [Fact]
    public async Task Paper_uses_reference_price_when_limit_missing()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger, _ => 175.25m);
        var candidate = Sample(limitPrice: null);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("Paper", result.Mode);
        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(175.25m, fill.Price);
    }

    [Fact]
    public async Task Paper_rejects_without_price_still_not_live()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        var candidate = Sample(limitPrice: null);

        var result = await router.RouteAsync(candidate, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("Paper", result.Mode);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.Equal(0, ledger.Count);
        Assert.Contains("No live order", result.Message, StringComparison.OrdinalIgnoreCase);
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
    public void PaperFillRecord_can_be_appended_directly()
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
}
