using TradingBot.Domain;
using TradingBot.Orders;
using TradingBot.Risk;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Evidence: real <see cref="LiveOrderGate"/> + real <see cref="GatedLiveOrderRouter"/>
/// with recording transport only (no network). Safe defaults never enable live submission.
/// </summary>
public class GatedLiveOrderRouterTests
{
    private static OrderCandidate Sample(string clientOrderId = "gated-live-1") => new(
        "AAPL",
        "BUY",
        "LIMIT",
        1m,
        100m,
        clientOrderId,
        DateTimeOffset.UtcNow);

    /// <summary>Settings that open the three primary safety flags (still needs full context).</summary>
    private static TradingSafetySettings AllGatesOpenSettings() => new()
    {
        AllowLiveOrders = true,
        KillSwitch = false,
        OrderMode = OrderMode.Live,
    };

    /// <summary>Context with every live eligibility flag open (test-only; not app default).</summary>
    private static LiveOrderContext AllGatesOpenContext() => new()
    {
        ManualApprovalPresent = true,
        LiveImplementationEnabled = true,
        HasUnknownState = false,
        HasMissingData = false,
        HasStaleMarketData = false,
        HasApiError = false,
    };

    private static GatedLiveOrderRouter CreateRouter(
        TradingSafetySettings settings,
        LiveOrderContext context,
        RecordingLiveOrderTransport transport,
        ClientOrderIdIndex? index = null)
    {
        // MUST use real LiveOrderGate — not a fake.
        return new GatedLiveOrderRouter(
            settings,
            context,
            transport,
            gate: new LiveOrderGate(),
            clientOrderIdIndex: index);
    }

    [Fact]
    public async Task Defaults_settings_and_empty_context_block_without_transport_call()
    {
        var transport = new RecordingLiveOrderTransport();
        var router = CreateRouter(
            TradingSafetySettings.CreateSafeDefaults(),
            new LiveOrderContext(),
            transport);

        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("live_blocked", result.Mode);
        Assert.Equal(0, transport.CallCount);
        Assert.NotEmpty(result.Blocks);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.LiveImplementationDisabled.Code);
        Assert.Contains("No transport call", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task All_gates_open_accepts_and_calls_transport_once()
    {
        var transport = new RecordingLiveOrderTransport();
        var router = CreateRouter(
            AllGatesOpenSettings(),
            AllGatesOpenContext(),
            transport);

        Assert.True(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample("open-gates-1"), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.Live.ToString(), result.Mode);
        Assert.Equal(1, transport.CallCount);
        Assert.Equal("open-gates-1", Assert.Single(transport.Calls).ClientOrderId);
        Assert.Empty(result.Blocks);
    }

    [Fact]
    public async Task Second_same_ClientOrderId_rejects_without_second_transport_call()
    {
        var transport = new RecordingLiveOrderTransport();
        var index = new ClientOrderIdIndex();
        var router = CreateRouter(
            AllGatesOpenSettings(),
            AllGatesOpenContext(),
            transport,
            index);

        Assert.True(router.IsLiveSubmissionEnabled);

        var first = await router.RouteAsync(Sample("dup-live-1"), CancellationToken.None);
        var second = await router.RouteAsync(Sample("dup-live-1"), CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.Equal(1, transport.CallCount);

        Assert.False(second.Accepted);
        Assert.Equal(1, transport.CallCount); // still one — no second submit
        Assert.Contains("duplicate client order id", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void IsLiveSubmissionEnabled_false_under_CreateSafeDefaults()
    {
        var transport = new RecordingLiveOrderTransport();
        var router = CreateRouter(
            TradingSafetySettings.CreateSafeDefaults(),
            AllGatesOpenContext(), // even with open context, settings defaults block enable flag
            transport);

        Assert.False(router.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void IsLiveSubmissionEnabled_requires_all_four_conditions()
    {
        var transport = new RecordingLiveOrderTransport();
        var openCtx = AllGatesOpenContext();
        var closedCtx = new LiveOrderContext { LiveImplementationEnabled = false };

        Assert.False(CreateRouter(
            new TradingSafetySettings
            {
                AllowLiveOrders = false,
                KillSwitch = false,
                OrderMode = OrderMode.Live,
            },
            openCtx,
            transport).IsLiveSubmissionEnabled);

        Assert.False(CreateRouter(
            new TradingSafetySettings
            {
                AllowLiveOrders = true,
                KillSwitch = true,
                OrderMode = OrderMode.Live,
            },
            openCtx,
            transport).IsLiveSubmissionEnabled);

        Assert.False(CreateRouter(
            new TradingSafetySettings
            {
                AllowLiveOrders = true,
                KillSwitch = false,
                OrderMode = OrderMode.DryRun,
            },
            openCtx,
            transport).IsLiveSubmissionEnabled);

        Assert.False(CreateRouter(
            AllGatesOpenSettings(),
            closedCtx,
            transport).IsLiveSubmissionEnabled);

        Assert.True(CreateRouter(
            AllGatesOpenSettings(),
            openCtx,
            transport).IsLiveSubmissionEnabled);
    }

    [Fact]
    public async Task Gate_blocks_on_missing_manual_approval_even_when_IsLiveSubmissionEnabled()
    {
        // IsLiveSubmissionEnabled does not require manual approval; RouteAsync gate does.
        var transport = new RecordingLiveOrderTransport();
        var context = new LiveOrderContext
        {
            ManualApprovalPresent = false,
            LiveImplementationEnabled = true,
        };
        var router = CreateRouter(AllGatesOpenSettings(), context, transport);

        Assert.True(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("live_blocked", result.Mode);
        Assert.Equal(0, transport.CallCount);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.ManualApprovalMissing.Code);
    }

    [Fact]
    public async Task Transport_failure_returns_Accepted_false_after_one_call()
    {
        var transport = new RecordingLiveOrderTransport
        {
            ResultFactory = _ => new LiveTransportResult(false, "Broker rejected (recorded)."),
        };
        var router = CreateRouter(
            AllGatesOpenSettings(),
            AllGatesOpenContext(),
            transport);

        var result = await router.RouteAsync(Sample("fail-tx-1"), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(OrderMode.Live.ToString(), result.Mode);
        Assert.Equal(1, transport.CallCount);
        Assert.Contains("Broker rejected", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Null_candidate_throws()
    {
        var router = CreateRouter(
            TradingSafetySettings.CreateSafeDefaults(),
            new LiveOrderContext(),
            new RecordingLiveOrderTransport());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => router.RouteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Null_settings_or_transport_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GatedLiveOrderRouter(
                null!,
                new LiveOrderContext(),
                new RecordingLiveOrderTransport()));

        Assert.Throws<ArgumentNullException>(() =>
            new GatedLiveOrderRouter(
                TradingSafetySettings.CreateSafeDefaults(),
                new LiveOrderContext(),
                null!));

        Assert.Throws<ArgumentNullException>(() =>
            new GatedLiveOrderRouter(
                TradingSafetySettings.CreateSafeDefaults(),
                (LiveOrderContext)null!,
                new RecordingLiveOrderTransport()));
    }

    [Fact]
    public void BlockedLiveOrderRouter_still_never_enables_live_when_gates_open()
    {
        // Legacy harness router remains hard-blocked even with open flags.
        var router = new BlockedLiveOrderRouter(AllGatesOpenSettings(), AllGatesOpenContext());
        Assert.False(router.IsLiveSubmissionEnabled);
    }
}
