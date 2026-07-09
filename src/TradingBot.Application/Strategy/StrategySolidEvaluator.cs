using System.Reflection;
using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Application;

/// <summary>
/// Evaluates whether the strategy stack is "solid" enough for automated loop stop.
/// Domain pieces call public APIs; Risk / practice-pipeline pieces use type/method reflection
/// so the report becomes true when sibling waves merge without recompiling this evaluator.
/// </summary>
public static class StrategySolidEvaluator
{
    public const string DailyLossGuardTypeName = "TradingBot.Risk.DailyLossGuard";
    public const string TradingSessionWindowTypeName = "TradingBot.Risk.TradingSessionWindow";
    public const string PracticeStrategyContextTypeName = "PracticeStrategyContext";
    public const string OrderCandidatePipelineTypeName = "TradingBot.Application.OrderCandidatePipeline";

    /// <summary>
    /// Evaluates using Domain, Application, and Risk assemblies loaded by the current process.
    /// </summary>
    public static StrategySolidReport Evaluate() =>
        Evaluate(assemblies: null);

    /// <summary>
    /// Evaluates Domain checks plus optional assembly scan for Risk types and
    /// <c>OrderCandidatePipeline</c> methods accepting <c>PracticeStrategyContext</c>.
    /// <para>
    /// <paramref name="assemblies"/> null → default Domain + Application + Risk scan (full stack).<br/>
    /// Empty sequence → Domain-only checks; Risk/pipeline flags forced false (partial report).
    /// </para>
    /// </summary>
    public static StrategySolidReport Evaluate(IEnumerable<Assembly>? assemblies)
    {
        var useDefaultStack = assemblies is null;
        var asmList = useDefaultStack
            ? DefaultAssemblies()
            : assemblies!.Where(static a => a is not null).Distinct().ToArray()!;

        var notes = new List<string>();

        var core3 = StrategySolidDomainChecks.IsCore3UniverseOk();
        if (!core3)
        {
            notes.Add("Core3 universe failed: StockMarketKind.나스닥코어3 must resolve to QQQ,NVDA,AAPL (count 3).");
        }

        var sizer = StrategySolidDomainChecks.IsPositionRiskSizerOk();
        if (!sizer)
        {
            notes.Add("PositionRiskSizer failed: Calculate(100000,1,2,100) must yield quantity 500.");
        }

        var trend = StrategySolidDomainChecks.IsTrendFollowParametersOk();
        if (!trend)
        {
            notes.Add("TrendFollowParameters.CreateSafeDefaults() missing or invalid.");
        }

        bool dailyLoss;
        bool sessionWindow;
        bool practicePipeline;

        if (useDefaultStack)
        {
            dailyLoss = TypeExists(asmList, DailyLossGuardTypeName, simpleName: "DailyLossGuard", allowAppDomainFallback: true);
            sessionWindow = TypeExists(asmList, TradingSessionWindowTypeName, simpleName: "TradingSessionWindow", allowAppDomainFallback: true);
            practicePipeline = HasPracticePipelineMethod(asmList, includeCompileTimePipeline: true);
        }
        else if (asmList.Count == 0)
        {
            // Explicit empty list → partial Domain-only report.
            dailyLoss = false;
            sessionWindow = false;
            practicePipeline = false;
            notes.Add("Partial report: empty assemblies — Risk and practice-pipeline flags forced false.");
        }
        else
        {
            dailyLoss = TypeExists(asmList, DailyLossGuardTypeName, simpleName: "DailyLossGuard", allowAppDomainFallback: false);
            sessionWindow = TypeExists(asmList, TradingSessionWindowTypeName, simpleName: "TradingSessionWindow", allowAppDomainFallback: false);
            practicePipeline = HasPracticePipelineMethod(asmList, includeCompileTimePipeline: false);
        }

        if (!dailyLoss)
        {
            notes.Add($"Type missing: {DailyLossGuardTypeName}");
        }

        if (!sessionWindow)
        {
            notes.Add($"Type missing: {TradingSessionWindowTypeName}");
        }

        if (!practicePipeline)
        {
            notes.Add(
                "OrderCandidatePipeline has no public method accepting PracticeStrategyContext " +
                "(merge pipeline-wire / practice context overload).");
        }

        var solid = core3 && sizer && trend && dailyLoss && sessionWindow && practicePipeline;
        if (solid)
        {
            notes.Add("All strategy-solid checks passed.");
        }

        return new StrategySolidReport(
            StrategySolid: solid,
            Core3UniverseOk: core3,
            PositionRiskSizerOk: sizer,
            TrendFollowParametersOk: trend,
            DailyLossGuardPresent: dailyLoss,
            TradingSessionWindowPresent: sessionWindow,
            PracticePipelineMethodPresent: practicePipeline,
            Notes: notes);
    }

    private static IReadOnlyList<Assembly> DefaultAssemblies()
    {
        var list = new List<Assembly>
        {
            typeof(StrategySolidDomainChecks).Assembly,
            typeof(StrategySolidEvaluator).Assembly,
            // Application references Risk — include for reflection type scan.
            typeof(DailyLossGuard).Assembly,
            typeof(TradingSessionWindow).Assembly,
        };

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (name is not null
                && name.StartsWith("TradingBot.", StringComparison.Ordinal)
                && !list.Contains(asm))
            {
                list.Add(asm);
            }
        }

        return list;
    }

    private static Type? FindLoadedType(string fullTypeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullTypeName, throwOnError: false);
            if (t is not null)
            {
                return t;
            }
        }

        return null;
    }

    private static bool TypeExists(
        IReadOnlyList<Assembly> assemblies,
        string fullTypeName,
        string simpleName,
        bool allowAppDomainFallback)
    {
        foreach (var asm in assemblies)
        {
            Type? t = null;
            try
            {
                t = asm.GetType(fullTypeName, throwOnError: false);
            }
            catch (ReflectionTypeLoadException)
            {
                // ignore unloadable types
            }

            if (t is not null)
            {
                return true;
            }

            try
            {
                if (SafeGetTypes(asm).Any(x => x.Name == simpleName || x.FullName == fullTypeName))
                {
                    return true;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // already handled inside SafeGetTypes
            }
        }

        if (!allowAppDomainFallback)
        {
            return false;
        }

        if (Type.GetType(fullTypeName, throwOnError: false) is not null)
        {
            return true;
        }

        return FindLoadedType(fullTypeName) is not null
            || AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Any(x => x.Name == simpleName || x.FullName == fullTypeName);
    }

    private static bool HasPracticePipelineMethod(
        IReadOnlyList<Assembly> assemblies,
        bool includeCompileTimePipeline)
    {
        var pipelineTypes = new List<Type>();

        if (includeCompileTimePipeline)
        {
            pipelineTypes.Add(typeof(OrderCandidatePipeline));
        }

        foreach (var asm in assemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t.Name == "OrderCandidatePipeline"
                    || t.FullName == OrderCandidatePipelineTypeName)
                {
                    if (!pipelineTypes.Contains(t))
                    {
                        pipelineTypes.Add(t);
                    }
                }
            }
        }

        foreach (var pipeline in pipelineTypes)
        {
            foreach (var method in pipeline.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (MethodAcceptsPracticeStrategyContext(method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MethodAcceptsPracticeStrategyContext(MethodInfo method)
    {
        foreach (var parameter in method.GetParameters())
        {
            var pt = parameter.ParameterType;
            if (pt.IsByRef)
            {
                pt = pt.GetElementType() ?? pt;
            }

            if (pt.Name == PracticeStrategyContextTypeName
                || (pt.FullName is not null
                    && pt.FullName.Contains(PracticeStrategyContextTypeName, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
