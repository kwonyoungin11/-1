using TradingBot.Domain;

namespace TradingBot.Risk;

/// <summary>
/// Daily loss halt: block new entries when realized daily loss exceeds a max percent (or absolute).
/// Trader rule as code — not investment advice. Fail-closed on invalid / missing inputs.
/// </summary>
public static class DailyLossGuard
{
    /// <summary>
    /// Blocks when (dayStartEquity − currentEquity) ≥ dayStartEquity × maxDailyLossPercent/100.
    /// Profit days (current ≥ start) always allow when inputs are valid.
    /// </summary>
    public static RiskDecision Evaluate(
        decimal dayStartEquity,
        decimal currentEquity,
        decimal maxDailyLossPercent)
    {
        if (dayStartEquity <= 0m || maxDailyLossPercent <= 0m)
        {
            return RiskDecision.Block(BlockedReason.DailyLossLimitDataInvalid);
        }

        var loss = dayStartEquity - currentEquity;
        if (loss <= 0m)
        {
            return RiskDecision.Allow();
        }

        var maxLoss = dayStartEquity * (maxDailyLossPercent / 100m);
        if (maxLoss <= 0m)
        {
            return RiskDecision.Block(BlockedReason.DailyLossLimitDataInvalid);
        }

        if (loss >= maxLoss)
        {
            return RiskDecision.Block(
                new BlockedReason(
                    BlockedReason.DailyLossLimitExceeded.Code,
                    $"Daily loss {loss} meets or exceeds max {maxLoss} ({maxDailyLossPercent}% of day-start equity). New entries blocked."));
        }

        return RiskDecision.Allow();
    }

    /// <summary>
    /// Same rule using realized PnL: loss when realizedPnl is negative.
    /// dayStartEquity is the percent base; realizedPnl ≤ −(dayStart × max%/100) → block.
    /// </summary>
    public static RiskDecision EvaluateFromRealizedPnl(
        decimal dayStartEquity,
        decimal realizedPnl,
        decimal maxDailyLossPercent)
    {
        // currentEquity equivalent = dayStart + realizedPnl
        return Evaluate(dayStartEquity, dayStartEquity + realizedPnl, maxDailyLossPercent);
    }

    /// <summary>
    /// Absolute max daily loss (currency units). Blocks when loss ≥ maxDailyLossAbsolute.
    /// Used by <see cref="RiskGate"/> when <see cref="TradingSafetySettings.MaxDailyLoss"/> is set.
    /// </summary>
    public static RiskDecision EvaluateAbsolute(
        decimal dayStartEquity,
        decimal currentEquity,
        decimal maxDailyLossAbsolute)
    {
        if (dayStartEquity <= 0m || maxDailyLossAbsolute <= 0m)
        {
            return RiskDecision.Block(BlockedReason.DailyLossLimitDataInvalid);
        }

        var loss = dayStartEquity - currentEquity;
        if (loss <= 0m)
        {
            return RiskDecision.Allow();
        }

        if (loss >= maxDailyLossAbsolute)
        {
            return RiskDecision.Block(
                new BlockedReason(
                    BlockedReason.DailyLossLimitExceeded.Code,
                    $"Daily loss {loss} meets or exceeds absolute max {maxDailyLossAbsolute}. New entries blocked."));
        }

        return RiskDecision.Allow();
    }
}
