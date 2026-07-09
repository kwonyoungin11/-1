using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class LimitOrderLifecyclePolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 9, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ttl_expired_cancels()
    {
        var d = LimitOrderLifecyclePolicy.EvaluateUnfilledLongLimit(
            limitPrice: 100m,
            lastPrice: 99m,
            submittedAtUtc: T0,
            nowUtc: T0.AddMinutes(31),
            ttl: TimeSpan.FromMinutes(30));
        Assert.Equal(WorkingOrderAction.Cancel, d.Action);
        Assert.Equal(WorkingOrderReason.TtlExpired, d.Reason);
    }

    [Fact]
    public void Adverse_far_move_cancels_no_market_chase()
    {
        var d = LimitOrderLifecyclePolicy.EvaluateUnfilledLongLimit(
            limitPrice: 100m,
            lastPrice: 110m,
            submittedAtUtc: T0,
            nowUtc: T0.AddMinutes(5),
            atr: 2.0);
        Assert.Equal(WorkingOrderAction.Cancel, d.Action);
        Assert.Equal(WorkingOrderReason.AdversePriceMove, d.Reason);
        Assert.Contains("시장가", d.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Small_adverse_allows_reprice_within_atr_cap()
    {
        var d = LimitOrderLifecyclePolicy.EvaluateUnfilledLongLimit(
            limitPrice: 100m,
            lastPrice: 100.3m,
            submittedAtUtc: T0,
            nowUtc: T0.AddMinutes(5),
            atr: 2.0); // max chase = 0.5
        Assert.Equal(WorkingOrderAction.Reprice, d.Action);
        Assert.NotNull(d.NewLimitPrice);
        Assert.True(d.NewLimitPrice > 100m);
    }

    [Fact]
    public void Kill_switch_stands_down()
    {
        var d = LimitOrderLifecyclePolicy.EvaluateUnfilledLongLimit(
            limitPrice: 100m,
            lastPrice: 99m,
            submittedAtUtc: T0,
            nowUtc: T0.AddMinutes(1),
            killOrDailyLoss: true);
        Assert.Equal(WorkingOrderAction.CancelAndStandDown, d.Action);
    }

    [Fact]
    public void News_day_size_half()
    {
        Assert.Equal(0.5m, LimitOrderLifecyclePolicy.SizeMultiplier(newsDay: true, symbolWarningActive: false));
        Assert.Equal(0m, LimitOrderLifecyclePolicy.SizeMultiplier(newsDay: false, symbolWarningActive: true));
    }

    [Fact]
    public void Entry_gate_blocks_on_warning()
    {
        var d = LimitOrderLifecyclePolicy.EvaluateNewEntryGate(
            killOrDailyLoss: false,
            dataStale: false,
            sessionOpen: true,
            symbolWarningActive: true,
            newsDay: false,
            trendFilterOk: true);
        Assert.Equal(WorkingOrderAction.BlockNewEntries, d.Action);
    }

    [Fact]
    public void Checklist_lists_required_and_rejects_news_sentiment()
    {
        Assert.Contains(AutoTradeReadinessChecklist.Required, s => s.Contains("TTL", StringComparison.Ordinal));
        Assert.Contains(
            AutoTradeReadinessChecklist.NotRequiredOrHarmful,
            s => s.Contains("감성", StringComparison.Ordinal));
    }
}
