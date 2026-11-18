using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Saving Done in the Task panel's form on a Human-gated Task without a valid Accept shows the reason in the
/// panel's warning and leaves the status as it was (Human gate Spec H-4, §6.8; Gitea #391).
/// </summary>
public sealed class TaskDetailGatedDoneTests
{
    private const string NeedsAcceptance = "needs You's acceptance. Move it to Needs You when the Proof is complete; you'll be woken when You decides.";

    /// <summary>Creates a Task with the Human gate on, in <paramref name="status"/>.</summary>
    private static TaskItem CreateGated(TaskToolHarness harness, TaskState status) =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft("Gate check", "Platform", null, status, Assignee: "Nova") { HumanGate = true },
            TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>A gated Task in Backlog and one In Progress: Save with Done shows the H-4 reason and writes nothing.</summary>
    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.InProgress)]
    public async Task Save_DoneOnGatedTaskWithNoAccept_ShowsTheReasonAndKeepsTheStatus(TaskState start)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateGated(harness, start);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id);

        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Equal($"{task.Id} {NeedsAcceptance}", TextOf(cut, ".task-detail-gate-refusal .task-detail-gate-refusal-text"));
        Assert.Empty(cut.FindAll(".task-detail-gate-override"));
        Assert.Equal(start, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>An ungated Task In Progress saves Done with no warning.</summary>
    [Fact]
    public async Task Save_DoneOnUngatedTask_Saves()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft("Plain", "Platform", null, TaskState.InProgress, Assignee: "Nova"),
            TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id);

        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Empty(cut.FindAll(".task-detail-gate-refusal"));
        Assert.Equal(TaskState.Done, harness.Store.Get(task.Id)?.Status);
    }
}
