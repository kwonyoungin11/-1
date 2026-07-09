using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>
/// Exports dry-run + paper ledger evidence for live-readiness documentation.
/// Never enables live orders, never calls Toss order HTTP, never includes secrets.
/// </summary>
public sealed class TradingEvidenceExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly IDryRunLedger _dryRunLedger;
    private readonly IPaperLedger _paperLedger;

    public TradingEvidenceExporter(IDryRunLedger dryRunLedger, IPaperLedger paperLedger)
    {
        _dryRunLedger = dryRunLedger ?? throw new ArgumentNullException(nameof(dryRunLedger));
        _paperLedger = paperLedger ?? throw new ArgumentNullException(nameof(paperLedger));
    }

    /// <summary>
    /// Reads real ledger snapshots via <see cref="IDryRunLedger.GetSnapshot"/> /
    /// <see cref="IPaperLedger.GetSnapshot"/> and builds a structured export document.
    /// </summary>
    public TradingEvidenceExportDocument Export()
    {
        return FromSnapshots(_dryRunLedger.GetSnapshot(), _paperLedger.GetSnapshot());
    }

    /// <summary>JSON string of <see cref="Export"/> (includes <c>live_orders=false</c>).</summary>
    public string ExportAsJson() => ToJson(Export());

    /// <summary>Plain-text lines for readiness docs (includes <c>live_orders=false</c>).</summary>
    public string ExportAsText() => ToText(Export());

    /// <summary>Build export from already-captured ledger lists (no live data).</summary>
    public static TradingEvidenceExportDocument FromSnapshots(
        IReadOnlyList<DryRunLedgerEntry> dryRunEntries,
        IReadOnlyList<PaperFillRecord> paperFills)
    {
        ArgumentNullException.ThrowIfNull(dryRunEntries);
        ArgumentNullException.ThrowIfNull(paperFills);

        var dryAccepted = 0;
        var modes = new HashSet<string>(StringComparer.Ordinal);
        var symbols = new List<string>(dryRunEntries.Count + paperFills.Count);
        var clientOrderIds = new List<string>(dryRunEntries.Count + paperFills.Count);
        var dryLines = new List<EvidenceExportDryRunLine>(dryRunEntries.Count);
        var paperLines = new List<EvidenceExportPaperLine>(paperFills.Count);

        foreach (var entry in dryRunEntries)
        {
            if (entry.Accepted)
            {
                dryAccepted++;
            }

            if (!string.IsNullOrWhiteSpace(entry.Mode))
            {
                modes.Add(entry.Mode);
            }

            var symbol = entry.Candidate.Symbol;
            var clientOrderId = entry.Candidate.ClientOrderId;
            symbols.Add(symbol);
            clientOrderIds.Add(clientOrderId);

            dryLines.Add(new EvidenceExportDryRunLine(
                Symbol: symbol,
                ClientOrderId: clientOrderId,
                Mode: entry.Mode,
                Accepted: entry.Accepted,
                Side: entry.Candidate.Side,
                Quantity: entry.Candidate.Quantity,
                LimitPrice: entry.Candidate.LimitPrice,
                RecordedAtUtc: entry.RecordedAtUtc));
        }

        foreach (var fill in paperFills)
        {
            modes.Add(OrderMode.Paper.ToString());
            symbols.Add(fill.Symbol);
            clientOrderIds.Add(fill.ClientOrderId);

            paperLines.Add(new EvidenceExportPaperLine(
                Symbol: fill.Symbol,
                ClientOrderId: fill.ClientOrderId,
                Mode: OrderMode.Paper.ToString(),
                Side: fill.Side,
                Quantity: fill.Quantity,
                Price: fill.Price,
                FilledAtUtc: fill.FilledAtUtc));
        }

        var orderedModes = modes.OrderBy(m => m, StringComparer.Ordinal).ToArray();

        return new TradingEvidenceExportDocument(
            ExportedAtUtc: DateTimeOffset.UtcNow,
            LiveOrders: false,
            LiveSubmissionEnabled: false,
            DryRunEntryCount: dryRunEntries.Count,
            DryRunAcceptedCount: dryAccepted,
            PaperFillCount: paperFills.Count,
            TotalEvidenceCount: dryRunEntries.Count + paperFills.Count,
            Modes: orderedModes,
            Symbols: symbols.ToArray(),
            ClientOrderIds: clientOrderIds.ToArray(),
            DryRunEntries: dryLines,
            PaperFills: paperLines);
    }

    public static string ToJson(TradingEvidenceExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <summary>
    /// Plain-text export with explicit <c>live_orders=false</c> and
    /// <c>LiveSubmissionEnabled=false</c> lines for readiness checklists.
    /// </summary>
    public static string ToText(TradingEvidenceExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        sb.AppendLine("# Trading evidence export (dry-run + paper only; live remains blocked)");
        sb.Append("exported_at_utc=").AppendLine(document.ExportedAtUtc.ToString("O"));
        sb.AppendLine("live_orders=false");
        sb.AppendLine("LiveSubmissionEnabled=false");
        sb.Append("dry_run_entry_count=").Append(document.DryRunEntryCount).AppendLine();
        sb.Append("dry_run_accepted_count=").Append(document.DryRunAcceptedCount).AppendLine();
        sb.Append("paper_fill_count=").Append(document.PaperFillCount).AppendLine();
        sb.Append("total_evidence_count=").Append(document.TotalEvidenceCount).AppendLine();
        sb.Append("modes=").AppendLine(string.Join(',', document.Modes));
        sb.Append("symbols=").AppendLine(string.Join(',', document.Symbols));
        sb.Append("client_order_ids=").AppendLine(string.Join(',', document.ClientOrderIds));

        sb.AppendLine("## dry_run_entries");
        foreach (var line in document.DryRunEntries)
        {
            sb.Append("symbol=").Append(line.Symbol)
                .Append(" client_order_id=").Append(line.ClientOrderId)
                .Append(" mode=").Append(line.Mode)
                .Append(" accepted=").Append(line.Accepted ? "true" : "false")
                .Append(" side=").Append(line.Side)
                .Append(" quantity=").Append(line.Quantity)
                .Append(" limit_price=").Append(line.LimitPrice?.ToString() ?? "null")
                .AppendLine();
        }

        sb.AppendLine("## paper_fills");
        foreach (var line in document.PaperFills)
        {
            sb.Append("symbol=").Append(line.Symbol)
                .Append(" client_order_id=").Append(line.ClientOrderId)
                .Append(" mode=").Append(line.Mode)
                .Append(" side=").Append(line.Side)
                .Append(" quantity=").Append(line.Quantity)
                .Append(" price=").Append(line.Price)
                .AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>
/// Structured readiness export. Always reports live blocked.
/// Contains counts, symbols, client order ids, modes — no secrets / tokens / accounts.
/// </summary>
public sealed record TradingEvidenceExportDocument(
    DateTimeOffset ExportedAtUtc,
    [property: JsonPropertyName("live_orders")] bool LiveOrders,
    [property: JsonPropertyName("LiveSubmissionEnabled")] bool LiveSubmissionEnabled,
    int DryRunEntryCount,
    int DryRunAcceptedCount,
    int PaperFillCount,
    int TotalEvidenceCount,
    IReadOnlyList<string> Modes,
    IReadOnlyList<string> Symbols,
    IReadOnlyList<string> ClientOrderIds,
    IReadOnlyList<EvidenceExportDryRunLine> DryRunEntries,
    IReadOnlyList<EvidenceExportPaperLine> PaperFills);

/// <summary>One dry-run line in a readiness export (no free-text secrets).</summary>
public sealed record EvidenceExportDryRunLine(
    string Symbol,
    string ClientOrderId,
    string Mode,
    bool Accepted,
    string Side,
    decimal Quantity,
    decimal? LimitPrice,
    DateTimeOffset RecordedAtUtc);

/// <summary>One paper fill line in a readiness export (virtual only).</summary>
public sealed record EvidenceExportPaperLine(
    string Symbol,
    string ClientOrderId,
    string Mode,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset FilledAtUtc);
