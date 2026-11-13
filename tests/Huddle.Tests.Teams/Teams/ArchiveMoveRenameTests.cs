using Agency.Huddle.App.SecondBrain;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// <see cref="ArchiveMove.Rename"/> (Team Rename Spec 4, tr7a): the retrying single rename the Team rename uses instead of
/// <see cref="ArchiveMove.Move"/>. It refuses an existing destination and writes no journal, because the <c>team_renames</c> row is the journal.
/// </summary>
public sealed class ArchiveMoveRenameTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("archive-rename-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(this.root, recursive: true);

    /// <summary>A folder with a nested file moves whole to the new name and the old name is gone.</summary>
    [Fact]
    public void Rename_FolderWithFiles_MovesItWhole()
    {
        string source = this.Seed("Old");
        string destination = Path.Combine(this.root, "New");

        ArchiveMoveResult result = ArchiveMove.Rename(source, destination, new PhysicalArchiveFileSystem());

        Assert.Equal(ArchiveMoveResult.Moved, result);
        Assert.False(Directory.Exists(source));
        Assert.Equal("alpha", File.ReadAllText(Path.Combine(destination, "a.txt")));
        Assert.Equal("delta", File.ReadAllText(Path.Combine(destination, "sub", "d.txt")));
    }

    /// <summary>An existing destination is refused with an <see cref="IOException"/>; neither folder is touched.</summary>
    [Fact]
    public void Rename_DestinationExists_ThrowsAndLeavesBothUntouched()
    {
        string source = this.Seed("Old");
        string destination = this.Seed("New");
        File.WriteAllText(Path.Combine(destination, "a.txt"), "other");

        _ = Assert.Throws<IOException>(() => ArchiveMove.Rename(source, destination, new PhysicalArchiveFileSystem()));

        Assert.Equal("alpha", File.ReadAllText(Path.Combine(source, "a.txt")));
        Assert.Equal("delta", File.ReadAllText(Path.Combine(source, "sub", "d.txt")));
        Assert.Equal("other", File.ReadAllText(Path.Combine(destination, "a.txt")));
    }

    /// <summary>A rename inside the Archive root leaves no journal, staging or deleting entry beside it.</summary>
    [Fact]
    public void Rename_InArchiveRoot_LeavesNoJournalOrStagingEntry()
    {
        string source = this.Seed("Old");

        _ = ArchiveMove.Rename(source, Path.Combine(this.root, "New"), new PhysicalArchiveFileSystem());

        string[] names = [.. Directory.EnumerateFileSystemEntries(this.root).Select(static p => Path.GetFileName(p)).Order(StringComparer.Ordinal)];
        Assert.Equal(["New"], names);
    }

    /// <summary>A file kept open through every retry reports <see cref="ArchiveMoveResult.InUse"/> and nothing moves.</summary>
    [Fact]
    public void Rename_FileSystemAlwaysInUse_ReturnsInUseAfterRetrying()
    {
        string source = this.Seed("Old");
        InUseFileSystem files = new(new PhysicalArchiveFileSystem());

        ArchiveMoveResult result = ArchiveMove.Rename(source, Path.Combine(this.root, "New"), files);

        Assert.Equal(ArchiveMoveResult.InUse, result);
        Assert.True(files.Attempts > 1);
        Assert.True(Directory.Exists(source));
    }

    private string Seed(string name)
    {
        string folder = Path.Combine(this.root, name);
        _ = Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(folder, "sub", "d.txt"), "delta");
        return folder;
    }

    /// <summary>Answers every <c>Move</c> with <see cref="ArchiveMoveResult.InUse"/> and counts the tries.</summary>
    private sealed class InUseFileSystem(IArchiveFileSystem inner) : IArchiveFileSystem
    {
        public int Attempts { get; private set; }

        public IReadOnlyList<string> ListEntries(string folder) => inner.ListEntries(folder);

        public bool FileExists(string path) => inner.FileExists(path);

        public void CreateDirectory(string path) => inner.CreateDirectory(path);

        public void DeleteFile(string path) => inner.DeleteFile(path);

        public void DeleteEmptyFolder(string path) => inner.DeleteEmptyFolder(path);

        public ArchiveMoveResult Move(string source, string destination)
        {
            this.Attempts++;
            return ArchiveMoveResult.InUse;
        }

        public void CopyTree(string source, string destination) => inner.CopyTree(source, destination);

        public (int Files, long Bytes) Measure(string folder) => inner.Measure(folder);

        public string? ReadText(string path) => inner.ReadText(path);

        public void WriteTextAtomic(string path, string text) => inner.WriteTextAtomic(path, text);

        public void DeleteTree(string path) => inner.DeleteTree(path);
    }
}
