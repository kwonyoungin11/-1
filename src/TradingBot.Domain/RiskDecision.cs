namespace TradingBot.Domain;

/// <summary>Result of evaluating risk / live gates. Fail-closed: default is block.</summary>
public sealed record RiskDecision(bool Allowed, IReadOnlyList<BlockedReason> Blocks)
{
    public static RiskDecision Allow() => new(true, Array.Empty<BlockedReason>());

    public static RiskDecision Block(params BlockedReason[] reasons)
    {
        if (reasons is null || reasons.Length == 0)
        {
            return new(false, new[] { BlockedReason.UnknownState });
        }

        return new(false, reasons);
    }

    public bool IsBlocked => !Allowed;
}
