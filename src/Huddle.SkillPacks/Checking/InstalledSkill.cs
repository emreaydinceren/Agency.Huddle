namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>A Skill folder in <c>Skills/</c>.</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Origin">The pinned origin the copy came from; null for built-in Skills and Skills the Human wrote.</param>
/// <param name="Sha256">The hash recorded in <c>.source.json</c>; null for built-in Skills and Skills the Human wrote.</param>
/// <param name="BuiltIn">True for a Skill that ships with Huddle.</param>
public sealed record InstalledSkill(string Name, string? Origin, string? Sha256, bool BuiltIn);
