using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins the <c>team_renames</c> journal (Spec 6.3, 5.5): writing, completing, discarding, reading and resolving renames.</summary>
public sealed class TeamRenameStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();
    private readonly ManualTimeProvider clock = new(Start);

    public void Dispose() => this.dir.Dispose();

    private TeamRenameStore Open() => new(this.dir.Options(), this.clock);

    /// <summary>Begin writes a Pending row stamped with the clock's time and returns its id.</summary>
    [Fact]
    public void Begin_WritesPendingRowStampedWithClock()
    {
        TeamRenameStore store = this.Open();

        long id = store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");

        TeamRename row = Assert.Single(store.Pending());
        Assert.Equal(new TeamRename(id, TeamRenameKind.Team, "Marketing", "Marketing", "Growth", TeamRenameState.Pending, Start), row);
        Assert.Equal(1, id);
    }

    /// <summary>Complete turns a Pending row into a Done row and removes it from Pending.</summary>
    [Fact]
    public void Complete_PendingRow_BecomesDone()
    {
        TeamRenameStore store = this.Open();
        long id = store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");

        store.Complete(id);

        Assert.Empty(store.Pending());
        TeamRename row = Assert.Single(store.DoneAfter(0));
        Assert.Equal(new TeamRename(id, TeamRenameKind.Team, "Marketing", "Marketing", "Growth", TeamRenameState.Done, Start), row);
    }

    /// <summary>Discard deletes a Pending row, so the name is no longer reserved.</summary>
    [Fact]
    public void Discard_PendingRow_DeletesIt()
    {
        TeamRenameStore store = this.Open();
        long id = store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");

        store.Discard(id);

        Assert.Empty(store.Pending());
        Assert.False(store.IsReserved("Growth"));
    }

    /// <summary>Discard refuses a Done row and leaves it in place.</summary>
    [Fact]
    public void Discard_DoneRow_ThrowsAndKeepsRow()
    {
        TeamRenameStore store = this.Open();
        long id = store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");
        store.Complete(id);

        _ = Assert.Throws<InvalidOperationException>(() => store.Discard(id));

        Assert.Equal(id, Assert.Single(store.DoneAfter(0)).Id);
    }

    /// <summary>Pending lists only Pending rows, in id order.</summary>
    [Fact]
    public void Pending_ReturnsOnlyPendingRowsInIdOrder()
    {
        TeamRenameStore store = this.Open();
        long first = store.Begin(TeamRenameKind.Team, "A", "A", "B");
        long second = store.Begin(TeamRenameKind.Team, "C", "C", "D");
        long third = store.Begin(TeamRenameKind.Project, "E", "p1", "p2");
        store.Complete(second);

        Assert.Equal([first, third], store.Pending().Select(r => r.Id).ToArray());
    }

    /// <summary>DoneAfter returns Done rows with a greater id, in id order, and skips Pending rows.</summary>
    [Fact]
    public void DoneAfter_ReturnsDoneRowsAboveIdInOrder()
    {
        TeamRenameStore store = this.Open();
        long a = store.Begin(TeamRenameKind.Team, "A", "A", "B");
        long b = store.Begin(TeamRenameKind.Team, "C", "C", "D");
        _ = store.Begin(TeamRenameKind.Team, "E", "E", "F");
        long d = store.Begin(TeamRenameKind.Team, "G", "G", "H");
        store.Complete(d);
        store.Complete(a);
        store.Complete(b);

        Assert.Equal([b, d], store.DoneAfter(a).Select(r => r.Id).ToArray());
        Assert.Equal([a, b, d], store.DoneAfter(0).Select(r => r.Id).ToArray());
        Assert.Empty(store.DoneAfter(d));
    }

    /// <summary>DoneSince returns Done rows renamed at or after the instant, in id order.</summary>
    [Fact]
    public void DoneSince_ReturnsDoneRowsFromInstantInclusive()
    {
        TeamRenameStore store = this.Open();
        long a = store.Begin(TeamRenameKind.Team, "A", "A", "B");
        this.clock.Advance(TimeSpan.FromDays(1));
        long b = store.Begin(TeamRenameKind.Team, "C", "C", "D");
        this.clock.Advance(TimeSpan.FromDays(1));
        long c = store.Begin(TeamRenameKind.Team, "E", "E", "F");
        _ = store.Begin(TeamRenameKind.Team, "G", "G", "H");
        store.Complete(a);
        store.Complete(b);
        store.Complete(c);

        Assert.Equal([b, c], store.DoneSince(Start.AddDays(1)).Select(r => r.Id).ToArray());
        Assert.Equal([a, b, c], store.DoneSince(Start).Select(r => r.Id).ToArray());
        Assert.Empty(store.DoneSince(Start.AddDays(3)));
    }

    /// <summary>LatestDoneBefore returns the id of the newest Done row strictly before the instant, or 0 when none.</summary>
    [Fact]
    public void LatestDoneBefore_ReturnsNewestEarlierDoneIdOrZero()
    {
        TeamRenameStore store = this.Open();
        long a = store.Begin(TeamRenameKind.Team, "A", "A", "B");
        this.clock.Advance(TimeSpan.FromDays(1));
        long b = store.Begin(TeamRenameKind.Team, "C", "C", "D");
        this.clock.Advance(TimeSpan.FromDays(1));
        _ = store.Begin(TeamRenameKind.Team, "E", "E", "F");
        store.Complete(a);
        store.Complete(b);

        Assert.Equal(0, store.LatestDoneBefore(Start));
        Assert.Equal(a, store.LatestDoneBefore(Start.AddDays(1)));
        Assert.Equal(b, store.LatestDoneBefore(Start.AddDays(5)));
    }

    /// <summary>LatestDoneBefore is 0 on an empty journal.</summary>
    [Fact]
    public void LatestDoneBefore_EmptyJournal_ReturnsZero()
    {
        Assert.Equal(0, this.Open().LatestDoneBefore(Start.AddYears(1)));
    }

    /// <summary>A name that was never renamed resolves to itself.</summary>
    [Fact]
    public void Resolve_UnknownName_ReturnsInput()
    {
        TeamRenameStore store = this.Open();

        Assert.Equal("Sales", store.Resolve("Sales"));
        Assert.Equal("Alpha", store.ResolveProject("Sales", "Alpha"));
    }

    /// <summary>One Done rename maps the old name to the new one, ignoring case.</summary>
    [Fact]
    public void Resolve_OneRename_ReturnsNewName()
    {
        TeamRenameStore store = this.Open();
        store.Complete(store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        Assert.Equal("Growth", store.Resolve("Marketing"));
        Assert.Equal("Growth", store.Resolve("MARKETING"));
        Assert.Equal("Growth", store.Resolve("Growth"));
    }

    /// <summary>A chain A to B to C resolves to C from A and from B.</summary>
    [Fact]
    public void Resolve_Chain_FollowsToLatestName()
    {
        TeamRenameStore store = this.Open();
        store.Complete(store.Begin(TeamRenameKind.Team, "A", "A", "B"));
        store.Complete(store.Begin(TeamRenameKind.Team, "B", "B", "C"));

        Assert.Equal("C", store.Resolve("A"));
        Assert.Equal("C", store.Resolve("B"));
        Assert.Equal("C", store.Resolve("C"));
    }

    /// <summary>A case-only rename resolves either spelling to the new one without looping.</summary>
    [Fact]
    public void Resolve_CaseOnlyRename_ReturnsNewSpelling()
    {
        TeamRenameStore store = this.Open();
        store.Complete(store.Begin(TeamRenameKind.Team, "Growth", "Growth", "growth"));

        Assert.Equal("growth", store.Resolve("GROWTH"));
        Assert.Equal("growth", store.Resolve("Growth"));
    }

    /// <summary>A Pending row is not followed by Resolve.</summary>
    [Fact]
    public void Resolve_PendingRow_IsNotFollowed()
    {
        TeamRenameStore store = this.Open();
        _ = store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");

        Assert.Equal("Marketing", store.Resolve("Marketing"));
    }

    /// <summary>A Project row records the Team name of its time, and a later Team rename still resolves it.</summary>
    [Fact]
    public void ResolveProject_AfterLaterTeamRename_ReturnsNewProjectUnderNewTeam()
    {
        TeamRenameStore store = this.Open();
        store.Complete(store.Begin(TeamRenameKind.Project, "Marketing", "Launch", "Rollout"));
        store.Complete(store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        Assert.Equal("Rollout", store.ResolveProject("Marketing", "Launch"));
        Assert.Equal("Rollout", store.ResolveProject("Growth", "LAUNCH"));
        Assert.Equal("Launch", store.ResolveProject("Other", "Launch"));
    }

    /// <summary>A Project row never changes the Team name Resolve returns.</summary>
    [Fact]
    public void Resolve_ProjectRow_DoesNotAffectTeamName()
    {
        TeamRenameStore store = this.Open();
        store.Complete(store.Begin(TeamRenameKind.Project, "Marketing", "Marketing", "Rollout"));

        Assert.Equal("Marketing", store.Resolve("Marketing"));
    }

    /// <summary>IsReserved is true for the old and the new name of a Team row, Pending or Done, in either case.</summary>
    [Fact]
    public void IsReserved_OldAndNewNames_IgnoringCase()
    {
        TeamRenameStore store = this.Open();
        _ = store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");
        store.Complete(store.Begin(TeamRenameKind.Team, "Sales", "Sales", "Revenue"));

        Assert.True(store.IsReserved("marketing"));
        Assert.True(store.IsReserved("GROWTH"));
        Assert.True(store.IsReserved("sales"));
        Assert.True(store.IsReserved("REVENUE"));
        Assert.False(store.IsReserved("Other"));
    }

    /// <summary>A Project row does not reserve its names as Team names.</summary>
    [Fact]
    public void IsReserved_ProjectRow_IsNotReserved()
    {
        TeamRenameStore store = this.Open();
        store.Complete(store.Begin(TeamRenameKind.Project, "Marketing", "Launch", "Rollout"));

        Assert.False(store.IsReserved("Launch"));
        Assert.False(store.IsReserved("Rollout"));
    }

    /// <summary>IsPending is true only while the Team row is Pending, for either name and any case.</summary>
    [Fact]
    public void IsPending_TrueOnlyWhilePending()
    {
        TeamRenameStore store = this.Open();
        long id = store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");

        Assert.True(store.IsPending("marketing"));
        Assert.True(store.IsPending("GROWTH"));
        Assert.False(store.IsPending("Other"));

        store.Complete(id);

        Assert.False(store.IsPending("Marketing"));
        Assert.False(store.IsPending("Growth"));
    }

    /// <summary>A second store over the same data directory sees the rows of the first.</summary>
    [Fact]
    public void Reopen_SameDataDir_SeesRows()
    {
        TeamRenameStore first = this.Open();
        long done = first.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");
        first.Complete(done);
        long pending = first.Begin(TeamRenameKind.Project, "Growth", "Launch", "Rollout");

        TeamRenameStore second = this.Open();

        Assert.Equal("Growth", second.Resolve("Marketing"));
        Assert.Equal(pending, Assert.Single(second.Pending()).Id);
        Assert.Equal(done, Assert.Single(second.DoneAfter(0)).Id);
    }
}
