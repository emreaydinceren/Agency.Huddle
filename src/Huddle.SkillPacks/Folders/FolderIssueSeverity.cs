namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>How serious a problem found while validating a Skill folder is.</summary>
public enum FolderIssueSeverity
{
    /// <summary>Worth surfacing, but not a defect: an unknown frontmatter key, for example.</summary>
    Info,

    /// <summary>Something was ignored or dropped, but the Skill still loads.</summary>
    Warning,

    /// <summary>The Skill failed to validate.</summary>
    Error,
}
