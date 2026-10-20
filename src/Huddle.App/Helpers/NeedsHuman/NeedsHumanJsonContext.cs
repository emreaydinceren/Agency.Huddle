using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.NeedsHuman;

/// <summary>Source-generated JSON metadata for <see cref="NeedsHumanVerdict"/>; the prompt asks for camelCase names.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NeedsHumanVerdict))]
internal sealed partial class NeedsHumanJsonContext : JsonSerializerContext;
