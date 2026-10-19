namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>The limit a Skill folder broke: its size (S003) or a name or link Windows cannot keep (S004).</summary>
public enum FolderLimit
{
    /// <summary>More than 200 files or 5 MB in total (S003).</summary>
    Size,

    /// <summary>A link, a name Windows cannot store, or two names that differ only in case (S004).</summary>
    Name,
}
