namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>One <c>key: value</c> line of a <c>SKILL.md</c>'s frontmatter.</summary>
/// <param name="Key">The key as written.</param>
/// <param name="Value">The value on one line: quotes stripped, a block scalar collapsed, list items joined with <c>"; "</c>.</param>
public sealed record SkillFrontmatterField(string Key, string Value);
