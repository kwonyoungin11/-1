using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>선택된 전략에 맞는 신호 생성기를 고릅니다.</summary>
public sealed class StrategySignalRouter
{
    private readonly IReadOnlyDictionary<TradingStrategyKind, IStrategySignalGenerator> _map;
    private readonly TrendFollowParameters? _trendFollowParameters;

    public StrategySignalRouter(
        IEnumerable<IStrategySignalGenerator>? generators = null,
        TrendFollowParameters? trendFollowParameters = null)
    {
        _trendFollowParameters = trendFollowParameters;
        var list = (generators ?? DefaultGenerators(trendFollowParameters)).ToList();
        _map = list.ToDictionary(g => g.Kind);
    }

    public static IReadOnlyList<IStrategySignalGenerator> DefaultGenerators(
        TrendFollowParameters? trendFollowParameters = null) =>
    [
        new HoldOnlySignalGenerator(),
        new SimplePracticeSignalGenerator(),
        new TrendFollowSignalGenerator(trendFollowParameters),
        new MeanReversionSignalGenerator(),
        new MomentumBreakoutSignalGenerator(),
    ];

    public StrategySignal Generate(
        TradingStrategyKind kind,
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc,
        TrendFollowParameters? trendFollowParameters = null)
    {
        // Practice / call-site override: use TrendFollowSignalGenerator with explicit params.
        var effectiveTrend = trendFollowParameters ?? _trendFollowParameters;
        if (kind == TradingStrategyKind.추세추종 && effectiveTrend is not null)
        {
            return new TrendFollowSignalGenerator(effectiveTrend)
                .Generate(quote, baseOrderQuantity, nowUtc);
        }

        if (!_map.TryGetValue(kind, out var gen))
        {
            gen = new HoldOnlySignalGenerator();
        }

        return gen.Generate(quote, baseOrderQuantity, nowUtc);
    }
}
