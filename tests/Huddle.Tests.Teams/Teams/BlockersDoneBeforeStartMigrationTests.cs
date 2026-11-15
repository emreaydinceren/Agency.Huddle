using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Teams;

/// <summary>An installed development Team gets <c>blockers-done-before-start</c> once, and a later turn-off sticks (#538).</summary>
public sealed class BlockersDoneBeforeStartMigrationTests
{
    private static FakeTeamFolders Folders(string teamType, string gates)
    {
        Assert.True(TeamFile.TryParse($"---\nteamType: '{teamType}@1.0.0'\ngates: [{gates}]\n---\n", out TeamFile? file));
        return new FakeTeamFolders { ReadResult = file, UpdateResult = true };
    }

    private static BlockersDoneBeforeStartMigration Build(TempDataDir dir, ITeamFolders folders) =>
        new(Options.Create(new TeamOptions { DataDir = dir.Path }), new FakeTeamCatalog { Teams = [new TeamSummary("Platform", [], [], true)] }, folders, NullLogger<BlockersDoneBeforeStartMigration>.Instance);

    /// <summary>A development Team without the gate gets it on the first start and keeps its other gates.</summary>
    [Fact]
    public async Task StartAsync_DevelopmentTeamWithoutGate_AddsItAndKeepsTheOthers()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = Folders("development", "'checks-before-merge'");

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([GateId.ChecksBeforeMerge, GateId.BlockersDoneBeforeStart], folders.ReadResult?.Process.Gates);
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

    /// <summary>A Team that already has the gate is not written again.</summary>
    [Fact]
    public async Task StartAsync_TeamAlreadyHasGate_IsUnchanged()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = Folders("development", "'blockers-done-before-start'");

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([GateId.BlockersDoneBeforeStart], folders.ReadResult?.Process.Gates);
    }

    /// <summary>The marker is its own file, so the accept-before-merge migration's marker does not stop this one.</summary>
    [Fact]
    public async Task StartAsync_WritesItsOwnMarker()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = Folders("development", "'checks-before-merge'");

        await Build(dir, folders).StartAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(dir.Path, "blockers-done-before-start.migrated")));
        Assert.False(File.Exists(Path.Combine(dir.Path, "accept-before-merge.migrated")));
    }
}
