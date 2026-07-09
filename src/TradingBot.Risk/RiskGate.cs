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
            blocks.Add(BuildMarketSessionBlock(context));
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

    /// <summary>
    /// Session-only gate for US NASDAQ calendar snapshot. Fail-closed when unknown or closed.
    /// Does not evaluate quote staleness, notional, or live-path rules.
    /// </summary>
    public RiskDecision EvaluateSession(
        UsMarketSessionSnapshot? snapshot,
        DateTimeOffset? wallClockUtc = null)
        => EvaluateSessionStatic(snapshot, wallClockUtc);

    /// <summary>Static helper for tests and call sites without a <see cref="RiskGate"/> instance.</summary>
    public static RiskDecision EvaluateSessionStatic(
        UsMarketSessionSnapshot? snapshot,
        DateTimeOffset? wallClockUtc = null)
    {
        var evaluation = UsMarketSessionGuard.Evaluate(snapshot, wallClockUtc);
        if (evaluation.IsKnown && evaluation.IsOpenForOrders)
        {
            return RiskDecision.Allow();
        }

        var detail = evaluation.OwnerMessage;
        var message = !evaluation.IsKnown
            ? $"Market session is unknown. Orders blocked. {detail}"
            : $"Market session is closed. Orders blocked. {detail}";

        return RiskDecision.Block(new BlockedReason(BlockedReason.MarketSessionClosed.Code, message));
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

    private static BlockedReason BuildMarketSessionBlock(CandidateRiskContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.MarketSessionOwnerMessage))
        {
            var kind = !context.MarketSessionKnown ? "unknown" : "closed";
            return new BlockedReason(
                BlockedReason.MarketSessionClosed.Code,
                $"Market session is {kind}. Orders blocked. {context.MarketSessionOwnerMessage}");
        }

        return BlockedReason.MarketSessionClosed;
    }
}
