using TradingBot.Application;
using TradingBot.Domain;

namespace TradingBot.Application.Tests;

/// <summary>
/// CERS application wiring: generator + router + pipeline candle context.
/// Practice signals only · live orders gated · not investment advice.
/// </summary>
public class CersSignalGeneratorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");

    [Fact]
    public void CersSignalGenerator_without_candles_not_actionable()
    {
        var gen = new CersSignalGenerator();
        Assert.Equal(TradingStrategyKind.CERS비용회귀, gen.Kind);

        var quote = new QuoteSnapshot("VMAR", 100m, "USD", Now);
        var signal = gen.Generate(quote, baseOrderQuantity: 2m, Now);

        Assert.False(signal.IsActionable);
        Assert.Equal(SignalSide.Hold, signal.Side);
        Assert.Contains("CERS", signal.StrategyName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("실주문", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void CersSignalGenerator_with_edge_dip_series_emits_buy()
    {
        var candles = BuildEdgeDipSeries();
        var quote = new QuoteSnapshot("VMAR", (decimal)candles[^1].Close, "USD", Now);

        var signal = CersSignalGenerator.GenerateFromCandles(
            quote,
            baseOrderQuantity: 2m,
            nowUtc: Now,
            candles: candles,
            openLong: null);

        Assert.True(signal.IsActionable, signal.OwnerMessage);
        Assert.Equal(SignalSide.Buy, signal.Side);
        Assert.Equal("VMAR", signal.Symbol);
        Assert.Equal(2m, signal.SuggestedQuantity);
        Assert.Contains("CERS", signal.StrategyName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("실주문", signal.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("투자 조언 아님", signal.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Router_maps_cers_kind()
    {
        var router = new StrategySignalRouter();
        Assert.Contains(
            StrategySignalRouter.DefaultGenerators(),
            g => g.Kind == TradingStrategyKind.CERS비용회귀 && g is CersSignalGenerator);

        var candles = BuildEdgeDipSeries();
        var quote = new QuoteSnapshot("VMAR", (decimal)candles[^1].Close, "USD", Now);
        var practice = new PracticeStrategyContext(Candles: candles);

        var signal = router.Generate(
            TradingStrategyKind.CERS비용회귀,
            quote,
            baseOrderQuantity: 2m,
            nowUtc: Now,
            practice: practice);

        Assert.True(signal.IsActionable, signal.OwnerMessage);
        Assert.Equal(SignalSide.Buy, signal.Side);
        Assert.Contains("CERS", signal.StrategyName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Router_cers_without_candles_not_actionable()
    {
        var router = new StrategySignalRouter();
        var quote = new QuoteSnapshot("VMAR", 100m, "USD", Now);

        // No practice / no candles → fail-closed hold.
        var bare = router.Generate(TradingStrategyKind.CERS비용회귀, quote, 2m, Now);
        Assert.False(bare.IsActionable);

        var emptyPractice = router.Generate(
            TradingStrategyKind.CERS비용회귀,
            quote,
            2m,
            Now,
            practice: new PracticeStrategyContext());
        Assert.False(emptyPractice.IsActionable);
    }

    [Fact]
    public void Pipeline_cers_produces_candidate_when_edge()
    {
        var candles = BuildEdgeDipSeries();
        var last = candles[^1].Close;
        var now = Now;
        var quotes = new[] { new QuoteSnapshot("VMAR", (decimal)last, "USD", now) };
        var practice = new PracticeStrategyContext(Candles: candles);
        var settings = new TradingSafetySettings
        {
            MaxOrderNotional = 50_000m,
            MarketDataMaxStalenessSeconds = 120,
        };

        var pipeline = new OrderCandidatePipeline();
        var result = pipeline.BuildCandidates(
            quotes,
            settings,
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.CERS비용회귀,
            practice: practice);

        Assert.NotEmpty(result);
        Assert.Equal("BUY", result[0].Candidate.Side);
        Assert.Equal("VMAR", result[0].Candidate.Symbol);
        Assert.Contains("CERS", result[0].Signal.StrategyName, StringComparison.OrdinalIgnoreCase);
        Assert.True(result[0].IsAcceptedForDryRun, result[0].OwnerStatusMessage);
    }

    [Fact]
    public void Pipeline_cers_without_candles_produces_no_candidates()
    {
        var now = Now;
        var quotes = new[] { new QuoteSnapshot("VMAR", 100m, "USD", now) };
        var pipeline = new OrderCandidatePipeline();

        var noPractice = pipeline.BuildCandidates(
            quotes,
            TradingSafetySettings.CreateSafeDefaults(),
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.CERS비용회귀);
        Assert.Empty(noPractice);

        var nullCandles = pipeline.BuildCandidates(
            quotes,
            TradingSafetySettings.CreateSafeDefaults(),
            defaultOrderQuantity: 2m,
            nowUtc: now,
            strategy: TradingStrategyKind.CERS비용회귀,
            practice: new PracticeStrategyContext());
        Assert.Empty(nullCandles);
    }

    /// <summary>
    /// Warm flat EMA, then sharp drop + hammer reclaim so last-bar CERS edge exceeds threshold.
    /// </summary>
    private static IReadOnlyList<CandlePoint> BuildEdgeDipSeries()
    {
        var list = new List<CandlePoint>();
        var t0 = DateTimeOffset.Parse("2026-01-02T14:30:00Z");
        const double level = 100.0;
        const double volume = 1_500;

        // Warm-up flat so EMA/RSI seed.
        for (var i = 0; i < 50; i++)
        {
            list.Add(Bar(t0, i, level, level * 1.001, level * 0.999, level, volume));
        }

        // Controlled sell-off ~10% over 6 red bars.
        var px = level;
        for (var d = 0; d < 6; d++)
        {
            var i = list.Count;
            var o = px;
            var c = o * (1.0 - 0.10 / 6.0);
            list.Add(Bar(t0, i, o, o * 1.001, c * 0.997, c, volume * 1.6));
            px = c;
        }

        // Hammer reclaim (green, long lower wick, volume spike) — CERS entry bar.
        {
            var i = list.Count;
            var o = px;
            var c = o * 1.006;
            var l = o * 0.988;
            var h = c * 1.002;
            list.Add(Bar(t0, i, o, h, l, c, volume * 3.0));
        }

        // Sanity: last finite edge must clear entry threshold (test is deterministic).
        var edge = CersMath.LastExpectedEdge(list);
        Assert.True(
            edge is double e && e > CersPreset.EntryThreshold,
            $"fixture must produce edge > {CersPreset.EntryThreshold}, got {edge}");

        return list;
    }

    private static CandlePoint Bar(
        DateTimeOffset t0,
        int index,
        double open,
        double high,
        double low,
        double close,
        double volume) =>
        new(t0.AddMinutes(index), open, high, low, close, volume);
}
