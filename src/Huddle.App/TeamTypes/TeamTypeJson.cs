using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>The one set of JSON options that reads and writes <c>team-type.json</c>.</summary>
internal static class TeamTypeJson
{
    /// <summary>The options for reading manifests: camelCase names, lower-case enum names, comments and trailing commas.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
