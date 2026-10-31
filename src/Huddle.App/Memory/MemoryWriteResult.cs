namespace Agency.Huddle.App.Memory;

/// <summary>What one <see cref="MemoryWriter.WriteAsync"/> did (Team Memory Helper spec §6.6). Each case is one the <c>memory</c> tool's <c>write</c> already told apart.</summary>
internal abstract record MemoryWriteResult
{
    private MemoryWriteResult()
    {
    }

    /// <summary>The file was written.</summary>
    /// <param name="Path">The file's full path.</param>
    /// <param name="Version">The file's new version.</param>
    /// <param name="Created">Whether the file did not exist before.</param>
    internal sealed record Written(string Path, string Version, bool Created) : MemoryWriteResult;

    /// <summary>The file name is not one memory file name.</summary>
    internal sealed record BadName : MemoryWriteResult;

    /// <summary>The description breaks a mechanical rule.</summary>
    /// <param name="Problem">What is wrong with it, as <see cref="MemoryFileRules.DescriptionProblem"/> says.</param>
    internal sealed record BadDescription(string Problem) : MemoryWriteResult;

    /// <summary>The composed file is over <c>Team:Memory:MaxFileBytes</c>.</summary>
    /// <param name="Size">The size it would have.</param>
    /// <param name="Max">The cap.</param>
    internal sealed record TooLarge(int Size, int Max) : MemoryWriteResult;

    /// <summary>No version was given, and the file exists: the writer has not read it.</summary>
    internal sealed record Exists : MemoryWriteResult;

    /// <summary>A version was given, and the file is gone.</summary>
    internal sealed record Missing : MemoryWriteResult;

    /// <summary>The file changed since the version the writer read.</summary>
    /// <param name="CurrentVersion">Its version now.</param>
    /// <param name="CurrentText">Its text now.</param>
    internal sealed record Stale(string CurrentVersion, string CurrentText) : MemoryWriteResult;

    /// <summary>The file could not be read or written.</summary>
    /// <param name="Path">The file's full path.</param>
    /// <param name="Error">The failure, or <see langword="null"/> when another process held the file.</param>
    internal sealed record Failed(string Path, Exception? Error) : MemoryWriteResult;
}
