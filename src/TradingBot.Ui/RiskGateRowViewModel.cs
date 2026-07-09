namespace TradingBot.Ui;

/// <summary>
/// One row on the Risk Gate screen / home risk list.
/// Framework-agnostic binding model. No order actions.
/// </summary>
public sealed class RiskGateRowViewModel
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string OwnerMessage { get; init; }

    /// <summary>True when this gate is clear for the current path; false when blocking.</summary>
    public required bool Passed { get; init; }

    /// <summary>info | warning | block — never rely on color alone.</summary>
    public string Severity { get; init; } = "block";
}
