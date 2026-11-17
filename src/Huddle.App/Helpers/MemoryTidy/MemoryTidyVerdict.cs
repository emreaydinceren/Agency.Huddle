using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>The Memory Tidy Helper's answer for one folder, as the model wrote it.</summary>
/// <param name="Actions">The changes it asks for; empty when there is nothing to tidy. Required: a missing list is not a quiet "nothing".</param>
internal sealed record MemoryTidyVerdict([property: JsonRequired] IReadOnlyList<MemoryTidyAction>? Actions);
