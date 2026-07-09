using TradingBot.Observability;

namespace TradingBot.Observability.Tests;

public class InMemoryAuditLogTests
{
    [Fact]
    public void Append_stores_entry_with_redacted_message()
    {
        var log = new InMemoryAuditLog();
        log.Append(
            category: "oauth",
            message: "Token response Authorization: Bearer secret-token-value",
            correlationId: "corr-1",
            severity: AuditSeverity.Information);

        var recent = log.GetRecent(1);
        Assert.Single(recent);
        Assert.Equal("oauth", recent[0].Category);
        Assert.Equal("corr-1", recent[0].CorrelationId);
        Assert.DoesNotContain("secret-token-value", recent[0].Message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", recent[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Append_entry_record_preserves_severity_and_timestamp()
    {
        var log = new InMemoryAuditLog();
        var ts = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        log.Append(new AuditEntry(ts, "risk", "Candidate blocked", "cid-9", AuditSeverity.Warning));

        var snap = log.Snapshot();
        Assert.Single(snap);
        Assert.Equal(ts, snap[0].Timestamp);
        Assert.Equal(AuditSeverity.Warning, snap[0].Severity);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void GetRecent_returns_oldest_first_within_window()
    {
        var log = new InMemoryAuditLog();
        log.Append("system", "one", "a");
        log.Append("system", "two", "b");
        log.Append("system", "three", "c");

        var recent = log.GetRecent(2);
        Assert.Equal(2, recent.Count);
        Assert.Equal("two", recent[0].Message);
        Assert.Equal("three", recent[1].Message);
    }

    [Fact]
    public void Capacity_trims_oldest_entries()
    {
        var log = new InMemoryAuditLog(capacity: 2);
        log.Append("system", "keep-me-not", "1");
        log.Append("system", "second", "2");
        log.Append("system", "third", "3");

        Assert.Equal(2, log.Count);
        var snap = log.Snapshot();
        Assert.Equal("second", snap[0].Message);
        Assert.Equal("third", snap[1].Message);
    }

    [Fact]
    public void Empty_category_and_correlation_get_safe_defaults()
    {
        var log = new InMemoryAuditLog();
        log.Append(new AuditEntry(DateTimeOffset.UtcNow, "  ", "ok", "  "));

        var entry = Assert.Single(log.Snapshot());
        Assert.Equal("unknown", entry.Category);
        Assert.Equal("none", entry.CorrelationId);
    }

    [Fact]
    public void Invalid_capacity_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryAuditLog(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryAuditLog(-1));
    }
}
