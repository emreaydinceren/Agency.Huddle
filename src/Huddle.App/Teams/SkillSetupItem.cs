namespace Agency.Huddle.App.Teams;

/// <summary>One thing a card or draft needs before it can be hired.</summary>
/// <param name="Kind">Pack or Skill.</param>
/// <param name="Name">The Pack id or Skill name.</param>
/// <param name="Problem">What is wrong.</param>
/// <param name="Detail">The Pack title, the first error message, or the Source's reason.</param>
internal sealed record SkillSetupItem(SkillSetupKind Kind, string Name, SkillAvailability Problem, string Detail);
