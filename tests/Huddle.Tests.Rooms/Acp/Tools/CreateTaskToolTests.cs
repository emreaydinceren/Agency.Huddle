namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

/// <summary>Covers Spec §11.2's <c>create_task</c> tool: its checks, in order, its success text, and that every expected failure is text, never a throw.</summary>
public sealed class CreateTaskToolTests
{
    /// <summary>A missing <c>title</c> is refused, naming the argument, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_MissingTitle_ReturnsRefusalNamingItAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["team"] = "Platform" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'title' is a required argument.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A missing <c>team</c> is refused, naming the argument, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_MissingTeam_ReturnsRefusalNamingItAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'team' is a required argument.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>An <c>originRoomId</c> the caller isn't a Member of is refused with <c>FollowRoomTool</c>'s "not a member" wording, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_OriginRoomCallerNotMember_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        User? kai = await harness.Directory.GetUserAsync(RequireId(harness.KaiId), ct);
        Assert.NotNull(kai);
        Room room = await harness.Chat.EnsureRoomForAsync(kai, ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["originRoomId"] = room.Id };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            $"You are not a member of room '{room.Name}' (id {room.Id}); pass an originRoomId only for a Room you belong to, or omit it.",
            result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A member's <c>originRoomId</c> is accepted, and the written Task carries it.</summary>
    [Fact]
    public async Task CreateTask_OriginRoomCallerIsMember_WrittenTaskHasOriginSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        User? nova = await harness.Directory.GetUserAsync(RequireId(harness.NovaId), ct);
        Assert.NotNull(nova);
        Room room = await harness.Chat.EnsureRoomForAsync(nova, ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["originRoomId"] = room.Id };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Created PLAT-0001 \"Support SAML login\" in Platform, unassigned. No one is notified.", result);
        TaskItem written = Assert.Single(harness.Store.All);
        Assert.Equal(room.Id, written.OriginRoomId);
    }

    /// <summary>
    /// With no <c>originRoomId</c>, the Task's origin is the Room of the caller's one running Turn. Models leave the argument out,
    /// and a Task with no origin is woken in a {Human, assignee} Team chat the Human never opened, not where it was asked for.
    /// </summary>
    [Fact]
    public async Task CreateTask_NoOriginRoom_OneTurnRunning_OriginIsThatRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        User? nova = await harness.Directory.GetUserAsync(RequireId(harness.NovaId), ct);
        Assert.NotNull(nova);
        Room room = await harness.Chat.EnsureRoomForAsync(nova, ct);
        TurnActivity turns = new();
        turns.Begin(nova.Id, room.Id);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), nova.Id, turns);

        _ = await tool.InvokeAsync(new JsonObject { ["title"] = "Support SAML login", ["team"] = "Platform" }, ct);

        Assert.Equal(room.Id, Assert.Single(harness.Store.All).OriginRoomId);
    }

    /// <summary>With Turns running in two Rooms at once the call cannot be tied to one of them, so the origin stays empty.</summary>
    [Fact]
    public async Task CreateTask_NoOriginRoom_TurnsInTwoRooms_LeavesOriginEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        User? nova = await harness.Directory.GetUserAsync(RequireId(harness.NovaId), ct);
        Assert.NotNull(nova);
        Room direct = await harness.Chat.EnsureRoomForAsync(nova, ct);
        Room shared = await harness.Chat.CreateRoomForAsync([nova.Id, RequireId(harness.KaiId)], ct);
        TurnActivity turns = new();
        turns.Begin(nova.Id, direct.Id);
        turns.Begin(nova.Id, shared.Id);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), nova.Id, turns);

        _ = await tool.InvokeAsync(new JsonObject { ["title"] = "Support SAML login", ["team"] = "Platform" }, ct);

        Assert.Null(Assert.Single(harness.Store.All).OriginRoomId);
    }

    /// <summary>A running Turn in a Room the caller is not a Member of is not taken as the origin, and the create still succeeds.</summary>
    [Fact]
    public async Task CreateTask_NoOriginRoom_TurnInARoomCallerIsNotIn_LeavesOriginEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        User? kai = await harness.Directory.GetUserAsync(RequireId(harness.KaiId), ct);
        Assert.NotNull(kai);
        Room kaisRoom = await harness.Chat.EnsureRoomForAsync(kai, ct);
        TurnActivity turns = new();
        turns.Begin(RequireId(harness.NovaId), kaisRoom.Id);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId), turns);

        string result = await tool.InvokeAsync(new JsonObject { ["title"] = "Support SAML login", ["team"] = "Platform" }, ct);

        Assert.Equal("Created PLAT-0001 \"Support SAML login\" in Platform, unassigned. No one is notified.", result);
        Assert.Null(Assert.Single(harness.Store.All).OriginRoomId);
    }

    /// <summary>An <c>originRoomId</c> the model passes wins over the Room of the running Turn.</summary>
    [Fact]
    public async Task CreateTask_ExplicitOriginRoom_BeatsTheTurnsRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        User? nova = await harness.Directory.GetUserAsync(RequireId(harness.NovaId), ct);
        Assert.NotNull(nova);
        Room direct = await harness.Chat.EnsureRoomForAsync(nova, ct);
        Room shared = await harness.Chat.CreateRoomForAsync([nova.Id, RequireId(harness.KaiId)], ct);
        TurnActivity turns = new();
        turns.Begin(nova.Id, direct.Id);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), nova.Id, turns);

        _ = await tool.InvokeAsync(new JsonObject { ["title"] = "Support SAML login", ["team"] = "Platform", ["originRoomId"] = shared.Id }, ct);

        Assert.Equal(shared.Id, Assert.Single(harness.Store.All).OriginRoomId);
    }

    /// <summary>A Won't do status is refused: the caller is told to create the Task first, then call <c>update_task</c>.</summary>
    [Fact]
    public async Task CreateTask_WontDoStatus_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["status"] = "Cancelled" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Create the task first; to mark it Cancelled, Duplicate or Rejected, call update_task.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A date that doesn't parse as <c>yyyy-MM-dd</c> is refused, naming the offending argument.</summary>
    [Fact]
    public async Task CreateTask_BadDate_ReturnsRefusalNamingItAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["due_date"] = "09/01/2026" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'due_date' must be a date in the form yyyy-MM-dd.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A <see cref="TaskResult.Refused"/> from <see cref="TaskService"/> - here, two problems at once - is surfaced as text, its problems joined with a newline.</summary>
    [Fact]
    public async Task CreateTask_ServiceRefusal_ReturnsProblemsJoinedWithNewline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = new string('x', 201), ["team"] = "Nonexistent Team" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            "Title is 201 characters; the limit is 200.\nUnknown team 'Nonexistent Team'. Known teams: Platform.",
            result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A successful create says who is assigned, and reports the notify clause <see cref="TaskTriggerService.Preview"/> resolves for it.</summary>
    [Fact]
    public async Task CreateTask_Success_AssignedToAgent_ReportsCreatedAndNotified()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["assignee"] = "Kai" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Created PLAT-0001 \"Support SAML login\" in Platform, assigned to Kai. Kai will be notified.", result);
        TaskItem written = Assert.Single(harness.Store.All);
        Assert.Equal("Kai", written.Assignee);
    }

    /// <summary>An unassigned Task's success text says so, and that no one is notified.</summary>
    [Fact]
    public async Task CreateTask_Success_NoAssignee_ReportsUnassignedAndNoOneNotified()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Created PLAT-0001 \"Support SAML login\" in Platform, unassigned. No one is notified.", result);
    }

    /// <summary><c>assignee: "me"</c> assigns the caller, and says "assigned to you", with no one notified.</summary>
    [Fact]
    public async Task CreateTask_AssigneeMe_AssignsCallerAndReportsNoOneNotified()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["assignee"] = "me" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Created PLAT-0001 \"Support SAML login\" in Platform, assigned to you. No one is notified.", result);
        TaskItem written = Assert.Single(harness.Store.All);
        Assert.Equal("Nova", written.Assignee);
    }

    /// <summary>An unknown caller is refused, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_UnknownCaller_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), "ghost-agent-id");
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Could not identify caller 'ghost-agent-id' as an Agent.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A <c>blocked_by</c> that isn't a list is refused, naming the argument, rather than thrown.</summary>
    [Fact]
    public async Task CreateTask_BlockedByWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["blocked_by"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'blocked_by' must be a list of text.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A <c>title</c> given as a number rather than text is refused, naming the argument, rather than thrown.</summary>
    [Fact]
    public async Task CreateTask_TitleWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = 42, ["team"] = "Platform" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'title' must be text.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A racing file already sitting at the id the allocator is about to use maps <see cref="TaskService.Create"/>'s <see cref="InvalidOperationException"/> to the §9.1 wording, never a throw.</summary>
    [Fact]
    public async Task CreateTask_ServiceThrowsOnCollision_ReturnsTextNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string collidingPath = Path.Combine(harness.Store.RootDirectory, TestTaskStore.RelativePath("Platform", null, closed: false, "PLAT-0001.md"));
        Directory.CreateDirectory(Path.GetDirectoryName(collidingPath) ?? throw new InvalidOperationException("Expected a parent directory."));
        File.WriteAllText(collidingPath, "not a task file");
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Could not save the task: Task 'PLAT-0001' already exists.", result);
        Assert.Equal("not a task file", File.ReadAllText(collidingPath));
    }

    /// <summary>An unknown <c>status</c> value is refused with the shared D10 "unknown value" wording, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_InvalidStatus_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["status"] = "Not A Status" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'status' has an unknown value 'Not A Status'.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>An unknown <c>priority</c> value is refused with the shared D10 "unknown value" wording, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_InvalidPriority_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["priority"] = "Not A Priority" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'priority' has an unknown value 'Not A Priority'.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>An <c>originRoomId</c> naming no known Room is refused, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_UnknownOriginRoomId_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["originRoomId"] = "no-such-room" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown room 'no-such-room'.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A well-formed <c>parent</c> id that names no existing Task is refused by <see cref="TaskService"/>, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_UnresolvableParent_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["parent"] = "PLAT-0999" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown task 'PLAT-0999'.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A well-formed <c>blocked_by</c> id that names no existing Task is refused by <see cref="TaskService"/>, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_UnresolvableBlockedBy_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["blocked_by"] = new JsonArray { "PLAT-0999" } };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown task 'PLAT-0999'.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>The schema requires exactly <c>title</c> and <c>team</c>.</summary>
    [Fact]
    public async Task CreateTask_InputSchema_RequiresTitleAndTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId));

        JsonArray? required = tool.InputSchema["required"] as JsonArray;

        Assert.NotNull(required);
        List<string?> requiredNames = [.. required.Select(node => (string?)node)];
        Assert.Equal(["title", "team"], requiredNames);
    }

    private static string RequireId(string? id) => id ?? throw new InvalidOperationException("Expected a seeded id.");

    private static FakeTeamCatalog ProjectCatalog() => new()
    {
        Teams =
        [
            // Both Teams list a "Site", so a source in Marketing gives no default only because its Team differs, not its Project.
            new TeamSummary("Platform", ["Web", "Api", "Docs", "Site"], ["Nova", "Kai"], true),
            new TeamSummary("Marketing", ["Site"], [], true),
        ],
    };

    private static CreateTaskTool ToolFor(TaskToolHarness harness, TurnActivity? turns = null, ITeamCatalog? catalog = null) =>
        new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), RequireId(harness.NovaId), turns, catalog: catalog, store: harness.Store);

    private static async Task<Room> RoomOfAsync(TaskToolHarness harness, string? team, string? project, CancellationToken ct) =>
        await harness.Directory.CreateRoomAsync("Work", [KnownIds.Human, RequireId(harness.NovaId)], team, false, project, ct);

    private static TaskItem SeedIn(TaskToolHarness harness, string id, string team, string project) =>
        harness.SeedOnDisk(TestTasks.Make(id: id, location: new TaskLocation(team, project, false)));

    private static JsonObject Args(string? project = null, string? parent = null, string? originRoomId = null, string team = "Platform")
    {
        JsonObject arguments = new() { ["title"] = "Follow-up", ["team"] = team };
        if (project is not null)
        {
            arguments["project"] = project;
        }

        if (parent is not null)
        {
            arguments["parent"] = parent;
        }

        if (originRoomId is not null)
        {
            arguments["originRoomId"] = originRoomId;
        }

        return arguments;
    }

    private static TaskItem Written(TaskToolHarness harness, string result) =>
        harness.Store.All.Single(task => string.Equals(task.Title, "Follow-up", StringComparison.Ordinal)
            && result.StartsWith($"Created {task.Id} ", StringComparison.Ordinal)); // contains-ok: picks the Task the result names; each test then asserts the whole text or the Project.

    private static string Filed(TaskItem task, string location, string clause) =>
        $"Created {task.Id} \"Follow-up\" in {location}, unassigned. {clause} Pass project to file it elsewhere. No one is notified.";

    /// <summary>With no project, the Room of the running Turn files the Task in its Project, and the result says so.</summary>
    [Fact]
    public async Task CreateTask_NoProject_TurnRoomInProject_FilesInRoomsProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "Platform", "Web", ct);
        TurnActivity turns = new();
        turns.Begin(RequireId(harness.NovaId), room.Id);

        string result = await ToolFor(harness, turns, ProjectCatalog()).InvokeAsync(Args(), ct);

        TaskItem written = Written(harness, result);
        Assert.Equal("Web", written.Location.Project);
        Assert.Equal(Filed(written, "Platform/Web", "Filed in project \"Web\" (from Room \"Work\", where this Task was asked for)."), result);
    }

    /// <summary>An explicit <c>originRoomId</c> in a Project files the Task there, in the catalog's spelling of the Project.</summary>
    [Fact]
    public async Task CreateTask_NoProject_ExplicitOriginRoomInProject_FilesInThatProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "platform", "web", ct);

        string result = await ToolFor(harness, catalog: ProjectCatalog()).InvokeAsync(Args(originRoomId: room.Id), ct);

        TaskItem written = Written(harness, result);
        Assert.Equal("Web", written.Location.Project);
        Assert.Equal(Filed(written, "Platform/Web", "Filed in project \"Web\" (from Room \"Work\", where this Task was asked for)."), result);
    }

    /// <summary>With no Room Project, the parent Task's Project is the default, and the result names the parent.</summary>
    [Fact]
    public async Task CreateTask_NoProject_ParentInProject_FilesInParentsProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem parent = SeedIn(harness, "PLAT-0100", "Platform", "Api");

        string result = await ToolFor(harness, catalog: ProjectCatalog()).InvokeAsync(Args(parent: parent.Id.ToString()), ct);

        TaskItem written = Written(harness, result);
        Assert.Equal("Api", written.Location.Project);
        Assert.Equal(Filed(written, "Platform/Api", "Filed in project \"Api\" (from PLAT-0100, its parent)."), result);
    }

    /// <summary>With no Room Project and no parent, the Task the Turn was woken for gives the Project, and the result names it.</summary>
    [Fact]
    public async Task CreateTask_NoProject_TaskInHandInProject_FilesInItsProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "Platform", null, ct);
        TaskItem inHand = SeedIn(harness, "PLAT-0101", "Platform", "Docs");
        TurnActivity turns = new();
        turns.RecordWake(RequireId(harness.NovaId), room.Id, inHand.Id);
        turns.Begin(RequireId(harness.NovaId), room.Id);

        string result = await ToolFor(harness, turns, ProjectCatalog()).InvokeAsync(Args(), ct);

        TaskItem written = Written(harness, result);
        Assert.Equal("Docs", written.Location.Project);
        Assert.Equal(Filed(written, "Platform/Docs", "Filed in project \"Docs\" (from PLAT-0101, the Task you were woken for)."), result);
    }

    /// <summary>The Room's Project beats the parent's, which beats the Task in hand's.</summary>
    [Fact]
    public async Task CreateTask_NoProject_RoomBeatsParentBeatsTaskInHand()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room projectRoom = await RoomOfAsync(harness, "Platform", "Web", ct);
        Room plainRoom = await RoomOfAsync(harness, "Platform", null, ct);
        TaskItem parent = SeedIn(harness, "PLAT-0100", "Platform", "Api");
        TaskItem inHand = SeedIn(harness, "PLAT-0101", "Platform", "Docs");
        string nova = RequireId(harness.NovaId);

        TurnActivity roomTurns = new();
        roomTurns.RecordWake(nova, projectRoom.Id, inHand.Id);
        roomTurns.Begin(nova, projectRoom.Id);
        string byRoom = await ToolFor(harness, roomTurns, ProjectCatalog()).InvokeAsync(Args(parent: parent.Id.ToString()), ct);

        TurnActivity plainTurns = new();
        plainTurns.RecordWake(nova, plainRoom.Id, inHand.Id);
        plainTurns.Begin(nova, plainRoom.Id);
        string byParent = await ToolFor(harness, plainTurns, ProjectCatalog()).InvokeAsync(Args(parent: parent.Id.ToString()), ct);

        Assert.Equal("Web", Written(harness, byRoom).Location.Project);
        Assert.Equal("Api", Written(harness, byParent).Location.Project);
    }

    /// <summary>A <c>project</c> the model passes is never overridden by a default, and the text carries no Filed clause.</summary>
    [Fact]
    public async Task CreateTask_ExplicitProject_IsNeverOverridden()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "Platform", "Web", ct);
        TurnActivity turns = new();
        turns.Begin(RequireId(harness.NovaId), room.Id);

        string result = await ToolFor(harness, turns, ProjectCatalog()).InvokeAsync(Args(project: "Docs"), ct);

        Assert.Equal("Docs", Written(harness, result).Location.Project);
        Assert.DoesNotContain("Filed in project", result, StringComparison.Ordinal);
    }

    /// <summary>An empty or whitespace <c>project</c> counts as omitted and gets the default.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTask_BlankProject_GetsTheDefault(string blank)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "Platform", "Web", ct);
        TurnActivity turns = new();
        turns.Begin(RequireId(harness.NovaId), room.Id);

        string result = await ToolFor(harness, turns, ProjectCatalog()).InvokeAsync(Args(project: blank), ct);

        Assert.Equal("Web", Written(harness, result).Location.Project);
    }

    /// <summary>With no Room Project, parent or Task in hand, the Task is filed at the Team root and the text is unchanged.</summary>
    [Fact]
    public async Task CreateTask_NoContext_StaysAtTeamRootWithUnchangedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string result = await ToolFor(harness, new TurnActivity(), ProjectCatalog()).InvokeAsync(Args(), ct);

        Assert.Equal("Created PLAT-0001 \"Follow-up\" in Platform, unassigned. No one is notified.", result);
        Assert.Null(Assert.Single(harness.Store.All).Location.Project);
    }

    /// <summary>A source in another Team gives no default: not the Room's, not the parent's, not the Task in hand's.</summary>
    [Theory]
    [InlineData("room")]
    [InlineData("parent")]
    [InlineData("inhand")]
    public async Task CreateTask_SourceInAnotherTeam_GivesNoDefault(string source)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string nova = RequireId(harness.NovaId);
        Room room = await RoomOfAsync(harness, source == "room" ? "Marketing" : "Platform", source == "room" ? "Site" : null, ct);
        TaskItem foreign = SeedIn(harness, "MKT-0100", "Marketing", "Site");
        TurnActivity turns = new();
        if (source == "inhand")
        {
            turns.RecordWake(nova, room.Id, foreign.Id);
        }

        turns.Begin(nova, room.Id);

        string result = await ToolFor(harness, turns, ProjectCatalog()).InvokeAsync(Args(parent: source == "parent" ? foreign.Id.ToString() : null), ct);

        Assert.DoesNotContain("Filed in project", result, StringComparison.Ordinal);
        Assert.Null(Written(harness, result).Location.Project);
    }

    /// <summary>A Project the catalog no longer lists gives no default, and the next fallback is tried.</summary>
    [Fact]
    public async Task CreateTask_RoomsProjectNotInCatalog_FallsBackToParent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "Platform", "Old", ct);
        TaskItem parent = SeedIn(harness, "PLAT-0100", "Platform", "Api");
        TurnActivity turns = new();
        turns.Begin(RequireId(harness.NovaId), room.Id);

        string result = await ToolFor(harness, turns, ProjectCatalog()).InvokeAsync(Args(parent: parent.Id.ToString()), ct);

        TaskItem written = Written(harness, result);
        Assert.Equal("Api", written.Location.Project);
        Assert.Equal(Filed(written, "Platform/Api", "Filed in project \"Api\" (from PLAT-0100, its parent)."), result);
    }

    /// <summary>A Project that is not listed anywhere gives no default at all.</summary>
    [Fact]
    public async Task CreateTask_OnlySourceProjectNotInCatalog_StaysAtTeamRoot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "Platform", "Old", ct);
        TurnActivity turns = new();
        turns.Begin(RequireId(harness.NovaId), room.Id);

        string result = await ToolFor(harness, turns, ProjectCatalog()).InvokeAsync(Args(), ct);

        Assert.DoesNotContain("Filed in project", result, StringComparison.Ordinal);
        Assert.Null(Written(harness, result).Location.Project);
    }

    /// <summary>With no catalog injected nothing is defaulted.</summary>
    [Fact]
    public async Task CreateTask_NoCatalog_DefaultsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await RoomOfAsync(harness, "Platform", "Web", ct);
        TurnActivity turns = new();
        turns.Begin(RequireId(harness.NovaId), room.Id);

        string result = await ToolFor(harness, turns).InvokeAsync(Args(), ct);

        Assert.DoesNotContain("Filed in project", result, StringComparison.Ordinal);
        Assert.Null(Written(harness, result).Location.Project);
    }
}
