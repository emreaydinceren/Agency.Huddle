namespace Agency.Huddle.Tests.Teams;

using Agency.Huddle.App.Teams;

/// <summary>Tests for the TeamPageTabs logic.</summary>
public sealed class TeamPageTabsTests
{
    private static readonly TeamPageFeatures Both = new(Library: true, Tasks: true);
    private static readonly TeamPageFeatures LibraryOnly = new(Library: true, Tasks: false);
    private static readonly TeamPageFeatures TasksOnly = new(Library: false, Tasks: true);
    private static readonly TeamPageFeatures NeitherFeature = new(Library: false, Tasks: false);

    /// <summary>Team with both features on includes Members, Files, and Tasks.</summary>
    [Fact]
    public void Available_Team_BothFeatures_ReturnsMembersFilesAndTasks()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Tasks];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, Both);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with Library on and Tasks off includes Members and Files.</summary>
    [Fact]
    public void Available_Team_LibraryOnly_ReturnsMembersAndFiles()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Files];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, LibraryOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with Tasks on and Library off includes Members and Tasks.</summary>
    [Fact]
    public void Available_Team_TasksOnly_ReturnsMembersAndTasks()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Tasks];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, TasksOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with both features off includes only Members.</summary>
    [Fact]
    public void Available_Team_NeitherFeature_ReturnsMembersOnly()
    {
        TeamPageTab[] expected = [TeamPageTab.Members];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, NeitherFeature);
        Assert.Equal(expected, actual);
    }

    /// <summary>Project with both features on includes Files and Tasks.</summary>
    [Fact]
    public void Available_Project_BothFeatures_ReturnsFilesAndTasks()
    {
        TeamPageTab[] expected = [TeamPageTab.Files, TeamPageTab.Tasks];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: true, Both);
        Assert.Equal(expected, actual);
    }

    /// <summary>Project with Tasks on and Library off includes only Tasks.</summary>
    [Fact]
    public void Available_Project_TasksOnly_ReturnsTasksOnly()
    {
        TeamPageTab[] expected = [TeamPageTab.Tasks];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: true, TasksOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Project with Library on and Tasks off includes only Files.</summary>
    [Fact]
    public void Available_Project_LibraryOnly_ReturnsFilesOnly()
    {
        TeamPageTab[] expected = [TeamPageTab.Files];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: true, LibraryOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Project with both features off includes no tabs.</summary>
    [Fact]
    public void Available_Project_NeitherFeature_ReturnsEmpty()
    {
        TeamPageTab[] expected = [];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: true, NeitherFeature);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with null tab resolves to Members.</summary>
    [Fact]
    public void Resolve_Team_NullTab_ReturnsMembersTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: null, Both);
        Assert.Equal(TeamPageTab.Members, actual);
    }

    /// <summary>Team with empty tab resolves to Members.</summary>
    [Fact]
    public void Resolve_Team_EmptyTab_ReturnsMembersTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: "", Both);
        Assert.Equal(TeamPageTab.Members, actual);
    }

    /// <summary>Team with bogus tab resolves to Members (first available).</summary>
    [Fact]
    public void Resolve_Team_BogusTab_ReturnsMembersTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: "bogus", Both);
        Assert.Equal(TeamPageTab.Members, actual);
    }

    /// <summary>Team with "files" tab resolves to Files.</summary>
    [Fact]
    public void Resolve_Team_FilesTab_ReturnsFilesTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: "files", Both);
        Assert.Equal(TeamPageTab.Files, actual);
    }

    /// <summary>Team with "FILES" (uppercase) resolves to Files (case-insensitive).</summary>
    [Fact]
    public void Resolve_Team_UppercaseFilesTab_ReturnsFilesTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: "FILES", Both);
        Assert.Equal(TeamPageTab.Files, actual);
    }

    /// <summary>Team with "tasks" when Tasks is off falls back to Members.</summary>
    [Fact]
    public void Resolve_Team_TasksTab_WithTasksOff_ReturnsMembersTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: "tasks", LibraryOnly);
        Assert.Equal(TeamPageTab.Members, actual);
    }

    /// <summary>Team with "files" when Library is off falls back to Members.</summary>
    [Fact]
    public void Resolve_Team_FilesTab_WithLibraryOff_ReturnsMembersTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: "files", TasksOnly);
        Assert.Equal(TeamPageTab.Members, actual);
    }

    /// <summary>Team with "1" resolves to Members.</summary>
    [Fact]
    public void Resolve_Team_NumericOne_ReturnsMembersTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: false, tab: "1", Both);
        Assert.Equal(TeamPageTab.Members, actual);
    }

    /// <summary>Project with null tab resolves to Files.</summary>
    [Fact]
    public void Resolve_Project_NullTab_ReturnsFilesTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: true, tab: null, Both);
        Assert.Equal(TeamPageTab.Files, actual);
    }

    /// <summary>Project with "members" (not available) falls back to Files.</summary>
    [Fact]
    public void Resolve_Project_MembersTab_ReturnsFilesTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: true, tab: "members", Both);
        Assert.Equal(TeamPageTab.Files, actual);
    }

    /// <summary>Project with null and Library off resolves to Tasks.</summary>
    [Fact]
    public void Resolve_Project_NullTab_WithLibraryOff_ReturnsTasksTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: true, tab: null, TasksOnly);
        Assert.Equal(TeamPageTab.Tasks, actual);
    }

    /// <summary>Project with both features off has no available tabs, so resolves to Files.</summary>
    [Fact]
    public void Resolve_Project_BothOff_ReturnsFilesTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: true, tab: null, NeitherFeature);
        Assert.Equal(TeamPageTab.Files, actual);
    }

    /// <summary>Project with "2" resolves to Files.</summary>
    [Fact]
    public void Resolve_Project_NumericTwo_ReturnsFilesTab()
    {
        TeamPageTab actual = TeamPageTabs.Resolve(isProject: true, tab: "2", Both);
        Assert.Equal(TeamPageTab.Files, actual);
    }

    /// <summary>Segment(Members) returns "members".</summary>
    [Fact]
    public void Segment_Members_ReturnsMembers()
    {
        string actual = TeamPageTabs.Segment(TeamPageTab.Members);
        Assert.Equal("members", actual);
    }

    /// <summary>Segment(Files) returns "files".</summary>
    [Fact]
    public void Segment_Files_ReturnsFiles()
    {
        string actual = TeamPageTabs.Segment(TeamPageTab.Files);
        Assert.Equal("files", actual);
    }

    /// <summary>Segment(Tasks) returns "tasks".</summary>
    [Fact]
    public void Segment_Tasks_ReturnsTasks()
    {
        string actual = TeamPageTabs.Segment(TeamPageTab.Tasks);
        Assert.Equal("tasks", actual);
    }
}
