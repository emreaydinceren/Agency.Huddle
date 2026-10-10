namespace Agency.Huddle.App.Tabs;

/// <summary>Pins pinned Tabs, the move-left/right rule, focus between groups, and the version 2 snapshot of <see cref="TabService"/>.</summary>
public sealed class TabGroupTests
{
    private static (TabService Service, Tab A, Tab B, Tab C) ThreePermanent()
    {
        TabService service = new();
        Tab a = service.Open("/teammates");
        service.Keep(a.Id);
        Tab b = service.Open("/settings");
        service.Keep(b.Id);
        Tab c = service.Open("/library");
        service.Keep(c.Id);
        return (service, a, b, c);
    }

    private static string[] Keys(TabGroup group) => [.. group.Tabs.Select(static tab => tab.Key)];

    /// <summary>A new service has one empty group that has focus.</summary>
    [Fact]
    public void New_HasOneEmptyFocusedGroup()
    {
        TabService service = new();

        TabGroup group = Assert.Single(service.Groups);
        Assert.Empty(group.Tabs);
        Assert.Equal(group.Id, service.FocusedGroupId);
        Assert.Null(service.Active);
    }

    /// <summary>The number of groups is capped in source, and the cap is two: one split.</summary>
    [Fact]
    public void MaxGroups_IsTwo()
    {
        Assert.Equal(2, TabService.MaxGroups);
    }

    /// <summary>Pinning makes a Tab permanent and moves it in front of the unpinned ones.</summary>
    [Fact]
    public void Pin_MovesTheTabToTheFrontAndKeepsIt()
    {
        (TabService service, _, _, Tab c) = ThreePermanent();

        service.Pin(c.Id);

        Assert.Equal(["library", "teammates", "settings"], Keys(service.Groups[0]));
        Tab pinned = service.Groups[0].Tabs[0];
        Assert.True(pinned.IsPinned);
        Assert.False(pinned.IsPreview);
    }

    /// <summary>Pinning a preview ends its preview status, so the next preview does not replace it.</summary>
    [Fact]
    public void Pin_APreview_MakesItPermanent()
    {
        TabService service = new();
        Tab preview = service.Open("/teammates");

        service.Pin(preview.Id);
        service.Open("/settings");

        Assert.Equal(2, service.Tabs.Count);
        Assert.Contains(service.Tabs, static tab => tab.IsPinned && tab.Key == "teammates");
    }

    /// <summary>Pinned Tabs keep the order they were pinned in, ahead of every unpinned Tab.</summary>
    [Fact]
    public void Pin_SeveralTabs_KeepsThePinOrder()
    {
        (TabService service, Tab a, _, Tab c) = ThreePermanent();

        service.Pin(c.Id);
        service.Pin(a.Id);

        Assert.Equal(["library", "teammates", "settings"], Keys(service.Groups[0]));
    }

    /// <summary>Unpinning keeps the Tab permanent and puts it just after the pinned Tabs.</summary>
    [Fact]
    public void Unpin_PlacesTheTabAfterThePinnedOnesAndKeepsIt()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.Pin(a.Id);
        service.Pin(b.Id);

        service.Unpin(a.Id);

        Assert.Equal(["settings", "teammates", "library"], Keys(service.Groups[0]));
        Assert.False(service.Groups[0].Tabs[1].IsPinned);
        Assert.False(service.Groups[0].Tabs[1].IsPreview);
    }

    /// <summary>A new preview opens after the pinned Tabs, never in front of them, even when the active Tab is an early pinned one.</summary>
    [Fact]
    public void Open_WithAnEarlyPinnedTabActive_InsertsAfterAllPinnedTabs()
    {
        TabService service = new();
        Tab first = service.Open("/teammates");
        service.Pin(first.Id);
        Tab second = service.Open("/settings");
        service.Pin(second.Id);
        service.Activate(first.Id);

        service.Open("/library");

        Assert.Equal(["teammates", "settings", "library"], Keys(service.Groups[0]));
    }

    /// <summary>Pin, Unpin and every move leave an unknown id alone and raise nothing.</summary>
    [Fact]
    public void UnknownIds_ChangeNothing()
    {
        (TabService service, _, _, _) = ThreePermanent();
        int changes = 0;
        service.Changed += () => changes++;

        service.Pin("nope");
        service.Unpin("nope");
        bool left = service.MoveLeft("nope");
        bool right = service.MoveRight("nope");

        Assert.False(left);
        Assert.False(right);
        Assert.Equal(0, changes);
    }

    /// <summary>Moving a Tab right from the only group opens a second group holding it, which takes focus.</summary>
    [Fact]
    public void MoveRight_FromTheOnlyGroup_OpensASecondGroup()
    {
        (TabService service, _, Tab b, _) = ThreePermanent();

        bool moved = service.MoveRight(b.Id);

        Assert.True(moved);
        Assert.Equal(2, service.Groups.Count);
        Assert.Equal(["teammates", "library"], Keys(service.Groups[0]));
        Assert.Equal(["settings"], Keys(service.Groups[1]));
        Assert.Equal(service.Groups[1].Id, service.FocusedGroupId);
        Assert.Equal(b.Id, service.Active?.Id);
    }

    /// <summary>Moving a Tab left from the only group opens the new group on the left.</summary>
    [Fact]
    public void MoveLeft_FromTheOnlyGroup_OpensAGroupOnTheLeft()
    {
        (TabService service, _, Tab b, _) = ThreePermanent();

        service.MoveLeft(b.Id);

        Assert.Equal(["settings"], Keys(service.Groups[0]));
        Assert.Equal(["teammates", "library"], Keys(service.Groups[1]));
        Assert.Equal(service.Groups[0].Id, service.FocusedGroupId);
    }

    /// <summary>A lone Tab has nowhere to go: moving it would only leave an empty group behind.</summary>
    [Fact]
    public void Move_ALoneTab_IsRefused()
    {
        TabService service = new();
        Tab only = service.Open("/teammates");

        Assert.False(service.CanMoveLeft(only.Id));
        Assert.False(service.CanMoveRight(only.Id));
        Assert.False(service.MoveRight(only.Id));
        Assert.Single(service.Groups);
    }

    /// <summary>With the group limit reached, a Tab cannot be pushed past the outer edge to make a third group.</summary>
    [Fact]
    public void Move_PastTheOuterEdge_IsRefusedAtTheGroupLimit()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);

        Assert.False(service.CanMoveRight(b.Id));
        Assert.False(service.CanMoveLeft(a.Id));
        Assert.False(service.MoveRight(b.Id));
        Assert.Equal(TabService.MaxGroups, service.Groups.Count);
    }

    /// <summary>A Tab moves into the neighbouring group, is made permanent, becomes its active Tab and takes focus there.</summary>
    [Fact]
    public void Move_IntoANeighbour_AppendsActivatesAndFocuses()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);

        bool moved = service.MoveRight(a.Id);

        Assert.True(moved);
        Assert.Equal(["library"], Keys(service.Groups[0]));
        Assert.Equal(["settings", "teammates"], Keys(service.Groups[1]));
        Assert.Equal(a.Id, service.Groups[1].ActiveId);
        Assert.Equal(service.Groups[1].Id, service.FocusedGroupId);
    }

    /// <summary>A moved preview becomes permanent: the next preview opens beside it instead of replacing it.</summary>
    [Fact]
    public void Move_APreview_MakesItPermanent()
    {
        (TabService service, _, _, _) = ThreePermanent();
        Tab preview = service.Open("/tasks/all");

        service.MoveRight(preview.Id);
        service.Open("/tasks/mine");

        Assert.Equal(["tasks:all", "tasks:mine"], Keys(service.Groups[1]));
        Assert.False(service.Groups[1].Tabs[0].IsPreview);
    }

    /// <summary>A pinned Tab keeps its pin and joins the pinned Tabs of the group it moves into.</summary>
    [Fact]
    public void Move_APinnedTab_StaysPinnedAtTheFrontOfTheTarget()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);
        service.Pin(a.Id);

        service.MoveRight(a.Id);

        Assert.Equal(["teammates", "settings"], Keys(service.Groups[1]));
        Assert.True(service.Groups[1].Tabs[0].IsPinned);
    }

    /// <summary>Moving the last Tab out of a group removes the group.</summary>
    [Fact]
    public void Move_TheLastTabOut_RemovesTheEmptyGroup()
    {
        (TabService service, _, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);

        service.MoveLeft(b.Id);

        Assert.Single(service.Groups);
        Assert.Equal(3, service.Groups[0].Tabs.Count);
    }

    /// <summary>Moving the active Tab out of a group leaves its neighbour active there.</summary>
    [Fact]
    public void Move_TheActiveTab_ActivatesAnotherInTheSource()
    {
        (TabService service, _, Tab b, _) = ThreePermanent();
        service.Activate(b.Id);

        service.MoveRight(b.Id);

        Assert.Equal("library", service.Groups[0].Active?.Key);
    }

    /// <summary>Opening a page whose key is open in another group focuses that group instead of duplicating the Tab.</summary>
    [Fact]
    public void Open_AKeyInAnotherGroup_FocusesThatGroup()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);
        Assert.Equal(service.Groups[1].Id, service.FocusedGroupId);

        Tab reopened = service.Open("/teammates");

        Assert.Equal(a.Id, reopened.Id);
        Assert.Equal(3, service.Tabs.Count);
        Assert.Equal(service.Groups[0].Id, service.FocusedGroupId);
        Assert.Equal(a.Id, service.Active?.Id);
    }

    /// <summary>A new preview opens in the focused group and replaces only that group's preview.</summary>
    [Fact]
    public void Open_NewPage_UsesTheFocusedGroup()
    {
        (TabService service, _, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);
        service.Open("/tasks/all");

        service.Open("/tasks/mine");

        Assert.Equal(["settings", "tasks:mine"], Keys(service.Groups[1]));
        Assert.Equal(["teammates", "library"], Keys(service.Groups[0]));
    }

    /// <summary>Focusing a group changes the active Tab the URL should name, without changing either group's own active Tab.</summary>
    [Fact]
    public void FocusGroup_ChangesActiveToThatGroupsActiveTab()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);
        service.Activate(a.Id);
        service.FocusGroup(service.Groups[1].Id);

        Assert.Equal(b.Id, service.Active?.Id);
        Assert.Equal(a.Id, service.Groups[0].ActiveId);
    }

    /// <summary>Focusing the focused group, or an unknown one, changes nothing and raises nothing.</summary>
    [Fact]
    public void FocusGroup_Noop_RaisesNothing()
    {
        TabService service = new();
        service.Open("/teammates");
        int changes = 0;
        service.Changed += () => changes++;

        service.FocusGroup(service.FocusedGroupId);
        service.FocusGroup("g99");

        Assert.Equal(0, changes);
    }

    /// <summary>Closing the last Tab of a group removes it and focuses the group that remains.</summary>
    [Fact]
    public void Close_TheLastTabOfAGroup_RemovesItAndFocusesTheOther()
    {
        (TabService service, _, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);

        service.Close(b.Id);

        Assert.Single(service.Groups);
        Assert.Equal(service.Groups[0].Id, service.FocusedGroupId);
        Assert.Equal("library", service.Active?.Key);
    }

    /// <summary>Closing a Tab in the unfocused group does not move focus.</summary>
    [Fact]
    public void Close_InTheUnfocusedGroup_KeepsFocus()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);
        string focused = service.FocusedGroupId;

        service.Close(a.Id);

        Assert.Equal(focused, service.FocusedGroupId);
        Assert.Equal(b.Id, service.Active?.Id);
    }

    /// <summary>The last Tab of the only group closes to an empty group, not to no group.</summary>
    [Fact]
    public void Close_TheLastTabOverall_LeavesOneEmptyGroup()
    {
        TabService service = new();
        Tab only = service.Open("/teammates");

        service.Close(only.Id);

        Assert.Empty(Assert.Single(service.Groups).Tabs);
    }

    /// <summary>Next and previous cycle within the focused group only.</summary>
    [Fact]
    public void ActivateRelative_StaysInTheFocusedGroup()
    {
        (TabService service, _, Tab b, _) = ThreePermanent();
        service.MoveRight(b.Id);

        Tab? next = service.ActivateRelative(1);

        Assert.Null(next);
        service.FocusGroup(service.Groups[0].Id);
        Assert.Equal("teammates", service.ActivateRelative(1)?.Key);
    }

    /// <summary>The snapshot of one group is a group node with the pinned flag; of two, a split with the focused index.</summary>
    [Fact]
    public void Export_OneGroupIsAGroupAndTwoAreASplit()
    {
        (TabService service, Tab a, Tab b, _) = ThreePermanent();
        service.Pin(a.Id);
        TabSnapshot single = service.Export();

        service.MoveRight(b.Id);
        TabSnapshot split = service.Export();

        Assert.Equal(TabSnapshot.CurrentVersion, single.Version);
        Assert.Equal(TabGroupSnapshot.GroupKind, single.Root.Kind);
        Assert.True(single.Root.Tabs[0].Pinned);
        Assert.Equal(TabGroupSnapshot.SplitKind, split.Root.Kind);
        Assert.Equal(2, split.Root.Children?.Count);
        Assert.Equal(1, split.Root.Focused);
    }

    /// <summary>A two-group layout with its pins, actives and focus survives a trip through JSON.</summary>
    [Fact]
    public void Restore_ASplit_RoundTripsThroughJson()
    {
        (TabService source, Tab a, Tab b, _) = ThreePermanent();
        source.Pin(a.Id);
        source.MoveRight(b.Id);
        source.FocusGroup(source.Groups[0].Id);
        TabService target = new();

        bool accepted = target.Restore(TabSnapshot.TryParse(source.Export().ToJson()));

        Assert.True(accepted);
        Assert.Equal(2, target.Groups.Count);
        Assert.Equal(source.Groups.Select(static group => Keys(group)), target.Groups.Select(static group => Keys(group)));
        Assert.True(target.Groups[0].Tabs[0].IsPinned);
        Assert.Equal(target.Groups[0].Id, target.FocusedGroupId);
        Assert.Equal(source.Active?.Key, target.Active?.Key);
    }

    /// <summary>A version 1 snapshot, written before groups and pins existed, is still read.</summary>
    [Fact]
    public void Restore_VersionOne_IsStillRead()
    {
        const string json = """
            {"version":1,"root":{"kind":"group","activeId":"t2","tabs":[
              {"id":"t1","url":"/teammates","title":"Teammates","icon":"teammates","preview":false},
              {"id":"t2","url":"/settings","title":"Settings","icon":"settings","preview":true}]}}
            """;
        TabService service = new();

        bool accepted = service.Restore(TabSnapshot.TryParse(json));

        Assert.True(accepted);
        Assert.Equal("settings", service.Active?.Key);
        Assert.DoesNotContain(service.Tabs, static tab => tab.IsPinned);
    }

    /// <summary>Layouts the service could never have written are refused whole.</summary>
    [Theory]
    [InlineData("""{"version":2,"root":{"kind":"group","tabs":[{"id":"t1","url":"/a","title":"A","icon":"page","preview":false},{"id":"t2","url":"/b","title":"B","icon":"page","preview":false,"pinned":true}]}}""")]
    [InlineData("""{"version":2,"root":{"kind":"group","tabs":[{"id":"t1","url":"/a","title":"A","icon":"page","preview":true,"pinned":true}]}}""")]
    [InlineData("""{"version":2,"root":{"kind":"split","children":[{"kind":"group","tabs":[{"id":"t1","url":"/a","title":"A","icon":"page","preview":false}]}]}}""")]
    [InlineData("""{"version":2,"root":{"kind":"split","children":[{"kind":"group","tabs":[{"id":"t1","url":"/a","title":"A","icon":"page","preview":false}]},{"kind":"group","tabs":[]}]}}""")]
    [InlineData("""{"version":2,"root":{"kind":"split","children":[{"kind":"group","tabs":[{"id":"t1","url":"/a","title":"A","icon":"page","preview":false}]},{"kind":"group","tabs":[{"id":"t2","url":"/a","title":"A","icon":"page","preview":false}]}]}}""")]
    [InlineData("""{"version":2,"root":{"kind":"split","children":[{"kind":"group","tabs":[{"id":"t1","url":"/a","title":"A","icon":"page","preview":false}]},{"kind":"group","tabs":[{"id":"t2","url":"/b","title":"B","icon":"page","preview":false}]},{"kind":"group","tabs":[{"id":"t3","url":"/c","title":"C","icon":"page","preview":false}]}]}}""")]
    public void Restore_ImpossibleLayouts_AreRefusedAndKeepTabs(string json)
    {
        TabService service = new();
        service.Open("/teammates");

        bool accepted = service.Restore(TabSnapshot.TryParse(json));

        Assert.False(accepted);
        Assert.Single(service.Tabs);
    }
}
