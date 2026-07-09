using System.Text.Json;
using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Evidence export for readiness docs. Uses real InMemory ledgers + GetSnapshot()
/// and real dry-run/paper routers. Live remains blocked in every export.
/// </summary>
public class TradingEvidenceExporterTests
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
    public void Export_empty_ledgers_includes_live_blocked_flags_and_zero_counts()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();

        // Real GetSnapshot() on empty ledgers
        Assert.Empty(dry.GetSnapshot());
        Assert.Empty(paper.GetSnapshot());

        var exporter = new TradingEvidenceExporter(dry, paper);
        var doc = exporter.Export();

        Assert.False(doc.LiveOrders);
        Assert.False(doc.LiveSubmissionEnabled);
        Assert.Equal(0, doc.DryRunEntryCount);
        Assert.Equal(0, doc.DryRunAcceptedCount);
        Assert.Equal(0, doc.PaperFillCount);
        Assert.Equal(0, doc.TotalEvidenceCount);
        Assert.Empty(doc.Modes);
        Assert.Empty(doc.Symbols);
        Assert.Empty(doc.ClientOrderIds);
        Assert.Empty(doc.DryRunEntries);
        Assert.Empty(doc.PaperFills);
        Assert.True(doc.ExportedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Export_from_real_ledgers_includes_counts_symbols_client_order_ids_modes()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        var dryRouter = new DryRunOrderRouter(dryLedger);
        var paperRouter = new PaperOrderRouter(paperLedger);

        Assert.False(dryRouter.IsLiveSubmissionEnabled);
        Assert.False(paperRouter.IsLiveSubmissionEnabled);

        await dryRouter.RouteAsync(Candidate("AAPL", "dry-1", quantity: 2m, limitPrice: 190m), CancellationToken.None);
        await dryRouter.RouteAsync(Candidate("MSFT", "dry-2", quantity: 3m, limitPrice: 400m), CancellationToken.None);
        await paperRouter.RouteAsync(Candidate("NVDA", "paper-1", quantity: 4m, limitPrice: 100m), CancellationToken.None);

        // Must use real GetSnapshot() content
        var drySnap = dryLedger.GetSnapshot();
        var paperSnap = paperLedger.GetSnapshot();
        Assert.Equal(2, drySnap.Count); // full dry-run snapshot from real ledger
        Assert.Single(paperSnap);
        Assert.Equal("dry-1", drySnap[0].Candidate.ClientOrderId);
        Assert.Equal("dry-2", drySnap[1].Candidate.ClientOrderId);
        Assert.Equal("paper-1", paperSnap[0].ClientOrderId);

        var exporter = new TradingEvidenceExporter(dryLedger, paperLedger);
        var doc = exporter.Export();

        Assert.Equal(2, doc.DryRunEntryCount);
        Assert.Equal(2, doc.DryRunAcceptedCount);
        Assert.Equal(1, doc.PaperFillCount);
        Assert.Equal(3, doc.TotalEvidenceCount);

        Assert.Contains(OrderMode.DryRun.ToString(), doc.Modes);
        Assert.Contains(OrderMode.Paper.ToString(), doc.Modes);
        Assert.DoesNotContain(OrderMode.Live.ToString(), doc.Modes);

        Assert.Equal(new[] { "AAPL", "MSFT", "NVDA" }, doc.Symbols);
        Assert.Equal(new[] { "dry-1", "dry-2", "paper-1" }, doc.ClientOrderIds);

        Assert.Equal(2, doc.DryRunEntries.Count);
        Assert.Equal("AAPL", doc.DryRunEntries[0].Symbol);
        Assert.Equal("dry-1", doc.DryRunEntries[0].ClientOrderId);
        Assert.Equal(OrderMode.DryRun.ToString(), doc.DryRunEntries[0].Mode);
        Assert.True(doc.DryRunEntries[0].Accepted);
        Assert.Equal(2m, doc.DryRunEntries[0].Quantity);
        Assert.Equal(190m, doc.DryRunEntries[0].LimitPrice);

        Assert.Equal("MSFT", doc.DryRunEntries[1].Symbol);
        Assert.Equal("dry-2", doc.DryRunEntries[1].ClientOrderId);

        var paperLine = Assert.Single(doc.PaperFills);
        Assert.Equal("NVDA", paperLine.Symbol);
        Assert.Equal("paper-1", paperLine.ClientOrderId);
        Assert.Equal(OrderMode.Paper.ToString(), paperLine.Mode);
        Assert.Equal(4m, paperLine.Quantity);
        Assert.Equal(100m, paperLine.Price);

        Assert.False(doc.LiveOrders);
        Assert.False(doc.LiveSubmissionEnabled);
    }

    [Fact]
    public async Task ExportAsText_contains_explicit_live_orders_false_and_LiveSubmissionEnabled_false()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        await new DryRunOrderRouter(dryLedger).RouteAsync(Candidate("AAPL", "d-text"), CancellationToken.None);
        await new PaperOrderRouter(paperLedger).RouteAsync(
            Candidate("TSLA", "p-text", quantity: 5m, limitPrice: 200m),
            CancellationToken.None);

        // Prove GetSnapshot was the source
        Assert.Single(dryLedger.GetSnapshot());
        Assert.Single(paperLedger.GetSnapshot());

        var text = new TradingEvidenceExporter(dryLedger, paperLedger).ExportAsText();

        Assert.Contains("live_orders=false", text, StringComparison.Ordinal);
        Assert.Contains("LiveSubmissionEnabled=false", text, StringComparison.Ordinal);
        Assert.Contains("dry_run_entry_count=1", text, StringComparison.Ordinal);
        Assert.Contains("paper_fill_count=1", text, StringComparison.Ordinal);
        Assert.Contains("client_order_id=d-text", text, StringComparison.Ordinal);
        Assert.Contains("client_order_id=p-text", text, StringComparison.Ordinal);
        Assert.Contains("symbol=AAPL", text, StringComparison.Ordinal);
        Assert.Contains("symbol=TSLA", text, StringComparison.Ordinal);
        Assert.Contains("mode=DryRun", text, StringComparison.Ordinal);
        Assert.Contains("mode=Paper", text, StringComparison.Ordinal);

        // Must not claim live is enabled
        Assert.DoesNotContain("live_orders=true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LiveSubmissionEnabled=true", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsJson_includes_live_orders_false_and_LiveSubmissionEnabled_false()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        await new DryRunOrderRouter(dryLedger).RouteAsync(Candidate("MSFT", "json-1"), CancellationToken.None);

        Assert.Single(dryLedger.GetSnapshot());

        var json = new TradingEvidenceExporter(dryLedger, paperLedger).ExportAsJson();

        Assert.Contains("\"live_orders\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"LiveSubmissionEnabled\": false", json, StringComparison.Ordinal);
        Assert.Contains("json-1", json, StringComparison.Ordinal);
        Assert.Contains("MSFT", json, StringComparison.Ordinal);
        Assert.Contains(OrderMode.DryRun.ToString(), json, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("live_orders").GetBoolean());
        Assert.False(root.GetProperty("LiveSubmissionEnabled").GetBoolean());
        Assert.Equal(1, root.GetProperty("dryRunEntryCount").GetInt32());
        Assert.Equal(0, root.GetProperty("paperFillCount").GetInt32());
        Assert.Equal("json-1", root.GetProperty("clientOrderIds")[0].GetString());
    }

    [Fact]
    public void FromSnapshots_matches_ledger_GetSnapshot_contents()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        dry.Append(new DryRunLedgerEntry(
            EntryId: Guid.CreateVersion7(),
            RecordedAtUtc: DateTimeOffset.UtcNow,
            Candidate: Candidate("IBM", "cid-ibm", quantity: 7m, limitPrice: 50m),
            Accepted: true,
            Mode: OrderMode.DryRun.ToString(),
            Message: "Dry-run accepted. No live order was submitted."));
        paper.Append(new PaperFillRecord(
            FillId: Guid.CreateVersion7(),
            Symbol: "AMD",
            Side: "BUY",
            Quantity: 8m,
            Price: 120m,
            FilledAtUtc: DateTimeOffset.UtcNow,
            ClientOrderId: "cid-amd",
            Note: "Paper virtual fill. No live order was submitted."));

        var drySnap = dry.GetSnapshot();
        var paperSnap = paper.GetSnapshot();
        var doc = TradingEvidenceExporter.FromSnapshots(drySnap, paperSnap);

        Assert.Equal(drySnap.Count, doc.DryRunEntryCount);
        Assert.Equal(paperSnap.Count, doc.PaperFillCount);
        Assert.Equal(drySnap[0].Candidate.ClientOrderId, doc.DryRunEntries[0].ClientOrderId);
        Assert.Equal(paperSnap[0].ClientOrderId, doc.PaperFills[0].ClientOrderId);
        Assert.Equal(drySnap[0].Candidate.Symbol, doc.Symbols[0]);
        Assert.Equal(paperSnap[0].Symbol, doc.Symbols[1]);
        Assert.False(doc.LiveOrders);
        Assert.False(doc.LiveSubmissionEnabled);
    }

    [Fact]
    public void Export_never_includes_secret_like_fields()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        dry.Append(new DryRunLedgerEntry(
            EntryId: Guid.CreateVersion7(),
            RecordedAtUtc: DateTimeOffset.UtcNow,
            Candidate: Candidate("AAPL", "safe-id"),
            Accepted: true,
            Mode: OrderMode.DryRun.ToString(),
            Message: "Bearer super-secret-token should not appear in export body fields"));

        var json = new TradingEvidenceExporter(dry, paper).ExportAsJson();
        var text = new TradingEvidenceExporter(dry, paper).ExportAsText();

        // Free-text ledger messages are intentionally not exported (no secrets path).
        Assert.DoesNotContain("Bearer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-token", text, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("live_orders=false", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TradingEvidenceExporter_null_ledgers_throw()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();

        Assert.Throws<ArgumentNullException>(() => new TradingEvidenceExporter(null!, paper));
        Assert.Throws<ArgumentNullException>(() => new TradingEvidenceExporter(dry, null!));
    }

    [Fact]
    public void FromSnapshots_null_lists_throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradingEvidenceExporter.FromSnapshots(null!, Array.Empty<PaperFillRecord>()));
        Assert.Throws<ArgumentNullException>(() =>
            TradingEvidenceExporter.FromSnapshots(Array.Empty<DryRunLedgerEntry>(), null!));
    }

    [Fact]
    public async Task Export_document_LiveOrders_and_LiveSubmissionEnabled_always_false_even_with_evidence()
    {
        var dryLedger = new InMemoryDryRunLedger();
        var paperLedger = new InMemoryPaperLedger();
        await new DryRunOrderRouter(dryLedger).RouteAsync(Candidate("AAPL", "a1"), CancellationToken.None);
        await new PaperOrderRouter(paperLedger).RouteAsync(
            Candidate("NVDA", "p1", quantity: 1m, limitPrice: 10m),
            CancellationToken.None);

        var doc = new TradingEvidenceExporter(dryLedger, paperLedger).Export();
        var json = TradingEvidenceExporter.ToJson(doc);
        var text = TradingEvidenceExporter.ToText(doc);

        Assert.False(doc.LiveOrders);
        Assert.False(doc.LiveSubmissionEnabled);
        Assert.Contains("\"live_orders\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"LiveSubmissionEnabled\": false", json, StringComparison.Ordinal);
        Assert.Contains("live_orders=false", text, StringComparison.Ordinal);
        Assert.Contains("LiveSubmissionEnabled=false", text, StringComparison.Ordinal);
        Assert.Equal(2, doc.TotalEvidenceCount);
    }
}
