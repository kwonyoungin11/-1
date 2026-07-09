using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>선택된 전략에 맞는 신호 생성기를 고릅니다.</summary>
public sealed class StrategySignalRouter
{
    private readonly IReadOnlyDictionary<TradingStrategyKind, IStrategySignalGenerator> _map;

    public StrategySignalRouter(IEnumerable<IStrategySignalGenerator>? generators = null)
    {
        var list = (generators ?? DefaultGenerators()).ToList();
        _map = list.ToDictionary(g => g.Kind);
    }

    public static IReadOnlyList<IStrategySignalGenerator> DefaultGenerators() =>
    [
        new HoldOnlySignalGenerator(),
        new SimplePracticeSignalGenerator(),
        new TrendFollowSignalGenerator(),
        new MeanReversionSignalGenerator(),
        new MomentumBreakoutSignalGenerator(),
    ];

    public StrategySignal Generate(
        TradingStrategyKind kind,
        QuoteSnapshot quote,
        decimal baseOrderQuantity,
        DateTimeOffset nowUtc)
    {
        if (!_map.TryGetValue(kind, out var gen))
        {
            gen = new HoldOnlySignalGenerator();
        }

        return gen.Generate(quote, baseOrderQuantity, nowUtc);
    }
}
