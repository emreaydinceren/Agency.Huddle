namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;

/// <summary>Covers Spec §11.2's <c>create_task</c> tool: its checks, in order, its success text, and that every expected failure is text, never a throw.</summary>
public sealed class CreateTaskToolTests
{
    /// <summary>A missing <c>title</c> is refused, naming the argument, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_MissingTitle_ReturnsRefusalNamingItAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["originRoomId"] = room.Id };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Created PLAT-0001 \"Support SAML login\" in Platform, unassigned. No one is notified.", result);
        TaskItem written = Assert.Single(harness.Store.All);
        Assert.Equal(room.Id, written.OriginRoomId);
    }

    /// <summary>A Won't do status is refused: the caller is told to create the Task first, then call <c>update_task</c>.</summary>
    [Fact]
    public async Task CreateTask_WontDoStatus_RefusesAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
        JsonObject arguments = new() { ["title"] = new string('x', 201), ["team"] = "Nonexistent Team" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            "Title is 201 characters; the limit is 200.\nUnknown team 'Nonexistent Team'. Known teams: Platform.",
            result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A successful create says who is assigned, and reports the notify clause <see cref="TaskTriggerService.Preview"/> resolves for it.</summary>
    [Fact]
    public async Task CreateTask_Success_AssignedToTeammate_ReportsCreatedAndNotified()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), "ghost-agent-id");
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Could not identify caller 'ghost-agent-id' as a Teammate.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A <c>blocked_by</c> that isn't a list is refused, naming the argument, rather than thrown.</summary>
    [Fact]
    public async Task CreateTask_BlockedByWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));
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
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, new FakePromptSource(), RequireId(harness.NovaId));

        JsonArray? required = tool.InputSchema["required"] as JsonArray;

        Assert.NotNull(required);
        List<string?> requiredNames = [.. required.Select(node => (string?)node)];
        Assert.Equal(["title", "team"], requiredNames);
    }

    private static string RequireId(string? id) => id ?? throw new InvalidOperationException("Expected a seeded id.");
}
