using TradingBot.Domain;
using TradingBot.Orders;
using TradingBot.Risk;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Evidence aggregation tests using real ledgers + routers (no fake business reimplementations).
/// Proves dry-run entries + paper fills (qty/price) with LiveModePresent always false for real routes.
/// </summary>
public class EvidenceBuilderTests
{
    private static OrderCandidate Candidate(
        string symbol,
        string clientOrderId,
        decimal quantity = 1m,
        decimal? limitPrice = 190.5m,
        string side = "BUY") => new(
        symbol,
        side,
        "LIMIT",
        quantity,
        limitPrice,
        clientOrderId,
        DateTimeOffset.UtcNow);

    [Fact]
    public void Build_empty_ledgers_returns_zero_counts()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        var builder = new EvidenceBuilder(dry, paper);

        var snap = builder.Build();

        Assert.Equal(0, snap.Summary.DryRunEntryCount);
        Assert.Equal(0, snap.Summary.DryRunAcceptedCount);
        Assert.Equal(0, snap.Summary.PaperFillCount);
        Assert.Equal(0, snap.Summary.TotalEvidenceCount);
        Assert.Empty(snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.Empty(snap.RecentDryRunSymbols);
        Assert.Empty(snap.RecentPaperSymbols);
        Assert.Empty(snap.DryRunEntries);
        Assert.Empty(snap.PaperFills);
        Assert.True(snap.CapturedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Paper_router_records_fills_and_evidence_aggregates_with_qty_price()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var paperRouter = new PaperOrderRouter(paperLedger);
        var dryRouter = new DryRunOrderRouter(dryLedger);

        Assert.False(dryRouter.IsLiveSubmissionEnabled);
        Assert.False(paperRouter.IsLiveSubmissionEnabled);

        await dryRouter.RouteAsync(Candidate("AAPL", "dry-1", quantity: 2m, limitPrice: 190m), CancellationToken.None);
        await dryRouter.RouteAsync(Candidate("MSFT", "dry-2", quantity: 3m, limitPrice: 400m), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("NVDA", "paper-1", quantity: 4m, limitPrice: 100m), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("TSLA", "paper-2", quantity: 5m, limitPrice: 200m, side: "SELL"), CancellationToken.None);

        Assert.Equal(2, dryLedger.Count);
        Assert.Equal(2, paperLedger.Count);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(2, snap.Summary.DryRunEntryCount);
        Assert.Equal(2, snap.Summary.DryRunAcceptedCount);
        Assert.Equal(2, snap.Summary.PaperFillCount);
        Assert.Equal(4, snap.Summary.TotalEvidenceCount);
        Assert.Contains(OrderMode.DryRun.ToString(), snap.Summary.ModesPresent);
        Assert.Contains(OrderMode.Paper.ToString(), snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.DoesNotContain(OrderMode.Live.ToString(), snap.Summary.ModesPresent);

        Assert.Equal(new[] { "AAPL", "MSFT" }, snap.RecentDryRunSymbols);
        Assert.Equal(new[] { "NVDA", "TSLA" }, snap.RecentPaperSymbols);

        Assert.Equal(2, snap.DryRunEntries.Count);
        Assert.Equal("dry-1", snap.DryRunEntries[0].Candidate.ClientOrderId);
        Assert.Equal(2m, snap.DryRunEntries[0].Candidate.Quantity);
        Assert.Equal(190m, snap.DryRunEntries[0].Candidate.LimitPrice);
        Assert.Equal("dry-2", snap.DryRunEntries[1].Candidate.ClientOrderId);
        Assert.Equal(3m, snap.DryRunEntries[1].Candidate.Quantity);
        Assert.Equal(400m, snap.DryRunEntries[1].Candidate.LimitPrice);
        Assert.All(snap.DryRunEntries, e => Assert.Equal(OrderMode.DryRun.ToString(), e.Mode));
        Assert.All(snap.DryRunEntries, e => Assert.True(e.Accepted));

        Assert.Equal(2, snap.PaperFills.Count);
        Assert.Equal("NVDA", snap.PaperFills[0].Symbol);
        Assert.Equal(4m, snap.PaperFills[0].Quantity);
        Assert.Equal(100m, snap.PaperFills[0].Price);
        Assert.Equal("TSLA", snap.PaperFills[1].Symbol);
        Assert.Equal(5m, snap.PaperFills[1].Quantity);
        Assert.Equal(200m, snap.PaperFills[1].Price);
        Assert.Equal("SELL", snap.PaperFills[1].Side);
        Assert.All(snap.PaperFills, f => Assert.Contains("No live order", f.Note, StringComparison.OrdinalIgnoreCase));
        Assert.All(snap.PaperFills, f => Assert.True(f.Quantity > 0m && f.Price > 0m));
    }

    [Fact]
    public async Task Recent_symbols_respect_limit_last_N()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var dryRouter = new DryRunOrderRouter(dryLedger);
        var paperRouter = new PaperOrderRouter(paperLedger);

        await dryRouter.RouteAsync(Candidate("AAA", "d1"), CancellationToken.None);
        await dryRouter.RouteAsync(Candidate("BBB", "d2"), CancellationToken.None);
        await dryRouter.RouteAsync(Candidate("CCC", "d3"), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("XXX", "p1"), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("YYY", "p2"), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("ZZZ", "p3"), CancellationToken.None);

        var snap = new EvidenceBuilder(dryLedger, paperLedger, recentSymbolLimit: 2).Build();

        Assert.Equal(new[] { "BBB", "CCC" }, snap.RecentDryRunSymbols);
        Assert.Equal(new[] { "YYY", "ZZZ" }, snap.RecentPaperSymbols);
        Assert.Equal(3, snap.Summary.DryRunEntryCount);
        Assert.Equal(3, snap.Summary.PaperFillCount);
        Assert.Equal(3, snap.DryRunEntries.Count);
        Assert.Equal(3, snap.PaperFills.Count);
        Assert.False(snap.Summary.LiveModePresent);
    }

    [Fact]
    public async Task Paper_only_evidence_modes_include_Paper_not_Live()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var paperRouter = new PaperOrderRouter(paperLedger);

        var result = await paperRouter.RouteAsync(
            Candidate("AAPL", "p-only", quantity: 9m, limitPrice: 55.5m),
            CancellationToken.None);
        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.False(paperRouter.IsLiveSubmissionEnabled);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(0, snap.Summary.DryRunEntryCount);
        Assert.Equal(1, snap.Summary.PaperFillCount);
        Assert.Equal(new[] { OrderMode.Paper.ToString() }, snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.Equal(new[] { "AAPL" }, snap.RecentPaperSymbols);
        Assert.Empty(snap.RecentDryRunSymbols);
        var fill = Assert.Single(snap.PaperFills);
        Assert.Equal(9m, fill.Quantity);
        Assert.Equal(55.5m, fill.Price);
    }

    [Fact]
    public async Task DryRun_only_evidence_modes_include_DryRun_not_Live()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var dryRouter = new DryRunOrderRouter(dryLedger);

        var result = await dryRouter.RouteAsync(
            Candidate("MSFT", "d-only", quantity: 11m, limitPrice: 300m),
            CancellationToken.None);
        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.DryRun.ToString(), result.Mode);
        Assert.False(dryRouter.IsLiveSubmissionEnabled);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(1, snap.Summary.DryRunEntryCount);
        Assert.Equal(1, snap.Summary.DryRunAcceptedCount);
        Assert.Equal(0, snap.Summary.PaperFillCount);
        Assert.Equal(new[] { OrderMode.DryRun.ToString() }, snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.Equal(new[] { "MSFT" }, snap.RecentDryRunSymbols);
        Assert.Empty(snap.RecentPaperSymbols);
        var entry = Assert.Single(snap.DryRunEntries);
        Assert.Equal(11m, entry.Candidate.Quantity);
        Assert.Equal(300m, entry.Candidate.LimitPrice);
    }

    [Fact]
    public async Task Rejected_paper_does_not_add_fill_or_Paper_mode()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var paperRouter = new PaperOrderRouter(paperLedger);

        var result = await paperRouter.RouteAsync(
            Candidate("AAPL", "no-price", limitPrice: null),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.Equal(0, paperLedger.Count);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(0, snap.Summary.PaperFillCount);
        Assert.Empty(snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.Empty(snap.PaperFills);
    }

    [Fact]
    public async Task Blocked_live_routes_do_not_pollute_dry_run_or_paper_ledgers()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var liveRouter = new BlockedLiveOrderRouter(TradingSafetySettings.CreateSafeDefaults());

        Assert.False(liveRouter.IsLiveSubmissionEnabled);

        var result = await liveRouter.RouteAsync(Candidate("AAPL", "live-attempt"), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(0, dryLedger.Count);
        Assert.Equal(0, paperLedger.Count);

        // Even if operator later routes dry/paper, evidence must stay non-live.
        await new DryRunOrderRouter(dryLedger).RouteAsync(Candidate("AAPL", "after-block"), CancellationToken.None);
        await new PaperOrderRouter(paperLedger).RouteAsync(
            Candidate("AAPL", "after-block-p", quantity: 2m, limitPrice: 10m),
            CancellationToken.None);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(1, snap.Summary.DryRunEntryCount);
        Assert.Equal(1, snap.Summary.PaperFillCount);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.DoesNotContain(OrderMode.Live.ToString(), snap.Summary.ModesPresent);
        Assert.Equal(2m, snap.PaperFills[0].Quantity);
        Assert.Equal(10m, snap.PaperFills[0].Price);
    }

    [Fact]
    public void EvidenceBuilder_null_ledgers_throw()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();

        Assert.Throws<ArgumentNullException>(() => new EvidenceBuilder(null!, paper));
        Assert.Throws<ArgumentNullException>(() => new EvidenceBuilder(dry, null!));
    }

    [Fact]
    public void EvidenceBuilder_negative_symbol_limit_throws()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvidenceBuilder(dry, paper, recentSymbolLimit: -1));
    }

    [Fact]
    public async Task Zero_symbol_limit_returns_empty_recent_lists_keeps_full_entries()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        await new DryRunOrderRouter(dryLedger).RouteAsync(Candidate("AAPL", "d1"), CancellationToken.None);
        await new PaperOrderRouter(paperLedger).RouteAsync(
            Candidate("NVDA", "p1", quantity: 6m, limitPrice: 77m),
            CancellationToken.None);

        var snap = new EvidenceBuilder(dryLedger, paperLedger, recentSymbolLimit: 0).Build();

        Assert.Empty(snap.RecentDryRunSymbols);
        Assert.Empty(snap.RecentPaperSymbols);
        Assert.Single(snap.DryRunEntries);
        Assert.Single(snap.PaperFills);
        Assert.Equal(2, snap.Summary.TotalEvidenceCount);
        Assert.Equal(6m, snap.PaperFills[0].Quantity);
        Assert.Equal(77m, snap.PaperFills[0].Price);
        Assert.False(snap.Summary.LiveModePresent);
    }

    [Fact]
    public void LiveModePresent_true_only_when_ledger_contains_live_mode_string()
    {
        // Defensive: if a poisoned dry-run entry ever carried Live mode, summary must flag it.
        // Real routers never write this; this proves the detector works for readiness audits.
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        dry.Append(new DryRunLedgerEntry(
            EntryId: Guid.CreateVersion7(),
            RecordedAtUtc: DateTimeOffset.UtcNow,
            Candidate: Candidate("POISON", "should-not-happen"),
            Accepted: true,
            Mode: OrderMode.Live.ToString(),
            Message: "synthetic poison for detector test only"));

        var snap = new EvidenceBuilder(dry, paper).Build();

        Assert.True(snap.Summary.LiveModePresent);
        Assert.Contains(OrderMode.Live.ToString(), snap.Summary.ModesPresent);
    }
}
