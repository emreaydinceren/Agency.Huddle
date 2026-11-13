using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// <see cref="WorkspaceRenameRewrite"/> (Team Rename Spec 4, Workspace): the NEW Team's <c>_team.md</c> <c>workspace:</c> follows the moved
/// folder when it lay inside the old one, a workspace elsewhere is not touched, and a second run changes nothing.
/// </summary>
public sealed class WorkspaceRenameRewriteTests : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    private string OldFolder => Path.Combine(this.dir.Path, "Teams", "Growth");

    private string NewFolder => Path.Combine(this.dir.Path, "Teams", "Revenue");

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    private static TeamRename Rename() => new(1, TeamRenameKind.Team, "Growth", "Growth", "Revenue", TeamRenameState.Pending, Stamp);

    private static FakeTeamFolders FoldersWith(string? workspace) => new()
    {
        UpdateResult = true,
        ReadResult = new TeamFile(new VersionedRef("web", "1.2.0"), null, workspace, null, string.Empty),
    };

    /// <summary>A workspace inside the old Team folder is pointed at the same place under the new folder, and the write goes to the new Team.</summary>
    [Fact]
    public async Task RunAsync_WorkspaceInsideOldFolder_PointsUnderNewFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTeamFolders folders = FoldersWith(Path.Combine(this.OldFolder, "site", "src"));
        WorkspaceRenameRewrite rewrite = new(folders, this.dir.Options());

        await rewrite.RunAsync(Rename(), ct);

        Assert.Equal(Path.Combine(this.NewFolder, "site", "src"), folders.ReadResult?.Workspace);
        Assert.Contains("UpdateTeamFile:Revenue", folders.Calls);
        Assert.Empty(rewrite.TeamsTouched(Rename()));
    }

    /// <summary>A workspace outside the old folder, even one that shares its leading characters, is left as it is.</summary>
    [Fact]
    public async Task RunAsync_WorkspaceOutsideOldFolder_Untouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string outside = Path.Combine(this.dir.Path, "Teams", "Growth2", "site");
        FakeTeamFolders folders = FoldersWith(outside);
        WorkspaceRenameRewrite rewrite = new(folders, this.dir.Options());

        await rewrite.RunAsync(Rename(), ct);

        Assert.Equal(outside, folders.ReadResult?.Workspace);
    }

    /// <summary>A second run finds the workspace already under the new folder and leaves it there.</summary>
    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTeamFolders folders = FoldersWith(Path.Combine(this.OldFolder, "site"));
        WorkspaceRenameRewrite rewrite = new(folders, this.dir.Options());

        await rewrite.RunAsync(Rename(), ct);
        await rewrite.RunAsync(Rename(), ct);

        Assert.Equal(Path.Combine(this.NewFolder, "site"), folders.ReadResult?.Workspace);
    }

    /// <summary>A Team with no workspace is left without one.</summary>
    [Fact]
    public async Task RunAsync_NoWorkspace_StaysNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTeamFolders folders = FoldersWith(null);
        WorkspaceRenameRewrite rewrite = new(folders, this.dir.Options());

        await rewrite.RunAsync(Rename(), ct);

        Assert.Null(folders.ReadResult?.Workspace);
    }
}
