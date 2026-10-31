namespace Agency.Huddle.Seeder.Model;

/// <summary>A Skill: <c>Skills/&lt;Name&gt;/SKILL.md</c>.</summary>
/// <param name="Name">Lower-case words joined by <c>-</c>; also the folder name.</param>
/// <param name="Description">One sentence, at most 300 characters.</param>
/// <param name="Body">The Markdown steps.</param>
internal sealed record SeedSkill(string Name, string Description, string Body);
