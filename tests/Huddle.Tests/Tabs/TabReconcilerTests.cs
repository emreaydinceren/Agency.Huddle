namespace Agency.Huddle.App.Tabs;

public sealed class TabReconcilerTests
{
    private static Data.Room Room(string id, string name, bool archived = false) =>
        new(id, name, DateTimeOffset.UnixEpoch) { Archived = archived };

    /// <summary>A Room's Tab takes the Room's current name.</summary>
    [Fact]
    public void ReconcileRooms_RenamedRoom_RetitlesItsTab()
    {
        TabService tabs = new();
        tabs.Open("/rooms/ab");

        TabReconciler.ReconcileRooms(tabs, [Room("ab", "Design")]);

        Assert.Equal("Design", Assert.Single(tabs.Tabs).Title);
    }

    /// <summary>An archived Room's Tab closes.</summary>
    [Fact]
    public void ReconcileRooms_ArchivedRoom_ClosesItsTab()
    {
        TabService tabs = new();
        tabs.Open("/rooms/ab");

        TabReconciler.ReconcileRooms(tabs, [Room("ab", "Design", archived: true)]);

        Assert.Empty(tabs.Tabs);
    }

    /// <summary>A Room that no longer exists closes its Tab, and the neighbour becomes active.</summary>
    [Fact]
    public void ReconcileRooms_DeletedRoom_ClosesItsTabAndActivatesTheNeighbour()
    {
        TabService tabs = new();
        Tab keep = tabs.Open("/teammates");
        tabs.Keep(keep.Id);
        tabs.Open("/rooms/gone");

        TabReconciler.ReconcileRooms(tabs, []);

        Assert.Equal(keep.Id, tabs.Active?.Id);
        Assert.Single(tabs.Tabs);
    }

    /// <summary>Tabs for other pages are left alone.</summary>
    [Fact]
    public void ReconcileRooms_OtherPages_AreUntouched()
    {
        TabService tabs = new();
        tabs.Open("/settings");

        TabReconciler.ReconcileRooms(tabs, []);

        Assert.Equal("Settings", Assert.Single(tabs.Tabs).Title);
    }

    private static Teams.TeamSummary Team(string name, params string[] projects) => new(name, projects, [], true);

    /// <summary>A Team or Project that is no longer in the catalog loses its Tab; one that is stays.</summary>
    [Fact]
    public void ReconcileTeams_RemovedTeamAndProject_CloseTheirTabs()
    {
        TabService tabs = new();
        Tab keep = tabs.Open("/teams/Core");
        tabs.Keep(keep.Id);
        Tab project = tabs.Open("/teams/Core/projects/Gone");
        tabs.Keep(project.Id);
        Tab removed = tabs.Open("/teams/Removed");
        tabs.Keep(removed.Id);

        TabReconciler.ReconcileTeams(tabs, new Agency.Huddle.Tests.Teams.FakeTeamCatalog { Teams = [Team("Core", "Alpha")] });

        Assert.Equal(["team:core"], tabs.Tabs.Select(static t => t.Key));
    }

    /// <summary>An empty catalog means "not loaded yet", so nothing is closed.</summary>
    [Fact]
    public void ReconcileTeams_EmptyCatalog_ClosesNothing()
    {
        TabService tabs = new();
        tabs.Open("/teams/Core");

        TabReconciler.ReconcileTeams(tabs, new Agency.Huddle.Tests.Teams.FakeTeamCatalog());

        Assert.Single(tabs.Tabs);
    }
}
