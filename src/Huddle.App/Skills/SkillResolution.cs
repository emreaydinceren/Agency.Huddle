namespace Agency.Huddle.App.Skills;

/// <summary>The outcome of resolving a set of assigned Skill names to their current <see cref="Skill"/> records.</summary>
/// <param name="Skills">The Skills that resolved successfully.</param>
/// <param name="Warnings">Human-readable warnings about names that did not resolve.</param>
internal sealed record SkillResolution(IReadOnlyList<Skill> Skills, IReadOnlyList<string> Warnings);
