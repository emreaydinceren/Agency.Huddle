using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Source-generated JSON metadata for <see cref="MemoryTidyEntry"/>; enums are written by name so reordering one cannot rewrite history.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MemoryTidyEntry))]
internal sealed partial class MemoryTidyJsonContext : JsonSerializerContext;
