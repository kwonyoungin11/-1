using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class CersEvaluatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");

    [Fact]
    public void Flat_with_high_expected_edge_returns_actionable_buy()
    {
        // Force entry via injected expected > threshold (avoids flaky synthetic edge).
        var candles = BuildFlat(40, 100.0);
        var signal = CersEvaluator.Evaluate(
            symbol: "VMAR",
            candles: candles,
            openLong: null,
            nowUtc: Now,
            expectedEdgeOverride: 0.01,
            suggestedQuantity: 2m);

        Assert.Equal(SignalSide.Buy, signal.Side);
        Assert.True(signal.IsActionable);
        Assert.Equal("VMAR", signal.Symbol);
        Assert.Equal(2m, signal.SuggestedQuantity);
        Assert.Contains("CERS", signal.StrategyName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("실주문", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Flat_with_low_expected_edge_returns_hold_not_actionable()
    {
        var candles = BuildFlat(40, 100.0);
        var signal = CersEvaluator.Evaluate(
            symbol: "VMAR",
            candles: candles,
            openLong: null,
            nowUtc: Now,
            expectedEdgeOverride: 0.001);

        Assert.Equal(SignalSide.Hold, signal.Side);
        Assert.False(signal.IsActionable);
    }

    [Fact]
    public void In_position_stop_loss_triggers_sell()
    {
        // Entry 100, last close 98.7 → ret = -0.013 <= -0.012
        var candles = BuildFlat(40, 98.7);
        var open = new CersOpenPosition(EntryPrice: 100.0, BarsHeld: 5, EntryExpected: 0.01);
        var signal = CersEvaluator.Evaluate(
            symbol: "VMAR",
            candles: candles,
            openLong: open,
            nowUtc: Now,
            expectedEdgeOverride: 0.0,
            emaOverride: 110.0); // keep mean far above so SL is the reason

        Assert.Equal(SignalSide.Sell, signal.Side);
        Assert.True(signal.IsActionable);
        Assert.Contains("손절", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void In_position_mean_touch_when_close_ge_ema_exits()
    {
        var candles = BuildFlat(40, 100.0);
        // entryExpected large so TP ret threshold is not hit before mean-touch exit.
        var open = new CersOpenPosition(EntryPrice: 95.0, BarsHeld: 3, EntryExpected: 0.20);
        var signal = CersEvaluator.Evaluate(
            symbol: "VMAR",
            candles: candles,
            openLong: open,
            nowUtc: Now,
            expectedEdgeOverride: 0.0,
            emaOverride: 100.0); // close == ema → mean touch

        Assert.Equal(SignalSide.Sell, signal.Side);
        Assert.True(signal.IsActionable);
        Assert.Contains("평균", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void In_position_max_hold_exits()
    {
        var candles = BuildFlat(40, 100.0);
        var open = new CersOpenPosition(EntryPrice: 100.0, BarsHeld: 40, EntryExpected: 0.02);
        var signal = CersEvaluator.Evaluate(
            symbol: "VMAR",
            candles: candles,
            openLong: open,
            nowUtc: Now,
            expectedEdgeOverride: 0.0,
            emaOverride: 120.0); // close < ema, ret ~0, hold >= 40

        Assert.Equal(SignalSide.Sell, signal.Side);
        Assert.True(signal.IsActionable);
        Assert.Contains("보유", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void In_position_take_profit_when_ret_ge_entry_expected_times_1_5()
    {
        // entryExpected=0.02 → TP ret >= 0.03. Entry 100 → close 103.5
        var candles = BuildFlat(40, 103.5);
        var open = new CersOpenPosition(EntryPrice: 100.0, BarsHeld: 5, EntryExpected: 0.02);
        var signal = CersEvaluator.Evaluate(
            symbol: "VMAR",
            candles: candles,
            openLong: open,
            nowUtc: Now,
            expectedEdgeOverride: 0.0,
            emaOverride: 120.0); // close still below ema so TP is the reason

        Assert.Equal(SignalSide.Sell, signal.Side);
        Assert.True(signal.IsActionable);
        Assert.Contains("익절", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void In_position_no_exit_when_within_band()
    {
        // Small green ret, below EMA, hold short → Hold
        var candles = BuildFlat(40, 100.5);
        var open = new CersOpenPosition(EntryPrice: 100.0, BarsHeld: 5, EntryExpected: 0.05);
        var signal = CersEvaluator.Evaluate(
            symbol: "VMAR",
            candles: candles,
            openLong: open,
            nowUtc: Now,
            expectedEdgeOverride: 0.0,
            emaOverride: 105.0);

        Assert.Equal(SignalSide.Hold, signal.Side);
        Assert.False(signal.IsActionable);
    }

    [Fact]
    public void Empty_candles_return_hold_not_actionable()
    {
        var signal = CersEvaluator.Evaluate("VMAR", Array.Empty<CandlePoint>(), null, Now);
        Assert.Equal(SignalSide.Hold, signal.Side);
        Assert.False(signal.IsActionable);
    }

    private static IReadOnlyList<CandlePoint> BuildFlat(int count, double price)
    {
        var list = new List<CandlePoint>(count);
        var t0 = DateTimeOffset.Parse("2026-01-02T14:30:00Z");
        for (var i = 0; i < count; i++)
        {
            list.Add(new CandlePoint(
                t0.AddMinutes(i),
                price,
                price * 1.001,
                price * 0.999,
                price,
                1_000));
        }

        return list;
    }
}
