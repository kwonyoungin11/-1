using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// JSON cache for OHLCV series under <c>artifacts/candles/</c>.
/// Offline backtest input only — not investment advice; never stores secrets.
/// </summary>
public static class CandleJsonStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Default cache path: <c>{repoRoot}/artifacts/candles/{SYMBOL}_{interval}.json</c>.
    /// </summary>
    public static string DefaultCachePath(string repoRoot, string symbol, string interval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        var safeSymbol = symbol.Trim().ToUpperInvariant();
        var safeInterval = interval.Trim().ToLowerInvariant();
        return Path.Combine(repoRoot, "artifacts", "candles", $"{safeSymbol}_{safeInterval}.json");
    }

    public static async Task SaveAsync(
        string path,
        string symbol,
        string interval,
        IReadOnlyList<CandlePoint> candles,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        ArgumentNullException.ThrowIfNull(candles);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var dto = new CandleCacheFile
        {
            Symbol = symbol.Trim().ToUpperInvariant(),
            Interval = interval.Trim().ToLowerInvariant(),
            Source = source ?? "unknown",
            SavedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Count = candles.Count,
            Candles = candles.Select(ToDto).ToList(),
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads cache if file exists and has at least one candle; otherwise null.
    /// </summary>
    public static async Task<CandleCacheLoadResult?> TryLoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<CandleCacheFile>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (dto?.Candles is null || dto.Candles.Count == 0)
        {
            return null;
        }

        var list = new List<CandlePoint>(dto.Candles.Count);
        foreach (var c in dto.Candles)
        {
            if (!DateTimeOffset.TryParse(c.Time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t))
            {
                continue;
            }

            list.Add(new CandlePoint(
                Time: t,
                Open: c.Open,
                High: c.High,
                Low: c.Low,
                Close: c.Close,
                Volume: c.Volume));
        }

        if (list.Count == 0)
        {
            return null;
        }

        list.Sort(static (a, b) => a.Time.CompareTo(b.Time));
        return new CandleCacheLoadResult(
            Symbol: dto.Symbol ?? string.Empty,
            Interval: dto.Interval ?? string.Empty,
            Source: dto.Source,
            Candles: list);
    }

    private static CandleDto ToDto(CandlePoint c) =>
        new()
        {
            Time = c.Time.ToString("O", CultureInfo.InvariantCulture),
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume,
        };

    private sealed class CandleCacheFile
    {
        public string? Symbol { get; set; }
        public string? Interval { get; set; }
        public string? Source { get; set; }
        public string? SavedAtUtc { get; set; }
        public int Count { get; set; }
        public List<CandleDto>? Candles { get; set; }
    }

    private sealed class CandleDto
    {
        public string? Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }
}

/// <summary>Successful candle cache load.</summary>
public sealed record CandleCacheLoadResult(
    string Symbol,
    string Interval,
    string? Source,
    IReadOnlyList<CandlePoint> Candles);
