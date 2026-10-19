namespace Agency.Huddle.App.Skills;

/// <summary>
/// One resolved Skill: what the shipped defaults or a Human wrote under Contract A, merged with
/// any on-disk override, and ready to be shown, granted, or read by <c>read_skill</c> (Spec §6.2).
/// </summary>
/// <param name="Name">The Skill's name; equals its folder name (Spec §7.2).</param>
/// <param name="Description">The one-line trigger description shown wherever Skills are listed.</param>
/// <param name="Tools">The App Tools this Skill grants, already filtered to <see cref="SkillGrants.Grantable"/>.</param>
/// <param name="Files">This Skill's file names, <c>SKILL.md</c> first, then ordinal order.</param>
/// <param name="Source">Where this Skill's current text came from.</param>
/// <param name="FolderPath">The on-disk folder this Skill was resolved from, or <see langword="null"/> for a <see cref="SkillSource.Default"/> with no folder on disk.</param>
/// <param name="Provenance">The <c>.source.json</c> record of an imported Skill, or <see langword="null"/> for a Skill that was not imported (SK-7).</param>
/// <param name="ModifiedLocally">Whether the folder's current hash differs from <see cref="SkillProvenance.Sha256"/>; always <see langword="false"/> without <paramref name="Provenance"/> (SK-9).</param>
public sealed record Skill(
    string Name,
    string Description,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Files,
    SkillSource Source,
    string? FolderPath,
    SkillProvenance? Provenance = null,
    bool ModifiedLocally = false);
