using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class DailyLossGuardTests
{
    [Fact]
    public void Allows_when_loss_below_percent_limit()
    {
        // start 100k, current 99k → loss 1k = 1% < 2%
        var decision = DailyLossGuard.Evaluate(
            dayStartEquity: 100_000m,
            currentEquity: 99_000m,
            maxDailyLossPercent: 2.0m);

        Assert.True(decision.Allowed);
        Assert.Empty(decision.Blocks);
    }

    [Fact]
    public void Blocks_when_loss_equals_percent_limit()
    {
        // loss exactly 2%
        var decision = DailyLossGuard.Evaluate(100_000m, 98_000m, 2.0m);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitExceeded.Code);
    }

    [Fact]
    public void Blocks_when_loss_exceeds_percent_limit()
    {
        var decision = DailyLossGuard.Evaluate(100_000m, 97_000m, 2.0m);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitExceeded.Code);
    }

    [Fact]
    public void Allows_when_account_is_flat_or_up()
    {
        Assert.True(DailyLossGuard.Evaluate(100_000m, 100_000m, 2.0m).Allowed);
        Assert.True(DailyLossGuard.Evaluate(100_000m, 101_000m, 2.0m).Allowed);
    }

    [Fact]
    public void EvaluateFromRealizedPnl_blocks_on_negative_pnl_at_limit()
    {
        // -2000 on 100k at 2% limit
        var decision = DailyLossGuard.EvaluateFromRealizedPnl(
            dayStartEquity: 100_000m,
            realizedPnl: -2_000m,
            maxDailyLossPercent: 2.0m);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitExceeded.Code);
    }

    [Fact]
    public void EvaluateFromRealizedPnl_allows_small_loss()
    {
        var decision = DailyLossGuard.EvaluateFromRealizedPnl(100_000m, -500m, 2.0m);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void EvaluateAbsolute_blocks_at_absolute_max()
    {
        var decision = DailyLossGuard.EvaluateAbsolute(
            dayStartEquity: 100_000m,
            currentEquity: 99_000m,
            maxDailyLossAbsolute: 1_000m);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitExceeded.Code);
    }

    [Fact]
    public void EvaluateAbsolute_allows_below_absolute_max()
    {
        var decision = DailyLossGuard.EvaluateAbsolute(100_000m, 99_500m, 1_000m);
        Assert.True(decision.Allowed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_day_start_equity_blocks(decimal start)
    {
        var decision = DailyLossGuard.Evaluate(start, 100_000m, 2.0m);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitDataInvalid.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_max_percent_blocks(decimal maxPct)
    {
        var decision = DailyLossGuard.Evaluate(100_000m, 99_000m, maxPct);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitDataInvalid.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Invalid_absolute_max_blocks(decimal maxAbs)
    {
        var decision = DailyLossGuard.EvaluateAbsolute(100_000m, 99_000m, maxAbs);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitDataInvalid.Code);
    }
}
