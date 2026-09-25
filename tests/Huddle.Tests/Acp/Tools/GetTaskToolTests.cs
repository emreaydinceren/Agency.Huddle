namespace Agency.Huddle.Tests.Acp.Tools;

using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;

/// <summary>Covers Spec §11.3's <c>get_task</c> tool: rendering one Task's full text, and its id refusals.</summary>
public sealed class GetTaskToolTests
{
    /// <summary>A missing <c>taskId</c> is refused, naming the argument.</summary>
    [Fact]
    public async Task GetTask_MissingTaskId_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        GetTaskTool tool = new(harness.Store, new FakePromptSource());

        string result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Equal("'taskId' is a required argument.", result);
    }

    /// <summary>Text that doesn't parse as a <see cref="TaskId"/> is refused with the §11.1 wording.</summary>
    [Fact]
    public async Task GetTask_NotAnId_ReturnsIdRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "not-an-id!" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'not-an-id!' is not a task id; ids look like PLAT-0042.", result);
    }

    /// <summary>An id that parses but names no known Task is refused with the §11.1 wording.</summary>
    [Fact]
    public async Task GetTask_UnknownId_ReturnsIdRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0999" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown task 'PLAT-0999'.", result);
    }

    /// <summary>The output starts with the exact §11.1 line for the Task.</summary>
    [Fact]
    public async Task GetTask_KnownId_OutputStartsWithTheLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        TaskItem task = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0001", title: "Support SAML login", assignee: "Kai", changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.StartsWith(TaskToolText.Line(task), result, StringComparison.Ordinal);
    }

    /// <summary>A Closed Task's line carries the "(closed)" suffix, exactly as <see cref="TaskToolText.Line"/> renders it.</summary>
    [Fact]
    public async Task GetTask_ClosedTask_LineShowsClosed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        TaskItem task = CreateInStore(harness.Store, TestTasks.Make(
            id: "PLAT-0001",
            location: new TaskLocation("Platform", null, true),
            changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("(closed)", result, StringComparison.Ordinal);
        Assert.StartsWith(TaskToolText.Line(task), result, StringComparison.Ordinal);
    }

    /// <summary>A non-empty field, such as tags, gets its own <c>key: value</c> line; an empty one, such as an unset parent, gets none.</summary>
    [Fact]
    public async Task GetTask_ShowsKeyValueLinesOnlyForNonEmptyFields()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(
            id: "PLAT-0001",
            tags: ["urgent"],
            parent: null,
            changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("tags: urgent", result, StringComparison.Ordinal);
        Assert.DoesNotContain("parent:", result, StringComparison.Ordinal);
    }

    /// <summary>A blocker is shown with its own current status, per the §11.3 example.</summary>
    [Fact]
    public async Task GetTask_BlockedBy_ShowsEachBlockersStatus()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0011", status: TaskState.Done, changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0030", status: TaskState.InProgress, changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        _ = CreateInStore(harness.Store, TestTasks.Make(
            id: "PLAT-0002",
            blockedBy: [TaskIdOf("PLAT-0011"), TaskIdOf("PLAT-0030")],
            changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0002" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("blocked_by: PLAT-0011 (Done), PLAT-0030 (In Progress)", result, StringComparison.Ordinal);
    }

    /// <summary>The description follows a blank line, after the id line and any <c>key: value</c> lines.</summary>
    [Fact]
    public async Task GetTask_DescriptionFollowsABlankLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(
            id: "PLAT-0001",
            description: "Some details.",
            changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("\n\nSome details.", result, StringComparison.Ordinal);
    }

    /// <summary>Without <c>include_change_log</c>, the Change log section is omitted entirely.</summary>
    [Fact]
    public async Task GetTask_WithoutIncludeChangeLog_OmitsChangeLogSection()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0001", changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.DoesNotContain("Change log:", result, StringComparison.Ordinal);
    }

    /// <summary><c>include_change_log</c> appends at most the last 50 entries, dropping the oldest.</summary>
    [Fact]
    public async Task GetTask_IncludeChangeLog_AppendsAtMostTheLastFifty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        List<ChangeLogEntry> entries = [];
        for (int number = 1; number <= 55; number++)
        {
            string summary = string.Create(CultureInfo.InvariantCulture, $"change-{number:D3}");
            entries.Add(new ChangeLogEntry(start.AddMinutes(number), "Nova", summary));
        }

        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0001", changeLog: entries));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001", ["include_change_log"] = true };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Change log:", result, StringComparison.Ordinal);
        Assert.DoesNotContain("change-001", result, StringComparison.Ordinal);
        Assert.DoesNotContain("change-005", result, StringComparison.Ordinal);
        Assert.Contains("change-006", result, StringComparison.Ordinal);
        Assert.Contains("change-055", result, StringComparison.Ordinal);

        int occurrences = 0;
        int index = 0;
        while ((index = result.IndexOf("change-", index, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            index += "change-".Length;
        }

        Assert.Equal(50, occurrences);
    }

    /// <summary>The Change log block's exact shape (Spec §11.3): a blank line, then "Change log:", then one line per entry via the §11.3 entry format, oldest first.</summary>
    [Fact]
    public async Task GetTask_IncludeChangeLog_ExactBlockShape()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        List<ChangeLogEntry> entries =
        [
            new ChangeLogEntry(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), "Nova", "created"),
            new ChangeLogEntry(new DateTimeOffset(2026, 9, 2, 10, 30, 0, TimeSpan.Zero), "Kai", "status: To Do -> In Progress"),
        ];
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0001", description: "Some details.", changeLog: entries));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001", ["include_change_log"] = true };

        string result = await tool.InvokeAsync(arguments, ct);

        const string expectedTail =
            "\n\nChange log:\n" +
            "2026-09-01 09:00 Nova: created\n" +
            "2026-09-02 10:30 Kai: status: To Do -> In Progress";
        Assert.EndsWith(expectedTail, result, StringComparison.Ordinal);
    }

    /// <summary>An <c>include_change_log</c> given as text rather than a boolean is refused, naming the argument, rather than thrown.</summary>
    [Fact]
    public async Task GetTask_IncludeChangeLogWrongType_ReturnsRefusalNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0001", changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001", ["include_change_log"] = "yes" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("include_change_log", result, StringComparison.Ordinal);
    }

    /// <summary>The Creator field, when set, gets its own <c>creator: value</c> line.</summary>
    [Fact]
    public async Task GetTask_ShowsCreator()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0001", creator: "Nova", changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("creator: Nova", result, StringComparison.Ordinal);
    }

    /// <summary>A Parent is shown as a reference with its own current status, exactly as <c>blocked_by</c> is (Spec §11.3).</summary>
    [Fact]
    public async Task GetTask_ParentShowsStatus()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0010", status: TaskState.Done, changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        _ = CreateInStore(harness.Store, TestTasks.Make(
            id: "PLAT-0002",
            parent: TaskIdOf("PLAT-0010"),
            changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0002" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("parent: PLAT-0010 (Done)", result, StringComparison.Ordinal);
    }

    /// <summary>A DuplicateOf reference is shown with its own current status, exactly as <c>blocked_by</c> is (Spec §11.3).</summary>
    [Fact]
    public async Task GetTask_DuplicateOfShowsStatus()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0020", status: TaskState.Rejected, changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        _ = CreateInStore(harness.Store, TestTasks.Make(
            id: "PLAT-0003",
            status: TaskState.Duplicate,
            duplicateOf: TaskIdOf("PLAT-0020"),
            changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0003" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("duplicate_of: PLAT-0020 (Rejected)", result, StringComparison.Ordinal);
    }

    /// <summary><c>start_date</c> and <c>due_date</c> get their own <c>key: value</c> lines, in <c>yyyy-MM-dd</c>.</summary>
    [Fact]
    public async Task GetTask_ShowsStartAndDueDateKeys()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(
            id: "PLAT-0001",
            startDate: new DateOnly(2026, 1, 15),
            dueDate: new DateOnly(2026, 2, 1),
            changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("start_date: 2026-01-15", result, StringComparison.Ordinal);
        Assert.Contains("due_date: 2026-02-01", result, StringComparison.Ordinal);
    }

    /// <summary>An empty field, such as unset tags, produces no line at all.</summary>
    [Fact]
    public async Task GetTask_NoTags_OmitsTagsLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        _ = CreateInStore(harness.Store, TestTasks.Make(id: "PLAT-0001", changeLog: [TestTasks.Entry("2026-09-01T09:00:00Z", "Nova", "created")]));
        GetTaskTool tool = new(harness.Store, new FakePromptSource());
        JsonObject arguments = new() { ["taskId"] = "PLAT-0001" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.DoesNotContain("tags:", result, StringComparison.Ordinal);
    }

    private static TaskId TaskIdOf(string text)
    {
        _ = TaskId.TryParse(text, out TaskId id);
        return id;
    }

    private static TaskItem CreateInStore(TaskStore store, TaskItem task)
    {
        TaskItem toWrite = task with { Path = TaskLayout.PathFor(store.RootDirectory, task.Location, task.Id) };
        return store.Create(toWrite, TaskFileFormat.Compose(toWrite))
            ?? throw new InvalidOperationException("Fixture collided with an existing file.");
    }
}
