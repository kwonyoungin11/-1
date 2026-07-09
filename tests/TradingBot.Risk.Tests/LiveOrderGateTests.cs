using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class LiveOrderGateTests
{
    [Fact]
    public void Default_settings_block_live()
    {
        var gate = new LiveOrderGate();
        var decision = gate.Evaluate(TradingSafetySettings.CreateSafeDefaults(), new LiveOrderContext());

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
    }

    [Fact]
    public void Even_open_flags_without_implementation_still_block()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = false,
            OrderMode = OrderMode.Live,
        };

        var gate = new LiveOrderGate();
        var decision = gate.Evaluate(settings, new LiveOrderContext
        {
            ManualApprovalPresent = true,
            LiveImplementationEnabled = false,
        });

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveImplementationDisabled.Code);
    }

    [Fact]
    public void Unknown_or_missing_or_stale_or_api_error_blocks()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = false,
            OrderMode = OrderMode.Live,
        };

        var gate = new LiveOrderGate();
        var decision = gate.Evaluate(settings, new LiveOrderContext
        {
            ManualApprovalPresent = true,
            LiveImplementationEnabled = true,
            HasUnknownState = true,
            HasMissingData = true,
            HasStaleMarketData = true,
            HasApiError = true,
        });

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.UnknownState.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.ApiError.Code);
    }
}
