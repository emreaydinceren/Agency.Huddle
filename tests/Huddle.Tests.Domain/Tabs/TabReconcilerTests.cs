namespace Agency.Huddle.App.Tabs;

public sealed class TabReconcilerTests
{
    private static Data.Room Room(string id, string name, bool archived = false) =>
        new(id, name, DateTimeOffset.UnixEpoch) { Archived = archived };

    /// <summary>A Task View's Tab takes the View's display name, so the Tab shows no slug, and the breadcrumb holds only the ancestor (the View's name is the page's h1).</summary>
    [Fact]
    public void ReconcileViews_ViewTab_TakesTheDisplayNameNotTheSlug()
    {
        TabService tabs = new();
        Tab tab = tabs.Open("/tasks/my-tasks");
        Assert.Equal("my-tasks", tab.Title);

        TabReconciler.ReconcileViews(tabs, [new Tasks.Views.TaskView { Id = "my-tasks", Name = "My Tasks", Kind = Tasks.Views.ViewKind.List }]);

        Tab retitled = Assert.Single(tabs.Tabs);
        Assert.Equal("My Tasks", retitled.Title);
        Assert.Equal(["Tasks"], tabs.Trail(retitled).Select(static crumb => crumb.Text));
    }

    /// <summary>A Tab whose View is unknown, and a Tab for another page, keep their titles.</summary>
    [Fact]
    public void ReconcileViews_UnknownViewAndOtherPages_AreUntouched()
    {
        TabService tabs = new();
        Tab gone = tabs.Open("/tasks/gone");
        tabs.Keep(gone.Id);
        tabs.Open("/agents");

        TabReconciler.ReconcileViews(tabs, [new Tasks.Views.TaskView { Id = "my-tasks", Name = "My Tasks", Kind = Tasks.Views.ViewKind.List }]);

        Assert.Equal(["gone", "Agents"], tabs.Tabs.Select(static tab => tab.Title));
    }

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
        Tab keep = tabs.Open("/agents");
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
