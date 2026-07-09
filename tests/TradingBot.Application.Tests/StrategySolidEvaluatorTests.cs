using System.Reflection;
using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Application.Tests;

public class StrategySolidEvaluatorTests
{
    [Fact]
    public void Evaluate_domain_and_risk_flags_are_true_on_current_codebase()
    {
        var report = StrategySolidEvaluator.Evaluate();

        Assert.True(report.Core3UniverseOk);
        Assert.True(report.PositionRiskSizerOk);
        Assert.True(report.TrendFollowParametersOk);
        Assert.True(report.DailyLossGuardPresent);
        Assert.True(report.TradingSessionWindowPresent);

        // Risk types are compile-referenced; reflection must still find them.
        Assert.NotNull(typeof(DailyLossGuard));
        Assert.NotNull(typeof(TradingSessionWindow));
    }

    [Fact]
    public void Evaluate_StrategySolid_equals_conjunction_of_all_flags()
    {
        var report = StrategySolidEvaluator.Evaluate();

        var expected =
            report.Core3UniverseOk
            && report.PositionRiskSizerOk
            && report.TrendFollowParametersOk
            && report.DailyLossGuardPresent
            && report.TradingSessionWindowPresent
            && report.PracticePipelineMethodPresent;

        Assert.Equal(expected, report.StrategySolid);
        Assert.Equal(
            report.StrategySolid ? "strategy_solid" : "strategy_not_solid",
            report.ToStatusToken());
    }

    [Fact]
    public void Evaluate_empty_assemblies_marks_risk_and_pipeline_absent()
    {
        // Explicit empty list → Domain-only partial report (Risk/pipeline forced false).
        var report = StrategySolidEvaluator.Evaluate(Array.Empty<Assembly>());

        Assert.True(report.Core3UniverseOk);
        Assert.True(report.PositionRiskSizerOk);
        Assert.True(report.TrendFollowParametersOk);
        Assert.False(report.DailyLossGuardPresent);
        Assert.False(report.TradingSessionWindowPresent);
        Assert.False(report.PracticePipelineMethodPresent);
        Assert.False(report.StrategySolid);
        Assert.Contains(report.Notes, n => n.Contains("Partial report", StringComparison.Ordinal));
        Assert.Contains(report.Notes, n => n.Contains("PracticeStrategyContext", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_PracticePipelineMethodPresent_matches_reflection_on_OrderCandidatePipeline()
    {
        var report = StrategySolidEvaluator.Evaluate();
        var reflected = OrderCandidatePipelineHasPracticeContextParameter();
        Assert.Equal(reflected, report.PracticePipelineMethodPresent);
    }

    /// <summary>
    /// Loop stop condition: when the practice pipeline overload is merged,
    /// StrategySolid must be true on this codebase. Until then the flag may be false.
    /// </summary>
    [Fact]
    public void Evaluate_StrategySolid_is_true_when_practice_pipeline_is_present()
    {
        var report = StrategySolidEvaluator.Evaluate();

        if (!report.PracticePipelineMethodPresent)
        {
            // Pipeline wave not merged yet — Domain + Risk pieces must still be solid.
            Assert.True(report.Core3UniverseOk);
            Assert.True(report.PositionRiskSizerOk);
            Assert.True(report.TrendFollowParametersOk);
            Assert.True(report.DailyLossGuardPresent);
            Assert.True(report.TradingSessionWindowPresent);
            Assert.False(report.StrategySolid);
            return;
        }

        Assert.True(
            report.StrategySolid,
            "Practice pipeline present but StrategySolid false: " + string.Join("; ", report.Notes));
    }

    private static bool OrderCandidatePipelineHasPracticeContextParameter()
    {
        var methods = typeof(OrderCandidatePipeline).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var method in methods)
        {
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.ParameterType.Name is "PracticeStrategyContext"
                    || (parameter.ParameterType.FullName?.Contains(
                            "PracticeStrategyContext",
                            StringComparison.Ordinal) ?? false))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
