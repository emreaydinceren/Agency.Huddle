namespace Agency.Huddle.App.Skills;

/// <summary>Where a resolved <see cref="Skill"/>'s current text came from.</summary>
public enum SkillSource
{
    /// <summary>The shipped default, with no override folder on disk.</summary>
    Default,

    /// <summary>A shipped default whose folder also exists on disk, overriding one or more files.</summary>
    Overridden,

    /// <summary>A Skill the Human wrote, with no shipped default of the same name.</summary>
    Yours,
}
