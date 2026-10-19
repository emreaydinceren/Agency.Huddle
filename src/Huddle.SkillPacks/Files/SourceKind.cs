namespace Agency.Huddle.SkillPacks.Files;

/// <summary>Where a Source's files came from.</summary>
public enum SourceKind
{
    /// <summary>A GitHub repository.</summary>
    Github,

    /// <summary>Any other git repository.</summary>
    Git,

    /// <summary>A folder on disk.</summary>
    Local,
}
