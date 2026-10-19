namespace Agency.Huddle.App.TeamTypes;

/// <summary>A skill a role carries.</summary>
/// <param name="Name">The skill name.</param>
/// <param name="Source">Either <c>builtin</c> or <c>owner/repo</c>.</param>
/// <param name="Path">The path inside the source, or null for the default location.</param>
internal sealed record SkillRef(string Name, string Source, string? Path);
