namespace Agency.Huddle.App.Skills;

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
