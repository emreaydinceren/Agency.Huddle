using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Teams;

/// <summary>An installed development Team gets <c>accept-before-merge</c> once, a later turn-off sticks, and a failure neither stops the host nor hides a Team (#401).</summary>
public sealed class AcceptBeforeMergeMigrationTests
{
    private static FakeTeamFolders Folders(string teamType, string gates)
    {
        Assert.True(TeamFile.TryParse($"---\nteamType: '{teamType}@1.0.0'\ngates: [{gates}]\n---\n", out TeamFile? file));
        return new FakeTeamFolders { ReadResult = file, UpdateResult = true };
    }

    private static AcceptBeforeMergeMigration Build(TempDataDir dir, ITeamFolders folders) =>
        new(Options.Create(new TeamOptions { DataDir = dir.Path }), new FakeTeamCatalog { Teams = [new TeamSummary("Platform", [], [], true)] }, folders, NullLogger<AcceptBeforeMergeMigration>.Instance);

    private static string Marker(TempDataDir dir) => Path.Combine(dir.Path, "accept-before-merge.migrated");

    /// <summary>A development Team without the gate gets it on the first start and keeps its other gates.</summary>
    [Fact]
    public async Task StartAsync_DevelopmentTeamWithoutGate_AddsIt()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = Folders("development", "'checks-before-merge'");

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([GateId.ChecksBeforeMerge, GateId.AcceptBeforeMerge], folders.ReadResult?.Process.Gates);
    }

    /// <summary>The second start does nothing, so a Human who turned the gate off keeps it off.</summary>
    [Fact]
    public async Task StartAsync_SecondStart_DoesNotAddItAgain()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = Folders("development", "'checks-before-merge'");
        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);
        Assert.True(folders.UpdateTeamFile("Platform", file => file.WithGates([GateId.ChecksBeforeMerge])));

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([GateId.ChecksBeforeMerge], folders.ReadResult?.Process.Gates);
    }

    /// <summary>A Team of another type is left alone.</summary>
    [Fact]
    public async Task StartAsync_OtherTeamType_IsUntouched()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = Folders("web", "'checks-before-merge'");

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([GateId.ChecksBeforeMerge], folders.ReadResult?.Process.Gates);
    }

    /// <summary>A Team of another type is not rewritten at all: its file on disk keeps its bytes (line endings, <c>process:</c> block and all).</summary>
    [Fact]
    public async Task StartAsync_OtherTeamType_FileOnDiskIsNotWritten()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "_team.md");
        const string text = "---\nteamType: 'web@1.2.0'\ngates: ['checks-before-merge']\nprocess:\n  wip: 3\n---\n";
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
        Assert.True(TeamFile.TryParse(text, out TeamFile? parsed));
        DiskTeamFolders folders = new(dir.Path) { File = parsed, TeamFilePath = path };

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(text, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>A Team whose file cannot be read does not stop the host and leaves no finished marker, so the next start tries it again and adds the gate.</summary>
    [Fact]
    public async Task StartAsync_UnreadableTeamFile_RetriesAtNextStart()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = new(dir.Path) { File = null };

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("#complete", await File.ReadAllTextAsync(Marker(dir), TestContext.Current.CancellationToken), StringComparison.Ordinal);

        Assert.True(TeamFile.TryParse("---\nteamType: 'development@1.0.0'\ngates: ['checks-before-merge']\n---\n", out TeamFile? file));
        folders.File = file;
        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([GateId.ChecksBeforeMerge, GateId.AcceptBeforeMerge], folders.File?.Process.Gates);
    }

    /// <summary>A file that cannot be written (a locked or missing target) is logged and skipped: StartAsync does not throw and the Team is retried.</summary>
    [Fact]
    public async Task StartAsync_WriteFails_DoesNotThrowAndRetries()
    {
        using TempDataDir dir = new();
        Assert.True(TeamFile.TryParse("---\nteamType: 'development@1.0.0'\ngates: ['checks-before-merge']\n---\n", out TeamFile? file));
        DiskTeamFolders folders = new(dir.Path) { File = file, TeamFilePath = Path.Combine(dir.Path, "missing", "_team.md") };

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("#complete", await File.ReadAllTextAsync(Marker(dir), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>A Team already handled by an earlier, unfinished pass is not given the gate again, so a Human's turn-off survives the retry.</summary>
    [Fact]
    public async Task StartAsync_TeamHandledInEarlierPass_KeepsTheHumansChoice()
    {
        using TempDataDir dir = new();
        await File.WriteAllTextAsync(Marker(dir), "Platform", TestContext.Current.CancellationToken);
        FakeTeamFolders folders = Folders("development", "'checks-before-merge'");

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([GateId.ChecksBeforeMerge], folders.ReadResult?.Process.Gates);
    }
}
