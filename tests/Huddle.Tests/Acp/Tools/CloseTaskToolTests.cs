using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="CloseTaskTool"/> (Spec §11.6): closing an Active Task, the already-closed
/// refusal, the §11.1 id refusals (including the R5 <c>NotFound</c> mapping for an unknown id), an
/// unknown caller refusing with nothing written, and the R4 "Could not save the task" path for a
/// racing file (corrections-B4 D10 item 5).
/// </summary>
public sealed class CloseTaskToolTests
{
    /// <summary>Creates a Task through the harness's real <see cref="TaskService"/>, as the Human actor, for a test to close.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string? assignee = null)
    {
        TaskActor actor = TaskActors.Human(harness.Options.Value);
        TaskDraft draft = new("Support SAML login", "Platform", null, Assignee: assignee);
        TaskResult result = harness.Service.Create(draft, actor);
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Builds a <see cref="CloseTaskTool"/> over <paramref name="harness"/>'s real collaborators, calling as <paramref name="callerAgentId"/>.</summary>
    private static CloseTaskTool CreateTool(TaskToolHarness harness, string callerAgentId) =>
        new(harness.Service, harness.Store, harness.Triggers, harness.Directory, new FakePromptSource(), callerAgentId);

    /// <summary>Narrows <see cref="TaskToolHarness.NovaId"/>, seeded by <see cref="TaskToolHarness.CreateAsync"/>, to a non-null caller id.</summary>
    private static string RequireNovaId(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return novaId;
    }

    /// <summary>Closing an Active Task moves it into "_closed" and reports success with the notify clause (Spec §11.6, §10.2).</summary>
    [Fact]
    public async Task InvokeAsync_ActiveTask_ClosesAndReturnsSuccessTextWithNotifyClause()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, assignee: "Kai");
        CloseTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Closed {task.Id}. Kai will be notified.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.True(stored.Location.Closed);
    }

    /// <summary>Closing an unassigned Task's notify clause says no one is notified (Spec §10.2, <see cref="WakeBlock.NoAssignee"/>).</summary>
    [Fact]
    public async Task InvokeAsync_UnassignedTask_SuccessTextSaysNoOneIsNotified()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        CloseTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Closed {task.Id}. No one is notified.", result);
    }

    /// <summary>Closing an already-Closed Task is refused, naming it, and leaves it as it was (Spec §11.6).</summary>
    [Fact]
    public async Task InvokeAsync_AlreadyClosed_ReturnsAlreadyClosedRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        TaskResult closeResult = harness.Service.Close(task.Id, TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(closeResult);
        CloseTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"{task.Id} is already closed.", result);
    }

    /// <summary>Text that doesn't parse as a <see cref="TaskId"/> is refused with the §11.1 wording.</summary>
    [Fact]
    public async Task InvokeAsync_TaskIdNotAnId_ReturnsRefusalText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CloseTaskTool tool = CreateTool(harness, RequireNovaId(harness));
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
        CloseTaskTool tool = CreateTool(harness, RequireNovaId(harness));
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
        TaskItem task = CreateTask(harness);
        CloseTaskTool tool = CreateTool(harness, "ghost-id");
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Could not identify caller 'ghost-id' as a Teammate.", result);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.False(stored.Location.Closed);
    }

    /// <summary>
    /// A save that collides with a racing file already at the "_closed" target path (R4 "Create
    /// collision", corrections-B4 D10 item 5) is turned into the §9.1 "Could not save the task" text,
    /// never thrown.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_MoveCollidesWithRacingFile_ReturnsCouldNotSaveText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        string closedDir = Path.Combine(harness.TasksDirPath, "Platform", "_closed");
        Directory.CreateDirectory(closedDir);
        File.WriteAllText(Path.Combine(closedDir, $"{task.Id}.md"), "racing file");
        harness.Store.RebuildFromWatcher();
        CloseTaskTool tool = CreateTool(harness, RequireNovaId(harness));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            $"Could not save the task: A file named {task.Id}.md already exists in {closedDir}.",
            result);
    }
}
