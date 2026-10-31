using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins <see cref="FileStateDiff.Compare(FolderSnapshot, FolderSnapshot, string)"/>: a pure
/// comparison of two <see cref="FolderSnapshot"/> instances into an ordered set of
/// <see cref="FileChange"/> values, per FC §6.5.
/// </summary>
public sealed class FileStateDiffTests
{
    /// <summary>A file present only in <c>after</c> is reported as <see cref="FileChangeKind.Added"/>.</summary>
    [Fact]
    public void Compare_FileOnlyInAfter_IsAdded()
    {
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer));
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, DateTimeOffset.UtcNow),
        });

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        FileChange change = Assert.Single(changes);
        Assert.Equal(FileChangeKind.Added, change.Kind);
    }

    /// <summary>A file present only in <c>before</c> is reported as <see cref="FileChangeKind.Deleted"/>.</summary>
    [Fact]
    public void Compare_FileOnlyInBefore_IsDeleted()
    {
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, DateTimeOffset.UtcNow),
        });
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer));

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        FileChange change = Assert.Single(changes);
        Assert.Equal(FileChangeKind.Deleted, change.Kind);
    }

    /// <summary>A file whose size differs between snapshots is reported as <see cref="FileChangeKind.Changed"/>.</summary>
    [Fact]
    public void Compare_SizeDiffers_IsChanged()
    {
        DateTimeOffset modified = DateTimeOffset.UtcNow;
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, modified),
        });
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(20, modified),
        });

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        FileChange change = Assert.Single(changes);
        Assert.Equal(FileChangeKind.Changed, change.Kind);
    }

    /// <summary>A file whose modified time differs, with size unchanged, is reported as <see cref="FileChangeKind.Changed"/>.</summary>
    [Fact]
    public void Compare_OnlyModifiedTimeDiffers_IsChanged()
    {
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, DateTimeOffset.UtcNow),
        });
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, DateTimeOffset.UtcNow.AddMinutes(5)),
        });

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        FileChange change = Assert.Single(changes);
        Assert.Equal(FileChangeKind.Changed, change.Kind);
    }

    /// <summary>Two identical snapshots produce no changes.</summary>
    [Fact]
    public void Compare_Identical_ReturnsEmpty()
    {
        DateTimeOffset modified = DateTimeOffset.UtcNow;
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, modified),
        });
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, modified),
        });

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        Assert.Empty(changes);
    }

    /// <summary>A file that changed and then changed back to the same size and time is not reported.</summary>
    [Fact]
    public void Compare_ChangedAndChangedBack_ReturnsEmpty()
    {
        DateTimeOffset modified = DateTimeOffset.UtcNow;
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, modified),
        });
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["a.md"] = new FileEntry(10, modified),
        });

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        Assert.Empty(changes);
    }

    /// <summary>Several changes come back sorted by relative path, ordinal, kinds mixed.</summary>
    [Fact]
    public void Compare_SeveralChanges_SortedByRelativePathOrdinal()
    {
        DateTimeOffset modified = DateTimeOffset.UtcNow;
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["b.md"] = new FileEntry(10, modified),
            [@"a\c.md"] = new FileEntry(10, modified),
        });
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            ["b.md"] = new FileEntry(10, modified),
            ["A.md"] = new FileEntry(5, modified),
        });

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        Assert.Collection(
            changes,
            first => Assert.Equal(FileChangeKind.Added, first.Kind),
            second => Assert.Equal(FileChangeKind.Deleted, second.Kind));
        Assert.Equal(Path.Combine(@"C:\folder", "A.md"), changes[0].FullPath);
        Assert.Equal(Path.Combine(@"C:\folder", @"a\c.md"), changes[1].FullPath);
    }

    /// <summary><see cref="FileChange.FullPath"/> is the folder combined with the relative path.</summary>
    [Fact]
    public void Compare_FullPath_IsFolderPlusRelativePath()
    {
        FolderSnapshot before = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer));
        FolderSnapshot after = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
        {
            [@"sub\a.md"] = new FileEntry(10, DateTimeOffset.UtcNow),
        });

        IReadOnlyList<FileChange> changes = FileStateDiff.Compare(before, after, @"C:\folder");

        FileChange change = Assert.Single(changes);
        Assert.Equal(Path.Combine(@"C:\folder", @"sub\a.md"), change.FullPath);
    }
}
