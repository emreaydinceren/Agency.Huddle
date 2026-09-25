using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="ReopenTaskTool"/> (Spec §11.6): reopening a Closed Task, the already-active
/// refusal, the §11.1 id refusals (including the R5 <c>NotFound</c> mapping for an unknown id), an
/// unknown caller refusing with nothing written, and the R4 "Could not save the task" path for a
/// racing file (corrections-B4 D10 item 5).
/// </summary>
public sealed class ReopenTaskToolTests
{
    /// <summary>Creates a Task through the harness's real <see cref="TaskService"/>, as the Human actor, for a test to reopen.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string? assignee = null)
    {
        TaskActor actor = TaskActors.Human(harness.Options.Value);
        TaskDraft draft = new("Support SAML login", "Platform", null, Assignee: assignee);
        TaskResult result = harness.Service.Create(draft, actor);
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Closes <paramref name="task"/> directly through the harness's real <see cref="TaskService"/>, so a test starts from a Closed Task without going through <see cref="ReopenTaskTool"/> itself.</summary>
    private static TaskItem CloseTask(TaskToolHarness harness, TaskItem task)
    {
        TaskResult result = harness.Service.Close(task.Id, TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Builds a <see cref="ReopenTaskTool"/> over <paramref name="harness"/>'s real collaborators, calling as <paramref name="callerAgentId"/>.</summary>
    private static ReopenTaskTool CreateTool(TaskToolHarness harness, string callerAgentId) =>
        new(harness.Service, harness.Store, harness.Triggers, harness.Directory, new FakePromptSource(), callerAgentId);

    /// <summary>Narrows <see cref="TaskToolHarness.NovaId"/>, seeded by <see cref="TaskToolHarness.CreateAsync"/>, to a non-null caller id.</summary>
    private static string RequireNovaId(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return novaId;
    }

    /// <summary>Reopening a Closed Task moves it back out of "_closed" and reports success with the notify clause (Spec §11.6, §10.2).</summary>
    [Fact]
    public async Task InvokeAsync_ClosedTask_ReopensAndReturnsSuccessTextWithNotifyClause()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CloseTask(harness, CreateTask(harness, assignee: "Kai"));
        ReopenTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Reopened {task.Id}. Kai will be notified.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.False(stored.Location.Closed);
    }

    /// <summary>Reopening an Active Task is refused, naming it, and leaves it as it was (Spec §11.6).</summary>
    [Fact]
    public async Task InvokeAsync_AlreadyActive_ReturnsAlreadyActiveRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        ReopenTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"{task.Id} is already active.", result);
    }

    /// <summary>Text that doesn't parse as a <see cref="TaskId"/> is refused with the §11.1 wording.</summary>
    [Fact]
    public async Task InvokeAsync_TaskIdNotAnId_ReturnsRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ReopenTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = "not-an-id" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'not-an-id' is not a task id; ids look like PLAT-0042.", result);
    }

    /// <summary>An id that parses but names no known Task is refused with the §11.1 wording (R5: the service reports <c>NotFound</c>).</summary>
    [Fact]
    public async Task InvokeAsync_TaskIdUnknown_ReturnsRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ReopenTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = "PLAT-9999" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown task 'PLAT-9999'.", result);
    }

    /// <summary>A caller id the directory doesn't know is refused, and nothing is written.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownCaller_ReturnsRefusalAndTaskUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CloseTask(harness, CreateTask(harness));
        ReopenTaskTool tool = CreateTool(harness, "ghost-id");
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Could not identify caller 'ghost-id' as a Teammate.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.True(stored.Location.Closed);
    }

    /// <summary>
    /// A save that collides with a racing file already at the active target path (R4 "Create
    /// collision", corrections-B4 D10 item 5) is turned into the §9.1 "Could not save the task" text,
    /// never thrown.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_MoveCollidesWithRacingFile_ReturnsCouldNotSaveText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CloseTask(harness, CreateTask(harness));
        string activeDir = Path.Combine(harness.TasksDirPath, "Platform");
        Directory.CreateDirectory(activeDir);
        File.WriteAllText(Path.Combine(activeDir, $"{task.Id}.md"), "racing file");
        harness.Store.RebuildFromWatcher();
        ReopenTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            $"Could not save the task: A file named {task.Id}.md already exists in {activeDir}.",
            result);
    }
}
