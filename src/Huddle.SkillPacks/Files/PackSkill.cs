using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>A Skill inside a Pack: its own folder, or a named entry of a catalog. Exactly one of Path and Catalog is set (P009).</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Path">Where the folder is. Null when <paramref name="Catalog"/> is set. An empty entry means Not downloaded.</param>
/// <param name="Catalog">The id of a catalog that lists this Skill. Null when <paramref name="Path"/> is set.</param>
/// <param name="Origin">The pinned tree URL when the Pack gives its own path; ignored when <paramref name="Catalog"/> is set.</param>
public sealed record PackSkill(string Name, PathEntries? Path, string? Catalog, string? Origin);
