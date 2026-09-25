namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins corrections-B7's 14.5.i (the third page-wiring step): the detail drawer hosts a real
/// <see cref="TaskDetail"/> (<see cref="TaskDetailMode.Panel"/>) instead of the placeholder, for both
/// an opened row and "+ New task" (the header button and the empty-state button, seeded from
/// <see cref="NewTaskDefaults"/>); switching what the drawer shows always goes through
/// <see cref="TaskDetail.ConfirmDiscardAsync"/> first, and Cancel on that "Discard changes?" box
/// keeps the Task that was already open; the drawer's <c>OverlayAutoClose</c> follows
/// <see cref="TaskDetail.HasPending"/>; and the page also hosts <see cref="WakeToasts"/> and
/// <see cref="AiReactingChip"/>, the latter watching every Task in the current effective View,
/// unnarrowed by the search box.
/// </summary>
public sealed class TasksPageDetailTests
{
    /// <summary>Clicking a List row opens the drawer's <see cref="TaskDetail"/> for that row's Task.</summary>
    [Fact]
    public async Task RowOpen_ClickingATask_ShowsTaskDetailForThatId()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");

        TaskDetail detail = cut.FindComponent<TaskDetail>().Instance;
        Assert.Equal(task.Id, detail.Id);
        Assert.Equal(TaskDetailMode.Panel, detail.Mode);
    }

    /// <summary>Clicking the already-open Task again is a no-op: no "Discard changes?" prompt, and the same <see cref="TaskDetail"/> instance (with its pending edit intact) stays mounted.</summary>
    [Fact]
    public async Task RowOpen_ClickingTheAlreadyOpenTask_DoesNotPromptOrRecreateTheDetail()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");
        TaskDetail firstInstance = cut.FindComponent<TaskDetail>().Instance;
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        await ClickTaskRowAsync(cut, "Alpha");

        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
        Assert.Same(firstInstance, cut.FindComponent<TaskDetail>().Instance);
        Assert.True(firstInstance.HasPending);
    }

    /// <summary>"+ New task" (the header button) opens the drawer's <see cref="TaskDetail"/> in create mode; with no single-Team filter, <see cref="NewTaskDefaults"/> is blank.</summary>
    [Fact]
    public async Task NewTaskHeaderButton_NoSingleTeamFilter_OpensCreateModeWithBlankDefaults()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await cut.InvokeAsync(() => cut.Find(".task-toolbar-new-task").Click());

        TaskDetail detail = cut.FindComponent<TaskDetail>().Instance;
        Assert.Null(detail.Id);
        TaskDraft draft = detail.Draft ?? throw new InvalidOperationException("Draft missing.");
        Assert.Equal(string.Empty, draft.Team);
        Assert.Null(draft.Project);
    }

    /// <summary>"+ New task" seeds <see cref="NewTaskDefaults"/> from the effective View's filter when it names exactly one Team and Project (Spec §13.3), the same rule <see cref="TaskToolbar"/>'s own button applies.</summary>
    [Fact]
    public async Task NewTaskHeaderButton_FilterNamesOneTeamAndProject_SeedsTheDraft()
    {
        using TaskToolHarness harness = new();
        TaskView view = new()
        {
            Id = "platform-alpha",
            Name = "Platform Alpha",
            Kind = ViewKind.List,
            Filter = new TaskFilter { Teams = ["Platform"], Projects = [new ProjectRef("Platform", "Alpha")] },
        };
        Assert.True(harness.Views.Save(view).Saved);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "platform-alpha");

        await cut.InvokeAsync(() => cut.Find(".task-toolbar-new-task").Click());

        TaskDraft draft = cut.FindComponent<TaskDetail>().Instance.Draft ?? throw new InvalidOperationException("Draft missing.");
        Assert.Equal("Platform", draft.Team);
        Assert.Equal("Alpha", draft.Project);
    }

    /// <summary>Opening a second Task while the first has a pending edit asks "Discard changes?"; Cancel leaves the first Task open with its edit intact (corrections-B7 14.5.i item 2).</summary>
    [Fact]
    public async Task SwitchingToAnotherTask_WithPendingEdits_CancelKeepsTheCurrentTask()
    {
        using TaskToolHarness harness = new();
        TaskItem taskA = CreateTask(harness, "Alpha");
        _ = CreateTask(harness, "Bravo");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        _ = cut.InvokeAsync(() => FindTaskRow(cut, "Bravo").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        Assert.Equal("Discard changes?", cut.Find(".mud-dialog-title").TextContent.Trim());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).Click());

        Assert.Equal(taskA.Id, cut.FindComponent<TaskDetail>().Instance.Id);
        Assert.True(cut.FindComponent<TaskDetail>().Instance.HasPending);
    }

    /// <summary>Confirming "Discard changes?" switches the drawer to the newly clicked Task, discarding the first's pending edit (corrections-B7 14.5.i item 2).</summary>
    [Fact]
    public async Task SwitchingToAnotherTask_WithPendingEdits_DiscardSwitchesToTheNewTask()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");
        TaskItem taskB = CreateTask(harness, "Bravo");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        _ = cut.InvokeAsync(() => FindTaskRow(cut, "Bravo").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Discard", StringComparison.Ordinal)).Click());

        cut.WaitForAssertion(() => Assert.Equal(taskB.Id, cut.FindComponent<TaskDetail>().Instance.Id));
        Assert.False(cut.FindComponent<TaskDetail>().Instance.HasPending);
    }

    /// <summary>
    /// Settled J58 - this is the regression test for the real bug the delivery manager caught in
    /// review: as first built, <c>OverlayAutoClose</c> read <c>this.detail.HasPending</c> directly,
    /// which stayed stale after a pending edit because <see cref="TaskDetail"/> changing its own
    /// internal <c>pending</c> does not, by itself, re-render this page - so a Human could edit a
    /// field, click the drawer's overlay, and lose the edit silently even though <c>OverlayAutoClose</c>
    /// was meant to stop exactly that. The fix is <see cref="TaskDetail.HasPendingChanged"/>: this test
    /// edits Priority and asserts <c>OverlayAutoClose</c> is already <see langword="false"/> with no
    /// other event in between, then asserts Revert flips it back to <see langword="true"/>.
    /// </summary>
    [Fact]
    public async Task DetailDrawer_OverlayAutoClose_FollowsHasPendingWithNoOtherEvent()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");
        Assert.True(DetailDrawer(cut).Instance.OverlayAutoClose);

        await EditPriorityAsync(cut, TaskPriority.Urgent);

        Assert.False(DetailDrawer(cut).Instance.OverlayAutoClose);

        await ClickRevertAsync(cut);

        Assert.True(DetailDrawer(cut).Instance.OverlayAutoClose);
    }

    /// <summary>The page hosts <see cref="WakeToasts"/> (renders nothing itself) and a page-header <see cref="AiReactingChip"/>.</summary>
    [Fact]
    public async Task Page_HostsWakeToastsAndAiReactingChip()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        Assert.Single(cut.FindComponents<WakeToasts>());
        Assert.Single(cut.FindComponents<AiReactingChip>());
    }

    /// <summary>The <see cref="AiReactingChip"/> watches every Task in the current effective View - the whole set, still including a Task a search term currently excludes from the List/Board (Spec §10.8).</summary>
    [Fact]
    public async Task AiReactingChip_WatchesEveryTaskInTheEffectiveView_EvenWhenSearchExcludesOne()
    {
        using TaskToolHarness harness = new();
        TaskItem taskA = CreateTask(harness, "Alpha");
        TaskItem taskB = CreateTask(harness, "Bravo");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        IRenderedComponent<MudTextField<string>> searchBox = cut.FindComponents<MudTextField<string>>()
            .Single(t => (t.Instance.Class ?? string.Empty).Contains("task-toolbar-search", StringComparison.Ordinal));
        await cut.InvokeAsync(() => searchBox.Instance.ValueChanged.InvokeAsync("Alpha"));

        // The search now excludes Bravo from the rendered List (Spec §13.12's own "No tasks match" path
        // never fires here because Alpha still matches), but AiReactingChip.Watched is unnarrowed.
        List<TaskId> watched = [.. cut.FindComponent<AiReactingChip>().Instance.Watched.OrderBy(id => id.ToString(), StringComparer.Ordinal)];
        List<TaskId> expected = [.. new[] { taskA.Id, taskB.Id }.OrderBy(id => id.ToString(), StringComparer.Ordinal)];
        Assert.Equal(expected, watched);
    }

    /// <summary>Registers the harness's services into a fresh <see cref="MudBunitContext"/> - mirrors <see cref="TasksPageTests"/>'s own helper.</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Renders <see cref="TasksPage"/>, optionally routed to <paramref name="viewId"/>.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string? viewId = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            if (viewId is not null)
            {
                builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            }

            builder.CloseComponent();
        });

    /// <summary>Creates a Task through the real <see cref="TaskService"/>, exactly as a Human would.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title)
    {
        TaskResult result = harness.Service.Create(new TaskDraft(title, "Platform", null), TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Finds the List row whose Title cell contains <paramref name="title"/>.</summary>
    private static IElement FindTaskRow(IRenderedComponent<ContainerFragment> cut, string title)
    {
        IElement cell = cut.FindAll("td").First(td => td.TextContent.Contains(title, StringComparison.Ordinal));
        return cell.Closest("tr") ?? throw new InvalidOperationException("The clicked cell has no row.");
    }

    /// <summary>Clicks the List row whose Title cell contains <paramref name="title"/>.</summary>
    private static Task ClickTaskRowAsync(IRenderedComponent<ContainerFragment> cut, string title) =>
        cut.InvokeAsync(() => FindTaskRow(cut, title).Click());

    /// <summary>
    /// Sets the mounted <see cref="TaskDetail"/>'s Priority through its rendered <c>MudSelect</c>,
    /// adding a pending edit (the same technique <c>TaskDetailTests</c>/<c>TaskDetailActionsTests</c>
    /// use) - scoped to <see cref="TaskDetail"/>'s own subtree, because the always-mounted
    /// <see cref="ViewEditorDrawer"/> has its own unrelated <c>MudSelect&lt;TaskPriority&gt;</c> (its
    /// Priority filter).
    /// </summary>
    private static async Task EditPriorityAsync(IRenderedComponent<ContainerFragment> cut, TaskPriority value)
    {
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponent<TaskDetail>().FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(value));
    }

    /// <summary>
    /// Clicks the mounted <see cref="TaskDetail"/>'s Revert button. Per the R8 "J57" note, a click
    /// right after a render must go through <c>InvokeAsync</c> even when nothing else is awaited in
    /// between - <see cref="TaskDetail"/> can re-render on its own after its first render, and a bare
    /// <c>Find(...).Click()</c> can then hit a stale element id.
    /// </summary>
    private static Task ClickRevertAsync(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(() => cut.FindComponent<TaskDetail>().FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), "Revert", StringComparison.Ordinal)).Click());

    /// <summary>The page's own detail drawer, picked out from <see cref="ViewEditorDrawer"/>'s <c>MudDrawer</c> by its <c>tasks-detail-drawer</c> CSS hook.</summary>
    private static IRenderedComponent<MudDrawer> DetailDrawer(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudDrawer>().Single(d => (d.Instance.Class ?? string.Empty).Contains("tasks-detail-drawer", StringComparison.Ordinal));
}
