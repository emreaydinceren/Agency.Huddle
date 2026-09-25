using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="ListTasksTool"/> (Spec §11.4): mapping the tool's arguments onto the same
/// <see cref="Agency.Huddle.App.Tasks.Views.TaskQuery.Filter"/> and
/// <see cref="Agency.Huddle.App.Tasks.Views.TaskQuery.Sort"/> Views use (Spec §12.5), the
/// <c>assignee</c> aliases (corrections-B4 D10 item 3: <c>"me"</c> to the caller's Name,
/// <c>"unassigned"</c> to <c>"@unassigned"</c>, and a Persona Alias to its Name), the
/// <c>"project"</c>-without-<c>"team"</c> refusal, the <c>limit</c> default and range, and that
/// every argument is parsed without ever throwing (corrections-B4 D10 item 4).
/// </summary>
public sealed class ListTasksToolTests
{
    /// <summary>
    /// Writes each fixture Task to <paramref name="harness"/>'s disk-backed store at the path its own
    /// <see cref="TaskItem.Location"/> maps to (<see cref="TaskLayout.PathFor"/>), then rescans. A Task's
    /// scanned Location comes from the folder it is filed under, not from the value composed into its
    /// frontmatter, so every fixture must land under Team[/Project][/_closed] to actually exercise the
    /// scope/team/project filters it is meant to.
    /// </summary>
    private static void Seed(TaskToolHarness harness, params TaskItem[] tasks)
    {
        foreach (TaskItem task in tasks)
        {
            string fullPath = TaskLayout.PathFor(harness.TasksDirPath, task.Location, task.Id);
            string relativePath = Path.GetRelativePath(harness.TasksDirPath, fullPath);
            TestTaskStore.WriteTask(harness.TasksDirPath, relativePath, task);
        }

        harness.Store.RebuildFromWatcher();
    }

    /// <summary>Resolves and asserts Nova's seeded user id, narrowing it to non-null for the rest of the test.</summary>
    private static string NovaId(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return novaId;
    }

    private static ListTasksTool CreateTool(TaskToolHarness harness, string callerAgentId) =>
        new(harness.Store, harness.Directory, harness.Personas, new FakePromptSource(), callerAgentId);

    /// <summary>Filtering by <c>team</c> returns only Tasks in that Team, and the returned line matches <see cref="TaskToolText.Line"/>.</summary>
    [Fact]
    public async Task ListTasks_FilterByTeam_ReturnsOnlyThatTeamAsATaskToolTextLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem platform = TestTasks.Make(id: "PLAT-0001", title: "In Platform");
        TaskItem other = TestTasks.Make(id: "OPS-0001", title: "In Ops", location: new TaskLocation("Ops", null, false));
        Seed(harness, platform, other);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["team"] = "Platform" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>Filtering by <c>project</c> together with <c>team</c> matches only that Team/Project, matching a null-Project ProjectRef's semantics (§12.5).</summary>
    [Fact]
    public async Task ListTasks_FilterByProjectWithTeam_ReturnsOnlyThatProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem withProject = TestTasks.Make(id: "PLAT-0001", title: "In Auth v2", location: new TaskLocation("Platform", "Auth v2", false));
        TaskItem noProject = TestTasks.Make(id: "PLAT-0002", title: "No project", location: new TaskLocation("Platform", null, false));
        Seed(harness, withProject, noProject);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["team"] = "Platform", ["project"] = "Auth v2" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>A <c>project</c> argument with no <c>team</c> is refused (corrections-B4 D10 item 3), before any query runs.</summary>
    [Fact]
    public async Task ListTasks_ProjectWithoutTeam_ReturnsProjectNeedsTeamRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["project"] = "Auth v2" }, ct);

        Assert.Equal("project needs team", result);
    }

    /// <summary><c>assignee: "me"</c> resolves to the calling Agent's own Name (corrections-B4 D10 item 3), not to the Human's.</summary>
    [Fact]
    public async Task ListTasks_AssigneeMe_ResolvesToCallersName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem novaTask = TestTasks.Make(id: "PLAT-0001", title: "Nova's", assignee: "Nova");
        TaskItem kaiTask = TestTasks.Make(id: "PLAT-0002", title: "Kai's", assignee: "Kai");
        Seed(harness, novaTask, kaiTask);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["assignee"] = "me" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>An unknown caller id used with <c>assignee: "me"</c> is refused, and the raw id is never used as a Name (corrections-B4 D10 item 2).</summary>
    [Fact]
    public async Task ListTasks_AssigneeMe_UnknownCaller_ReturnsRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, "ghost-id");

        string result = await tool.InvokeAsync(new JsonObject { ["assignee"] = "me" }, ct);

        Assert.Equal("Could not identify caller 'ghost-id' as a Teammate.", result);
    }

    /// <summary><c>assignee: "unassigned"</c> maps to <c>"@unassigned"</c> (corrections-B4 D10 item 3), matching Tasks with no Assignee.</summary>
    [Fact]
    public async Task ListTasks_AssigneeUnassigned_ReturnsOnlyUnassignedTasks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem unassigned = TestTasks.Make(id: "PLAT-0001", title: "Nobody", assignee: null);
        TaskItem assigned = TestTasks.Make(id: "PLAT-0002", title: "Someone", assignee: "Nova");
        Seed(harness, unassigned, assigned);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["assignee"] = "unassigned" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>An <c>assignee</c> value that is a Persona Alias resolves to that Persona's Name (corrections-B4 D10 item 3: <c>PersonaStore</c> added to the ctor for aliases).</summary>
    [Fact]
    public async Task ListTasks_AssigneeByAlias_ResolvesToPersonaName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Personas.Add(new PersonaIdentity("Rho", "Rho", "R", ["Platform"]), "You are Rho.");
        TaskItem rhoTask = TestTasks.Make(id: "PLAT-0001", title: "Rho's", assignee: "Rho");
        TaskItem novaTask = TestTasks.Make(id: "PLAT-0002", title: "Nova's", assignee: "Nova");
        Seed(harness, rhoTask, novaTask);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["assignee"] = "R" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>An <c>assignee</c> value that is already a plain Name matches directly.</summary>
    [Fact]
    public async Task ListTasks_AssigneeByName_ReturnsOnlyThatAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem kaiTask = TestTasks.Make(id: "PLAT-0001", title: "Kai's", assignee: "Kai");
        TaskItem novaTask = TestTasks.Make(id: "PLAT-0002", title: "Nova's", assignee: "Nova");
        Seed(harness, kaiTask, novaTask);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["assignee"] = "Kai" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>Filtering by a single <c>status</c> value returns only Tasks in that state.</summary>
    [Fact]
    public async Task ListTasks_FilterByStatus_ReturnsOnlyThoseStates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem inProgress = TestTasks.Make(id: "PLAT-0001", title: "Doing", status: TaskState.InProgress);
        TaskItem done = TestTasks.Make(id: "PLAT-0002", title: "Finished", status: TaskState.Done);
        Seed(harness, inProgress, done);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["status"] = new JsonArray { "In Progress" } }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>Multiple <c>status</c> values are OR'd (§12.5: values within one dimension are OR'd).</summary>
    [Fact]
    public async Task ListTasks_FilterByStatus_MultipleValues_OrsThem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", title: "Queued", status: TaskState.ToDo);
        TaskItem inProgress = TestTasks.Make(id: "PLAT-0002", title: "Doing", status: TaskState.InProgress);
        TaskItem done = TestTasks.Make(id: "PLAT-0003", title: "Finished", status: TaskState.Done);
        Seed(harness, toDo, inProgress, done);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["status"] = new JsonArray { "To Do", "In Progress" } }, ct);

        string expected = string.Join(
            '\n',
            TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")),
            TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0002")));
        Assert.Equal(expected, result);
    }

    /// <summary>A <c>status</c> value that isn't a known wire name is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_FilterByStatus_UnknownValue_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["status"] = new JsonArray { "Blah" } }, ct);

        Assert.Equal("'status' has an unknown value 'Blah'.", result);
    }

    /// <summary>Filtering by <c>priority</c> returns only Tasks at that priority.</summary>
    [Fact]
    public async Task ListTasks_FilterByPriority_ReturnsOnlyThatPriority()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem urgent = TestTasks.Make(id: "PLAT-0001", title: "Fire", priority: TaskPriority.Urgent);
        TaskItem low = TestTasks.Make(id: "PLAT-0002", title: "Someday", priority: TaskPriority.Low);
        Seed(harness, urgent, low);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["priority"] = new JsonArray { "Urgent" } }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>A <c>priority</c> value that isn't a known wire name is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_FilterByPriority_UnknownValue_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["priority"] = new JsonArray { "Extreme" } }, ct);

        Assert.Equal("'priority' has an unknown value 'Extreme'.", result);
    }

    /// <summary>With no <c>scope</c> argument, only Active (non-closed) Tasks are returned (§11.4's default).</summary>
    [Fact]
    public async Task ListTasks_ScopeOmitted_DefaultsToActiveOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem active = TestTasks.Make(id: "PLAT-0001", title: "Open", location: new TaskLocation("Platform", null, false));
        TaskItem closed = TestTasks.Make(id: "PLAT-0002", title: "Shut", location: new TaskLocation("Platform", null, true));
        Seed(harness, active, closed);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync([], ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary><c>scope: "closed"</c> returns only Closed Tasks, and excludes Active ones.</summary>
    [Fact]
    public async Task ListTasks_ScopeClosed_ReturnsOnlyClosedTasks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem active = TestTasks.Make(id: "PLAT-0001", title: "Open", location: new TaskLocation("Platform", null, false));
        TaskItem closed = TestTasks.Make(id: "PLAT-0002", title: "Shut", location: new TaskLocation("Platform", null, true));
        Seed(harness, active, closed);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["scope"] = "closed" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0002")), result);
    }

    /// <summary>A <c>scope</c> value that is neither "active" nor "closed" is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_ScopeInvalidValue_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["scope"] = "sideways" }, ct);

        Assert.Equal("'scope' must be 'active' or 'closed'.", result);
    }

    /// <summary><c>text</c> matches a case-insensitive <c>Contains</c> over id and title (§12.5).</summary>
    [Fact]
    public async Task ListTasks_TextSearch_MatchesTitleCaseInsensitive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem saml = TestTasks.Make(id: "PLAT-0001", title: "Support SAML login");
        TaskItem other = TestTasks.Make(id: "PLAT-0002", title: "Fix the footer");
        Seed(harness, saml, other);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["text"] = "saml" }, ct);

        Assert.Equal(TaskToolText.Line(harness.Store.All.Single(t => t.Id.ToString() == "PLAT-0001")), result);
    }

    /// <summary>When results exceed <c>limit</c>, the list is cut off and ends with the §11.4 trailer line naming the shown and total counts.</summary>
    [Fact]
    public async Task ListTasks_LimitTruncates_AddsTrailerLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem first = TestTasks.Make(id: "PLAT-0001", title: "One");
        TaskItem second = TestTasks.Make(id: "PLAT-0002", title: "Two");
        TaskItem third = TestTasks.Make(id: "PLAT-0003", title: "Three");
        Seed(harness, first, second, third);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["limit"] = 2 }, ct);

        Assert.Equal("Showing 2 of 3; narrow the filters or raise limit.", result.Split('\n')[^1]);
    }

    /// <summary>With <c>limit</c> omitted, the default of 50 applies: a 51st Task is cut off with the trailer, proving the default rather than merely "no limit".</summary>
    [Fact]
    public async Task ListTasks_LimitOmitted_DefaultsTo50()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem[] tasks = new TaskItem[51];
        for (int i = 0; i < 51; i++)
        {
            tasks[i] = TestTasks.Make(id: $"PLAT-{i + 1:D4}", title: $"Task {i + 1}");
        }

        Seed(harness, tasks);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync([], ct);

        Assert.Equal("Showing 50 of 51; narrow the filters or raise limit.", result.Split('\n')[^1]);
    }

    /// <summary>A <c>limit</c> of 0 is out of the 1-200 range and is refused (§11.4).</summary>
    [Fact]
    public async Task ListTasks_LimitZero_ReturnsRangeRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["limit"] = 0 }, ct);

        Assert.Equal("'limit' must be between 1 and 200.", result);
    }

    /// <summary>A <c>limit</c> above 200 is out of range and is refused (§11.4).</summary>
    [Fact]
    public async Task ListTasks_LimitAboveMax_ReturnsRangeRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["limit"] = 201 }, ct);

        Assert.Equal("'limit' must be between 1 and 200.", result);
    }

    /// <summary>No matching Tasks returns the exact §11.4 wording, not an empty string.</summary>
    [Fact]
    public async Task ListTasks_NoResults_ReturnsNoTasksMatch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["team"] = "Nowhere" }, ct);

        Assert.Equal("No tasks match.", result);
    }

    /// <summary>With no explicit sort keys, results follow §12.5's default tiebreaker: priority descending, then due date ascending, then id ascending.</summary>
    [Fact]
    public async Task ListTasks_DefaultOrder_IsPriorityDescendingThenDueDateThenId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem low = TestTasks.Make(id: "PLAT-0003", title: "Low", priority: TaskPriority.Low);
        TaskItem urgentLaterDue = TestTasks.Make(id: "PLAT-0002", title: "Urgent later", priority: TaskPriority.Urgent, dueDate: new DateOnly(2026, 12, 1));
        TaskItem urgentSoonerDue = TestTasks.Make(id: "PLAT-0001", title: "Urgent sooner", priority: TaskPriority.Urgent, dueDate: new DateOnly(2026, 10, 1));
        Seed(harness, low, urgentLaterDue, urgentSoonerDue);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync([], ct);

        int firstIndex = result.IndexOf("PLAT-0001", StringComparison.Ordinal);
        int secondIndex = result.IndexOf("PLAT-0002", StringComparison.Ordinal);
        int thirdIndex = result.IndexOf("PLAT-0003", StringComparison.Ordinal);
        Assert.True(firstIndex >= 0 && secondIndex > firstIndex && thirdIndex > secondIndex);
    }

    /// <summary>A <c>team</c> argument holding a number is refused, naming the argument, never thrown (corrections-B4 D10 item 4).</summary>
    [Fact]
    public async Task ListTasks_TeamWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["team"] = 7 }, ct);

        Assert.Equal("'team' must be text.", result);
    }

    /// <summary>A <c>project</c> argument holding a number is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_ProjectWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["project"] = 7 }, ct);

        Assert.Equal("'project' must be text.", result);
    }

    /// <summary>An <c>assignee</c> argument holding a number is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_AssigneeWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["assignee"] = 7 }, ct);

        Assert.Equal("'assignee' must be text.", result);
    }

    /// <summary>A <c>status</c> argument that isn't an array is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_StatusWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["status"] = "In Progress" }, ct);

        Assert.Equal("'status' must be a list of text.", result);
    }

    /// <summary>A <c>priority</c> argument that isn't an array is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_PriorityWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["priority"] = "Urgent" }, ct);

        Assert.Equal("'priority' must be a list of text.", result);
    }

    /// <summary>A <c>text</c> argument holding a number is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_TextWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["text"] = 7 }, ct);

        Assert.Equal("'text' must be text.", result);
    }

    /// <summary>A <c>limit</c> argument holding text is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_LimitWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["limit"] = "many" }, ct);

        Assert.Equal("'limit' must be a whole number.", result);
    }

    /// <summary>A <c>scope</c> argument holding a number is refused, naming the argument, never thrown.</summary>
    [Fact]
    public async Task ListTasks_ScopeWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTasksTool tool = CreateTool(harness, NovaId(harness));

        string result = await tool.InvokeAsync(new JsonObject { ["scope"] = 7 }, ct);

        Assert.Equal("'scope' must be text.", result);
    }
}
