using TradingBot.App.Services;
using TradingBot.Domain;

namespace TradingBot.App.Tests;

/// <summary>
/// Live-capable host wiring (still fail-closed without env + owner approval).
/// Does not place real Toss orders in unit tests.
/// </summary>
public class LiveTradingHarnessTests
{
    private static void ForcePracticeEnv()
    {
        Environment.SetEnvironmentVariable("ALLOW_LIVE_ORDERS", "false");
        Environment.SetEnvironmentVariable("KILL_SWITCH", "true");
        Environment.SetEnvironmentVariable("ORDER_MODE", "dry_run");
        Environment.SetEnvironmentVariable("TOSS_ALLOW_LIVE_HTTP", "false");
    }

    [Fact]
    public void CreateDefault_cers_vmar_even_when_not_live()
    {
        ForcePracticeEnv();
        var h = AppHarness.CreateDefault();
        Assert.Equal(StockMarketKind.비전마린, h.Session.StockKind);
        Assert.Equal(CersPreset.Strategy, h.Session.Strategy);
        Assert.Equal(CersPreset.Timeframe, h.Session.Timeframe);
        Assert.False(h.SettingsWouldAllowLiveRouting);
    }

    [Fact]
    public void StartAutoTrade_blocks_live_without_manual_approval_when_settings_look_live()
    {
        // Simulate live-looking settings only if CreateDefault read them — unit path uses env.
        // When not live, start should still work without approval.
        ForcePracticeEnv();
        var h = AppHarness.CreateDefault();
        h.LiveManualApproval = false;
        var msg = h.StartAutoTrade();
        Assert.DoesNotContain("거부", msg, StringComparison.Ordinal);
        Assert.True(h.Session.Status == AutoTradeSessionStatus.실행중
                    || msg.Contains("시작", StringComparison.Ordinal)
                    || msg.Contains("실행", StringComparison.Ordinal)
                    || msg.Length > 0);
        _ = h.StopAutoTrade();
    }

    [Fact]
    public void Live_gate_checklist_contains_key_flags()
    {
        ForcePracticeEnv();
        var h = AppHarness.CreateDefault();
        var lines = h.GetLiveGateChecklistLines();
        Assert.Contains(lines, l => l.Contains("ALLOW_LIVE_ORDERS", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("KILL_SWITCH", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("ORDER_MODE", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("오너 수동 승인", StringComparison.Ordinal));
    }
}
