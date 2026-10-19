namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>Which rule a folder issue comes from, so the checks can map it to a code (§6.3).</summary>
public enum FolderRule
{
    /// <summary>Rule 1: no <c>SKILL.md</c>.</summary>
    NoSkillMd,

    /// <summary>Rule 3: <c>name</c> missing, invalid or not the folder name.</summary>
    Name,

    /// <summary>Rule 4: <c>description</c> missing or over 500 characters.</summary>
    Description,

    /// <summary>Rule 4: <c>description</c> over 300 characters (a warning).</summary>
    LongDescription,

    /// <summary>Rule 5: a <c>tools</c> entry that is not grantable.</summary>
    UngrantableTool,

    /// <summary>Rule 6: an unknown frontmatter key.</summary>
    UnknownKey,

    /// <summary>More than 200 files.</summary>
    TooManyFiles,

    /// <summary>Rule 7: a file over 65,536 UTF-8 bytes.</summary>
    LargeFile,
}
