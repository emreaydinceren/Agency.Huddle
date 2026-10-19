namespace Agency.Huddle.App.FileChanges;

/// <summary>The kind of change <see cref="FileStateDiff"/> found between two snapshots of a file.</summary>
internal enum FileChangeKind
{
    /// <summary>The file exists in the later snapshot only.</summary>
    Added,

    /// <summary>The file exists in both snapshots, with a different size or modified time.</summary>
    Changed,

    /// <summary>The file exists in the earlier snapshot only.</summary>
    Deleted,
}
