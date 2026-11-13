using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins <see cref="ITeamRenames.IsProjectReserved"/> and <see cref="ITeamRenames.IsProjectPending"/> (Team Rename Spec 5.5, Project rule).</summary>
public sealed class TeamRenameStoreProjectReservedTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly TeamRenameStore store;

    /// <summary>Opens a journal over a temporary data folder.</summary>
    public TeamRenameStoreProjectReservedTests() => this.store = new TeamRenameStore(this.dir.Options(), new ManualTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero)));

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>A Pending Project row reserves and is pending for both its old and its new name, ignoring case.</summary>
    [Fact]
    public void Pending_ReservesAndIsPending_BothNamesIgnoringCase()
    {
        _ = this.store.Begin(TeamRenameKind.Project, "Growth", "Launch", "Launch-2027");

        Assert.True(this.store.IsProjectReserved("growth", "LAUNCH"));
        Assert.True(this.store.IsProjectReserved("Growth", "launch-2027"));
        Assert.True(this.store.IsProjectPending("growth", "launch"));
        Assert.True(this.store.IsProjectPending("Growth", "Launch-2027"));
    }

    /// <summary>A Done Project row still reserves its names but is no longer pending.</summary>
    [Fact]
    public void Done_ReservesButIsNotPending()
    {
        this.store.Complete(this.store.Begin(TeamRenameKind.Project, "Growth", "Launch", "Launch-2027"));

        Assert.True(this.store.IsProjectReserved("Growth", "Launch"));
        Assert.True(this.store.IsProjectReserved("Growth", "Launch-2027"));
        Assert.False(this.store.IsProjectPending("Growth", "Launch"));
    }

    /// <summary>An unrelated Project, the same name in another Team, and a Team row are not Project-reserved.</summary>
    [Fact]
    public void Unrelated_IsNeitherReservedNorPending()
    {
        _ = this.store.Begin(TeamRenameKind.Project, "Growth", "Launch", "Launch-2027");
        _ = this.store.Begin(TeamRenameKind.Team, "Sales", "Sales", "Revenue");

        Assert.False(this.store.IsProjectReserved("Growth", "Retro"));
        Assert.False(this.store.IsProjectReserved("Ops", "Launch"));
        Assert.False(this.store.IsProjectReserved("Sales", "Sales"));
        Assert.False(this.store.IsProjectPending("Ops", "Launch"));
    }

    /// <summary>A Project row journalled under the Team's old name still reserves after a later Team rename, asked by either name.</summary>
    [Fact]
    public void AfterLaterTeamRename_StillReservesByTodaysTeamName()
    {
        this.store.Complete(this.store.Begin(TeamRenameKind.Project, "Marketing", "Launch", "Launch-2027"));
        this.store.Complete(this.store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        Assert.True(this.store.IsProjectReserved("Growth", "Launch"));
        Assert.True(this.store.IsProjectReserved("Marketing", "Launch"));
        Assert.False(this.store.IsProjectReserved("Growth", "Retro"));
    }

    /// <summary>A Project row journalled under the old Team name stays pending when asked by today's Team name.</summary>
    [Fact]
    public void PendingProjectRow_AfterLaterTeamRename_IsPendingByTodaysTeamName()
    {
        _ = this.store.Begin(TeamRenameKind.Project, "Marketing", "Launch", "Launch-2027");
        this.store.Complete(this.store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        Assert.True(this.store.IsProjectPending("Growth", "Launch"));
    }
}
