using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.Contracts;

public static class ProtocolJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(message, Options);
    }

    public static ProtocolMessage Deserialize(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var message = JsonSerializer.Deserialize<ProtocolMessage>(line, Options);
        if (message is null)
        {
            throw new JsonException("The line deserialised to a null message.");
        }

        if (message.Version != ProtocolVersion.Current)
        {
            throw new JsonException($"Unsupported protocol version '{message.Version}'.");
        }

        return message;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowOutOfOrderMetadataProperties = true,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}