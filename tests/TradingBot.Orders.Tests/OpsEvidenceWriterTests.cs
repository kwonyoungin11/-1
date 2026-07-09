using TradingBot.Domain;
using TradingBot.Orders;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Ops multi-session evidence from real InMemory ledgers + DryRun/Paper routers.
/// Live remains blocked in every export.
/// </summary>
public class OpsEvidenceWriterTests
{
    private static OrderCandidate Candidate(
        string symbol,
        string clientOrderId,
        decimal quantity = 1m,
        decimal? limitPrice = 100m) => new(
        symbol,
        "BUY",
        "LIMIT",
        quantity,
        limitPrice,
        clientOrderId,
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task CaptureSession_uses_real_GetSnapshot_from_ledgers()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        await new DryRunOrderRouter(dry).RouteAsync(Candidate("AAPL", "d1"), CancellationToken.None);
        await new PaperOrderRouter(paper).RouteAsync(Candidate("NVDA", "p1", 2m, 110m), CancellationToken.None);

        var session = OpsEvidenceWriter.CaptureSession(
            "session-1",
            dry,
            paper,
            label: "unit-test",
            sessionStartedUtc: new DateTimeOffset(2026, 7, 9, 14, 0, 0, TimeSpan.Zero),
            sessionEndedUtc: new DateTimeOffset(2026, 7, 9, 15, 0, 0, TimeSpan.Zero));

        Assert.Equal("session-1", session.SessionId);
        Assert.Equal("unit-test", session.Label);
        Assert.Single(session.DryRunEntries);
        Assert.Single(session.PaperFills);
        Assert.Equal(dry.GetSnapshot()[0].Candidate.ClientOrderId, session.DryRunEntries[0].Candidate.ClientOrderId);
        Assert.Equal(paper.GetSnapshot()[0].ClientOrderId, session.PaperFills[0].ClientOrderId);
    }

    [Fact]
    public async Task BuildMultiSessionExportText_includes_session_ids_fill_counts_and_live_orders_false()
    {
        var dry1 = new InMemoryDryRunLedger();
        var paper1 = new InMemoryPaperLedger();
        await new DryRunOrderRouter(dry1).RouteAsync(Candidate("AAPL", "s1-d1"), CancellationToken.None);
        await new PaperOrderRouter(paper1).RouteAsync(Candidate("AAPL", "s1-p1", 1m, 190m), CancellationToken.None);
        await new PaperOrderRouter(paper1).RouteAsync(Candidate("MSFT", "s1-p2", 2m, 400m), CancellationToken.None);

        var dry2 = new InMemoryDryRunLedger();
        var paper2 = new InMemoryPaperLedger();
        await new PaperOrderRouter(paper2).RouteAsync(Candidate("TSLA", "s2-p1", 3m, 250m), CancellationToken.None);

        var s1 = OpsEvidenceWriter.CaptureSession("session-1", dry1, paper1, label: "day-1");
        var s2 = OpsEvidenceWriter.CaptureSession("session-2", dry2, paper2, label: "day-2");

        var text = OpsEvidenceWriter.BuildMultiSessionExportText(
            new[] { s1, s2 },
            exportedAtUtc: new DateTimeOffset(2026, 7, 9, 18, 0, 0, TimeSpan.Zero));

        Assert.Contains("session-1", text, StringComparison.Ordinal);
        Assert.Contains("session-2", text, StringComparison.Ordinal);
        Assert.Contains("session_count=2", text, StringComparison.Ordinal);
        Assert.Contains("paper_fill_count=2", text, StringComparison.Ordinal); // session-1
        Assert.Contains("paper_fill_count=1", text, StringComparison.Ordinal); // session-2
        Assert.Contains("total_paper_fill_count=3", text, StringComparison.Ordinal);
        Assert.Contains("total_dry_run_entry_count=1", text, StringComparison.Ordinal);
        Assert.Contains("live_orders=false", text, StringComparison.Ordinal);
        Assert.Contains("LiveSubmissionEnabled=false", text, StringComparison.Ordinal);
        Assert.Contains("client_order_id=s1-p1", text, StringComparison.Ordinal);
        Assert.Contains("client_order_id=s2-p1", text, StringComparison.Ordinal);
        Assert.Contains("symbol=TSLA", text, StringComparison.Ordinal);
        Assert.DoesNotContain("live_orders=true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitOrderAsync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSyntheticTwoSessionDrill_has_two_sessions_and_never_live()
    {
        var text = OpsEvidenceWriter.BuildSyntheticTwoSessionDrill(
            new DateTimeOffset(2026, 7, 9, 14, 0, 0, TimeSpan.Zero));

        Assert.Contains("## session-1", text, StringComparison.Ordinal);
        Assert.Contains("## session-2", text, StringComparison.Ordinal);
        Assert.Contains("session_count=2", text, StringComparison.Ordinal);
        Assert.Contains("live_orders=false", text, StringComparison.Ordinal);
        Assert.Contains("ALLOW_LIVE_ORDERS=false", text, StringComparison.Ordinal);
        Assert.Contains("KILL_SWITCH=true", text, StringComparison.Ordinal);
        Assert.Contains("ORDER_MODE=dry_run", text, StringComparison.Ordinal);
        Assert.Contains("total_paper_fill_count=5", text, StringComparison.Ordinal);
        Assert.Contains("total_dry_run_entry_count=3", text, StringComparison.Ordinal);
        Assert.Contains("client_order_id=s1-paper-1", text, StringComparison.Ordinal);
        Assert.Contains("client_order_id=s2-paper-3", text, StringComparison.Ordinal);
        Assert.Contains("secrets_included=false", text, StringComparison.Ordinal);
        Assert.DoesNotContain("live_orders=true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteMultiSessionExport_creates_file_without_secrets()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tradingbot-ops-evidence-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "paper-multi-session-export.txt");
        try
        {
            var dry = new InMemoryDryRunLedger();
            var paper = new InMemoryPaperLedger();
            paper.Append(new PaperFillRecord(
                Guid.CreateVersion7(),
                "IBM",
                "BUY",
                1m,
                50m,
                DateTimeOffset.UtcNow,
                "cid-ibm",
                "Paper virtual fill. No live order was submitted."));

            var session = OpsEvidenceWriter.CaptureSession("session-1", dry, paper);
            OpsEvidenceWriter.WriteMultiSessionExport(path, new[] { session });

            Assert.True(File.Exists(path));
            var text = File.ReadAllText(path);
            Assert.Contains("session-1", text, StringComparison.Ordinal);
            Assert.Contains("live_orders=false", text, StringComparison.Ordinal);
            Assert.Contains("client_order_id=cid-ibm", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ClientSecret", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("api_key", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildMultiSessionExportText_empty_sessions_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            OpsEvidenceWriter.BuildMultiSessionExportText(Array.Empty<OpsEvidenceSession>()));
    }

    [Fact]
    public void CaptureSession_null_ledgers_throw()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        Assert.Throws<ArgumentNullException>(() =>
            OpsEvidenceWriter.CaptureSession("s", null!, paper));
        Assert.Throws<ArgumentNullException>(() =>
            OpsEvidenceWriter.CaptureSession("s", dry, null!));
    }

    [Fact]
    public void Routers_used_for_synthetic_drill_never_enable_live_submission()
    {
        var dry = new InMemoryDryRunLedger();
        var paper = new InMemoryPaperLedger();
        Assert.False(new DryRunOrderRouter(dry).IsLiveSubmissionEnabled);
        Assert.False(new PaperOrderRouter(paper).IsLiveSubmissionEnabled);
        Assert.False(new BlockedLiveOrderRouter(TradingSafetySettings.CreateSafeDefaults()).IsLiveSubmissionEnabled);
    }
}
