using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="TaskToolText"/>, the shared helpers every App Tool (D10) reuses (Spec
/// §11.1): rendering a Task as one line, resolving a task id argument against a
/// <see cref="TaskStore"/>, resolving the calling Agent's <see cref="TaskActor"/>, phrasing the
/// notify clause from a <see cref="WakePreview"/>, parsing arguments without ever throwing, and
/// turning a <see cref="TaskService"/> exception into a refusal string (corrections-B4 D10 items
/// 2, 4 and 5 - settled to live here so 10.2-10.6 can build against one shared surface in
/// parallel).
/// </summary>
public sealed class TaskToolTextTests
{
    /// <summary>§11.1's worked example: every field present, in order, separated by " | ".</summary>
    [Fact]
    public void Line_FullTask_RendersIdStatusPriorityAssigneeLocationTitle()
    {
        TaskItem task = TestTasks.Make(
            id: "PLAT-0042",
            title: "Support SAML login",
            status: TaskState.InProgress,
            priority: TaskPriority.Urgent,
            assignee: "Nova",
            location: new TaskLocation("Platform", "Auth v2", Closed: false));

        string line = TaskToolText.Line(task);

        Assert.Equal("PLAT-0042 | In Progress | Urgent | Nova | Platform/Auth v2 | Support SAML login", line);
    }

    /// <summary>No assignee renders "unassigned"; no Project renders just the Team; Closed appends "(closed)".</summary>
    [Fact]
    public void Line_UnassignedNoProjectClosed_RendersUnassignedAndClosedTeam()
    {
        TaskItem task = TestTasks.Make(
            id: "PLAT-0007",
            title: "X",
            status: TaskState.Backlog,
            priority: TaskPriority.Low,
            assignee: null,
            location: new TaskLocation("Platform", null, Closed: true));

        string line = TaskToolText.Line(task);

        Assert.Equal("PLAT-0007 | Backlog | Low | unassigned | Platform (closed) | X", line);
    }

    /// <summary>Text that does not parse as a <see cref="TaskId"/> is refused with the §11.1 wording, and no Task is returned.</summary>
    [Fact]
    public void TryResolve_NotAnId_ReturnsRefusalAndNoTask()
    {
        using TaskToolHarness harness = new();

        bool resolved = TaskToolText.TryResolve(harness.Store, "not-an-id", out TaskItem? task, out string refusal);

        Assert.False(resolved);
        Assert.Null(task);
        Assert.Equal("'not-an-id' is not a task id; ids look like PLAT-0042.", refusal);
    }

    /// <summary>Text that parses as a <see cref="TaskId"/> but names no known Task is refused with the §11.1 wording.</summary>
    [Fact]
    public void TryResolve_UnknownId_ReturnsRefusalAndNoTask()
    {
        using TaskToolHarness harness = new();

        bool resolved = TaskToolText.TryResolve(harness.Store, "PLAT-9999", out TaskItem? task, out string refusal);

        Assert.False(resolved);
        Assert.Null(task);
        Assert.Equal("Unknown task 'PLAT-9999'.", refusal);
    }

    /// <summary>A known id resolves to the matching Task, with no refusal text.</summary>
    [Fact]
    public void TryResolve_KnownId_ReturnsTaskAndNoRefusal()
    {
        using TaskToolHarness harness = new();
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Existing");
        TestTaskStore.WriteTask(harness.TasksDirPath, "Platform/existing.md", task);
        harness.Store.RebuildFromWatcher();

        bool resolved = TaskToolText.TryResolve(harness.Store, "PLAT-0001", out TaskItem? found, out string refusal);

        Assert.True(resolved);
        Assert.NotNull(found);
        Assert.Equal(task.Id, found.Id);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>A known caller id resolves to an <see cref="TaskActorKind.Agent"/> <see cref="TaskActor"/> carrying the directory's Name.</summary>
    [Fact]
    public async Task ResolveActorAsync_KnownCaller_ReturnsAgentActorWithDirectoryName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);

        (TaskActor? actor, string? refusal) = await TaskToolText.ResolveActorAsync(directory, nova.Id, ct);

        Assert.NotNull(actor);
        Assert.Equal(TaskActorKind.Agent, actor.Kind);
        Assert.Equal("Nova", actor.Name);
        Assert.Equal(nova.Id, actor.UserId);
        Assert.Null(refusal);
    }

    /// <summary>An unknown caller id yields a refusal and no <see cref="TaskActor"/> — the raw id is never used as a Name.</summary>
    [Fact]
    public async Task ResolveActorAsync_UnknownCaller_ReturnsRefusalAndNoActor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);

        (TaskActor? actor, string? refusal) = await TaskToolText.ResolveActorAsync(directory, "ghost-id", ct);

        Assert.Null(actor);
        Assert.Equal("Could not identify caller 'ghost-id' as a Teammate.", refusal);
    }

    /// <summary><see cref="WakeBlock.None"/> notifies the named assignee.</summary>
    [Fact]
    public void NotifyClause_BlockNone_NamesTheAssignee()
    {
        WakePreview preview = new("Kai", PresenceState.Asleep, WakeBlock.None);

        string clause = TaskToolText.NotifyClause(preview, "Nova");

        Assert.Equal("Kai will be notified.", clause);
    }

    /// <summary><see cref="WakeBlock.NoAssignee"/> says no one is notified.</summary>
    [Fact]
    public void NotifyClause_BlockNoAssignee_SaysNoOneIsNotified()
    {
        WakePreview preview = new(null, null, WakeBlock.NoAssignee);

        string clause = TaskToolText.NotifyClause(preview, "Nova");

        Assert.Equal("No one is notified.", clause);
    }

    /// <summary><see cref="WakeBlock.AssigneeIsActor"/> (self-assign) says no one is notified.</summary>
    [Fact]
    public void NotifyClause_BlockAssigneeIsActor_SaysNoOneIsNotified()
    {
        WakePreview preview = new("Nova", null, WakeBlock.AssigneeIsActor);

        string clause = TaskToolText.NotifyClause(preview, "Nova");

        Assert.Equal("No one is notified.", clause);
    }

    /// <summary><see cref="WakeBlock.AssigneeIsHuman"/> tells the caller they aren't notified either.</summary>
    [Fact]
    public void NotifyClause_BlockAssigneeIsHuman_SaysYouArentNotified()
    {
        WakePreview preview = new("You", null, WakeBlock.AssigneeIsHuman);

        string clause = TaskToolText.NotifyClause(preview, "Nova");

        Assert.Equal("You aren't notified; the Human sees it on the Tasks page.", clause);
    }

    /// <summary><see cref="WakeBlock.BudgetPaused"/> names the paused wake budget.</summary>
    [Fact]
    public void NotifyClause_BlockBudgetPaused_SaysWakeUpsArePaused()
    {
        WakePreview preview = new("Kai", null, WakeBlock.BudgetPaused);

        string clause = TaskToolText.NotifyClause(preview, "Nova");

        Assert.Equal("No one is notified: wake-ups for this task are paused.", clause);
    }

    /// <summary><see cref="WakeBlock.Disabled"/> says wake-ups are off.</summary>
    [Fact]
    public void NotifyClause_BlockDisabled_SaysWakeUpsAreOff()
    {
        WakePreview preview = new("Kai", null, WakeBlock.Disabled);

        string clause = TaskToolText.NotifyClause(preview, "Nova");

        Assert.Equal("No one is notified: wake-ups are off.", clause);
    }

    /// <summary>A present, correctly-typed string argument is returned, with no refusal.</summary>
    [Fact]
    public void TryGetString_PresentString_ReturnsValue()
    {
        JsonObject arguments = new() { ["title"] = "Support SAML login" };

        bool ok = TaskToolText.TryGetString(arguments, "title", out string? value, out string refusal);

        Assert.True(ok);
        Assert.Equal("Support SAML login", value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>An absent string argument is not an error: value is null and there is no refusal (the caller decides whether it was required).</summary>
    [Fact]
    public void TryGetString_Missing_ReturnsNullValueAndNoRefusal()
    {
        JsonObject arguments = [];

        bool ok = TaskToolText.TryGetString(arguments, "title", out string? value, out string refusal);

        Assert.True(ok);
        Assert.Null(value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>A string argument holding a number is refused, naming the argument and the expected type, never thrown.</summary>
    [Fact]
    public void TryGetString_WrongType_ReturnsRefusalNamingArgument()
    {
        JsonObject arguments = new() { ["title"] = 42 };

        bool ok = TaskToolText.TryGetString(arguments, "title", out string? value, out string refusal);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("'title' must be text.", refusal);
    }

    /// <summary>A present, correctly-typed integer argument is returned, with no refusal.</summary>
    [Fact]
    public void TryGetInt_PresentInt_ReturnsValue()
    {
        JsonObject arguments = new() { ["limit"] = 25 };

        bool ok = TaskToolText.TryGetInt(arguments, "limit", 50, out int value, out string refusal);

        Assert.True(ok);
        Assert.Equal(25, value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>An absent optional integer argument (a default supplied) falls back to that default, with no refusal.</summary>
    [Fact]
    public void TryGetInt_MissingWithDefault_ReturnsDefault()
    {
        JsonObject arguments = [];

        bool ok = TaskToolText.TryGetInt(arguments, "limit", 50, out int value, out string refusal);

        Assert.True(ok);
        Assert.Equal(50, value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>An absent required integer argument (no default supplied) is refused, naming the argument.</summary>
    [Fact]
    public void TryGetInt_MissingRequired_ReturnsRefusal()
    {
        JsonObject arguments = [];

        bool ok = TaskToolText.TryGetInt(arguments, "count", null, out int value, out string refusal);

        Assert.False(ok);
        Assert.Equal(0, value);
        Assert.Equal("'count' is a required argument.", refusal);
    }

    /// <summary>An integer argument holding text is refused, naming the argument and the expected type, never thrown.</summary>
    [Fact]
    public void TryGetInt_WrongType_ReturnsRefusalNamingArgument()
    {
        JsonObject arguments = new() { ["limit"] = "not-a-number" };

        bool ok = TaskToolText.TryGetInt(arguments, "limit", 50, out int value, out string refusal);

        Assert.False(ok);
        Assert.Equal(50, value);
        Assert.Equal("'limit' must be a whole number.", refusal);
    }

    /// <summary>A present, correctly-typed boolean argument is returned, with no refusal.</summary>
    [Fact]
    public void TryGetBool_PresentBool_ReturnsValue()
    {
        JsonObject arguments = new() { ["include_change_log"] = true };

        bool ok = TaskToolText.TryGetBool(arguments, "include_change_log", false, out bool value, out string refusal);

        Assert.True(ok);
        Assert.True(value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>An absent optional boolean argument (a default supplied) falls back to that default, with no refusal.</summary>
    [Fact]
    public void TryGetBool_MissingWithDefault_ReturnsDefault()
    {
        JsonObject arguments = [];

        bool ok = TaskToolText.TryGetBool(arguments, "include_change_log", false, out bool value, out string refusal);

        Assert.True(ok);
        Assert.False(value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>An absent required boolean argument (no default supplied) is refused, naming the argument.</summary>
    [Fact]
    public void TryGetBool_MissingRequired_ReturnsRefusal()
    {
        JsonObject arguments = [];

        bool ok = TaskToolText.TryGetBool(arguments, "confirm", null, out bool value, out string refusal);

        Assert.False(ok);
        Assert.False(value);
        Assert.Equal("'confirm' is a required argument.", refusal);
    }

    /// <summary>A boolean argument holding text is refused, naming the argument and the expected type, never thrown.</summary>
    [Fact]
    public void TryGetBool_WrongType_ReturnsRefusalNamingArgument()
    {
        JsonObject arguments = new() { ["include_change_log"] = "yes" };

        bool ok = TaskToolText.TryGetBool(arguments, "include_change_log", false, out bool value, out string refusal);

        Assert.False(ok);
        Assert.False(value);
        Assert.Equal("'include_change_log' must be true or false.", refusal);
    }

    /// <summary>A present array of strings is returned in full, with no refusal.</summary>
    [Fact]
    public void TryGetStringList_PresentArray_ReturnsItems()
    {
        JsonObject arguments = new() { ["tags"] = new JsonArray { "urgent", "auth" } };

        bool ok = TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? value, out string refusal);

        Assert.True(ok);
        Assert.NotNull(value);
        Assert.Equal(["urgent", "auth"], value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>An absent list argument is not an error: value is null and there is no refusal.</summary>
    [Fact]
    public void TryGetStringList_Missing_ReturnsNullValueAndNoRefusal()
    {
        JsonObject arguments = [];

        bool ok = TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? value, out string refusal);

        Assert.True(ok);
        Assert.Null(value);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>A list argument that isn't an array is refused, naming the argument and the expected type.</summary>
    [Fact]
    public void TryGetStringList_NotAnArray_ReturnsRefusalNamingArgument()
    {
        JsonObject arguments = new() { ["tags"] = "urgent" };

        bool ok = TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? value, out string refusal);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("'tags' must be a list of text.", refusal);
    }

    /// <summary>An array containing a non-string item is refused, naming the argument and the expected type.</summary>
    [Fact]
    public void TryGetStringList_ItemWrongType_ReturnsRefusalNamingArgument()
    {
        JsonObject arguments = new() { ["tags"] = new JsonArray { "urgent", 42 } };

        bool ok = TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? value, out string refusal);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("'tags' must be a list of text.", refusal);
    }

    /// <summary>§9.1's exact wording, carrying the exception's own message through unchanged.</summary>
    [Fact]
    public void SaveFailed_AnyException_FormatsTheSaveFailedWording()
    {
        InvalidOperationException exception = new("id PLAT-0001 already exists");

        string text = TaskToolText.SaveFailed(exception);

        Assert.Equal("Could not save the task: id PLAT-0001 already exists", text);
    }

    /// <summary>A successful <see cref="TaskService"/> call returns its result, with no refusal.</summary>
    [Fact]
    public void TryRun_OperationSucceeds_ReturnsResultAndNoRefusal()
    {
        TaskItem task = TestTasks.Make();

        bool ok = TaskToolText.TryRun(() => new TaskResult.Unchanged(task), out TaskResult? result, out string refusal);

        Assert.True(ok);
        Assert.IsType<TaskResult.Unchanged>(result);
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>The R4 "Create collision" path: an <see cref="InvalidOperationException"/> from the service becomes the §9.1 refusal text, never a thrown exception.</summary>
    [Fact]
    public void TryRun_InvalidOperationException_ReturnsSaveFailedRefusal()
    {
        bool ok = TaskToolText.TryRun(
            TaskResult () => throw new InvalidOperationException("no id could be allocated"),
            out TaskResult? result,
            out string refusal);

        Assert.False(ok);
        Assert.Null(result);
        Assert.Equal("Could not save the task: no id could be allocated", refusal);
    }

    /// <summary>The R4 "Create collision" path: an <see cref="IOException"/> from the store becomes the §9.1 refusal text, never a thrown exception.</summary>
    [Fact]
    public void TryRun_IOException_ReturnsSaveFailedRefusal()
    {
        bool ok = TaskToolText.TryRun(
            TaskResult () => throw new IOException("A file named PLAT-0001.md already exists in Platform"),
            out TaskResult? result,
            out string refusal);

        Assert.False(ok);
        Assert.Null(result);
        Assert.Equal("Could not save the task: A file named PLAT-0001.md already exists in Platform", refusal);
    }
}
