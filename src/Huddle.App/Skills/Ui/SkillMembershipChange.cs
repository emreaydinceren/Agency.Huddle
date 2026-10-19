namespace Agency.Huddle.App.Skills.Ui;

/// <summary>A Skill the Pack now lists that some holder does not have.</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Source">The Source id.</param>
/// <param name="Commit">The pointer's commit, short.</param>
/// <param name="License">The licence the catalog declares.</param>
internal sealed record SkillMembershipChange(string Name, string Source, string Commit, string? License);
