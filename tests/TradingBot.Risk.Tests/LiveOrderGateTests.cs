using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class LiveOrderGateTests
{
    /// <summary>Settings that look "live-ish" but still fail-closed without full readiness.</summary>
    private static TradingSafetySettings LiveLookingSettings() => new()
    {
        AllowLiveOrders = true,
        KillSwitch = false,
        OrderMode = OrderMode.Live,
    };

    private static LiveOrderContext NearOpenContext(
        bool manual = true,
        bool liveImpl = false,
        bool unknown = false,
        bool missing = false,
        bool stale = false,
        bool apiError = false) => new()
    {
        ManualApprovalPresent = manual,
        LiveImplementationEnabled = liveImpl,
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
    public void Safe_defaults_always_include_three_primary_blocks_even_with_empty_context()
    {
        var defaults = TradingSafetySettings.CreateSafeDefaults();
        Assert.False(defaults.AllowLiveOrders);
        Assert.True(defaults.KillSwitch);
        Assert.Equal(OrderMode.DryRun, defaults.OrderMode);

        var decision = new LiveOrderGate().Evaluate(defaults, new LiveOrderContext());

        Assert.True(decision.IsBlocked);
        Assert.True(decision.Blocks.Count >= 4); // kill + allow + mode + manual (+ impl disabled)
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.ManualApprovalMissing.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveImplementationDisabled.Code);
    }

    [Fact]
    public void Even_open_flags_without_implementation_still_block()
    {
        var settings = LiveLookingSettings();

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
        var settings = LiveLookingSettings();

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

    [Fact]
    public void Paper_mode_blocks_live_even_when_other_settings_open()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = false,
            OrderMode = OrderMode.Paper,
        };

        var decision = new LiveOrderGate().Evaluate(settings, NearOpenContext(liveImpl: true));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
    }

    [Fact]
    public void Dry_run_mode_blocks_live()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = false,
            OrderMode = OrderMode.DryRun,
        };

        var decision = new LiveOrderGate().Evaluate(settings, NearOpenContext(liveImpl: true));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
    }

    [Fact]
    public void Kill_switch_alone_blocks_when_other_flags_open()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = true,
            OrderMode = OrderMode.Live,
        };

        var decision = new LiveOrderGate().Evaluate(settings, NearOpenContext(liveImpl: true));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
        // Only kill switch among the three primary settings flags
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
    }

    [Fact]
    public void Allow_live_false_blocks_even_in_live_mode_with_kill_off()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = false,
            KillSwitch = false,
            OrderMode = OrderMode.Live,
        };

        var decision = new LiveOrderGate().Evaluate(settings, NearOpenContext(liveImpl: true));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
    }

    [Fact]
    public void Missing_manual_approval_blocks_near_open_path()
    {
        var decision = new LiveOrderGate().Evaluate(
            LiveLookingSettings(),
            NearOpenContext(manual: false, liveImpl: true));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.ManualApprovalMissing.Code);
    }

    [Theory]
    [InlineData(true, false, false, false, "unknown_state")]
    [InlineData(false, true, false, false, "missing_data")]
    [InlineData(false, false, true, false, "stale_market_data")]
    [InlineData(false, false, false, true, "api_error")]
    public void Each_fail_closed_context_flag_blocks_independently(
        bool unknown,
        bool missing,
        bool stale,
        bool apiError,
        string expectedCode)
    {
        var decision = new LiveOrderGate().Evaluate(
            LiveLookingSettings(),
            NearOpenContext(
                manual: true,
                liveImpl: true,
                unknown: unknown,
                missing: missing,
                stale: stale,
                apiError: apiError));

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == expectedCode);
    }

    [Fact]
    public void Null_settings_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LiveOrderGate().Evaluate(null!, new LiveOrderContext()));
    }

    [Fact]
    public void Null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LiveOrderGate().Evaluate(TradingSafetySettings.CreateSafeDefaults(), null!));
    }

    [Fact]
    public void RiskGate_EvaluateLiveSubmission_uses_same_fail_closed_defaults()
    {
        var decision = new RiskGate().EvaluateLiveSubmission(
            TradingSafetySettings.CreateSafeDefaults(),
            new LiveOrderContext());

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveImplementationDisabled.Code);
    }

    [Fact]
    public void RiskGate_EvaluateForCandidate_live_settings_still_block_without_context_approvals()
    {
        // When OrderMode is Live or AllowLiveOrders, EvaluateForCandidate routes to LiveOrderGate.
        var settings = LiveLookingSettings();
        var decision = new RiskGate().EvaluateForCandidate(settings);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.ManualApprovalMissing.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.LiveImplementationDisabled.Code);
    }

    [Fact]
    public void RiskGate_EvaluateForCandidate_dry_run_allows_candidate_path()
    {
        var settings = TradingSafetySettings.CreateSafeDefaults();
        var decision = new RiskGate().EvaluateForCandidate(settings);

        Assert.True(decision.Allowed);
        Assert.Empty(decision.Blocks);
    }

    [Fact]
    public void Default_context_LiveImplementationEnabled_is_false()
    {
        var ctx = new LiveOrderContext();
        Assert.False(ctx.LiveImplementationEnabled);
        Assert.False(ctx.ManualApprovalPresent);
    }
}
