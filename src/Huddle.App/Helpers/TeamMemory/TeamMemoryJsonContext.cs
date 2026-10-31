using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>Source-generated JSON metadata for <see cref="TeamMemoryVerdict"/>; the prompt asks for camelCase names.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TeamMemoryVerdict))]
internal sealed partial class TeamMemoryJsonContext : JsonSerializerContext;
