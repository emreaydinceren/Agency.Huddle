namespace Agency.Huddle.App.SecondBrain;

/// <summary>How one rename attempt done through <see cref="IArchiveFileSystem.Move"/> ended.</summary>
internal enum ArchiveMoveResult
{
    /// <summary>The path now sits at its destination.</summary>
    Moved,

    /// <summary>Another program holds a file open; nothing moved.</summary>
    InUse,

    /// <summary>The destination is on another volume, so a rename cannot work; nothing moved. The caller copies instead.</summary>
    CrossVolume,
}
