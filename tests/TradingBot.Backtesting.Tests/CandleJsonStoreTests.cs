using TradingBot.Domain;

namespace TradingBot.Backtesting.Tests;

public class CandleJsonStoreTests
{
    [Fact]
    public async Task Save_and_load_roundtrip_sorted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tb-candle-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "VMAR_1m.json");
            var candles = new List<CandlePoint>
            {
                new(DateTimeOffset.Parse("2026-07-09T14:01:00Z"), 1, 1.1, 0.9, 1.05, 100),
                new(DateTimeOffset.Parse("2026-07-09T14:00:00Z"), 1, 1.2, 0.8, 1.0, 200),
            };

            await CandleJsonStore.SaveAsync(path, "vmar", "1m", candles, source: "test");
            var loaded = await CandleJsonStore.TryLoadAsync(path);

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Candles.Count);
            Assert.True(loaded.Candles[0].Time < loaded.Candles[1].Time);
            Assert.Equal("VMAR", loaded.Symbol);
            Assert.Equal("1m", loaded.Interval);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task TryLoad_missing_returns_null()
    {
        var path = Path.Combine(Path.GetTempPath(), "no-such-candles-" + Guid.NewGuid().ToString("N") + ".json");
        var loaded = await CandleJsonStore.TryLoadAsync(path);
        Assert.Null(loaded);
    }

    [Fact]
    public void DefaultCachePath_uses_artifacts_candles()
    {
        var p = CandleJsonStore.DefaultCachePath("/repo", "vmar", "1m");
        Assert.Equal(Path.Combine("/repo", "artifacts", "candles", "VMAR_1m.json"), p);
    }
}
