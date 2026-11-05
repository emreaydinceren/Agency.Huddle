using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.AgentsMdChange;

/// <summary>Source-generated JSON metadata for <see cref="AgentsMdChangeVerdict"/>; the prompt asks for camelCase names.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AgentsMdChangeVerdict))]
internal sealed partial class AgentsMdChangeJsonContext : JsonSerializerContext;
