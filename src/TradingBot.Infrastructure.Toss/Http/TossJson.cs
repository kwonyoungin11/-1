using System.Text.Json;

namespace TradingBot.Infrastructure.Toss.Http;

internal static class TossJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static T DeserializeRequired<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, Options);
        if (value is null)
        {
            throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from Toss response.");
        }

        return value;
    }
}
