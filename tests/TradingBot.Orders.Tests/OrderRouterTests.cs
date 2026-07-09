using System.Net.Http;
using System.Reflection;
using TradingBot.Domain;
using TradingBot.Orders;
using TradingBot.Risk;

namespace TradingBot.Orders.Tests;

/// <summary>
/// Router contract evidence: dry-run/paper accept without live; BlockedLive never enables live.
/// </summary>
public class OrderRouterTests
{
    private static OrderCandidate Sample(
        string clientOrderId = "test-client-order-1",
        decimal quantity = 1m,
        decimal? limitPrice = 1m) => new(
        "AAPL",
        "BUY",
        "LIMIT",
        quantity,
        limitPrice,
        clientOrderId,
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task DryRun_accepts_without_live_submission()
    {
        var router = new DryRunOrderRouter();
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.DryRun.ToString(), result.Mode);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.Contains("No live order", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Paper_accepts_without_live_submission_and_records_qty_price()
    {
        var ledger = new InMemoryPaperLedger();
        var router = new PaperOrderRouter(ledger);
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(quantity: 8m, limitPrice: 123.45m), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(OrderMode.Paper.ToString(), result.Mode);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.Equal(1, ledger.Count);
        var fill = Assert.Single(ledger.GetSnapshot());
        Assert.Equal(8m, fill.Quantity);
        Assert.Equal(123.45m, fill.Price);
    }

    [Fact]
    public async Task BlockedLiveRouter_blocks_with_default_settings()
    {
        var router = new BlockedLiveOrderRouter(TradingSafetySettings.CreateSafeDefaults());
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.NotEmpty(result.Blocks);
        Assert.Equal("live_blocked", result.Mode);
        Assert.Contains("No API call", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.LiveOrdersNotAllowed.Code);
        Assert.Contains(result.Blocks, b => b.Code == BlockedReason.OrderModeNotLive.Code);
    }

    [Fact]
    public async Task BlockedLiveRouter_never_enables_live_even_when_all_gate_flags_open()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = false,
            OrderMode = OrderMode.Live,
        };
        var context = new LiveOrderContext
        {
            ManualApprovalPresent = true,
            LiveImplementationEnabled = true,
            HasUnknownState = false,
            HasMissingData = false,
            HasStaleMarketData = false,
            HasApiError = false,
        };

        var router = new BlockedLiveOrderRouter(settings, context);
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        // Hard guarantee: live path is not implemented and never accepts.
        Assert.False(result.Accepted);
        Assert.False(router.IsLiveSubmissionEnabled);
        Assert.Equal("live_not_implemented", result.Mode);
        Assert.Contains("No API call", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.DoesNotContain(result.Blocks, b => b.Code == BlockedReason.KillSwitchActive.Code);
    }

    [Fact]
    public async Task BlockedLiveRouter_open_settings_still_returns_not_implemented_stub()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = true,
            KillSwitch = false,
            OrderMode = OrderMode.Live,
        };
        var context = new LiveOrderContext();

        var router = new BlockedLiveOrderRouter(settings, context);
        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(router.IsLiveSubmissionEnabled);
        Assert.Equal("live_not_implemented", result.Mode);
        Assert.Contains("No API call", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, true, OrderMode.Live)]
    [InlineData(true, false, OrderMode.DryRun)]
    [InlineData(false, false, OrderMode.Live)]
    [InlineData(false, true, OrderMode.Paper)]
    public async Task BlockedLiveRouter_never_accepts_across_safety_permutations(
        bool allowLive,
        bool killSwitch,
        OrderMode orderMode)
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = allowLive,
            KillSwitch = killSwitch,
            OrderMode = orderMode,
        };

        var router = new BlockedLiveOrderRouter(settings);
        Assert.False(router.IsLiveSubmissionEnabled);

        var result = await router.RouteAsync(Sample(), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(router.IsLiveSubmissionEnabled);
        Assert.NotEmpty(result.Blocks);
        Assert.True(
            result.Mode is "live_blocked" or "live_not_implemented",
            $"Unexpected mode: {result.Mode}");
        Assert.NotEqual(OrderMode.Live.ToString(), result.Mode);
        Assert.Contains("No API call", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlockedLiveRouter_null_candidate_throws()
    {
        var router = new BlockedLiveOrderRouter(TradingSafetySettings.CreateSafeDefaults());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => router.RouteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void BlockedLiveRouter_null_settings_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BlockedLiveOrderRouter(null!));
    }

    [Fact]
    public void All_routers_report_IsLiveSubmissionEnabled_false()
    {
        IOrderRouter dry = new DryRunOrderRouter();
        IOrderRouter paper = new PaperOrderRouter(new InMemoryPaperLedger());
        IOrderRouter live = new BlockedLiveOrderRouter(TradingSafetySettings.CreateSafeDefaults());

        Assert.False(dry.IsLiveSubmissionEnabled);
        Assert.False(paper.IsLiveSubmissionEnabled);
        Assert.False(live.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void BlockedLiveOrderRouter_has_no_HttpClient_dependency()
    {
        Assert.False(TypeDependsOnHttp(typeof(BlockedLiveOrderRouter)));

        var ctors = typeof(BlockedLiveOrderRouter).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        foreach (var ctor in ctors)
        {
            foreach (var p in ctor.GetParameters())
            {
                Assert.False(
                    IsHttpRelated(p.ParameterType),
                    $"BlockedLiveOrderRouter must not take HTTP types; found {p.ParameterType.FullName}");
            }
        }
    }

    [Fact]
    public async Task DryRun_and_Paper_do_not_share_live_mode_strings()
    {
        var dry = await new DryRunOrderRouter().RouteAsync(Sample(), CancellationToken.None);
        var paperLedger = new InMemoryPaperLedger();
        var paper = await new PaperOrderRouter(paperLedger)
            .RouteAsync(Sample(limitPrice: 10m), CancellationToken.None);
        var live = await new BlockedLiveOrderRouter(TradingSafetySettings.CreateSafeDefaults())
            .RouteAsync(Sample(), CancellationToken.None);

        Assert.Equal(OrderMode.DryRun.ToString(), dry.Mode);
        Assert.Equal(OrderMode.Paper.ToString(), paper.Mode);
        Assert.Equal("live_blocked", live.Mode);
        Assert.All(new[] { dry.Mode, paper.Mode, live.Mode }, m =>
            Assert.NotEqual(OrderMode.Live.ToString(), m));
    }

    private static bool TypeDependsOnHttp(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var field in type.GetFields(flags))
        {
            if (IsHttpRelated(field.FieldType))
            {
                return true;
            }
        }

        foreach (var prop in type.GetProperties(flags))
        {
            if (IsHttpRelated(prop.PropertyType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHttpRelated(Type type) =>
        type == typeof(HttpClient)
        || type == typeof(HttpMessageHandler)
        || typeof(HttpMessageHandler).IsAssignableFrom(type)
        || (type.FullName?.Contains("HttpClient", StringComparison.Ordinal) == true);
}
