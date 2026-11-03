namespace Agency.Huddle.App.SecondBrain;

/// <summary>How a rename done through <see cref="IArchiveFileSystem.Move"/> ended.</summary>
internal enum ArchiveMoveResult
{
    /// <summary>The path now sits at its destination.</summary>
    Moved,

    /// <summary>Another program holds a file open; nothing moved.</summary>
    InUse,
}
