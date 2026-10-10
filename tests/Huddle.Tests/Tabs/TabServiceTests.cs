namespace Agency.Huddle.App.Tabs;

public sealed class TabServiceTests
{
    /// <summary>The first menu click opens a preview Tab and makes it active.</summary>
    [Fact]
    public void Open_FirstUrl_IsAnActivePreview()
    {
        TabService service = new();

        Tab tab = service.Open("/teammates");

        Assert.True(tab.IsPreview);
        Assert.Equal(tab, service.Active);
        Assert.Single(service.Tabs);
    }

    /// <summary>A second preview replaces the first in place instead of adding a Tab.</summary>
    [Fact]
    public void Open_WhilePreviewExists_ReplacesItInPlace()
    {
        TabService service = new();
        service.Open("/teammates");

        service.Open("/settings");

        Tab only = Assert.Single(service.Tabs);
        Assert.Equal("settings", only.Key);
        Assert.True(only.IsPreview);
    }

    /// <summary>A permanent Tab is never replaced; the next preview goes right of the active Tab.</summary>
    [Fact]
    public void Open_AfterPin_AddsPreviewRightOfActive()
    {
        TabService service = new();
        Tab first = service.Open("/teammates");
        service.Pin(first.Id);
        Tab second = service.Open("/settings");
        service.Pin(second.Id);
        service.Activate(first.Id);

        Tab third = service.Open("/library");

        Assert.Equal(["teammates", "library", "settings"], service.Tabs.Select(static t => t.Key));
        Assert.Equal(third, service.Active);
    }

    /// <summary>Opening a key that is open activates it, keeps it permanent and updates its URL.</summary>
    [Fact]
    public void Open_SameKey_ReusesPermanentTabAndUpdatesUrl()
    {
        TabService service = new();
        Tab library = service.Open("/library?scopeRoot=a&scopePath=");
        service.Pin(library.Id);
        service.Open("/teammates");

        Tab again = service.Open("/library?scopeRoot=b&scopePath=");

        Assert.Equal(library.Id, again.Id);
        Assert.False(again.IsPreview);
        Assert.Equal("/library?scopeRoot=b&scopePath=", again.Url);
        Assert.Equal(2, service.Tabs.Count);
        Assert.Equal(again, service.Active);
    }

    /// <summary>Re-opening the active Tab's own URL raises no change, so a location sync cannot loop.</summary>
    [Fact]
    public void Open_UnchangedActiveTab_DoesNotRaiseChanged()
    {
        TabService service = new();
        service.Open("/teammates");
        int raised = 0;
        service.Changed += () => raised++;

        service.Open("/teammates");

        Assert.Equal(0, raised);
    }

    /// <summary>A supplied title wins; a later open without one keeps it.</summary>
    [Fact]
    public void Open_WithTitle_KeepsItOnLaterOpenWithout()
    {
        TabService service = new();
        service.Open("/rooms/ab", "Design");

        Tab again = service.Open("/rooms/ab");

        Assert.Equal("Design", again.Title);
    }

    /// <summary>A Team's sub-tab is the same Tab as the Team.</summary>
    [Fact]
    public void Open_TeamSubTab_IsInTabNavigation()
    {
        TabService service = new();
        service.Open("/teams/Core");

        service.Open("/teams/core/tasks");

        Tab only = Assert.Single(service.Tabs);
        Assert.Equal("/teams/core/tasks", only.Url);
    }

    /// <summary>Closing the active Tab activates its right neighbour.</summary>
    [Fact]
    public void Close_Active_ActivatesRightNeighbour()
    {
        TabService service = new();
        Tab a = service.Open("/teammates");
        service.Pin(a.Id);
        Tab b = service.Open("/settings");
        service.Pin(b.Id);
        Tab c = service.Open("/library");
        service.Pin(c.Id);
        service.Activate(b.Id);

        service.Close(b.Id);

        Assert.Equal(c.Id, service.Active?.Id);
    }

    /// <summary>Closing the rightmost active Tab activates the left one; closing the last leaves none.</summary>
    [Fact]
    public void Close_LastTabs_FallsLeftThenNone()
    {
        TabService service = new();
        Tab a = service.Open("/teammates");
        service.Pin(a.Id);
        Tab b = service.Open("/settings");

        service.Close(b.Id);
        Assert.Equal(a.Id, service.Active?.Id);

        service.Close(a.Id);
        Assert.Null(service.Active);
        Assert.Empty(service.Tabs);
    }

    /// <summary>Closing a background Tab leaves the active Tab alone.</summary>
    [Fact]
    public void Close_Background_KeepsActive()
    {
        TabService service = new();
        Tab a = service.Open("/teammates");
        service.Pin(a.Id);
        Tab b = service.Open("/settings");

        service.Close(a.Id);

        Assert.Equal(b, service.Active);
    }

    /// <summary>A Room's Tab closes by key, as when it is archived.</summary>
    [Fact]
    public void CloseByKey_RemovesThatTab()
    {
        TabService service = new();
        service.Open("/rooms/ab");

        service.CloseByKey("room:ab");

        Assert.Empty(service.Tabs);
    }

    /// <summary>A rename retitles the Tab by key.</summary>
    [Fact]
    public void SetTitle_ChangesTitle()
    {
        TabService service = new();
        service.Open("/rooms/ab");

        service.SetTitle("room:ab", "Renamed");

        Assert.Equal("Renamed", Assert.Single(service.Tabs).Title);
    }

    /// <summary>Pinning by key makes the Tab permanent, so a double-click on a menu item keeps it.</summary>
    [Fact]
    public void PinByKey_MakesPermanent()
    {
        TabService service = new();
        service.Open("/settings");

        service.PinByKey("settings");

        Assert.False(Assert.Single(service.Tabs).IsPreview);
    }

    /// <summary>Exporting and restoring gives the same Tabs, order, active Tab and preview flag.</summary>
    [Fact]
    public void ExportThenRestore_RoundTrips()
    {
        TabService source = new();
        Tab a = source.Open("/rooms/ab", "Design");
        source.Pin(a.Id);
        source.Open("/settings");
        TabSnapshot? parsed = TabSnapshot.TryParse(source.Export().ToJson());

        TabService target = new();
        bool accepted = target.Restore(parsed);

        Assert.True(accepted);
        Assert.Equal(source.Tabs, target.Tabs);
        Assert.Equal(source.Active, target.Active);
    }

    /// <summary>Ids minted after a restore do not collide with restored ones.</summary>
    [Fact]
    public void Restore_ThenOpen_MintsFreshId()
    {
        TabService source = new();
        Tab a = source.Open("/teammates");
        source.Pin(a.Id);
        TabService target = new();
        target.Restore(source.Export());

        Tab fresh = target.Open("/settings");

        Assert.NotEqual(a.Id, fresh.Id);
    }

    /// <summary>Stored text that is not a snapshot is ignored.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"version\":2,\"root\":{\"kind\":\"group\",\"tabs\":[]}}")]
    [InlineData("{\"version\":1,\"root\":{\"kind\":\"split\",\"tabs\":[]}}")]
    public void Restore_UnusableStorage_IsRefusedAndKeepsTabs(string? json)
    {
        TabService service = new();
        service.Open("/teammates");

        bool accepted = service.Restore(TabSnapshot.TryParse(json));

        Assert.False(accepted);
        Assert.Single(service.Tabs);
    }

    /// <summary>An off-site or malformed URL in storage refuses the whole snapshot.</summary>
    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("https://evil.example/x")]
    [InlineData("\\\\evil\\x")]
    public void Restore_UnsafeUrl_IsRefused(string url)
    {
        TabSnapshot snapshot = new(
            TabSnapshot.CurrentVersion,
            new TabGroupSnapshot(TabGroupSnapshot.GroupKind, "t1", [new TabSnapshotEntry("t1", url, "X", "page", false)]));
        TabService service = new();

        bool accepted = service.Restore(snapshot);

        Assert.False(accepted);
        Assert.Empty(service.Tabs);
    }

    /// <summary>Two stored previews cannot both be restored.</summary>
    [Fact]
    public void Restore_TwoPreviews_IsRefused()
    {
        TabSnapshot snapshot = new(
            TabSnapshot.CurrentVersion,
            new TabGroupSnapshot(
                TabGroupSnapshot.GroupKind,
                "t1",
                [new TabSnapshotEntry("t1", "/settings", "Settings", "settings", true), new TabSnapshotEntry("t2", "/teammates", "Teammates", "teammates", true)]));
        TabService service = new();

        Assert.False(service.Restore(snapshot));
    }

    /// <summary>Moving by one wraps at both ends, and with fewer than two Tabs nothing moves.</summary>
    [Fact]
    public void ActivateRelative_WrapsAtBothEnds()
    {
        TabService service = new();
        Tab a = service.Open("/teammates");
        service.Pin(a.Id);
        Assert.Null(service.ActivateRelative(1));
        Tab b = service.Open("/settings");
        service.Pin(b.Id);
        Tab c = service.Open("/library");

        Assert.Equal(a.Id, service.ActivateRelative(1)?.Id);
        Assert.Equal(c.Id, service.ActivateRelative(-1)?.Id);
        Assert.Equal(c.Id, service.Active?.Id);
    }
}
