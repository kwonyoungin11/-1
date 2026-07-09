namespace TradingBot.Observability;

/// <summary>
/// Thread-safe in-memory audit log for tests, dry-run, and early cockpit wiring.
/// Messages are redacted on append so secrets are not retained.
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly object _gate = new();
    private readonly List<AuditEntry> _entries = new();
    private readonly int _capacity;

    public InMemoryAuditLog(int capacity = 10_000)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public void Append(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var safe = entry with
        {
            Category = string.IsNullOrWhiteSpace(entry.Category) ? "unknown" : entry.Category.Trim(),
            Message = AuditMessageRedactor.Redact(entry.Message),
            CorrelationId = string.IsNullOrWhiteSpace(entry.CorrelationId)
                ? "none"
                : entry.CorrelationId.Trim(),
        };

        lock (_gate)
        {
            _entries.Add(safe);
            TrimUnlocked();
        }
    }

    public void Append(
        string category,
        string message,
        string correlationId,
        AuditSeverity severity = AuditSeverity.Information)
    {
        Append(new AuditEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Category: category,
            Message: message,
            CorrelationId: correlationId,
            Severity: severity));
    }

    public IReadOnlyList<AuditEntry> GetRecent(int count = 100)
    {
        if (count <= 0)
        {
            return Array.Empty<AuditEntry>();
        }

        lock (_gate)
        {
            if (_entries.Count == 0)
            {
                return Array.Empty<AuditEntry>();
            }

            var start = Math.Max(0, _entries.Count - count);
            var length = _entries.Count - start;
            var copy = new AuditEntry[length];
            _entries.CopyTo(start, copy, 0, length);
            return copy;
        }
    }

    public IReadOnlyList<AuditEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    private void TrimUnlocked()
    {
        var overflow = _entries.Count - _capacity;
        if (overflow > 0)
        {
            _entries.RemoveRange(0, overflow);
        }
    }
}
