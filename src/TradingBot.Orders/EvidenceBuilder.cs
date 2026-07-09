using TradingBot.Domain;

namespace TradingBot.Orders;

/// <summary>
/// Builds a <see cref="TradingEvidenceSnapshot"/> from dry-run and paper ledgers only.
/// Never enables live orders or calls Toss order HTTP.
/// </summary>
public sealed class EvidenceBuilder
{
    public const int DefaultRecentSymbolLimit = 10;

    private readonly IDryRunLedger _dryRunLedger;
    private readonly IPaperLedger _paperLedger;
    private readonly int _recentSymbolLimit;

    public EvidenceBuilder(
        IDryRunLedger dryRunLedger,
        IPaperLedger paperLedger,
        int recentSymbolLimit = DefaultRecentSymbolLimit)
    {
        _dryRunLedger = dryRunLedger ?? throw new ArgumentNullException(nameof(dryRunLedger));
        _paperLedger = paperLedger ?? throw new ArgumentNullException(nameof(paperLedger));
        if (recentSymbolLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentSymbolLimit), recentSymbolLimit, "Must be >= 0.");
        }

        _recentSymbolLimit = recentSymbolLimit;
    }

    /// <summary>Thread-safe read of both ledgers into an immutable evidence snapshot.</summary>
    public TradingEvidenceSnapshot Build()
    {
        var dryEntries = _dryRunLedger.GetSnapshot();
        var paperFills = _paperLedger.GetSnapshot();
        var capturedAt = DateTimeOffset.UtcNow;

        var dryAccepted = 0;
        var modes = new HashSet<string>(StringComparer.Ordinal);
        var liveModePresent = false;

        foreach (var entry in dryEntries)
        {
            if (entry.Accepted)
            {
                dryAccepted++;
            }

            if (!string.IsNullOrWhiteSpace(entry.Mode))
            {
                modes.Add(entry.Mode);
                if (IsLiveMode(entry.Mode))
                {
                    liveModePresent = true;
                }
            }
        }

        if (paperFills.Count > 0)
        {
            modes.Add(OrderMode.Paper.ToString());
        }

        // Paper fills have no Mode field; they are always non-live by construction.
        // Dry-run modes already inspected above.

        var summary = new EvidenceSummary(
            DryRunEntryCount: dryEntries.Count,
            DryRunAcceptedCount: dryAccepted,
            PaperFillCount: paperFills.Count,
            TotalEvidenceCount: dryEntries.Count + paperFills.Count,
            ModesPresent: modes.OrderBy(m => m, StringComparer.Ordinal).ToArray(),
            LiveModePresent: liveModePresent);

        return new TradingEvidenceSnapshot(
            CapturedAtUtc: capturedAt,
            Summary: summary,
            RecentDryRunSymbols: TakeLastSymbols(
                dryEntries.Select(e => e.Candidate.Symbol),
                _recentSymbolLimit),
            RecentPaperSymbols: TakeLastSymbols(
                paperFills.Select(f => f.Symbol),
                _recentSymbolLimit),
            DryRunEntries: dryEntries,
            PaperFills: paperFills);
    }

    private static bool IsLiveMode(string mode) =>
        string.Equals(mode, OrderMode.Live.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase);

    /// <summary>Last N symbols in append order (most recent at the end).</summary>
    private static IReadOnlyList<string> TakeLastSymbols(IEnumerable<string> symbols, int limit)
    {
        if (limit == 0)
        {
            return Array.Empty<string>();
        }

        var list = symbols.ToList();
        if (list.Count <= limit)
        {
            return list;
        }

        return list.Skip(list.Count - limit).ToArray();
    }
}
