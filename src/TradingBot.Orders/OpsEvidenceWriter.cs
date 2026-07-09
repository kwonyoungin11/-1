using System.Text;
using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>
/// Writes capturable ops evidence for live-readiness (paper multi-session exports).
/// Never enables live orders, never calls Toss order HTTP, never includes secrets.
/// </summary>
public sealed class OpsEvidenceWriter
{
    /// <summary>
    /// Build multi-session paper+dry-run evidence text from real ledger snapshots.
    /// Each session is independent (separate practice windows); live remains blocked.
    /// </summary>
    public static string BuildMultiSessionExportText(
        IReadOnlyList<OpsEvidenceSession> sessions,
        DateTimeOffset? exportedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        if (sessions.Count == 0)
        {
            throw new ArgumentException("At least one session is required.", nameof(sessions));
        }

        var exportedAt = exportedAtUtc ?? DateTimeOffset.UtcNow;
        var sb = new StringBuilder();
        sb.AppendLine("# paper multi-session export (ops evidence)");
        sb.AppendLine("# source=InMemoryPaperLedger+InMemoryDryRunLedger snapshots");
        sb.AppendLine("# live remains blocked — this file is NOT a live order journal");
        sb.Append("exported_at_utc=").AppendLine(exportedAt.ToString("O"));
        sb.AppendLine("live_orders=false");
        sb.AppendLine("LiveSubmissionEnabled=false");
        sb.AppendLine("ALLOW_LIVE_ORDERS=false");
        sb.AppendLine("KILL_SWITCH=true");
        sb.AppendLine("ORDER_MODE=dry_run");
        sb.Append("session_count=").Append(sessions.Count).AppendLine();

        var totalPaperFills = 0;
        var totalDryRun = 0;

        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i] ?? throw new ArgumentException($"sessions[{i}] is null.", nameof(sessions));
            var dry = session.DryRunEntries ?? Array.Empty<DryRunLedgerEntry>();
            var paper = session.PaperFills ?? Array.Empty<PaperFillRecord>();
            totalDryRun += dry.Count;
            totalPaperFills += paper.Count;

            var sessionId = string.IsNullOrWhiteSpace(session.SessionId)
                ? $"session-{i + 1}"
                : session.SessionId.Trim();

            sb.AppendLine();
            sb.Append("## ").AppendLine(sessionId);
            sb.Append("session_id=").AppendLine(sessionId);
            if (!string.IsNullOrWhiteSpace(session.Label))
            {
                sb.Append("session_label=").AppendLine(session.Label.Trim());
            }

            if (session.SessionStartedUtc is { } started)
            {
                sb.Append("session_started_utc=").AppendLine(started.ToString("O"));
            }

            if (session.SessionEndedUtc is { } ended)
            {
                sb.Append("session_ended_utc=").AppendLine(ended.ToString("O"));
            }

            sb.AppendLine("live_orders=false");
            sb.Append("dry_run_entry_count=").Append(dry.Count).AppendLine();
            sb.Append("paper_fill_count=").Append(paper.Count).AppendLine();

            sb.AppendLine("### dry_run_entries");
            foreach (var entry in dry)
            {
                sb.Append("symbol=").Append(entry.Candidate.Symbol)
                    .Append(" client_order_id=").Append(entry.Candidate.ClientOrderId)
                    .Append(" mode=").Append(entry.Mode)
                    .Append(" accepted=").Append(entry.Accepted ? "true" : "false")
                    .Append(" side=").Append(entry.Candidate.Side)
                    .Append(" quantity=").Append(entry.Candidate.Quantity)
                    .Append(" limit_price=").Append(entry.Candidate.LimitPrice?.ToString() ?? "null")
                    .AppendLine();
            }

            sb.AppendLine("### paper_fills");
            foreach (var fill in paper)
            {
                sb.Append("symbol=").Append(fill.Symbol)
                    .Append(" client_order_id=").Append(fill.ClientOrderId)
                    .Append(" mode=").Append(OrderMode.Paper.ToString())
                    .Append(" side=").Append(fill.Side)
                    .Append(" quantity=").Append(fill.Quantity)
                    .Append(" price=").Append(fill.Price)
                    .AppendLine();
            }
        }

        sb.AppendLine();
        sb.AppendLine("## totals");
        sb.Append("total_dry_run_entry_count=").Append(totalDryRun).AppendLine();
        sb.Append("total_paper_fill_count=").Append(totalPaperFills).AppendLine();
        sb.AppendLine("live_orders=false");
        sb.AppendLine("evidence_kind=paper_multi_session");
        sb.AppendLine("secrets_included=false");

        return sb.ToString();
    }

    /// <summary>
    /// Capture a session from live ledger instances via <see cref="IDryRunLedger.GetSnapshot"/> /
    /// <see cref="IPaperLedger.GetSnapshot"/>.
    /// </summary>
    public static OpsEvidenceSession CaptureSession(
        string sessionId,
        IDryRunLedger dryRunLedger,
        IPaperLedger paperLedger,
        string? label = null,
        DateTimeOffset? sessionStartedUtc = null,
        DateTimeOffset? sessionEndedUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(dryRunLedger);
        ArgumentNullException.ThrowIfNull(paperLedger);

        return new OpsEvidenceSession(
            SessionId: sessionId.Trim(),
            Label: label,
            SessionStartedUtc: sessionStartedUtc,
            SessionEndedUtc: sessionEndedUtc,
            DryRunEntries: dryRunLedger.GetSnapshot(),
            PaperFills: paperLedger.GetSnapshot());
    }

    /// <summary>
    /// Write multi-session export text to <paramref name="outputPath"/> (UTF-8, no secrets).
    /// Creates parent directory if needed.
    /// </summary>
    public static void WriteMultiSessionExport(
        string outputPath,
        IReadOnlyList<OpsEvidenceSession> sessions,
        DateTimeOffset? exportedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var text = BuildMultiSessionExportText(sessions, exportedAtUtc);
        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, text);
    }

    /// <summary>
    /// Synthetic two-session paper drill using real <see cref="InMemoryDryRunLedger"/> /
    /// <see cref="InMemoryPaperLedger"/> + routers. Suitable for regenerating sample ops artifacts.
    /// </summary>
    public static string BuildSyntheticTwoSessionDrill(DateTimeOffset? baseUtc = null)
    {
        var day = (baseUtc ?? new DateTimeOffset(2026, 7, 9, 14, 0, 0, TimeSpan.Zero)).UtcDateTime;
        var session1Start = new DateTimeOffset(day.Date.AddHours(14), TimeSpan.Zero);
        var session2Start = new DateTimeOffset(day.Date.AddDays(1).AddHours(15), TimeSpan.Zero);

        // Session 1
        var dry1 = new InMemoryDryRunLedger();
        var paper1 = new InMemoryPaperLedger();
        var dryRouter1 = new DryRunOrderRouter(dry1);
        var paperRouter1 = new PaperOrderRouter(paper1);

        dryRouter1.RouteAsync(Candidate("AAPL", "s1-dry-1", 2m, 190m), CancellationToken.None)
            .GetAwaiter().GetResult();
        dryRouter1.RouteAsync(Candidate("MSFT", "s1-dry-2", 1m, 420m), CancellationToken.None)
            .GetAwaiter().GetResult();
        paperRouter1.RouteAsync(Candidate("AAPL", "s1-paper-1", 2m, 190.5m), CancellationToken.None)
            .GetAwaiter().GetResult();
        paperRouter1.RouteAsync(Candidate("NVDA", "s1-paper-2", 1m, 120m), CancellationToken.None)
            .GetAwaiter().GetResult();

        var s1 = CaptureSession(
            "session-1",
            dry1,
            paper1,
            label: "paper-drill-day-1",
            sessionStartedUtc: session1Start,
            sessionEndedUtc: session1Start.AddHours(1));

        // Session 2 (fresh ledgers — multi-session, not a single continuous process)
        var dry2 = new InMemoryDryRunLedger();
        var paper2 = new InMemoryPaperLedger();
        var dryRouter2 = new DryRunOrderRouter(dry2);
        var paperRouter2 = new PaperOrderRouter(paper2);

        dryRouter2.RouteAsync(Candidate("TSLA", "s2-dry-1", 3m, 250m), CancellationToken.None)
            .GetAwaiter().GetResult();
        paperRouter2.RouteAsync(Candidate("TSLA", "s2-paper-1", 3m, 251m), CancellationToken.None)
            .GetAwaiter().GetResult();
        paperRouter2.RouteAsync(Candidate("AMD", "s2-paper-2", 5m, 160m), CancellationToken.None)
            .GetAwaiter().GetResult();
        paperRouter2.RouteAsync(Candidate("MSFT", "s2-paper-3", 1m, 421m), CancellationToken.None)
            .GetAwaiter().GetResult();

        var s2 = CaptureSession(
            "session-2",
            dry2,
            paper2,
            label: "paper-drill-day-2",
            sessionStartedUtc: session2Start,
            sessionEndedUtc: session2Start.AddHours(1));

        return BuildMultiSessionExportText(
            new[] { s1, s2 },
            exportedAtUtc: session2Start.AddHours(2));
    }

    private static OrderCandidate Candidate(
        string symbol,
        string clientOrderId,
        decimal quantity,
        decimal limitPrice) =>
        new(
            symbol,
            "BUY",
            "LIMIT",
            quantity,
            limitPrice,
            clientOrderId,
            DateTimeOffset.UtcNow);
}

/// <summary>
/// One practice session window of dry-run + paper ledger snapshots for ops evidence.
/// </summary>
public sealed record OpsEvidenceSession(
    string SessionId,
    string? Label,
    DateTimeOffset? SessionStartedUtc,
    DateTimeOffset? SessionEndedUtc,
    IReadOnlyList<DryRunLedgerEntry> DryRunEntries,
    IReadOnlyList<PaperFillRecord> PaperFills);
