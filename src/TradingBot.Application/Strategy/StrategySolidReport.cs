namespace TradingBot.Application;

/// <summary>
/// Machine-checkable "strategy solid" status for loop stop conditions.
/// <see cref="StrategySolid"/> is true only when every required flag is true
/// (Domain SpaceX universe/sizing/params + Risk guards + practice pipeline method).
/// <see cref="Core3UniverseOk"/> keeps wire name; value is SpaceX/SPCX-only universe check.
/// </summary>
public sealed record StrategySolidReport(
    bool StrategySolid,
    bool Core3UniverseOk,
    bool PositionRiskSizerOk,
    bool TrendFollowParametersOk,
    bool DailyLossGuardPresent,
    bool TradingSessionWindowPresent,
    bool PracticePipelineMethodPresent,
    IReadOnlyList<string> Notes)
{
    /// <summary>Wire token for scripts (snake_case).</summary>
    public string ToStatusToken() => StrategySolid ? "strategy_solid" : "strategy_not_solid";
}
