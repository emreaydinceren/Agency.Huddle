using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins Spec §6.1 and §6.8 for the Team <c>raw/</c> folder: a file in it raises no File Change while
/// a file beside it (in <c>outputs/</c>, or at the Team root) is reported, and the Raw root is refused
/// with the existing reason text. A guard test: the behaviour is already in place.
/// </summary>
public sealed class RawNotWatchedTests
{
    /// <summary>Watching a Team folder scans its outputs and neighbour files, but never the Team's raw folder.</summary>
    [Fact]
    public void Scan_TeamHome_ReportsOutputsAndNeighbourFile_NotRawFile()
    {
        using TempDataDir dataDir = new();
        string teamsRoot = Path.GetFullPath(Path.Combine(dataDir.Path, "Teams"));
        string teamHome = Path.Combine(teamsRoot, "Home");
        Directory.CreateDirectory(Path.Combine(teamHome, "outputs"));
        Directory.CreateDirectory(Path.Combine(teamHome, "raw", "r1"));
        File.WriteAllText(Path.Combine(teamHome, "outputs", "a.md"), "output");
        File.WriteAllText(Path.Combine(teamHome, "raw", "r1", "a.pdf"), "raw");
        File.WriteAllText(Path.Combine(teamHome, "notes.md"), "notes");

        ScanResult result = FolderScanner.Scan(teamHome, new FileChangesOptions(), pruneUnderscore: false, teamsRoot);

        Assert.Equal(ScanOutcome.Scanned, result.Outcome);
        Assert.Contains(Path.Combine("outputs", "a.md"), result.Snapshot.Files.Keys);
        Assert.Contains("notes.md", result.Snapshot.Files.Keys);
        Assert.DoesNotContain(Path.Combine("raw", "r1", "a.pdf"), result.Snapshot.Files.Keys);
    }

    /// <summary>Watching the Raw root, or a Team's raw folder, is refused with the Uploads reason text the chat already shows.</summary>
    [Theory]
    [InlineData("./Raw")]
    [InlineData("./Teams/Home/raw")]
    public void TryResolve_RawRootOrTeamRaw_IsRefused(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("Uploads are announced in the chat, not watched.", reason);
    }
}
