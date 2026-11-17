using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Source-generated JSON metadata for <see cref="MemoryTidyEntry"/> and <see cref="MemoryTidyVerdict"/>; enums are written by name so
/// reordering one cannot rewrite history, and property names are read ignoring case because a model may write <c>Actions</c>.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MemoryTidyEntry))]
[JsonSerializable(typeof(MemoryTidyVerdict))]
internal sealed partial class MemoryTidyJsonContext : JsonSerializerContext;
