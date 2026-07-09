namespace TradingBot.Infrastructure.Toss;

/// <summary>
/// Minimal .env loader. Never logs values. Process env wins over file.
/// </summary>
public static class EnvFile
{
    public static IDictionary<string, string?> LoadMergedWithProcess(string? repoRoot)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            var path = Path.Combine(repoRoot, ".env");
            if (File.Exists(path))
            {
                foreach (var (k, v) in ParseFile(path))
                {
                    map[k] = v;
                }
            }
        }

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            map[key] = entry.Value?.ToString();
        }

        return map;
    }

    public static IReadOnlyDictionary<string, string?> ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var idx = line.IndexOf('=');
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (value.Length >= 2
                && ((value.StartsWith('"') && value.EndsWith('"'))
                    || (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            if (key.Length > 0)
            {
                map[key] = value;
            }
        }

        return map;
    }
}
