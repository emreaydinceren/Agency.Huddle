namespace Agency.Huddle.App.SkillSources;

/// <summary>One Skill now in <c>Skills/</c>.</summary>
/// <param name="Name">The Skill.</param>
/// <param name="Sha256">The hash of the copy now in <c>Skills/</c>.</param>
/// <param name="Outcome">Created, Replaced, or AlreadyCurrent (nothing written).</param>
internal sealed record CopiedSkill(string Name, string Sha256, CopyOutcome Outcome);
