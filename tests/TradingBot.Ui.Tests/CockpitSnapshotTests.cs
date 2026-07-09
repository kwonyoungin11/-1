using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.Ui.Tests;

public class CockpitSnapshotTests
{
    [Fact]
    public void Safe_default_keeps_live_closed_for_owner_home()
    {
        var snap = CockpitSnapshot.CreateSafeDefault();

        Assert.Equal(LiveLockState.Locked, snap.LiveLock);
        Assert.True(snap.KillSwitchActive);
        Assert.False(snap.AllowLiveOrders);
        Assert.Equal(OrderMode.DryRun, snap.OrderMode);
        Assert.False(snap.IsLiveTradingVisuallyOpen);
        Assert.Contains("실거래 잠김", snap.SafetyHeadline, StringComparison.Ordinal);
        Assert.True(snap.RecentBlockMessages.Count >= 1);
        Assert.False(string.IsNullOrWhiteSpace(snap.NextActionOwnerMessage));
    }

    [Fact]
    public void Legacy_CockpitState_still_safe()
    {
        var state = CockpitState.CreateSafeDefault();
        Assert.False(state.LiveOrdersAllowed);
        Assert.True(state.KillSwitchActive);
        Assert.Equal("dry_run", state.OrderMode);
    }
}
