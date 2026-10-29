using Agency.Huddle.App.Search;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.References;

/// <summary>Pins <see cref="ReferencePickerModel"/>: the group order, the caps, the Agent and Room notes, the insert texts, the live text and the empty texts of spec §5.3, §5.7 and §11 (AT-T10).</summary>
public sealed class ReferencePickerModelTests
{
    /// <summary>The footer line of spec §5.7.</summary>
    private const string FooterText = "Narrow with agent: task: room: team: lib:";

    /// <summary>A group that finished with the given hits and was not truncated, keyed by its filter and side.</summary>
    /// <param name="group">The filter the group belongs to.</param>
    /// <param name="scope">Active or Archived.</param>
    /// <param name="hits">The hits.</param>
    /// <returns>The entry for the group states.</returns>
    private static KeyValuePair<(SearchFilter Group, SearchScope Scope), SearchGroupState> Done(SearchFilter group, SearchScope scope, params SearchHit[] hits)
    {
        return new((group, scope), new SearchGroupDone(new SearchResult(hits, false)));
    }

    /// <summary>The group states of a search, keyed as the view model reads them.</summary>
    /// <param name="entries">The entries, one per group that arrived.</param>
    /// <returns>The states.</returns>
    private static Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> Groups(params KeyValuePair<(SearchFilter Group, SearchScope Scope), SearchGroupState>[] entries)
    {
        return entries.ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    /// <summary>The input of a picker, with the Composer's defaults unless a test sets them.</summary>
    /// <param name="filter">The filter the box is set to.</param>
    /// <param name="groups">The group states that arrived.</param>
    /// <param name="token">The text after the trigger.</param>
    /// <param name="trigger">The trigger character, @ or #.</param>
    /// <param name="members">The names of the Room's Members.</param>
    /// <param name="invite">Whether the box starts with <c>/invite </c>.</param>
    /// <param name="highlight">The highlighted index as the Composer last set it.</param>
    /// <returns>The input.</returns>
    private static ReferencePickerInput Input(
        SearchFilter filter,
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> groups,
        string token = "",
        char trigger = '@',
        string[]? members = null,
        bool invite = false,
        int highlight = 0)
    {
        return new(
            trigger,
            token,
            filter,
            groups,
            new HashSet<string>(members ?? Array.Empty<string>(), StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["root-1"] = @"E:\Docs" },
            invite,
            "composer-picker-list",
            highlight);
    }

    /// <summary>An Agent hit whose Title is the name.</summary>
    /// <param name="name">The Agent's name.</param>
    /// <returns>The hit.</returns>
    private static AgentHit AgentNamed(string name)
    {
        return new AgentHit(name, "Engineer", name.ToLowerInvariant(), Array.Empty<string>(), MatchTier.NamePrefix);
    }

    /// <summary>A Task hit whose Title is the task's title.</summary>
    /// <param name="number">The task's number in the HUD project.</param>
    /// <returns>The hit.</returns>
    private static TaskHit TaskNumbered(int number)
    {
        return new TaskHit(new TaskId("HUD", number), "Task " + number.ToString(System.Globalization.CultureInfo.InvariantCulture), default, null, "Teams", false, MatchTier.NamePrefix);
    }

    /// <summary>A Team hit whose Title is the name.</summary>
    /// <param name="name">The Team's name.</param>
    /// <returns>The hit.</returns>
    private static TeamHit TeamNamed(string name)
    {
        return new TeamHit(name, null, 0, MatchTier.NamePrefix);
    }

    /// <summary>A Room hit with no Team.</summary>
    /// <param name="id">The Room's id.</param>
    /// <param name="name">The Room's name.</param>
    /// <returns>The hit.</returns>
    private static RoomHit RoomNamed(string id, string name)
    {
        return new RoomHit(id, name, 0, null, null, null, null, false, MatchTier.NamePrefix);
    }

    /// <summary>A Library file under the root <c>root-1</c>, whose Title is its last path segment.</summary>
    /// <param name="relativePath">The file's path under its root, with slashes.</param>
    /// <returns>The hit.</returns>
    private static LibraryHit FileNamed(string relativePath)
    {
        return new LibraryHit("root-1", "Docs", relativePath, string.Empty, false, null, null, MatchTier.NamePrefix);
    }

    /// <summary>Lists the titles of the rows of one group, in render order.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The titles.</returns>
    private static string[] Titles(ReferencePickerGroup group)
    {
        return group.Rows.Select(row => row.Hit.Title).ToArray();
    }

    /// <summary>All five active groups are in the order of spec §5.3 under All, and a group with no hit is left out.</summary>
    [Fact]
    public void Build_All_GroupOrder()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.All,
            Groups(
                Done(SearchFilter.Library, SearchScope.Active, FileNamed("notes.md")),
                Done(SearchFilter.Teams, SearchScope.Active, TeamNamed("Marketing")),
                Done(SearchFilter.Rooms, SearchScope.Active, RoomNamed("room-1", "Lobby")),
                Done(SearchFilter.Tasks, SearchScope.Active, TaskNumbered(1)),
                Done(SearchFilter.Agents, SearchScope.Active, AgentNamed("Ann")))));

        Assert.Equal(["Agents", "Tasks", "Rooms", "Teams", "Library"], view.Groups.Select(group => group.Heading).ToArray());
    }

    /// <summary>A single filter shows only its own group, capped at 8 for Tasks and 20 for the other groups.</summary>
    [Fact]
    public void Build_SingleFilter_CapsTasksAt8AndOthersAt20()
    {
        SearchHit[] tasks = Enumerable.Range(1, 10).Select(TaskNumbered).ToArray();
        SearchHit[] mates = Enumerable.Range(1, 25).Select(number => AgentNamed("Mate " + number.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();

        ReferencePickerView taskView = ReferencePickerModel.Build(Input(SearchFilter.Tasks, Groups(Done(SearchFilter.Tasks, SearchScope.Active, tasks))));
        ReferencePickerView mateView = ReferencePickerModel.Build(Input(SearchFilter.Agents, Groups(Done(SearchFilter.Agents, SearchScope.Active, mates))));

        Assert.Equal(8, taskView.Groups.Single().Rows.Count);
        Assert.Equal(20, mateView.Groups.Single().Rows.Count);
    }

    /// <summary>Members come first among the Agents, each part in the ranker's order.</summary>
    [Fact]
    public void Build_MembersFirst()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.Agents,
            Groups(Done(SearchFilter.Agents, SearchScope.Active, AgentNamed("Ann"), AgentNamed("Bo"), AgentNamed("Cy"), AgentNamed("Dee"))),
            members: ["bo", "DEE"]));

        Assert.Equal(["Bo", "Dee", "Ann", "Cy"], Titles(view.Groups.Single()));
    }

    /// <summary>After <c>/invite </c> only the Agents group shows, and non-Members come first.</summary>
    [Fact]
    public void Build_InviteCommand_NonMembersFirst()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.All,
            Groups(
                Done(SearchFilter.Agents, SearchScope.Active, AgentNamed("Ann"), AgentNamed("Bo"), AgentNamed("Cy"), AgentNamed("Dee")),
                Done(SearchFilter.Tasks, SearchScope.Active, TaskNumbered(1))),
            members: ["bo", "Dee"],
            invite: true));

        Assert.Equal(["Agents"], view.Groups.Select(group => group.Heading).ToArray());
        Assert.Equal(["Ann", "Cy", "Bo", "Dee"], Titles(view.Groups.Single()));
    }

    /// <summary>An Agent who is not a Member is dimmed with the note of spec §5.7; a Member is neither.</summary>
    [Fact]
    public void Build_NonMember_DimmedWithNote()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.Agents,
            Groups(Done(SearchFilter.Agents, SearchScope.Active, AgentNamed("Ann"), AgentNamed("Bo"))),
            members: ["Bo"]));

        ReferencePickerRow ann = view.Groups.Single().Rows[1];
        ReferencePickerRow bo = view.Groups.Single().Rows[0];
        Assert.True(ann.Dimmed);
        Assert.Equal("Not in this Room", ann.Note);
        Assert.Equal("Ann, Not in this Room", ann.AccessibleName);
        Assert.False(bo.Dimmed);
        Assert.Null(bo.Note);
        Assert.Equal("Bo", bo.AccessibleName);
    }

    /// <summary>With no row, the empty text names the box as spec §5.7 gives it for each trigger and filter.</summary>
    /// <param name="filter">The filter the box is set to.</param>
    /// <param name="trigger">The trigger character.</param>
    /// <param name="token">The text after the trigger.</param>
    /// <param name="expected">The empty text.</param>
    [Theory]
    [InlineData(SearchFilter.All, '@', "zz", "Nothing matches '@zz'.")]
    [InlineData(SearchFilter.Tasks, '#', "zz", "No task matches '#zz'")]
    [InlineData(SearchFilter.Library, '@', "lib:ab", "Type 3 characters to search the Library.")]
    [InlineData(SearchFilter.Library, '@', "lib: my", "Type 3 characters to search the Library.")]
    [InlineData(SearchFilter.Library, '@', "lib:", "Type 3 characters to search the Library.")]
    [InlineData(SearchFilter.Library, '@', "lib:abc", "Nothing matches '@lib:abc'.")]
    public void Build_EmptyTexts(SearchFilter filter, char trigger, string token, string expected)
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(filter, Groups(), token: token, trigger: trigger));

        Assert.Equal(expected, view.EmptyText);
        Assert.Empty(view.Groups);
    }

    /// <summary>The footer is the line of spec §5.7, whatever the results.</summary>
    [Fact]
    public void Build_Footer()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(SearchFilter.All, Groups()));

        Assert.Equal(FooterText, view.Footer);
    }

    /// <summary>The footer shows on a bare <c>@</c> only: once any text follows the trigger, even a filter prefix, it is gone (spec §5.3).</summary>
    /// <param name="token">The text after the <c>@</c>.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("lib: x")]
    [InlineData("Nova")]
    public void Build_Footer_HiddenOnceTextFollowsTheAt(string token)
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(SearchFilter.All, Groups(), token: token));

        Assert.Equal(string.Empty, view.Footer);
    }

    /// <summary>The <c>#</c> picker is Tasks only, so its footer does not suggest the agent, room, team or library prefixes (spec §5.5).</summary>
    [Fact]
    public void Build_HashTrigger_HasNoPrefixFooter()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(SearchFilter.Tasks, Groups(), trigger: '#'));

        Assert.Equal(string.Empty, view.Footer);
    }

    /// <summary>Under All the Agents group cuts to five AFTER the Members are moved first, so a Member the ranker listed late is not hidden behind non-Members (spec §5.3).</summary>
    [Fact]
    public void Build_All_MembersListedLateStillComeFirst()
    {
        SearchHit[] mates = [AgentNamed("Alan"), AgentNamed("Ben"), AgentNamed("Cleo"), AgentNamed("Dana"), AgentNamed("Emily"), AgentNamed("Grace"), AgentNamed("Marcus")];

        ReferencePickerView view = ReferencePickerModel.Build(Input(SearchFilter.All, Groups(Done(SearchFilter.Agents, SearchScope.Active, mates)), members: ["Grace", "Marcus"]));

        Assert.Equal(["Grace", "Marcus", "Alan", "Ben", "Cleo"], Titles(Assert.Single(view.Groups)));
    }

    /// <summary>The live text names the count of selectable rows: none, one, or the number.</summary>
    /// <param name="count">The number of rows.</param>
    /// <param name="expected">The live text.</param>
    [Theory]
    [InlineData(0, "No results")]
    [InlineData(1, "1 result")]
    [InlineData(7, "7 results")]
    public void Build_LiveText(int count, string expected)
    {
        SearchHit[] hits = Enumerable.Range(1, count).Select(TaskNumbered).ToArray();

        ReferencePickerView view = ReferencePickerModel.Build(Input(SearchFilter.Tasks, Groups(Done(SearchFilter.Tasks, SearchScope.Active, hits))));

        Assert.Equal(expected, view.LiveText);
    }

    /// <summary>Each kind of hit inserts the text spec §6 gives it: an Agent's name, a Task id, and the links of a Room and a Team.</summary>
    [Fact]
    public void Build_InsertText_EachKind()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.All,
            Groups(
                Done(SearchFilter.Agents, SearchScope.Active, AgentNamed("Ann")),
                Done(SearchFilter.Tasks, SearchScope.Active, TaskNumbered(1)),
                Done(SearchFilter.Rooms, SearchScope.Active, RoomNamed("room-1", "Lobby")),
                Done(SearchFilter.Teams, SearchScope.Active, TeamNamed("Marketing")))));

        Assert.Equal(
            ["@Ann", new TaskId("HUD", 1).ToString(), "[Lobby](/rooms/room-1)", "[Marketing](/teams/Marketing)"],
            view.Groups.SelectMany(group => group.Rows).Select(row => row.InsertText).ToArray());
    }

    /// <summary>A Library file inserts its full path, joined from its root's path, in a code span.</summary>
    [Fact]
    public void Build_LibraryInsert_IsCodeSpanFullPath()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.Library,
            Groups(Done(SearchFilter.Library, SearchScope.Active, FileNamed("reports/q3.pdf"))),
            token: "q3"));

        string expected = "`" + Path.Combine(@"E:\Docs", "reports", "q3.pdf") + "`";
        Assert.Equal(expected, view.Groups.Single().Rows.Single().InsertText);
    }

    /// <summary>The highlight is clamped into the rows: past the last row it is the last, below zero it is the first.</summary>
    [Fact]
    public void Build_Highlight_Clamped()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> groups = Groups(Done(SearchFilter.Tasks, SearchScope.Active, TaskNumbered(1), TaskNumbered(2), TaskNumbered(3)));

        ReferencePickerView high = ReferencePickerModel.Build(Input(SearchFilter.Tasks, groups, highlight: 99));
        ReferencePickerView low = ReferencePickerModel.Build(Input(SearchFilter.Tasks, groups, highlight: -4));

        Assert.Equal(2, high.HighlightIndex);
        Assert.Equal(0, low.HighlightIndex);
    }

    /// <summary>Closed Tasks and Archived Rooms come after every active group, in that order.</summary>
    [Fact]
    public void Build_SecondPass_AfterActiveGroups()
    {
        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.All,
            Groups(
                Done(SearchFilter.Rooms, SearchScope.Archived, RoomNamed("room-2", "Old")),
                Done(SearchFilter.Tasks, SearchScope.Archived, TaskNumbered(9)),
                Done(SearchFilter.Teams, SearchScope.Active, TeamNamed("Marketing")),
                Done(SearchFilter.Rooms, SearchScope.Active, RoomNamed("room-1", "Lobby")),
                Done(SearchFilter.Tasks, SearchScope.Active, TaskNumbered(1)))));

        Assert.Equal(
            ["Tasks", "Rooms", "Teams", "Closed Tasks", "Archived Rooms"],
            view.Groups.Select(group => group.Heading).ToArray());
    }

    /// <summary>The Room's note says its Scope: none for a root Room, the Team for a scoped one, and the Team Room's note for a Team Room.</summary>
    /// <param name="team">The Room's Team, or null.</param>
    /// <param name="isTeamRoom">Whether the Room is its Team's Team Room.</param>
    /// <param name="expected">The note.</param>
    [Theory]
    [InlineData(null, false, null)]
    [InlineData("Marketing", false, "Marketing")]
    [InlineData("Marketing", true, "Team Room · Marketing")]
    public void Build_RoomNote_EachScope(string? team, bool isTeamRoom, string? expected)
    {
        RoomHit room = new("room-1", "Lobby", 0, null, null, null, null, false, MatchTier.NamePrefix, team, isTeamRoom);

        ReferencePickerView view = ReferencePickerModel.Build(Input(SearchFilter.Rooms, Groups(Done(SearchFilter.Rooms, SearchScope.Active, room))));

        Assert.Equal(expected, view.Groups.Single().Rows.Single().Note);
    }

    /// <summary>A Team and its Team Room are two rows, one in the Teams group and one in the Rooms group.</summary>
    [Fact]
    public void Build_TeamAndItsTeamRoom_AreTwoRows()
    {
        RoomHit teamRoom = new("room-9", "Marketing", 0, null, null, null, null, false, MatchTier.NamePrefix, "Marketing", true);

        ReferencePickerView view = ReferencePickerModel.Build(Input(
            SearchFilter.All,
            Groups(
                Done(SearchFilter.Teams, SearchScope.Active, TeamNamed("Marketing")),
                Done(SearchFilter.Rooms, SearchScope.Active, teamRoom))));

        Assert.Equal(["Rooms", "Teams"], view.Groups.Select(group => group.Heading).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, view.Groups.Sum(group => group.Rows.Count));
    }

    /// <summary>A Room hit that carries a Message id still inserts the bare Room link, never a message query.</summary>
    [Fact]
    public void Build_RoomHitWithMessageId_InsertHasNoMessageQuery()
    {
        RoomHit room = new("room-1", "Lobby", 1, "msg-1", "Ann", "snippet", null, false, MatchTier.NamePrefix);

        ReferencePickerView view = ReferencePickerModel.Build(Input(SearchFilter.Rooms, Groups(Done(SearchFilter.Rooms, SearchScope.Active, room))));

        Assert.Equal("[Lobby](/rooms/room-1)", view.Groups.Single().Rows.Single().InsertText);
    }
}
