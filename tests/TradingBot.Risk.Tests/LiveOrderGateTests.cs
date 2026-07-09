using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class LiveOrderGateTests
{
    private static TradingSafetySettings LiveLookingSettings() => new()
    {
        AllowLiveOrders = true,
        KillSwitch = false,
        OrderMode = OrderMode.Live,
    };

    private static LiveOrderContext HealthyContext(
        bool unknown = false,
        bool missing = false,
        bool stale = false,
        bool apiError = false) => new()
    {
        HasUnknownState = unknown,
        HasMissingData = missing,
        HasStaleMarketData = stale,
        HasApiError = apiError,
    };

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
    public void Safe_defaults_block_with_three_primary_settings_flags()
    {
        var defaults = TradingSafetySettings.CreateSafeDefaults();
        var decision = new LiveOrderGate().Evaluate(defaults, new LiveOrderContext());

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
    }

    [Fact]
    public void Open_settings_with_healthy_context_allow()
    {
        var decision = new LiveOrderGate().Evaluate(LiveLookingSettings(), HealthyContext());

        Assert.True(decision.Allowed);
        Assert.Empty(decision.Blocks);
    }

    [Fact]
    public void Unknown_or_missing_or_stale_or_api_error_blocks()
    {
        var decision = new LiveOrderGate().Evaluate(
            LiveLookingSettings(),
            HealthyContext(
                unknown: true,
                missing: true,
                stale: true,
                apiError: true));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.UnknownState.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.ApiError.Code);
    }

    [Fact]
    public void Paper_mode_blocks_live_even_when_other_settings_open()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = false,
            OrderMode = OrderMode.Paper,
        };

        var decision = new LiveOrderGate().Evaluate(settings, HealthyContext());

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
    }

    [Fact]
    public void Kill_switch_blocks_when_other_flags_open()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = true,
            OrderMode = OrderMode.Live,
        };

        var decision = new LiveOrderGate().Evaluate(settings, HealthyContext());

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
    }

    [Theory]
    [InlineData(true, false, false, false, "unknown_state")]
    [InlineData(false, true, false, false, "missing_data")]
    [InlineData(false, false, true, false, "stale_market_data")]
    [InlineData(false, false, false, true, "api_error")]
    public void Each_data_health_flag_blocks_independently(
        bool unknown,
        bool missing,
        bool stale,
        bool apiError,
        string expectedCode)
    {
        var decision = new LiveOrderGate().Evaluate(
            LiveLookingSettings(),
            HealthyContext(unknown, missing, stale, apiError));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == expectedCode);
    }

    [Fact]
    public void RiskGate_EvaluateForCandidate_live_settings_allow_with_healthy_context()
    {
        var settings = LiveLookingSettings();
        var decision = new RiskGate().EvaluateForCandidate(
            settings,
            new LiveOrderContext());

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void RiskGate_EvaluateForCandidate_dry_run_allows_candidate_path()
    {
        var decision = new RiskGate().EvaluateForCandidate(TradingSafetySettings.CreateSafeDefaults());

        Assert.True(decision.Allowed);
        Assert.Empty(decision.Blocks);
    }
}