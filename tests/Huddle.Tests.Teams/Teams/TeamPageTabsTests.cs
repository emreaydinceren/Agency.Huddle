namespace Agency.Huddle.Tests.Teams;

using Agency.Huddle.App.Teams;

/// <summary>Tests for the TeamPageTabs logic.</summary>
public sealed class TeamPageTabsTests
{
    private static readonly TeamPageFeatures Both = new(Library: true, Tasks: true);
    private static readonly TeamPageFeatures LibraryOnly = new(Library: true, Tasks: false);
    private static readonly TeamPageFeatures TasksOnly = new(Library: false, Tasks: true);
    private static readonly TeamPageFeatures NeitherFeature = new(Library: false, Tasks: false);

    /// <summary>Team with both features on includes Members, Files, the two Playbook tabs and Details: a Team page has no Tasks tab (the sidebar lists its Task Views).</summary>
    [Fact]
    public void Available_Team_BothFeatures_ReturnsMembersFilesPlaybooksAndDetails_WithNoTasks()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, Both);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with Library on and Tasks off includes Members, Files and Details.</summary>
    [Fact]
    public void Available_Team_LibraryOnly_ReturnsMembersAndFiles()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, LibraryOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with Tasks on and Library off includes only Members and Details: Tasks never adds a tab to a Team page.</summary>
    [Fact]
    public void Available_Team_TasksOnly_ReturnsMembersAndDetails()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details];
        IReadOnlyList<TeamPageTab> actual = TeamPageTabs.Available(isProject: false, TasksOnly);
        Assert.Equal(expected, actual);
    }

    /// <summary>Team with both features off includes only Members and Details.</summary>
    [Fact]
    public void Available_Team_NeitherFeature_ReturnsMembersOnly()
    {
        TeamPageTab[] expected = [TeamPageTab.Members, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details];
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

        Assert.Equal([TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Process, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, withProcess));
        Assert.Equal([TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.Process, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, libraryAndProcess));
        Assert.Equal([TeamPageTab.Members, TeamPageTab.Process, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, onlyProcess));
        Assert.Equal([TeamPageTab.Members, TeamPageTab.Files, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, Both));
    }

    /// <summary>The Manager Playbook tab and then the Agent Playbook tab sit between Process and Details on a Team page, whatever features are on (Team Playbooks spec §7.1).</summary>
    [Fact]
    public void Available_Team_PutsThePlaybookTabsBetweenProcessAndDetails()
    {
        List<TeamPageTab> tabs = [.. TeamPageTabs.Available(isProject: false, new TeamPageFeatures(Library: true, Tasks: true, Process: true))];

        Assert.Equal(tabs.IndexOf(TeamPageTab.Process) + 1, tabs.IndexOf(TeamPageTab.ManagerPlaybook));
        Assert.Equal(tabs.IndexOf(TeamPageTab.ManagerPlaybook) + 1, tabs.IndexOf(TeamPageTab.AgentPlaybook));
        Assert.Equal(tabs.IndexOf(TeamPageTab.AgentPlaybook) + 1, tabs.IndexOf(TeamPageTab.Details));
    }

    /// <summary>The Playbook tabs are present with Process off, and with the Library and Tasks off: it needs neither.</summary>
    [Fact]
    public void Available_Team_WithProcessOff_StillHasThePlaybookTabs()
    {
        TeamPageFeatures processOff = new(Library: false, Tasks: false, Process: false);

        Assert.Equal([TeamPageTab.Members, TeamPageTab.ManagerPlaybook, TeamPageTab.AgentPlaybook, TeamPageTab.Details], TeamPageTabs.Available(isProject: false, processOff));
    }

    /// <summary>A Project page has no Playbook tabs, whatever features are on.</summary>
    [Fact]
    public void Available_Project_HasNoPlaybookTabs()
    {
        IReadOnlyList<TeamPageTab> withEverything = TeamPageTabs.Available(isProject: true, new TeamPageFeatures(Library: true, Tasks: true, Process: true));

        Assert.DoesNotContain(TeamPageTab.ManagerPlaybook, withEverything);
        Assert.DoesNotContain(TeamPageTab.AgentPlaybook, withEverything);
        Assert.DoesNotContain(TeamPageTab.ManagerPlaybook, TeamPageTabs.Available(isProject: true, Both));
        Assert.DoesNotContain(TeamPageTab.AgentPlaybook, TeamPageTabs.Available(isProject: true, Both));
    }

    /// <summary>The Playbook tabs are labelled "Manager playbook" and "Agent playbook", their segments are "manager-playbook" and "agent-playbook", and the segments resolve on a Team page only.</summary>
    [Fact]
    public void PlaybookTabs_HaveTheirLabelsAndSegments_AndResolveOnATeamPageOnly()
    {
        Assert.Equal("Manager playbook", TeamPageTabs.Label(TeamPageTab.ManagerPlaybook));
        Assert.Equal("Agent playbook", TeamPageTabs.Label(TeamPageTab.AgentPlaybook));
        Assert.Equal("manager-playbook", TeamPageTabs.Segment(TeamPageTab.ManagerPlaybook));
        Assert.Equal("agent-playbook", TeamPageTabs.Segment(TeamPageTab.AgentPlaybook));
        Assert.Equal(TeamPageTab.ManagerPlaybook, TeamPageTabs.Resolve(isProject: false, "Manager-Playbook", NeitherFeature));
        Assert.Equal(TeamPageTab.AgentPlaybook, TeamPageTabs.Resolve(isProject: false, "agent-playbook", NeitherFeature));
        Assert.Equal(TeamPageTab.Files, TeamPageTabs.Resolve(isProject: true, "agent-playbook", Both));
    }

    /// <summary>The "details" segment resolves to the Details tab on a Team page, and a Project page has none.</summary>
    [Fact]
    public void Resolve_DetailsSegment_IsATeamTabOnly()
    {
        Assert.Equal("details", TeamPageTabs.Segment(TeamPageTab.Details));
        Assert.Equal(TeamPageTab.Details, TeamPageTabs.Resolve(isProject: false, "Details", NeitherFeature));
        Assert.DoesNotContain(TeamPageTab.Details, TeamPageTabs.Available(isProject: true, Both));
    }

    /// <summary>A Project has a Process tab after Library and Tasks when the Team has a process, and none without one.</summary>
    [Fact]
    public void Available_Project_WithProcess_EndsWithTheProcessTab()
    {
        TeamPageFeatures withProcess = new(Library: true, Tasks: true, Process: true);
        TeamPageFeatures onlyProcess = new(Library: false, Tasks: false, Process: true);

        Assert.Equal([TeamPageTab.Files, TeamPageTab.Tasks, TeamPageTab.Process], TeamPageTabs.Available(isProject: true, withProcess));
        Assert.Equal([TeamPageTab.Process], TeamPageTabs.Available(isProject: true, onlyProcess));
        Assert.Equal([TeamPageTab.Files, TeamPageTab.Tasks], TeamPageTabs.Available(isProject: true, Both));
        Assert.Equal(TeamPageTab.Process, TeamPageTabs.Resolve(isProject: true, "process", withProcess));
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
