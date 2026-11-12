namespace Agency.Huddle.Tests.Teams;

using Agency.Huddle.App.Teams;

/// <summary>Tests for the TeamPageTabs logic.</summary>
public sealed class TeamPageTabsTests
{
    private static readonly TeamPageFeatures Both = new(Library: true, Tasks: true);
    private static readonly TeamPageFeatures LibraryOnly = new(Library: true, Tasks: false);
    private static readonly TeamPageFeatures TasksOnly = new(Library: false, Tasks: true);
    private static readonly TeamPageFeatures NeitherFeature = new(Library: false, Tasks: false);

    /// <summary>Team with both features on includes Members, Files and Details: a Team page has no Tasks tab (the sidebar lists its Task Views).</summary>
    [Fact]
    public void Available_Team_BothFeatures_ReturnsMembersFilesAndDetails_WithNoTasks()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Details];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, Both);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with Library on and Tasks off includes Members, Files and Details.</summary>
    [Fact]
    public void Available_Team_LibraryOnly_ReturnsMembersAndFiles()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Details];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, LibraryOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with Tasks on and Library off includes only Members and Details: Tasks never adds a tab to a Team page.</summary>
    [Fact]
    public void Available_Team_TasksOnly_ReturnsMembersAndDetails()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Details];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, TasksOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with both features off includes only Members and Details.</summary>
    [Fact]
    public void Available_Team_NeitherFeature_ReturnsMembersOnly()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Details];
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

    /// <summary>Segment(Process) returns "process".</summary>
    [Fact]
    public void Segment_Process_ReturnsProcess()
    {
        string actual = TeamPageTabs.Segment(TeamPageTab.Process);
        Assert.Equal("process", actual);
    }

    /// <summary>A Team with a process file gets the Process tab, just before Details; without one it does not.</summary>
    [Fact]
    public void Available_Team_WithProcess_AddsProcessLast()
    {
        TeamPageFeatures withProcess = new(Library: true, Tasks: true, Process: true);
        TeamPageFeatures libraryAndProcess = new(Library: true, Tasks: false, Process: true);
        TeamPageFeatures onlyProcess = new(Library: false, Tasks: false, Process: true);

        Assert.Equal([TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Process, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, withProcess));
        Assert.Equal([TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Process, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, libraryAndProcess));
        Assert.Equal([TeamPageTab.Members, TeamPageTab.Process, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, onlyProcess));
        Assert.Equal([TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, Both));
    }

    /// <summary>The "details" segment resolves to the Details tab on a Team page, and a Project page has none.</summary>
    [Fact]
    public void Resolve_DetailsSegment_IsATeamTabOnly()
    {
        Assert.Equal("details", TeamPageTabs.Segment(TeamPageTab.Details));
        Assert.Equal(TeamPageTab.Details, TeamPageTabs.Resolve(isProject: false, "Details", NeitherFeature));
        Assert.DoesNotContain(TeamPageTab.Details, TeamPageTabs.Available(isProject: true, Both));
    }

    /// <summary>A Project never has a Process tab.</summary>
    [Fact]
    public void Available_Project_WithProcess_HasNoProcessTab()
    {
        TeamPageFeatures withProcess = new(Library: true, Tasks: true, Process: true);

        Assert.Equal([TeamPageTab.Files, TeamPageTab.Tasks], TeamPageTabs.Available(isProject: true, withProcess));
    }

    /// <summary>The "process" segment resolves to the Process tab when it exists and falls back to Members when it does not.</summary>
    [Fact]
    public void Resolve_Team_ProcessSegment_DependsOnTheFeature()
    {
        TeamPageFeatures withProcess = new(Library: true, Tasks: true, Process: true);

        Assert.Equal(TeamPageTab.Process, TeamPageTabs.Resolve(isProject: false, "Process", withProcess));
        Assert.Equal(TeamPageTab.Members, TeamPageTabs.Resolve(isProject: false, "process", Both));
    }
}
