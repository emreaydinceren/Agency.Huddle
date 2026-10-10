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
        tabs.Pin(keep.Id);
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
}
