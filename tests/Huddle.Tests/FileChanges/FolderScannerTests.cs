using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins <see cref="FolderScanner.Scan(string, FileChangesOptions)"/>: pruning of ignored
/// directories, the file cap, missing-folder handling and reparse-point skipping, per FC §6.4.
/// </summary>
public sealed class FolderScannerTests
{
    /// <summary>A folder that does not exist returns <see cref="ScanOutcome.Missing"/> with an empty snapshot.</summary>
    [Fact]
    public void Scan_MissingFolder_ReturnsMissingWithEmptySnapshot()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "does-not-exist");
        FileChangesOptions options = new();

        ScanResult result = FolderScanner.Scan(folder, options);

        Assert.Equal(ScanOutcome.Missing, result.Outcome);
        Assert.Empty(result.Snapshot.Files);
    }

    /// <summary>A nested file's key is its path relative to the scanned folder, with the platform separator.</summary>
    [Fact]
    public void Scan_NestedFiles_KeysAreRelativePathsWithPlatformSeparator()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "sub", "a.md"), "hello");
        FileChangesOptions options = new();

        ScanResult result = FolderScanner.Scan(folder, options);

        Assert.Equal(ScanOutcome.Scanned, result.Outcome);
        Assert.Contains(Path.Combine("sub", "a.md"), result.Snapshot.Files.Keys);
    }

    /// <summary>An ignored directory is pruned before its contents are walked, so it never counts toward the cap.</summary>
    [Fact]
    public void Scan_IgnoredDirectory_IsPrunedNotWalked()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        string nodeModules = Path.Combine(folder, "node_modules");
        Directory.CreateDirectory(nodeModules);
        for (int i = 0; i < 15; i++)
        {
            File.WriteAllText(Path.Combine(nodeModules, $"file{i}.txt"), "x");
        }

        File.WriteAllText(Path.Combine(folder, "a.md"), "a");
        File.WriteAllText(Path.Combine(folder, "b.md"), "b");

        FileChangesOptions options = new() { MaxFilesPerFolder = 5 };

        ScanResult result = FolderScanner.Scan(folder, options);

        Assert.Equal(ScanOutcome.Scanned, result.Outcome);
        Assert.Equal(2, result.Snapshot.Files.Count);
    }

    /// <summary>Ignore-directory matching is case-insensitive.</summary>
    [Fact]
    public void Scan_IgnoreIsCaseInsensitive()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        string nodeModules = Path.Combine(folder, "Node_Modules");
        Directory.CreateDirectory(nodeModules);
        File.WriteAllText(Path.Combine(nodeModules, "a.txt"), "a");
        File.WriteAllText(Path.Combine(folder, "b.md"), "b");

        FileChangesOptions options = new();

        ScanResult result = FolderScanner.Scan(folder, options);

        Assert.Equal(ScanOutcome.Scanned, result.Outcome);
        FileEntry entry = Assert.Single(result.Snapshot.Files).Value;
        Assert.NotNull(entry);
    }

    /// <summary>A folder with more than <see cref="FileChangesOptions.MaxFilesPerFolder"/> files returns <see cref="ScanOutcome.TooLarge"/>.</summary>
    [Fact]
    public void Scan_OverTheCap_ReturnsTooLarge()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        Directory.CreateDirectory(folder);
        for (int i = 0; i < 6; i++)
        {
            File.WriteAllText(Path.Combine(folder, $"file{i}.txt"), "x");
        }

        FileChangesOptions options = new() { MaxFilesPerFolder = 5 };

        ScanResult result = FolderScanner.Scan(folder, options);

        Assert.Equal(ScanOutcome.TooLarge, result.Outcome);
        Assert.Empty(result.Snapshot.Files);
    }

    /// <summary>A hidden file is still included: <see cref="EnumerationOptions.AttributesToSkip"/> must not default to skipping it.</summary>
    [Fact]
    public void Scan_HiddenFile_IsIncluded()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        Directory.CreateDirectory(folder);
        string hiddenFile = Path.Combine(folder, "hidden.md");
        File.WriteAllText(hiddenFile, "hidden");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);

        FileChangesOptions options = new();

        ScanResult result = FolderScanner.Scan(folder, options);

        Assert.Equal(ScanOutcome.Scanned, result.Outcome);
        Assert.Contains("hidden.md", result.Snapshot.Files.Keys);
    }

    /// <summary>A directory symbolic link is not followed, so a loop cannot trap the walk.</summary>
    [Fact]
    public void Scan_ReparsePoint_IsNotFollowed()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        string target = Path.Combine(dataDir.Path, "target");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "inside.md"), "inside");

        string link = Path.Combine(folder, "link");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links cannot be created here.");
            return;
        }

        FileChangesOptions options = new();

        ScanResult result = FolderScanner.Scan(folder, options);

        Assert.Equal(ScanOutcome.Scanned, result.Outcome);
        Assert.Empty(result.Snapshot.Files);
    }

    /// <summary>A scanned <see cref="FileEntry"/> carries the file's length and UTC last-write time.</summary>
    [Fact]
    public void Scan_EntryValues_AreLengthAndLastWriteTimeUtc()
    {
        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "a.md");
        File.WriteAllText(file, "hello world");
        DateTime modified = new(2026, 9, 22, 14, 2, 11, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, modified);

        ScanResult result = FolderScanner.Scan(folder, new FileChangesOptions());

        FileEntry entry = result.Snapshot.Files["a.md"];
        Assert.Equal(new FileInfo(file).Length, entry.Size);
        Assert.Equal(modified, entry.ModifiedUtc.UtcDateTime);
    }

    /// <summary>On Windows, scanned keys compare case-insensitively, matching the file system.</summary>
    [Fact]
    public void Scan_OnWindows_KeysCompareCaseInsensitively()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only case-insensitivity check.");
            return;
        }

        using TempDataDir dataDir = new();
        string folder = Path.Combine(dataDir.Path, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "A.md"), "a");

        ScanResult result = FolderScanner.Scan(folder, new FileChangesOptions());

        Assert.True(result.Snapshot.Files.ContainsKey("a.md"));
    }
}
