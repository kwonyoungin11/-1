using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Evidence aggregation tests using real ledgers + routers (no fake business reimplementations).
/// </summary>
public class EvidenceBuilderTests
{
    private static OrderCandidate Candidate(
        string symbol,
        string clientOrderId,
        decimal? limitPrice = 190.5m) => new(
        symbol,
        "BUY",
        "LIMIT",
        1m,
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
    public async Task Paper_router_records_fills_and_evidence_aggregates()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var paperRouter = new PaperOrderRouter(paperLedger);
        var dryRouter = new DryRunOrderRouter(dryLedger);

        await dryRouter.RouteAsync(Candidate("AAPL", "dry-1"), CancellationToken.None);
        await dryRouter.RouteAsync(Candidate("MSFT", "dry-2"), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("NVDA", "paper-1", limitPrice: 100m), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("TSLA", "paper-2", limitPrice: 200m), CancellationToken.None);

        Assert.Equal(2, dryLedger.Count);
        Assert.Equal(2, paperLedger.Count);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(2, snap.Summary.DryRunEntryCount);
        Assert.Equal(2, snap.Summary.DryRunAcceptedCount);
        Assert.Equal(2, snap.Summary.PaperFillCount);
        Assert.Equal(4, snap.Summary.TotalEvidenceCount);
        Assert.Contains("DryRun", snap.Summary.ModesPresent);
        Assert.Contains("Paper", snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.DoesNotContain("Live", snap.Summary.ModesPresent);

        Assert.Equal(new[] { "AAPL", "MSFT" }, snap.RecentDryRunSymbols);
        Assert.Equal(new[] { "NVDA", "TSLA" }, snap.RecentPaperSymbols);

        Assert.Equal(2, snap.DryRunEntries.Count);
        Assert.Equal("dry-1", snap.DryRunEntries[0].Candidate.ClientOrderId);
        Assert.Equal("dry-2", snap.DryRunEntries[1].Candidate.ClientOrderId);
        Assert.All(snap.DryRunEntries, e => Assert.Equal("DryRun", e.Mode));
        Assert.All(snap.DryRunEntries, e => Assert.True(e.Accepted));

        Assert.Equal(2, snap.PaperFills.Count);
        Assert.Equal("NVDA", snap.PaperFills[0].Symbol);
        Assert.Equal(100m, snap.PaperFills[0].Price);
        Assert.Equal("TSLA", snap.PaperFills[1].Symbol);
        Assert.Equal(200m, snap.PaperFills[1].Price);
        Assert.All(snap.PaperFills, f => Assert.Contains("No live order", f.Note, StringComparison.OrdinalIgnoreCase));
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
    }

    [Fact]
    public async Task Paper_only_evidence_modes_include_Paper_not_Live()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var paperRouter = new PaperOrderRouter(paperLedger);

        var result = await paperRouter.RouteAsync(Candidate("AAPL", "p-only"), CancellationToken.None);
        Assert.True(result.Accepted);
        Assert.Equal("Paper", result.Mode);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(0, snap.Summary.DryRunEntryCount);
        Assert.Equal(1, snap.Summary.PaperFillCount);
        Assert.Equal(new[] { "Paper" }, snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.Equal(new[] { "AAPL" }, snap.RecentPaperSymbols);
        Assert.Empty(snap.RecentDryRunSymbols);
    }

    [Fact]
    public async Task DryRun_only_evidence_modes_include_DryRun_not_Live()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var dryRouter = new DryRunOrderRouter(dryLedger);

        var result = await dryRouter.RouteAsync(Candidate("MSFT", "d-only"), CancellationToken.None);
        Assert.True(result.Accepted);
        Assert.Equal("DryRun", result.Mode);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(1, snap.Summary.DryRunEntryCount);
        Assert.Equal(1, snap.Summary.DryRunAcceptedCount);
        Assert.Equal(0, snap.Summary.PaperFillCount);
        Assert.Equal(new[] { "DryRun" }, snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.Equal(new[] { "MSFT" }, snap.RecentDryRunSymbols);
        Assert.Empty(snap.RecentPaperSymbols);
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
        Assert.Equal("Paper", result.Mode);
        Assert.Equal(0, paperLedger.Count);

        var snap = new EvidenceBuilder(dryLedger, paperLedger).Build();

        Assert.Equal(0, snap.Summary.PaperFillCount);
        Assert.Empty(snap.Summary.ModesPresent);
        Assert.False(snap.Summary.LiveModePresent);
        Assert.Empty(snap.PaperFills);
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
        await new PaperOrderRouter(paperLedger).RouteAsync(Candidate("NVDA", "p1"), CancellationToken.None);

        var snap = new EvidenceBuilder(dryLedger, paperLedger, recentSymbolLimit: 0).Build();

        Assert.Empty(snap.RecentDryRunSymbols);
        Assert.Empty(snap.RecentPaperSymbols);
        Assert.Single(snap.DryRunEntries);
        Assert.Single(snap.PaperFills);
        Assert.Equal(2, snap.Summary.TotalEvidenceCount);
    }
}
