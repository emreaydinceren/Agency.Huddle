using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>The Team Memory Helper's answer (Team Memory Helper spec §6.5).</summary>
/// <param name="Action"><c>none</c>, <c>create</c> or <c>update</c>. Required: a missing answer is not a quiet "none".</param>
/// <param name="Folder">The folder label, <c>Team</c> or <c>Team/Project</c>; unused for <c>none</c>.</param>
/// <param name="File">The file name; unused for <c>none</c>.</param>
/// <param name="Description">The file's description; unused for <c>none</c>.</param>
/// <param name="Text">The file's whole body, without frontmatter; unused for <c>none</c>.</param>
/// <param name="Reason">Why, in a few words: shown in the History and the Feed.</param>
internal sealed record TeamMemoryVerdict(
    [property: JsonRequired] string Action,
    string? Folder,
    string? File,
    string? Description,
    string? Text,
    string? Reason);
