namespace Agency.Huddle.App.Memory;

/// <summary>
/// What <see cref="MemoryWriter.ReplaceAndRemoveAsync"/> and <see cref="MemoryWriter.PruneReportedAsync"/> did (Memory Tidy spec §6.11).
/// It is not <see cref="MemoryWriteResult"/>, whose switchers read every case they do not name as "stale".
/// </summary>
internal abstract record MemoryTidyResult
{
    /// <summary>The change was made.</summary>
    /// <param name="WrittenVersion">The written file's version, or <see langword="null"/> when nothing was written (a drop).</param>
    /// <param name="Copies">The full paths of the backup copies of the files this step changed or removed.</param>
    /// <param name="LeftBehind">The full paths of files that could not be removed after the write.</param>
    internal sealed record Done(string? WrittenVersion, IReadOnlyList<string> Copies, IReadOnlyList<string> LeftBehind) : MemoryTidyResult;

    /// <summary>A named file changed or went since it was read; nothing was changed.</summary>
    /// <param name="File">The file name.</param>
    internal sealed record Stale(string File) : MemoryTidyResult;

    /// <summary>A memory rule refused the write; nothing was changed.</summary>
    /// <param name="Why">Plain text for the journal.</param>
    internal sealed record Refused(string Why) : MemoryTidyResult;

    /// <summary>A file could not be copied to the backup; nothing was changed.</summary>
    /// <param name="File">The file name.</param>
    internal sealed record CopyFailed(string File) : MemoryTidyResult;

    /// <summary>The disk failed after the copies were made.</summary>
    /// <param name="File">The file the step was working on.</param>
    /// <param name="Error">The exception, or <see langword="null"/> when the write was refused without one.</param>
    internal sealed record Failed(string File, Exception? Error) : MemoryTidyResult;
}
