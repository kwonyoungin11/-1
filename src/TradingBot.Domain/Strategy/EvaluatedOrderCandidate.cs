namespace TradingBot.Domain;

/// <summary>Order candidate plus risk decision. Still not a live submission.</summary>
public sealed record EvaluatedOrderCandidate(
    OrderCandidate Candidate,
    StrategySignal Signal,
    RiskDecision Risk,
    string OwnerStatusMessage)
{
    public bool IsAcceptedForDryRun => Risk.Allowed;
}
