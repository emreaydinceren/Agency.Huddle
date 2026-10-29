namespace Agency.Huddle.App.Tabs;

/// <summary>Pins <see cref="TabService.Navigate(string, string)"/>: following a breadcrumb navigates the Tab it sits in, in its own group.</summary>
public sealed class TabNavigateTests
{
    private static string[] Keys(TabService service) => [.. service.Tabs.Select(static tab => tab.Key)];

    /// <summary>A Tab pointed at a page with another key keeps its id and position and takes the key, URL, title and icon of the new page.</summary>
    [Fact]
    public void Navigate_ToAnotherKey_RewritesTheTabInPlace()
    {
        TabService service = new();
        Tab agents = service.Open("/agents");
        service.Keep(agents.Id);
        Tab project = service.Open("/teams/Core/projects/Alpha");
        service.Keep(project.Id);

        Tab? result = service.Navigate(project.Id, "/teams/Core");

        Assert.NotNull(result);
        Assert.Equal(project.Id, result.Id);
        Assert.Equal("team:core", result.Key);
        Assert.Equal("/teams/Core", result.Url);
        Assert.Equal("Core", result.Title);
        Assert.Equal("team", result.Icon);
        Assert.Equal(["agents", "team:core"], Keys(service));
    }

    /// <summary>A preview Tab that is navigated becomes permanent: the person chose where it goes.</summary>
    [Fact]
    public void Navigate_PreviewTab_BecomesPermanent()
    {
        TabService service = new();
        Tab project = service.Open("/teams/Core/projects/Alpha");

        Tab? result = service.Navigate(project.Id, "/teams/Core");

        Assert.False(result?.IsPreview);
    }

    /// <summary>A pinned Tab stays pinned and first in its group when it is navigated.</summary>
    [Fact]
    public void Navigate_PinnedTab_StaysPinned()
    {
        TabService service = new();
        Tab agents = service.Open("/agents");
        service.Keep(agents.Id);
        Tab project = service.Open("/teams/Core/projects/Alpha");
        service.Pin(project.Id);

        Tab? result = service.Navigate(project.Id, "/teams/Core");

        Assert.True(result?.IsPinned);
        Assert.Equal(["team:core", "agents"], Keys(service));
    }

    /// <summary>A URL with the Tab's own key only updates the URL; the title, such as a renamed Room's, is kept.</summary>
    [Fact]
    public void Navigate_SameKey_UpdatesTheUrlAndKeepsTheTitle()
    {
        TabService service = new();
        Tab room = service.Open("/rooms/ab12");
        service.SetTitle("room:ab12", "Planning");

        Tab? result = service.Navigate(room.Id, "/rooms/ab12?x=1");

        Assert.Equal("Planning", result?.Title);
        Assert.Equal("/rooms/ab12?x=1", result?.Url);
    }

    /// <summary>When another Tab already has the new page's key, that Tab is reused and focused instead of two Tabs sharing a key.</summary>
    [Fact]
    public void Navigate_ToAKeyOpenElsewhere_ReusesThatTab()
    {
        TabService service = new();
        Tab project = service.Open("/teams/Core/projects/Alpha");
        service.Keep(project.Id);
        Tab team = service.Open("/teams/Core");
        service.Keep(team.Id);
        service.Activate(project.Id);

        Tab? result = service.Navigate(project.Id, "/teams/Core");

        Assert.Equal(team.Id, result?.Id);
        Assert.Equal(team.Id, service.Active?.Id);
        Assert.Equal(["project:core/alpha", "team:core"], Keys(service));
    }

    /// <summary>A Tab id nobody has changes nothing and raises nothing.</summary>
    [Fact]
    public void Navigate_UnknownId_ReturnsNullAndRaisesNothing()
    {
        TabService service = new();
        service.Open("/agents");
        int raised = 0;
        service.Changed += () => raised++;

        Tab? result = service.Navigate("nope", "/settings");

        Assert.Null(result);
        Assert.Equal(0, raised);
    }

    /// <summary>A navigation that changes something raises <see cref="TabService.Changed"/> once.</summary>
    [Fact]
    public void Navigate_RaisesChangedOnce()
    {
        TabService service = new();
        Tab project = service.Open("/teams/Core/projects/Alpha");
        int raised = 0;
        service.Changed += () => raised++;

        service.Navigate(project.Id, "/teams/Core");

        Assert.Equal(1, raised);
    }

    /// <summary>Navigating a Tab in the unfocused group changes only that Tab and gives its group focus.</summary>
    [Fact]
    public void Navigate_InTheUnfocusedGroup_ChangesOnlyThatTabAndFocusesItsGroup()
    {
        TabService service = new();
        Tab project = service.Open("/teams/Core/projects/Alpha");
        service.Keep(project.Id);
        Tab settings = service.Open("/settings");
        service.Keep(settings.Id);
        Assert.True(service.MoveRight(settings.Id));
        string left = service.Groups[0].Id;
        string right = service.Groups[1].Id;
        Assert.Equal(right, service.FocusedGroupId);

        service.Navigate(project.Id, "/teams/Core");

        Assert.Equal(left, service.FocusedGroupId);
        Assert.Equal("team:core", service.Groups[0].Tabs[0].Key);
        Assert.Equal("settings", service.Groups[1].Tabs[0].Key);
    }
}