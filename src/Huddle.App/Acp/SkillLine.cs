namespace Agency.Huddle.App.Acp;

/// <summary>One skill in a <see cref="RoleBlockInput"/>.</summary>
/// <param name="Name">The skill's name.</param>
/// <param name="Description">The skill's description, which states its trigger.</param>
internal sealed record SkillLine(string Name, string Description);
