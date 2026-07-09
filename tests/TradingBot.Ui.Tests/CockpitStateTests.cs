using TradingBot.Ui;

namespace TradingBot.Ui.Tests;

public class CockpitStateTests
{
    [Fact]
    public void Safe_default_shows_live_blocked()
    {
        var state = CockpitState.CreateSafeDefault();
        Assert.False(state.LiveOrdersAllowed);
        Assert.True(state.KillSwitchActive);
        Assert.Equal("dry_run", state.OrderMode);
    }
}
