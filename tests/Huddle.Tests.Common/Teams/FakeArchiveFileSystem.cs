using Agency.Huddle.App.SecondBrain;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// An <see cref="IArchiveFileSystem"/> over the real disk (the wrapped <paramref name="inner"/>) that a test can make
/// misbehave: report that a rename crosses a volume, answer "another program has a file open" a number of times, fail or
/// corrupt the copy, and "crash" (throw <see cref="SimulatedCrashException"/>) just before or just after a named step.
/// Steps are logged in <see cref="Log"/> as <c>Operation:leaf</c>, where the leaf is the last segment of the destination
/// (or of the path for a delete), for example <c>CopyTree:.huddle-archiving-Home</c>, <c>Move:Home</c>,
/// <c>Move:.huddle-deleting-Home</c>, <c>DeleteTree:.huddle-deleting-Home</c>.
/// </summary>
/// <param name="inner">The real file system that does the work.</param>
internal sealed class FakeArchiveFileSystem(IArchiveFileSystem inner) : IArchiveFileSystem
{
    private const string HalfDelete = "DeleteTree:half";

    private readonly List<string> log = [];
    private int copiedFiles;

    /// <summary>Gets the steps taken, in order.</summary>
    public IReadOnlyList<string> Log => this.log;

    /// <summary>Gets or sets whether a rename between two different parent folders answers <see cref="ArchiveMoveResult.CrossVolume"/> without moving anything.</summary>
    public bool CrossVolume { get; set; }

    /// <summary>Gets or sets how many <c>Move</c> calls answer <see cref="ArchiveMoveResult.InUse"/> before one works; <see cref="int.MaxValue"/> means always.</summary>
    public int SharingViolations { get; set; }

    /// <summary>Gets or sets the step after which the fake throws <see cref="SimulatedCrashException"/>; <c>DeleteTree:half</c> deletes one file of the tree first, then throws.</summary>
    public string? CrashAfter { get; set; }

    /// <summary>Gets or sets the step before which the fake throws <see cref="SimulatedCrashException"/> (the step does not happen).</summary>
    public string? CrashBefore { get; set; }

    /// <summary>Gets or sets the 1-based number of the file whose copy throws an <see cref="IOException"/>; <see langword="null"/> copies everything.</summary>
    public int? FailCopyAtFile { get; set; }

    /// <summary>Gets or sets whether the copy writes one byte less into the last file it copies, so the verify step must notice.</summary>
    public bool CorruptCopy { get; set; }

    /// <summary>Gets or sets an exception every <c>Move</c> throws (after it is logged), for example a rename that fails for a reason other than a held-open file.</summary>
    public Exception? MoveThrows { get; set; }

    /// <summary>Gets or sets an exception every <c>CopyTree</c> throws before it copies anything.</summary>
    public Exception? CopyThrows { get; set; }

    /// <summary>Gets or sets the destination leaf whose rename always answers <see cref="ArchiveMoveResult.InUse"/>.</summary>
    public string? InUseFor { get; set; }

    /// <summary>Gets or sets whether <c>FileExists</c> and <c>ReadText</c> match path names case-sensitively, as a Linux file system does.</summary>
    public bool CaseSensitiveNames { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<string> ListEntries(string folder) => inner.ListEntries(folder);

    /// <inheritdoc />
    public bool FileExists(string path) => this.NameMatches(path) && inner.FileExists(path);

    /// <inheritdoc />
    public string? ReadText(string path)
    {
        this.log.Add("ReadText:" + Path.GetFileName(path));
        return this.NameMatches(path) ? inner.ReadText(path) : null;
    }

    /// <inheritdoc />
    public void WriteTextAtomic(string path, string text) => inner.WriteTextAtomic(path, text);

    /// <inheritdoc />
    public void CreateDirectory(string path) => inner.CreateDirectory(path);

    /// <inheritdoc />
    public void DeleteFile(string path) => inner.DeleteFile(path);

    /// <inheritdoc />
    public void DeleteEmptyFolder(string path) => inner.DeleteEmptyFolder(path);

    /// <inheritdoc />
    public ArchiveMoveResult Move(string source, string destination)
    {
        string step = "Move:" + Path.GetFileName(destination);
        this.Enter(step);
        if (this.MoveThrows is not null)
        {
            throw this.MoveThrows;
        }

        if (string.Equals(this.InUseFor, Path.GetFileName(destination), StringComparison.Ordinal))
        {
            return ArchiveMoveResult.InUse;
        }

        if (this.SharingViolations > 0)
        {
            this.SharingViolations = this.SharingViolations == int.MaxValue ? int.MaxValue : this.SharingViolations - 1;
            return ArchiveMoveResult.InUse;
        }

        if (this.CrossVolume && !string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(destination), StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveMoveResult.CrossVolume;
        }

        ArchiveMoveResult result = inner.Move(source, destination);
        this.Leave(step);
        return result;
    }

    /// <summary>Copies the folder tree (logged as <c>CopyTree:leaf</c>), failing or shortening the copy as configured.</summary>
    /// <param name="source">The folder to copy.</param>
    /// <param name="destination">The new folder.</param>
    public void CopyTree(string source, string destination)
    {
        string step = "CopyTree:" + Path.GetFileName(destination);
        this.Enter(step);
        if (this.CopyThrows is not null)
        {
            throw this.CopyThrows;
        }

        this.copiedFiles = 0;
        this.CopyInto(source, destination, Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Count());
        this.Leave(step);
    }

    /// <summary>Counts the files and bytes under the folder (logged as <c>Measure:leaf</c>).</summary>
    /// <param name="folder">The folder to measure.</param>
    /// <returns>The file count and total bytes.</returns>
    public (int Files, long Bytes) Measure(string folder)
    {
        this.log.Add("Measure:" + Path.GetFileName(folder));
        return inner.Measure(folder);
    }

    /// <summary>Deletes the folder tree (logged as <c>DeleteTree:leaf</c>); the <c>DeleteTree:half</c> crash deletes one file and throws.</summary>
    /// <param name="path">The folder to delete.</param>
    public void DeleteTree(string path)
    {
        string step = "DeleteTree:" + Path.GetFileName(path);
        this.Enter(step);
        if (this.CrashAfter == HalfDelete && Directory.Exists(path))
        {
            File.Delete(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).First());
            this.CrashAfter = null;
            throw new SimulatedCrashException(HalfDelete);
        }

        inner.DeleteTree(path);
        this.Leave(step);
    }

    private bool NameMatches(string path)
    {
        // Two levels are enough for the tests: the leaf and the Team folder that holds it.
        string? parent = Path.GetDirectoryName(path);
        string? grandparent = parent is null ? null : Path.GetDirectoryName(parent);
        return !this.CaseSensitiveNames
            || parent is null
            || (inner.ListEntries(parent).Contains(Path.GetFileName(path), StringComparer.Ordinal)
                && (grandparent is null || inner.ListEntries(grandparent).Contains(Path.GetFileName(parent), StringComparer.Ordinal)));
    }

    private void CopyInto(string source, string destination, int total)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            this.CopyInto(directory, Path.Combine(destination, Path.GetFileName(directory)), total);
        }

        foreach (string file in Directory.EnumerateFiles(source))
        {
            this.copiedFiles++;
            if (this.FailCopyAtFile == this.copiedFiles)
            {
                throw new IOException("Simulated copy failure.");
            }

            byte[] bytes = File.ReadAllBytes(file);
            bool last = this.copiedFiles == total;
            File.WriteAllBytes(Path.Combine(destination, Path.GetFileName(file)), this.CorruptCopy && last && bytes.Length > 0 ? bytes[..^1] : bytes);
        }
    }

    private void Enter(string step)
    {
        this.log.Add(step);
        if (string.Equals(this.CrashBefore, step, StringComparison.Ordinal))
        {
            throw new SimulatedCrashException(step);
        }
    }

    private void Leave(string step)
    {
        if (string.Equals(this.CrashAfter, step, StringComparison.Ordinal))
        {
            throw new SimulatedCrashException(step);
        }
    }
}

/// <summary>Thrown by <see cref="FakeArchiveFileSystem"/> to stand for the process dying at that step.</summary>
public sealed class SimulatedCrashException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public SimulatedCrashException()
    {
    }

    /// <summary>Creates the exception naming the step.</summary>
    /// <param name="message">The step the crash happened at.</param>
    public SimulatedCrashException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a cause.</summary>
    /// <param name="message">The step the crash happened at.</param>
    /// <param name="innerException">The cause.</param>
    public SimulatedCrashException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
