namespace Agency.Huddle.App.SkillSources;

/// <summary>The outcome of removing a row from SKILLS.md.</summary>
internal enum RemoveRowResult
{
    /// <summary>The row's lines were deleted.</summary>
    Removed,

    /// <summary>No row points at that file.</summary>
    NotFound,

    /// <summary>The file kept changing between the read and the write, three times.</summary>
    KeptChanging,
}
