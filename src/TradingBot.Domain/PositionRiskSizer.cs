namespace TradingBot.Domain;

/// <summary>
/// Position sizing from account risk percent and stop distance (trader rules as code).
/// Quantity is floored so that loss at stop ≈ equity × risk%/100. Not investment advice.
/// </summary>
public static class PositionRiskSizer
{
    /// <summary>
    /// Computes whole-share quantity from equity risk budget and stop-loss distance.
    /// Fail-closed: invalid or non-positive inputs yield quantity 0.
    /// </summary>
    /// <param name="equity">Account equity used as the risk base (e.g. day-start or current).</param>
    /// <param name="riskPercentPerTrade">Percent of equity to risk if stop hits (e.g. 1.0 = 1%).</param>
    /// <param name="stopLossPercent">Stop distance as percent of price (e.g. 2.0 = 2% below entry).</param>
    /// <param name="price">Entry / reference price per share.</param>
    public static PositionSizeResult Calculate(
        decimal equity,
        decimal riskPercentPerTrade,
        decimal stopLossPercent,
        decimal price)
    {
        if (equity <= 0m
            || riskPercentPerTrade <= 0m
            || stopLossPercent <= 0m
            || price <= 0m)
        {
            return PositionSizeResult.Zero(
                equity: equity,
                riskPercentPerTrade: riskPercentPerTrade,
                stopLossPercent: stopLossPercent,
                price: price,
                reason: "Invalid or non-positive sizing inputs; quantity is 0 (fail-closed).");
        }

        var riskBudget = equity * (riskPercentPerTrade / 100m);
        var stopDistancePerShare = price * (stopLossPercent / 100m);

        if (riskBudget <= 0m || stopDistancePerShare <= 0m)
        {
            return PositionSizeResult.Zero(
                equity: equity,
                riskPercentPerTrade: riskPercentPerTrade,
                stopLossPercent: stopLossPercent,
                price: price,
                reason: "Risk budget or stop distance is non-positive; quantity is 0 (fail-closed).");
        }

        // qty * stopDistance ≈ riskBudget → floor so realized stop loss does not exceed budget.
        var raw = riskBudget / stopDistancePerShare;
        var quantity = decimal.Floor(raw);
        if (quantity < 0m)
        {
            quantity = 0m;
        }

        var plannedLossAtStop = quantity * stopDistancePerShare;

        return new PositionSizeResult(
            Quantity: quantity,
            RiskBudget: riskBudget,
            StopDistancePerShare: stopDistancePerShare,
            PlannedLossAtStop: plannedLossAtStop,
            Equity: equity,
            RiskPercentPerTrade: riskPercentPerTrade,
            StopLossPercent: stopLossPercent,
            Price: price,
            IsValid: quantity > 0m,
            Message: quantity > 0m
                ? null
                : "Risk budget is smaller than one share stop distance; quantity is 0.");
    }
}

/// <summary>Result of <see cref="PositionRiskSizer.Calculate"/>.</summary>
public sealed record PositionSizeResult(
    decimal Quantity,
    decimal RiskBudget,
    decimal StopDistancePerShare,
    decimal PlannedLossAtStop,
    decimal Equity,
    decimal RiskPercentPerTrade,
    decimal StopLossPercent,
    decimal Price,
    bool IsValid,
    string? Message)
{
    public static PositionSizeResult Zero(
        decimal equity,
        decimal riskPercentPerTrade,
        decimal stopLossPercent,
        decimal price,
        string reason) =>
        new(
            Quantity: 0m,
            RiskBudget: 0m,
            StopDistancePerShare: 0m,
            PlannedLossAtStop: 0m,
            Equity: equity,
            RiskPercentPerTrade: riskPercentPerTrade,
            StopLossPercent: stopLossPercent,
            Price: price,
            IsValid: false,
            Message: reason);
}
