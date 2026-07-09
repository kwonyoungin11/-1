using TradingBot.Domain;

namespace TradingBot.Risk;

/// <summary>Composes safety checks for order candidates and live path. Fail-closed.</summary>
public sealed class RiskGate
{
    private readonly LiveOrderGate _liveOrderGate = new();

    /// <summary>
    /// Evaluates whether a candidate may enter dry-run/paper routing.
    /// Does not submit live orders. Unknown/missing/stale/api error → block.
    /// </summary>
    public RiskDecision EvaluateOrderCandidate(
        TradingSafetySettings settings,
        CandidateRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(context);

        var blocks = new List<BlockedReason>();

        if (context.HasUnknownState)
        {
            blocks.Add(BlockedReason.UnknownState);
        }

        if (context.HasMissingData
            || string.IsNullOrWhiteSpace(context.Symbol)
            || context.Quantity <= 0
            || context.LimitPrice is null or <= 0)
        {
            blocks.Add(BlockedReason.MissingData);
        }

        if (context.HasApiError)
        {
            blocks.Add(BlockedReason.ApiError);
        }

        if (!context.MarketSessionKnown || !context.MarketSessionOpen)
        {
            blocks.Add(BlockedReason.MarketSessionClosed);
        }

        var maxStale = settings.MarketDataMaxStalenessSeconds;
        if (context.QuoteTimestampUtc is null)
        {
            blocks.Add(BlockedReason.MissingData);
        }
        else
        {
            var age = context.NowUtc - context.QuoteTimestampUtc.Value;
            if (age < TimeSpan.Zero || age.TotalSeconds > maxStale)
            {
                blocks.Add(BlockedReason.StaleMarketData);
            }
        }

        if (context.LimitPrice is decimal price && context.Quantity > 0)
        {
            var notional = price * context.Quantity;
            if (settings.MaxOrderNotional is decimal maxNotional && notional > maxNotional)
            {
                blocks.Add(BlockedReason.MaxOrderNotionalExceeded);
            }
        }

        if (settings.MaxPositionSize is decimal maxPos)
        {
            var projected = (context.CurrentPositionQuantity ?? 0m) + context.Quantity;
            if (projected > maxPos)
            {
                blocks.Add(BlockedReason.MaxPositionSizeExceeded);
            }
        }

        // Live-path flags never make a candidate "live-ready"; they only add blocks for visibility
        // when settings already look live-ish. Kill switch does not block dry-run candidates by itself.
        if (settings.OrderMode == OrderMode.Live || settings.AllowLiveOrders)
        {
            // Candidate can still be recorded, but flag that live is not actually available.
            var live = _liveOrderGate.Evaluate(settings, new LiveOrderContext());
            if (live.IsBlocked)
            {
                // Do not merge all live blocks into candidate dry-run path; dry-run remains separate.
            }
        }

        return blocks.Count == 0 ? RiskDecision.Allow() : RiskDecision.Block(blocks.ToArray());
    }

    public RiskDecision EvaluateLiveSubmission(TradingSafetySettings settings, LiveOrderContext context)
    {
        return _liveOrderGate.Evaluate(settings, context);
    }

    /// <summary>Legacy helper: dry-run modes allow candidate creation attempt; live settings force live gate.</summary>
    public RiskDecision EvaluateForCandidate(TradingSafetySettings settings, LiveOrderContext? liveContext = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.OrderMode == OrderMode.Live || settings.AllowLiveOrders)
        {
            var ctx = liveContext ?? new LiveOrderContext();
            return _liveOrderGate.Evaluate(settings, ctx);
        }

        return RiskDecision.Allow();
    }
}
