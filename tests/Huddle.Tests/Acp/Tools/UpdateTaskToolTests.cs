using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="UpdateTaskTool"/> (Spec §11.5): every patchable argument sets its field
/// (including the <c>Optional&lt;T&gt;</c> "clear vs. leave" distinction for assignee, parent,
/// project and due date), the "nothing to change" and <c>Unchanged</c> texts, a refusal listing
/// every problem, the success text pairing <see cref="TaskDiff.Summarise"/>'s wording with the
/// notify clause, the §11.1 id refusals, an unknown caller refusing with nothing written, every
/// argument's wrong-type refusal (corrections-B4 D10 item 4), and the R4 "Could not save the task"
/// path for a racing file (corrections-B4 D10 item 5).
/// </summary>
public sealed class UpdateTaskToolTests
{
    /// <summary>Creates a Task through the harness's real <see cref="TaskService"/>, as the Human actor, for a test to update.</summary>
    private static TaskItem CreateTask(
        TaskToolHarness harness,
        string title = "Support SAML login",
        string team = "Platform",
        string? project = null,
        TaskState status = TaskState.ToDo,
        TaskPriority priority = TaskPriority.Medium,
        string? assignee = null,
        DateOnly? dueDate = null)
    {
        TaskActor actor = TaskActors.Human(harness.Options.Value);
        TaskDraft draft = new(title, team, project, status, priority, assignee, DueDate: dueDate);
        TaskResult result = harness.Service.Create(draft, actor);
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Builds an <see cref="UpdateTaskTool"/> over <paramref name="harness"/>'s real collaborators, calling as <paramref name="callerAgentId"/>.</summary>
    private static UpdateTaskTool CreateTool(TaskToolHarness harness, string callerAgentId) =>
        new(harness.Service, harness.Store, harness.Triggers, harness.Directory, new FakePromptSource(), callerAgentId);

    /// <summary>Narrows <see cref="TaskToolHarness.NovaId"/>, seeded by <see cref="TaskToolHarness.CreateAsync"/>, to a non-null caller id.</summary>
    private static string RequireNovaId(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return novaId;
    }

    /// <summary>A patch with only <c>taskId</c> is refused before touching the store (Spec §11.5).</summary>
    [Fact]
    public async Task InvokeAsync_NoFieldBesidesTaskId_ReturnsNothingToChangeRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Nothing to change: pass at least one field besides taskId.", result);
    }

    /// <summary>A resubmitted patch equal to the Task's current values returns the Spec §11.5 <c>Unchanged</c> text.</summary>
    [Fact]
    public async Task InvokeAsync_PatchEqualsCurrentValues_ReturnsUnchangedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, title: "Support SAML login");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["title"] = "Support SAML login" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"{task.Id} already has those values; nothing changed.", result);
    }

    /// <summary>The Spec §11.5 worked example: two field changes summarised and joined with the notify clause.</summary>
    [Fact]
    public async Task InvokeAsync_StatusAndPriorityChange_ReturnsSummaryAndNotifyClause()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, status: TaskState.ToDo, priority: TaskPriority.Medium, assignee: "Kai");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["status"] = "In Progress",
            ["priority"] = "High",
        };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            $"Updated {task.Id}: status: To Do → In Progress; priority: Medium → High. Kai will be notified.",
            result);
    }

    /// <summary>A refusal lists every violated rule, joined with "\n" (Spec §11.1).</summary>
    [Fact]
    public async Task InvokeAsync_MultipleProblems_ReturnsEveryProblemJoinedByNewline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        TaskItem other = CreateTask(harness, title: "Other");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["assignee"] = "Ghost",
            ["duplicate_of"] = other.Id.ToString(),
        };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            "Unknown teammate 'Ghost'.\nduplicate_of is only allowed when status is Duplicate.",
            result);
    }

    /// <summary>The <c>title</c> argument sets <see cref="TaskItem.Title"/>.</summary>
    [Fact]
    public async Task InvokeAsync_TitleGiven_SetsTitle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["title"] = "Renamed title" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("Renamed title", updated.Title);
    }

    /// <summary>The <c>description</c> argument sets <see cref="TaskItem.Description"/>.</summary>
    [Fact]
    public async Task InvokeAsync_DescriptionGiven_SetsDescription()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["description"] = "New details." };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("New details.", updated.Description);
    }

    /// <summary>The <c>status</c> argument sets <see cref="TaskItem.Status"/>.</summary>
    [Fact]
    public async Task InvokeAsync_StatusGiven_SetsStatus()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, status: TaskState.ToDo);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["status"] = "Review" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(TaskState.Review, updated.Status);
    }

    /// <summary>The <c>priority</c> argument sets <see cref="TaskItem.Priority"/>.</summary>
    [Fact]
    public async Task InvokeAsync_PriorityGiven_SetsPriority()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, priority: TaskPriority.Low);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["priority"] = "Urgent" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(TaskPriority.Urgent, updated.Priority);
    }

    /// <summary>The <c>assignee</c> argument sets <see cref="TaskItem.Assignee"/> to a named Teammate.</summary>
    [Fact]
    public async Task InvokeAsync_AssigneeGiven_SetsAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["assignee"] = "Kai" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("Kai", updated.Assignee);
    }

    /// <summary>An empty-string <c>assignee</c> clears it (<c>Optional&lt;T&gt;.Set(null)</c>), Spec §11.5.</summary>
    [Fact]
    public async Task InvokeAsync_AssigneeEmptyString_ClearsAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, assignee: "Kai");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["assignee"] = "" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.Assignee);
    }

    /// <summary>An absent <c>assignee</c> argument leaves the field unchanged: the "leave" half of the Optional&lt;T&gt; distinction that "" (the "clear" half) proves above.</summary>
    [Fact]
    public async Task InvokeAsync_AssigneeArgumentAbsent_LeavesCurrentAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, assignee: "Kai");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["title"] = "Renamed, assignee untouched" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("Kai", updated.Assignee);
    }

    /// <summary><c>assignee: "me"</c> resolves to the caller's own Name (Spec §11.5).</summary>
    [Fact]
    public async Task InvokeAsync_AssigneeMe_AssignsCaller()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["assignee"] = "me" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("Nova", updated.Assignee);
    }

    /// <summary>The <c>team</c> argument moves the Task to a different, already-known Team folder.</summary>
    [Fact]
    public async Task InvokeAsync_TeamGiven_MovesToNewTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Growth"));
        harness.Store.RebuildFromWatcher();
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["team"] = "Growth" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("Growth", updated.Location.Team);
    }

    /// <summary>The <c>project</c> argument sets <see cref="TaskLocation.Project"/> under the current Team.</summary>
    [Fact]
    public async Task InvokeAsync_ProjectGiven_SetsProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["project"] = "Auth v2" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("Auth v2", updated.Location.Project);
    }

    /// <summary>An empty-string <c>project</c> means no Project (Spec §11.5).</summary>
    [Fact]
    public async Task InvokeAsync_ProjectEmptyString_ClearsProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, project: "Auth v2");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["project"] = "" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.Location.Project);
    }

    /// <summary>The <c>parent</c> argument sets <see cref="TaskItem.Parent"/> to an existing Task's id.</summary>
    [Fact]
    public async Task InvokeAsync_ParentGiven_SetsParent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        TaskItem parent = CreateTask(harness, title: "Parent");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["parent"] = parent.Id.ToString() };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(parent.Id, updated.Parent);
    }

    /// <summary>An empty-string <c>parent</c> clears it (<c>Optional&lt;T&gt;.Set(null)</c>), Spec §11.5.</summary>
    [Fact]
    public async Task InvokeAsync_ParentEmptyString_ClearsParent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem parent = CreateTask(harness, title: "Parent");
        TaskItem task = CreateTask(harness, title: "Child");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject setParent = new() { ["taskId"] = task.Id.ToString(), ["parent"] = parent.Id.ToString() };
        await tool.InvokeAsync(setParent, ct);
        JsonObject clearParent = new() { ["taskId"] = task.Id.ToString(), ["parent"] = "" };

        await tool.InvokeAsync(clearParent, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.Parent);
    }

    /// <summary>The <c>blocked_by</c> argument replaces the whole list, rather than appending to it (Spec §11.5).</summary>
    [Fact]
    public async Task InvokeAsync_BlockedByGivenTwice_ReplacesWholeList()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        TaskItem first = CreateTask(harness, title: "Blocker one");
        TaskItem second = CreateTask(harness, title: "Blocker two");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject firstArguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["blocked_by"] = new JsonArray { first.Id.ToString() },
        };
        await tool.InvokeAsync(firstArguments, ct);
        JsonObject secondArguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["blocked_by"] = new JsonArray { second.Id.ToString() },
        };

        await tool.InvokeAsync(secondArguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal([second.Id], updated.BlockedBy);
    }

    /// <summary>The <c>tags</c> argument replaces the whole list, rather than appending to it (Spec §11.5).</summary>
    [Fact]
    public async Task InvokeAsync_TagsGivenTwice_ReplacesWholeList()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject firstArguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["tags"] = new JsonArray { "urgent", "auth" },
        };
        await tool.InvokeAsync(firstArguments, ct);
        JsonObject secondArguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["tags"] = new JsonArray { "ops" },
        };

        await tool.InvokeAsync(secondArguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(["ops"], updated.Tags);
    }

    /// <summary>The <c>start_date</c> argument sets <see cref="TaskItem.StartDate"/>, parsed as <c>yyyy-MM-dd</c>.</summary>
    [Fact]
    public async Task InvokeAsync_StartDateGiven_SetsStartDate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["start_date"] = "2026-01-15" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(new DateOnly(2026, 1, 15), updated.StartDate);
    }

    /// <summary>The <c>due_date</c> argument sets <see cref="TaskItem.DueDate"/>, parsed as <c>yyyy-MM-dd</c>.</summary>
    [Fact]
    public async Task InvokeAsync_DueDateGiven_SetsDueDate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["due_date"] = "2026-02-01" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(new DateOnly(2026, 2, 1), updated.DueDate);
    }

    /// <summary>An empty-string <c>due_date</c> clears it (Spec §11.5).</summary>
    [Fact]
    public async Task InvokeAsync_DueDateEmptyString_ClearsDueDate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, dueDate: new DateOnly(2026, 2, 1));
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["due_date"] = "" };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.DueDate);
    }

    /// <summary>
    /// <c>duplicate_of</c> sets <see cref="TaskItem.DuplicateOf"/> when paired with <c>status: "Duplicate"</c>,
    /// the only combination Spec §9.2 allows.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_StatusDuplicateWithDuplicateOf_SetsDuplicateOf()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        TaskItem original = CreateTask(harness, title: "Original");
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["status"] = "Duplicate",
            ["duplicate_of"] = original.Id.ToString(),
        };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(original.Id, updated.DuplicateOf);
    }

    /// <summary>A <c>reason</c> given with a Cancelled status is recorded in the Change log summary, never as a stored field (Spec §9.1).</summary>
    [Fact]
    public async Task InvokeAsync_ReasonWithCancelledStatus_RecordedInChangeLog()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new()
        {
            ["taskId"] = task.Id.ToString(),
            ["status"] = "Cancelled",
            ["reason"] = "no longer needed",
        };

        await tool.InvokeAsync(arguments, ct);

        TaskItem? updated = harness.Store.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("status: To Do → Cancelled (reason: no longer needed)", updated.ChangeLog[^1].Summary);
    }

    /// <summary>Text that doesn't parse as a <see cref="TaskId"/> is refused with the §11.1 wording.</summary>
    [Fact]
    public async Task InvokeAsync_TaskIdNotAnId_ReturnsRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = "not-an-id", ["title"] = "X" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'not-an-id' is not a task id; ids look like PLAT-0042.", result);
    }

    /// <summary>Text that parses as a <see cref="TaskId"/> but names no known Task is refused with the §11.1 wording.</summary>
    [Fact]
    public async Task InvokeAsync_TaskIdUnknown_ReturnsRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = "PLAT-9999", ["title"] = "X" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown task 'PLAT-9999'.", result);
    }

    /// <summary>A caller id the directory doesn't know is refused, and nothing is written.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownCaller_ReturnsRefusalAndTaskUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, "ghost-id");
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["title"] = "Should not apply" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Could not identify caller 'ghost-id' as a Teammate.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Equal(task.Title, stored.Title);
    }

    /// <summary>A string-typed argument holding the wrong JSON type is refused, naming the argument (corrections-B4 D10 item 4).</summary>
    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("status")]
    [InlineData("priority")]
    [InlineData("assignee")]
    [InlineData("team")]
    [InlineData("project")]
    [InlineData("parent")]
    [InlineData("duplicate_of")]
    [InlineData("start_date")]
    [InlineData("due_date")]
    [InlineData("reason")]
    public async Task InvokeAsync_StringArgumentWrongType_ReturnsRefusalNamingArgument(string argumentName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), [argumentName] = 42 };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"'{argumentName}' must be text.", result);
    }

    /// <summary>A list-typed argument holding a non-array JSON value is refused, naming the argument (corrections-B4 D10 item 4).</summary>
    [Theory]
    [InlineData("blocked_by")]
    [InlineData("tags")]
    public async Task InvokeAsync_ListArgumentWrongType_ReturnsRefusalNamingArgument(string argumentName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), [argumentName] = "not-a-list" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"'{argumentName}' must be a list of text.", result);
    }

    /// <summary>
    /// A save that collides with a racing file (R4 "Create collision", corrections-B4 D10 item 5)
    /// is turned into the §9.1 "Could not save the task" text, never thrown.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_MoveCollidesWithRacingFile_ReturnsCouldNotSaveText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        string growthDir = Path.Combine(harness.TasksDirPath, "Growth");
        Directory.CreateDirectory(growthDir);
        File.WriteAllText(Path.Combine(growthDir, $"{task.Id}.md"), "racing file");
        harness.Store.RebuildFromWatcher();
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["team"] = "Growth" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            $"Could not save the task: A file named {task.Id}.md already exists in {growthDir}.",
            result);
    }

    /// <summary>An unknown <c>status</c> value is refused with the shared D10 enum wording, naming the argument (settled across list_tasks and update_task).</summary>
    [Fact]
    public async Task InvokeAsync_StatusUnknownValue_ReturnsRefusalNamingArgument()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["status"] = "Sideways" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'status' has an unknown value 'Sideways'.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Equal(task.Status, stored.Status);
    }

    /// <summary>An unknown <c>priority</c> value is refused with the shared D10 enum wording, naming the argument (settled across list_tasks and update_task).</summary>
    [Fact]
    public async Task InvokeAsync_PriorityUnknownValue_ReturnsRefusalNamingArgument()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["priority"] = "Sideways" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'priority' has an unknown value 'Sideways'.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Equal(task.Priority, stored.Priority);
    }

    /// <summary>A <c>due_date</c> that isn't <c>yyyy-MM-dd</c> is refused, naming the bad value and the expected format.</summary>
    [Fact]
    public async Task InvokeAsync_DueDateNotYyyyMmDd_ReturnsRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["due_date"] = "02/01/2026" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'due_date' must be a date in the form yyyy-MM-dd.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.DueDate);
    }

    /// <summary>A <c>start_date</c> that isn't <c>yyyy-MM-dd</c> is refused with the shared D10 date wording, naming the argument (settled to match create_task).</summary>
    [Fact]
    public async Task InvokeAsync_StartDateNotYyyyMmDd_ReturnsRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["start_date"] = "02/01/2026" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'start_date' must be a date in the form yyyy-MM-dd.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.StartDate);
    }
}
