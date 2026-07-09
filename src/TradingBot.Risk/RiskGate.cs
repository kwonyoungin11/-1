using TradingBot.Domain;

namespace TradingBot.Risk;

/// <summary>Composes safety checks for order candidates. Fail-closed.</summary>
public sealed class RiskGate
{
    private readonly LiveOrderGate _liveOrderGate = new();

    public RiskDecision EvaluateForCandidate(TradingSafetySettings settings, LiveOrderContext? liveContext = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Dry-run and paper can still evaluate risk, but never enable live path by accident.
        if (settings.OrderMode == OrderMode.Live || settings.AllowLiveOrders)
        {
            var ctx = liveContext ?? new LiveOrderContext();
            return _liveOrderGate.Evaluate(settings, ctx);
        }

        // Non-live modes: still block if kill switch is on for live path reporting only.
        // Candidates are allowed to be created for dry-run/paper.
        return RiskDecision.Allow();
    }

    public RiskDecision EvaluateLiveSubmission(TradingSafetySettings settings, LiveOrderContext context)
    {
        return _liveOrderGate.Evaluate(settings, context);
    }
}
