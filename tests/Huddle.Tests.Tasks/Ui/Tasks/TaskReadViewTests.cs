namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins the Task read view (<see cref="TaskReadView"/>, T15 of <c>docs/Huddle.TaskReadView-Specifications.md</c>): it shows every value
/// the editor shows as text, offers no form control, keeps a dash for an empty value so rows never jump, opens the edit page's Tab
/// from Edit (permanent, one per Task), and redraws when the Task changes.
/// </summary>
public sealed class TaskReadViewTests
{
    /// <summary>A Task with every field set, as the read view must show it (D-1).</summary>
    /// <param name="harness">The Tasks stack.</param>
    private static (TaskItem Task, TaskItem Parent, TaskItem Blocker) CreateFullTask(TaskToolHarness harness)
    {
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem parent = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Parent task", "Platform", null), human)).Task;
        TaskItem blocker = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Blocker task", "Platform", null), human)).Task;
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft(
                "Ship the thing",
                "Platform",
                null,
                Status: TaskState.InProgress,
                Priority: TaskPriority.High,
                Assignee: "Nova",
                OriginRoomId: "room-gone",
                Parent: parent.Id,
                BlockedBy: [blocker.Id],
                Tags: ["alpha", "beta"],
                StartDate: new DateOnly(2030, 1, 2),
                DueDate: new DateOnly(2030, 3, 4),
                Description: "Plain **bold** words and a link to PLAT-0001."),
            human)).Task;
        return (task, parent, blocker);
    }

    /// <summary>Renders the read view for <paramref name="id"/> in a context with popovers (the actions menu is one).</summary>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, TaskId id) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskReadView>(0);
            builder.AddAttribute(1, nameof(TaskReadView.Id), id);
            builder.CloseComponent();
        });

    /// <summary>Every field the editor shows is a value here: Status, Priority, Assignee, Team, Parent, Blocked by, Tags, both dates, Origin and the rendered Description.</summary>
    [Fact]
    public async Task Render_TaskWithEveryField_ShowsEveryValueAsText()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        (TaskItem task, TaskItem parent, TaskItem blocker) = CreateFullTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        Assert.Equal("In Progress", cut.Find(".task-read-view-status dd").TextContent.Trim());
        Assert.Equal("High", cut.Find(".task-read-view-priority dd").TextContent.Trim());
        Assert.Equal("Nova", cut.Find(".task-read-view-assignee .task-assignee-name").TextContent.Trim());
        Assert.Equal("Platform", cut.Find(".task-read-view-team dd").TextContent.Trim());
        Assert.Equal(parent.Id.ToString(), cut.Find(".task-read-view-parent dd").TextContent.Trim());
        Assert.Equal(blocker.Id.ToString(), cut.Find(".task-read-view-blocked-by a").TextContent.Trim());
        Assert.Equal("Backlog", cut.Find(".task-read-view-blocker-status").TextContent.Trim());
        Assert.Equal(["alpha", "beta"], cut.FindAll(".task-read-view-tags .tag-chip").Select(static chip => chip.TextContent.Trim()));
        Assert.Equal(TaskDates.Human(new DateOnly(2030, 1, 2), harness.Clock), cut.Find(".task-read-view-start-date dd").TextContent.Trim());
        Assert.Equal(TaskDates.Human(new DateOnly(2030, 3, 4), harness.Clock), cut.Find(".task-read-view-due-date dd").TextContent.Trim());
        Assert.Equal("(Room no longer exists)", cut.Find(".task-read-view-origin dd").TextContent.Trim());
        Assert.Equal("bold", cut.Find(".task-read-view-description strong").TextContent);
    }

    /// <summary>The Status row carries one mark (#471): the status chip's own, with no state dot before it.</summary>
    [Fact]
    public async Task Render_StatusRow_HasTheChipAndNoStateDot()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        (TaskItem task, _, _) = CreateFullTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        Assert.Single(cut.FindAll(".task-read-view-status .task-status-chip"));
        Assert.Empty(cut.FindAll(".task-read-view-status .task-state-dot"));
    }

    /// <summary>A Task id in the property grid is a link that opens that Task, so a Human can follow the Parent and a blocker.</summary>
    [Fact]
    public async Task Render_ParentAndBlocker_AreLinksToThoseTasks()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        (TaskItem task, TaskItem parent, TaskItem blocker) = CreateFullTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        Assert.Equal($"/tasks/item/{parent.Id}", cut.Find(".task-read-view-parent a").GetAttribute("href"));
        Assert.Equal($"/tasks/item/{blocker.Id}", cut.Find(".task-read-view-blocked-by a").GetAttribute("href"));
    }

    /// <summary>The read view draws no <c>input</c>, <c>textarea</c> or <c>select</c> for a Task field: it reads (D-1). The Timeline's comment box (T15h) is the one control, and it writes no field.</summary>
    [Fact]
    public async Task Render_TaskWithEveryField_HasNoFormControl()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        (TaskItem task, _, _) = CreateFullTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        Assert.NotEmpty(cut.FindAll(".task-read-view"));
        Assert.DoesNotContain(cut.FindAll(".task-read-view input, .task-read-view textarea, .task-read-view select"), static control => control.Closest(".task-timeline-comment-box") is null);
    }

    /// <summary>An empty value is a dash in Quiet ink, so a Task with little set still has every row.</summary>
    [Fact]
    public async Task Render_TaskWithNothingSet_ShowsADashForEachEmptyValue()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Bare", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        foreach (string row in new[] { "project", "sprint", "parent", "blocked-by", "tags", "start-date", "due-date", "origin" })
        {
            Assert.Equal("—", cut.Find($".task-read-view-{row} dd").TextContent.Trim());
        }

        Assert.Equal("No description.", cut.Find(".task-read-view-description-empty").TextContent.Trim());
    }

    /// <summary>Edit opens <c>/tasks/item/{id}/edit</c> as a Tab that is permanent, not a preview, and takes focus.</summary>
    [Fact]
    public async Task Edit_Click_OpensAPermanentTabForTheEditPage()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Edit me", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        await cut.InvokeAsync(() => cut.Find(".task-read-view-edit").ClickAsync());

        Tab tab = Assert.Single(harness.Tabs.Tabs);
        Assert.Equal($"task-edit:{task.Id}", tab.Key);
        Assert.Equal($"/tasks/item/{task.Id}/edit", tab.Url);
        Assert.False(tab.IsPreview);
        Assert.Equal(tab.Id, harness.Tabs.Active?.Id);
    }

    /// <summary>Edit twice leaves one Tab: the second click reuses the first (B4).</summary>
    [Fact]
    public async Task Edit_ClickTwice_LeavesOneTabForTheTask()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Edit me", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        await cut.InvokeAsync(() => cut.Find(".task-read-view-edit").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".task-read-view-edit").ClickAsync());

        Assert.Single(harness.Tabs.Tabs, static tab => tab.Key.StartsWith("task-edit:", StringComparison.Ordinal));
    }

    /// <summary>Edit names the Task in its tooltip and accessible name: "Edit {id} in a new tab".</summary>
    [Fact]
    public async Task Edit_Button_NamesTheTaskAndTheTab()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Edit me", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        Assert.Equal($"Edit {task.Id} in a new tab", cut.Find(".task-read-view-edit").GetAttribute("aria-label"));
    }

    /// <summary>A change to the Task redraws the view at once, so a Save in the edit page shows here (B5).</summary>
    [Fact]
    public async Task TaskChanged_RedrawsTheView()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Edit me", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);
        Assert.Equal("Medium", cut.Find(".task-read-view-priority dd").TextContent.Trim());

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Priority = TaskPriority.Urgent }, task.Version, TaskActors.Human(harness.Options.Value)));

        cut.WaitForAssertion(() => Assert.Equal("Urgent", cut.Find(".task-read-view-priority dd").TextContent.Trim()));
    }

    /// <summary>A Task that is not gated shows no Human gate section; a gated one shows it as text with no switch (T15f).</summary>
    [Fact]
    public async Task HumanGate_ShownAsTextOnAGatedTaskOnly()
    {
        using TaskToolHarness harness = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem plain = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Plain", "Platform", null), human)).Task;
        TaskItem gated = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Gated", "Platform", null), human)).Task;
        gated = Assert.IsType<TaskResult.Saved>(harness.Service.Update(gated.Id, new TaskPatch { HumanGate = Optional<bool>.Set(true) }, gated.Version, human)).Task;
        await using MudBunitContext plainCtx = UiTestHost.Create().WithHarness(harness);
        await using MudBunitContext gatedCtx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> plainView = Render(plainCtx, plain.Id);
        IRenderedComponent<ContainerFragment> gatedView = Render(gatedCtx, gated.Id);

        Assert.Empty(plainView.FindAll(".task-human-gate"));
        Assert.Equal("Requires your acceptance: On", gatedView.Find(".task-human-gate-readonly-switch").TextContent.Trim());
        Assert.Empty(gatedView.FindAll(".task-human-gate input, .task-human-gate [role='switch']"));
    }

    /// <summary>A Cancelled Task's Reason is read back from the Change log entry Save wrote, since the file holds no Reason field.</summary>
    [Fact]
    public async Task Reason_CancelledTask_ShowsTheReasonFromTheChangeLog()
    {
        using TaskToolHarness harness = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Drop me", "Platform", null), human)).Task;
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Cancelled, Reason = "no longer needed" }, task.Version, human));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        Assert.Equal("no longer needed", cut.Find(".task-read-view-reason dd").TextContent.Trim());
    }

    /// <summary>The Timeline is always open under its own heading with the entry count: no expansion panel to open before the entries show.</summary>
    [Fact]
    public async Task History_IsAlwaysOpen_WithoutAnExpansionPanel()
    {
        using TaskToolHarness harness = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Show me", "Platform", null), human)).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task.Id);

        Assert.Empty(cut.FindComponents<MudExpansionPanels>());
        Assert.Equal("Timeline (1)", cut.Find(".task-detail-history-heading").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".task-detail-history-row"));
    }
}
