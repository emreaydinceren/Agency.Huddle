namespace Agency.Huddle.App.SkillSources;

/// <summary>What <see cref="SkillRegistry.Changed"/> carries.</summary>
/// <param name="Snapshot">The snapshot just published.</param>
/// <param name="AppearedKeys">Keys present now and not in the previous snapshot.</param>
/// <param name="ChangedSkills">Skill names whose state or pointer hash differs from the previous snapshot.</param>
internal sealed record RegistryChange(RegistrySnapshot Snapshot, IReadOnlyList<string> AppearedKeys, IReadOnlyList<string> ChangedSkills);
