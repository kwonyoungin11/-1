using TradingBot.Observability;

namespace TradingBot.Observability.Tests;

public class SessionAuditEventsTests
{
    [Fact]
    public void Categories_are_stable_session_auto_start_stop()
    {
        Assert.Equal("session", SessionAuditCategories.Session);
        Assert.Equal("auto_start", SessionAuditCategories.AutoStart);
        Assert.Equal("auto_stop", SessionAuditCategories.AutoStop);
    }

    [Fact]
    public void CreateAutoTradeStart_uses_session_category_and_auto_start_correlation()
    {
        var ts = new DateTimeOffset(2026, 7, 9, 15, 0, 0, TimeSpan.Zero);
        var entry = SessionAuditEvents.CreateAutoTradeStart(
            "자동매매(연습) 시작 · 실주문은 나가지 않습니다.",
            timestampUtc: ts);

        Assert.Equal(ts, entry.Timestamp);
        Assert.Equal(SessionAuditCategories.Session, entry.Category);
        Assert.Equal(SessionAuditCategories.AutoStart, entry.CorrelationId);
        Assert.Equal(AuditSeverity.Information, entry.Severity);
        Assert.Contains("연습", entry.Message, StringComparison.Ordinal);
        Assert.Contains("실주문", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateAutoTradeStop_uses_session_category_and_auto_stop_correlation()
    {
        var entry = SessionAuditEvents.CreateAutoTradeStop("자동매매(연습) 종료.");

        Assert.Equal(SessionAuditCategories.Session, entry.Category);
        Assert.Equal(SessionAuditCategories.AutoStop, entry.CorrelationId);
        Assert.Contains("종료", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_null_or_whitespace_message_gets_safe_default_not_secret()
    {
        var start = SessionAuditEvents.CreateAutoTradeStart(null);
        var stop = SessionAuditEvents.CreateAutoTradeStop("   ");

        Assert.False(string.IsNullOrWhiteSpace(start.Message));
        Assert.False(string.IsNullOrWhiteSpace(stop.Message));
        Assert.Contains("연습", start.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer ", start.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", start.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_redacts_bearer_token_material_from_owner_message()
    {
        const string secret = "super-secret-access-token-xyz";
        var poisoned = $"Authorization: Bearer {secret} — session start ok";

        var entry = SessionAuditEvents.CreateAutoTradeStart(poisoned);

        Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", entry.Message, StringComparison.Ordinal);
        Assert.Equal(SessionAuditCategories.AutoStart, entry.CorrelationId);
    }

    [Fact]
    public void Create_redacts_bearer_token_on_stop_as_well()
    {
        const string secret = "stop-token-should-not-persist";
        var entry = SessionAuditEvents.CreateAutoTradeStop($"Bearer {secret}");

        Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal);
        Assert.Contains("Bearer [REDACTED]", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendAutoTradeStart_and_Stop_write_redacted_entries_to_log()
    {
        var log = new InMemoryAuditLog();
        const string secret = "live-oauth-token-value-999";

        SessionAuditEvents.AppendAutoTradeStart(log, $"start with Bearer {secret}");
        SessionAuditEvents.AppendAutoTradeStop(log, "자동매매(연습) 종료.");

        Assert.Equal(2, log.Count);
        var snap = log.Snapshot();

        Assert.Equal(SessionAuditCategories.Session, snap[0].Category);
        Assert.Equal(SessionAuditCategories.AutoStart, snap[0].CorrelationId);
        Assert.DoesNotContain(secret, snap[0].Message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", snap[0].Message, StringComparison.Ordinal);

        Assert.Equal(SessionAuditCategories.AutoStop, snap[1].CorrelationId);
        Assert.Contains("종료", snap[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Append_null_log_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SessionAuditEvents.AppendAutoTradeStart(null!, "x"));
        Assert.Throws<ArgumentNullException>(() =>
            SessionAuditEvents.AppendAutoTradeStop(null!, "x"));
    }

    [Fact]
    public void Create_preserves_custom_severity()
    {
        var entry = SessionAuditEvents.CreateAutoTradeStop(
            "already stopped",
            severity: AuditSeverity.Warning);

        Assert.Equal(AuditSeverity.Warning, entry.Severity);
    }
}
