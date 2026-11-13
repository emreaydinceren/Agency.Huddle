using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>Pins <see cref="WatchedFolderResolver.TryResolve"/> against the rename journal (Team Rename R9): an entry under a Team's old name resolves to the renamed Team's folder.</summary>
public sealed class WatchedFolderResolverRenameTests
{
    /// <summary>A Watched Folder entry <c>Teams/Marketing/research</c> resolves to Growth's folder with either separator.</summary>
    /// <param name="entry">The entry text.</param>
    [Theory]
    [InlineData("Teams/Marketing/research")]
    [InlineData("Teams\\Marketing\\research")]
    public void TryResolve_OldTeamName_ResolvesToTheRenamedTeamsFolder(string entry)
    {
        using TempDataDir dataDir = new();
        TeamRenameStore renames = new(dataDir.Options());
        renames.Complete(renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()), renames);

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.Equal(Path.Combine(dataDir.Path, "Teams", "Growth", "research"), folder.FullPath);
    }

    /// <summary>A name that was never journalled resolves where it says.</summary>
    [Fact]
    public void TryResolve_NeverJournalledTeam_IsUnchanged()
    {
        using TempDataDir dataDir = new();
        TeamRenameStore renames = new(dataDir.Options());
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()), renames);

        bool resolved = resolver.TryResolve("Teams/Marketing/research", [], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.Equal(Path.Combine(dataDir.Path, "Teams", "Marketing", "research"), folder.FullPath);
    }
}
