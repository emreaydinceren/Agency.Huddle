using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// The Task panel shows a process gate's refusal of a status change, offers the Human a confirmed
/// "Move anyway", and lists the Team's enabled gates in its header (PR-7, Spec §Gates).
/// </summary>
public sealed class TaskGateRefusalTests
{
    private const string GateId = "qa-evidence-before-done";
    private const string ConfirmText = "This skips the qa-evidence-before-done gate and is logged in the Change log.";

    /// <summary>Saving a move to Done that the QA gate refuses shows the gate's message in an alert and writes nothing.</summary>
    [Fact]
    public async Task Save_GateRefuses_ShowsTheRefusalAndKeepsTheTask()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)));
        TaskItem task = CreateTask(harness, status: TaskState.Backlog);
        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Equal(TaskGateText.QaDone, cut.Find(".task-detail-gate-refusal .task-detail-gate-refusal-text").TextContent.Trim());
        Assert.Equal(TaskState.Backlog, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Move anyway asks the confirm text, and confirming repeats the change as the Human and logs the override.</summary>
    [Fact]
    public async Task MoveAnyway_Confirmed_SavesAsHumanAndLogsTheOverride()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)));
        TaskItem task = CreateTask(harness, status: TaskState.Backlog);
        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        _ = cut.InvokeAsync(() => cut.Find(".task-detail-gate-override").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        Assert.Equal(ConfirmText, cut.Find(".mud-dialog-content").TextContent.Trim());
        await cut.InvokeAsync(() => DialogButton(cut, "Move anyway").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(TaskState.Done, harness.Store.Get(task.Id)?.Status));
        TaskItem saved = Assert.IsType<TaskItem>(harness.Store.Get(task.Id));
        Assert.EndsWith("(gate overridden by Human: qa-evidence-before-done)", saved.ChangeLog[^1].Summary);
        Assert.Empty(cut.FindAll(".task-detail-gate-refusal"));
    }

    /// <summary>Creating a Task straight into Done that the QA gate refuses shows the refusal, and a confirmed Move anyway creates it as the Human.</summary>
    [Fact]
    public async Task Create_InGatedStatus_MoveAnywayConfirmed_CreatesTheTask()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)));
        await using MudBunitContext ctx = NewContext(harness);
        TaskDraft draft = new("Brand new", "Platform", null, Status: TaskState.Done);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Draft), draft);
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Panel);
            builder.CloseComponent();
        });
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());
        Assert.Equal(TaskGateText.QaDone, cut.Find(".task-detail-gate-refusal .task-detail-gate-refusal-text").TextContent.Trim());
        Assert.Empty(harness.Store.All);

        _ = cut.InvokeAsync(() => cut.Find(".task-detail-gate-override").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => DialogButton(cut, "Move anyway").ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(harness.Store.All));
        TaskItem created = Assert.Single(harness.Store.All);
        Assert.Equal("Brand new", created.Title);
        Assert.Equal(TaskState.Done, created.Status);
        Assert.EndsWith("(gate overridden by Human: qa-evidence-before-done)", created.ChangeLog[^1].Summary);
    }

    /// <summary>Cancelling the confirm changes nothing: the Task stays put and the refusal stays on screen.</summary>
    [Fact]
    public async Task MoveAnyway_Cancelled_DoesNothing()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)));
        TaskItem task = CreateTask(harness, status: TaskState.Backlog);
        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        _ = cut.InvokeAsync(() => cut.Find(".task-detail-gate-override").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => DialogButton(cut, "Cancel").ClickAsync());

        Assert.Equal(TaskState.Backlog, harness.Store.Get(task.Id)?.Status);
        Assert.Single(cut.FindAll(".task-detail-gate-refusal"));
    }

    /// <summary>The header lists the Team's enabled gates.</summary>
    [Fact]
    public async Task Header_TeamWithGates_ListsTheEnabledGates()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)));
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);

        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Equal("Gates: " + GateId, TextOf(cut, ".task-detail-gates"));
    }

    /// <summary>A Team with no process gates shows no Gates line.</summary>
    [Fact]
    public async Task Header_NoGates_ShowsNoGatesLine()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);

        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Empty(cut.FindAll(".task-detail-gates"));
    }

    /// <summary>The button in the open dialog whose text is exactly <paramref name="text"/>.</summary>
    private static IElement DialogButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));
}
