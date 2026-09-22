namespace Agency.Huddle.App.Skills;

/// <summary>
/// One resolved Skill: what the shipped defaults or a Human wrote under Contract A, merged with
/// any on-disk override, and ready to be shown, granted, or read by <c>read_skill</c> (Spec §6.2).
/// </summary>
/// <param name="Name">The Skill's name; equals its folder name (Spec §7.2).</param>
/// <param name="Description">The one-line trigger description shown wherever Skills are listed.</param>
/// <param name="Tools">The App Tools this Skill grants, already filtered to <see cref="SkillValidator.Grantable"/>.</param>
/// <param name="Files">This Skill's file names, <c>SKILL.md</c> first, then ordinal order.</param>
/// <param name="Source">Where this Skill's current text came from.</param>
/// <param name="FolderPath">The on-disk folder this Skill was resolved from, or <see langword="null"/> for a <see cref="SkillSource.Default"/> with no folder on disk.</param>
internal sealed record Skill(
    string Name,
    string Description,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Files,
    SkillSource Source,
    string? FolderPath);

/// <summary>Where a resolved <see cref="Skill"/>'s current text came from.</summary>
internal enum SkillSource
{
    /// <summary>The shipped default, with no override folder on disk.</summary>
    Default,

    /// <summary>A shipped default whose folder also exists on disk, overriding one or more files.</summary>
    Overridden,

    /// <summary>A Skill the Human wrote, with no shipped default of the same name.</summary>
    Yours,
}

/// <summary>How serious a problem found while resolving or validating a Skill is.</summary>
internal enum SkillIssueSeverity
{
    /// <summary>Worth surfacing, but not a defect — an unknown frontmatter key, for example.</summary>
    Info,

    /// <summary>Something was ignored or dropped, but the Skill still loaded.</summary>
    Warning,

    /// <summary>The Skill failed to validate.</summary>
    Error,
}

/// <summary>One problem found while resolving or validating a Skill, naming which Skill it came from.</summary>
/// <param name="Skill">The Skill's folder name.</param>
/// <param name="Message">A human-readable description of the problem.</param>
/// <param name="Severity">How serious the problem is.</param>
internal sealed record SkillIssue(string Skill, string Message, SkillIssueSeverity Severity);

/// <summary>The outcome of resolving a set of assigned Skill names to their current <see cref="Skill"/> records.</summary>
/// <param name="Skills">The Skills that resolved successfully.</param>
/// <param name="Warnings">Human-readable warnings about names that did not resolve.</param>
internal sealed record SkillResolution(IReadOnlyList<Skill> Skills, IReadOnlyList<string> Warnings);
