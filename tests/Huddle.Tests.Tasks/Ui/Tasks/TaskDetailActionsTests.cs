namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins Spec §13.6's buttons, Expand, Make a copy, Close/Reopen and the History, RED-only (14.4.t):
/// Expand closes the Panel and opens <c>TaskDetailDialog</c> with only the id, guarded by
/// <c>ConfirmDiscardAsync</c>'s "Discard changes?" message box when edits are pending (corrections-B5
/// D14 item 7); Cancel discards and closes unconditionally, with no confirmation, since the whole point
/// of clicking Cancel is to discard (corrections-B7 "14.4" item 1, Spec §13.6 Buttons); Make a copy opens
/// a new dialog in create mode from the Panel's own <em>effective</em> (pending-included) fields and
/// writes nothing until that copy is itself saved (Spec §13.6); Close task saves any pending edit and
/// closes as one <c>Update</c> then <c>Close</c>, stopping instead of closing on a <c>Conflict</c> or
/// <c>Refused</c> (corrections-B5 D14 item 9); a Closed Task shows Reopen; the History is a
/// <c>MudTimeline</c>, newest first (corrections-B5 D14 item 10); <c>MudExitPrompt</c> is enabled only
/// while edits are pending; create mode's Save calls <see cref="TaskService.Create"/> and shows refusals
/// exactly (corrections-B7 "14.4" item 2); the Budget banner carries <c>role="status"</c> (corrections-B7
/// "14.4" item 4); and the Description Preview resolves Task ids to links through
/// <see cref="MarkdownRenderer.ToHtml(string, ITaskReferenceResolver?)"/> (corrections-B7 "14.4" item 3).
/// Status, Assignee, Team/Project, Parent, Blocked by, dates and Tags (14.2) and the conflict UI (14.3)
/// are each a different task's tests and are not pinned here - see
/// <see cref="Agency.Huddle.Tests.Ui.Tasks.TaskDetailTests"/> and (once it lands)
/// <c>TaskDetailConflictTests</c>.
/// </summary>
public sealed class TaskDetailActionsTests
{
    /// <summary><see cref="TaskDetail.ConfirmDiscardAsync"/> (the method the page calls before switching <c>Id</c>, corrections-B5 D14 item 7) returns <see langword="true"/> immediately, with no message box, when nothing is pending.</summary>
    [Fact]
    public async Task ConfirmDiscardAsync_NoPending_ReturnsTrueWithNoMessageBox()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        TaskDetail detail = cut.FindComponent<TaskDetail>().Instance;

        bool confirmed = await cut.InvokeAsync(() => detail.ConfirmDiscardAsync());

        Assert.True(confirmed);
        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
    }

    /// <summary>Cancel discards pending edits and closes unconditionally - no "Discard changes?" prompt, unlike Expand (Spec §13.6 Buttons; corrections-B7 "14.4" item 1).</summary>
    [Fact]
    public async Task Cancel_WithPendingEdits_DiscardsImmediately_NoConfirmation_AndCloses()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        bool closed = false;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id, onClose: EventCallback.Factory.Create(this, () => closed = true));
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        await cut.InvokeAsync(() => cut.Find(".task-detail-cancel").ClickAsync());

        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
        Assert.Equal(TaskPriority.Medium, harness.Store.Get(task.Id)?.Priority);
        Assert.True(closed);
    }

    /// <summary>Make a copy opens a new Task as a copy of the saved one in the edit page's own Tab (<c>task-copy:{id}</c>, permanent) and writes nothing to the Store (Spec §13.6).</summary>
    [Fact]
    public async Task MakeACopy_OpensTheCopyTab_AndWritesNothing()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, title: "Ship the thing", priority: TaskPriority.Medium, assignee: "Nova");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        int tasksBefore = harness.Store.All.Count;

        await ClickMenuItemAsync(cut, "Make a copy…");

        Agency.Huddle.App.Tabs.Tab tab = Assert.Single(harness.Tabs.Tabs);
        Assert.Equal($"task-copy:{task.Id}", tab.Key);
        Assert.Equal($"/tasks/item/{task.Id}/copy", tab.Url);
        Assert.False(tab.IsPreview);
        Assert.Equal(tasksBefore, harness.Store.All.Count);
    }

    /// <summary>Close task with nothing pending calls only <see cref="TaskService.Close"/>: one new "closed" Change log entry, and the button becomes Reopen.</summary>
    [Fact]
    public async Task CloseTask_NoPendingEdits_ClosesTheTask_AndButtonBecomesReopen()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        int logCountBefore = task.ChangeLog.Count;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);

        await CloseTaskAsync(cut);

        TaskItem closed = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        Assert.True(closed.Location.Closed);
        Assert.Equal(logCountBefore + 1, closed.ChangeLog.Count);
        Assert.Equal("closed", closed.ChangeLog[^1].Summary);
        await OpenMenuItemsAsync(cut);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".mud-menu-item"), HasText("Reopen")));
    }

    /// <summary>Close task with a pending edit saves it first, then closes, as one <see cref="TaskService.Update"/> then <see cref="TaskService.Close"/> - two new Change log entries (corrections-B5 D14 item 9).</summary>
    [Fact]
    public async Task CloseTask_WithPendingEdits_SavesThenCloses_AsOneUpdateThenClose()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        int logCountBefore = task.ChangeLog.Count;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        await CloseTaskAsync(cut);

        TaskItem closed = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        Assert.True(closed.Location.Closed);
        Assert.Equal(TaskPriority.Urgent, closed.Priority);
        Assert.Equal(logCountBefore + 2, closed.ChangeLog.Count);
        Assert.Equal("priority: Medium → Urgent", closed.ChangeLog[^2].Summary);
        Assert.Equal("closed", closed.ChangeLog[^1].Summary);
    }

    /// <summary>When the pending edit's <see cref="TaskService.Update"/> half conflicts, Close task stops there: the Task stays open, and the edit is kept rather than lost (corrections-B5 D14 item 9's "stop on Conflict/Refused").</summary>
    [Fact]
    public async Task CloseTask_UpdateConflicts_DoesNotClose_KeepsThePendingEdit()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);
        TaskResult outsideEdit = harness.Service.Update(task.Id, new TaskPatch { Priority = TaskPriority.Low }, baseVersion: null, TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(outsideEdit);

        await CloseTaskAsync(cut);

        Assert.False(harness.Store.Get(task.Id)?.Location.Closed);
        Assert.Equal("Unsaved edits", cut.TextOf(".task-detail-unsaved-text"));
    }

    /// <summary>When the pending edit's <see cref="TaskService.Update"/> half is refused (an empty Title), Close task stops there: the Task stays open, the problem is shown, and the edit is kept.</summary>
    [Fact]
    public async Task CloseTask_UpdateRefused_DoesNotClose_ShowsTheProblem()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Edit title']").ClickAsync());
        IRenderedComponent<MudTextField<string>> titleField = cut.FindComponents<MudTextField<string>>().Single(HasClass<MudTextField<string>>("task-detail-title"));
        await cut.InvokeAsync(() => titleField.Instance.ValueChanged.InvokeAsync(""));

        await CloseTaskAsync(cut);

        Assert.False(harness.Store.Get(task.Id)?.Location.Closed);
        Assert.Equal("Title is empty.", cut.TextOf(".task-detail-save-error-problem"));
    }

    /// <summary>A Closed Task shows Reopen instead of Close task; clicking it reopens the Task with a "reopened" Change log entry.</summary>
    [Fact]
    public async Task ReopenButton_OnClosedTask_ReopensIt()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        TaskResult closeResult = harness.Service.Close(task.Id, TaskActors.Human(harness.Options.Value));
        TaskItem closed = Assert.IsType<TaskResult.Saved>(closeResult).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        Assert.DoesNotContain(await OpenMenuItemsAsync(cut), HasText("Close task…"));

        await ClickMenuItemAsync(cut, "Reopen");

        TaskItem reopened = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        Assert.False(reopened.Location.Closed);
        Assert.Equal("reopened", reopened.ChangeLog[^1].Summary);
        Assert.Equal(closed.ChangeLog.Count + 1, reopened.ChangeLog.Count);
    }

    /// <summary>
    /// The History's header names the entry count (Spec §13.6) and the History is always open: one entry per
    /// Change log record, each a <c>Size.Small</c> <see cref="MudTimelineItem"/>, with nothing to expand first.
    /// </summary>
    [Fact]
    public async Task History_HeaderNamesTheCount_ListsEntriesWithoutExpanding()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("History (1)", cut.Find(".task-detail-history-heading").TextContent.Trim());


        Assert.Single(cut.FindAll(".task-detail-history-entry"));
        Assert.Equal(harness.Options.Value.HumanName, cut.TextOf(".task-detail-history-actor"));
        Assert.Equal("created", cut.TextOf(".task-detail-history-summary"));
        Assert.All(cut.FindComponents<MudTimelineItem>(), item => Assert.Equal(Size.Small, item.Instance.Size));
    }

    /// <summary>The History lists every entry newest first, as a whole ordered list (R6: never by membership) - one save adds a second, newer entry ahead of "created".</summary>
    [Fact]
    public async Task History_ListsEveryEntry_NewestFirst()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Original", priority: TaskPriority.Medium);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);
        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());
        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));


        string human = harness.Options.Value.HumanName;
        Assert.Equal(
            [$"{human}: priority: Medium → Urgent", $"{human}: created"],
            cut.FindAll(".task-detail-history-row").Select(row => $"{row.QuerySelector(".task-detail-history-actor")?.TextContent.Trim()}: {row.QuerySelector(".task-detail-history-summary")?.TextContent.Trim()}").ToList());
    }

    /// <summary>
    /// A History entry's date renders as the Human's local time through the injected
    /// <see cref="TimeProvider"/> (J51), formatted <c>yyyy-MM-dd HH:mm</c> invariant - not
    /// <see cref="DateTimeOffset.ToLocalTime"/>'s machine time zone, so a fixed
    /// <see cref="TimeProvider.LocalTimeZone"/> in a test (a <see cref="ManualTimeProvider"/> built with
    /// <see cref="FixedUtcNow"/> and <see cref="FixedZone"/>, given to <see cref="TaskToolHarness"/>'s
    /// clock parameter) makes it deterministic. The expected text is a literal, not computed from the
    /// same <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/> call the component
    /// itself would use, so a bug in that conversion can't cancel out in both places.
    /// </summary>
    [Fact]
    public async Task History_EntryDate_FormatsAsHumanLocalTimeViaInjectedClock()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(
            ct, new ManualTimeProvider(TaskDetailActionsTests.FixedUtcNow, TaskDetailActionsTests.FixedZone));
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);


        Assert.Equal("Today 12:30", cut.Find(".task-detail-history-date").TextContent.Trim());
    }

    /// <summary><c>MudExitPrompt</c> is disabled with nothing pending, and enabled once an edit is made (Spec §13.6 "Leaving with unsaved edits").</summary>
    [Fact]
    public async Task ExitPrompt_DisabledUntilPending_ThenEnabled()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);

        Assert.True(cut.FindComponent<MudExitPrompt>().Instance.Disabled);

        await EditPriorityAsync(cut, TaskPriority.Urgent);

        Assert.False(cut.FindComponent<MudExitPrompt>().Instance.Disabled);
    }

    /// <summary>
    /// Expanded is a two-column layout (Spec §13.6, J-follow-up review): Priority, Status, Assignee, Team, Project and Sprint
    /// are inside the full-width strip, <c>.task-detail-col-strip</c>, Parent is inside <c>.task-detail-col-fields</c> (the right column), and the Description editor plus Origin are inside
    /// <c>.task-detail-col-side</c> - asserted by DOM containment (<see cref="INode.Contains"/>), the
    /// facts file's own pattern for "is this element inside that one".
    /// </summary>
    [Fact]
    public async Task Expanded_TwoColumnLayout_TitleAndDescriptionLeft_FieldsRight()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = harness.Service.Create(
            new TaskDraft("T", "Platform", null, Assignee: "Nova", OriginRoomId: "room-1"),
            TaskActors.Human(harness.Options.Value)) is TaskResult.Saved saved
            ? saved.Task
            : throw new InvalidOperationException("Create failed.");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderExpanded(ctx, task.Id);

        IElement fieldsColumn = cut.Find(".task-detail-col-fields");
        IElement sideColumn = cut.Find(".task-detail-col-side");

        IElement stripColumn = cut.Find(".task-detail-col-strip");
        Assert.True(stripColumn.Contains(cut.Find(".task-detail-priority")));
        Assert.True(stripColumn.Contains(cut.Find(".task-detail-status")));
        Assert.True(stripColumn.Contains(cut.Find(".task-detail-team")));
        Assert.True(stripColumn.Contains(cut.Find(".task-detail-project")));
        Assert.True(stripColumn.Contains(cut.Find(".task-detail-sprint-field")));
        Assert.True(fieldsColumn.Contains(cut.Find(".task-detail-parent")));
        Assert.True(stripColumn.Contains(cut.Find(".task-detail-assignee")));
        Assert.True(sideColumn.Contains(cut.Find(".task-detail-description")));
        Assert.True(sideColumn.Contains(cut.Find(".task-detail-origin")));
        Assert.False(sideColumn.Contains(cut.Find(".task-detail-priority")));
        Assert.False(fieldsColumn.Contains(cut.Find(".task-detail-description")));
    }

    /// <summary>The Budget banner carries <c>role="status"</c> (corrections-B7 "14.4" item 4 - it supersedes the plain <c>MudAlert</c> 14.1.i shipped with no role).</summary>
    [Fact]
    public async Task BudgetBanner_HasStatusRole()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        for (int i = 0; i < harness.Options.Value.Tasks.AgentWakeBudget; i++)
        {
            _ = harness.TaskActivity.TryConsumeAgentWake(task.Id);
        }

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("status", cut.Find(".task-detail-budget-banner").GetAttribute("role"));
    }

    /// <summary>The Description Preview resolves a Task id it names to a link, through the same <see cref="ITaskReferenceResolver"/>-taking <see cref="MarkdownRenderer.ToHtml(string, ITaskReferenceResolver?)"/> overload Messages use (corrections-B7 "14.4" item 3, Spec §13.13.2).</summary>
    [Fact]
    public async Task DescriptionPreview_NamesAnotherTaskId_RendersItAsALink()
    {
        using TaskToolHarness harness = new();
        TaskItem other = CreateTask(harness, title: "The other task");
        TaskItem task = CreateTask(harness);
        TaskResult described = harness.Service.Update(
            task.Id,
            new TaskPatch { Description = $"See {other.Id} for details." },
            task.Version,
            TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(described);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);

        await cut.InvokeAsync(() => cut.Find(".task-detail-description-toggle").QuerySelectorAll("button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Preview", StringComparison.Ordinal)).ClickAsync());

        IElement link = cut.Find($"a[href='/tasks/item/{other.Id}']");
        Assert.Equal(other.Id.ToString(), link.TextContent.Trim());
    }

    /// <summary>A non-create Save that comes back <see cref="TaskResult.Refused"/> shows its problems in the same alert as an <see cref="IOException"/> (Spec §13.6 "Save errors"), keeping <c>pending</c> instead of losing it.</summary>
    [Fact]
    public async Task Save_Refused_ShowsProblemsInTheSaveErrorAlert_KeepsPending()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderPanel(ctx, task.Id);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Edit title']").ClickAsync());
        IRenderedComponent<MudTextField<string>> titleField = cut.FindComponents<MudTextField<string>>().Single(HasClass<MudTextField<string>>("task-detail-title"));
        await cut.InvokeAsync(() => titleField.Instance.ValueChanged.InvokeAsync(""));

        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());

        Assert.Equal("Title is empty.", cut.TextOf(".task-detail-save-error-problem"));
        Assert.Equal("alert", cut.Find(".task-detail-save-error").GetAttribute("role"));
        Assert.Equal("Unsaved edits", cut.TextOf(".task-detail-unsaved-text"));
    }

    /// <summary>Create mode's Save calls <see cref="TaskService.Create"/> with the draft's fields and shows the created Task (corrections-B7 "14.4" item 2 - the path manual test TASKS-01 exercises).</summary>
    [Fact]
    public async Task CreateMode_Save_Success_CallsCreate_AndShowsTheCreatedTask()
    {
        using TaskToolHarness harness = new();
        TaskDraft draft = new("Copy of Ship the thing", "Platform", null, Priority: TaskPriority.Urgent);
        int tasksBefore = harness.Store.All.Count;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderCreate(ctx, draft);

        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());

        Assert.Equal(tasksBefore + 1, harness.Store.All.Count);
        TaskItem created = harness.Store.All.Single(t => string.Equals(t.Title, "Copy of Ship the thing", StringComparison.Ordinal));
        Assert.Equal(TaskPriority.Urgent, created.Priority);
        Assert.Equal(TaskState.Backlog, created.Status);
        Assert.Equal("Copy of Ship the thing", cut.TextOf(".task-detail-title-display"));
    }

    /// <summary>
    /// Manual test TASKS-01 finding F8: after a successful create-mode Save the editor stays open (J51)
    /// and must switch to the created Task's own header - the copy-id button present, and no draft-only controls -
    /// exactly as when that Task is opened normally. (The id itself is the edit page's eyebrow.)
    /// </summary>
    [Fact]
    public async Task CreateMode_Save_Success_HeaderShowsTheCopyIdButton()
    {
        using TaskToolHarness harness = new();
        TaskDraft draft = new("Ship the thing", "Platform", null);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderCreate(ctx, draft);

        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());

        TaskItem created = harness.Store.All.Single(t => string.Equals(t.Title, "Ship the thing", StringComparison.Ordinal));
        Assert.Equal(created.Title, cut.TextOf(".task-detail-title-display"));
        Assert.Single(cut.FindAll(".task-detail-copy-id"));
    }

    /// <summary>Create mode's Save shows a <see cref="TaskResult.Refused"/>'s problems exactly and creates nothing (corrections-B7 "14.4" item 2).</summary>
    [Fact]
    public async Task CreateMode_Save_Refused_ShowsProblemsExactly_CreatesNothing()
    {
        using TaskToolHarness harness = new();
        TaskDraft draft = new("", "Platform", null);
        int tasksBefore = harness.Store.All.Count;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderCreate(ctx, draft);

        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());

        Assert.Equal(tasksBefore, harness.Store.All.Count);
        Assert.Equal("Title is empty.", cut.TextOf(".task-detail-save-error-problem"));
    }

    /// <summary>Edits <c>Priority</c> through the one <see cref="MudSelect{T}"/> the Panel renders and waits for the render this produces - the same interaction <see cref="TaskDetailTests"/> uses.</summary>
    /// <param name="cut">The rendered Panel.</param>
    /// <param name="value">The new Priority.</param>
    private static async Task EditPriorityAsync(IRenderedComponent<ContainerFragment> cut, TaskPriority value)
    {
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(value));
    }

    /// <summary>A predicate matching a rendered component whose <c>Class</c> parameter contains <paramref name="className"/> - for picking one <see cref="MudTextField{T}"/>/<see cref="MudToggleGroup{T}"/> out of several by its own CSS hook.</summary>
    /// <typeparam name="TComponent">The MudBlazor component type.</typeparam>
    /// <param name="className">The CSS class to look for.</param>
    private static Func<IRenderedComponent<TComponent>, bool> HasClass<TComponent>(string className)
        where TComponent : MudComponentBase =>
        component => (component.Instance.Class ?? "").Contains(className, StringComparison.Ordinal);

    /// <summary>A predicate matching a rendered <see cref="IElement"/> button whose trimmed text equals <paramref name="text"/> - for asserting absence with <c>Where</c>/<c>Empty</c> rather than <c>FindButton</c>'s single-match lookup.</summary>
    /// <param name="text">The button's exact text.</param>
    private static Predicate<IElement> HasText(string text) =>
        element => string.Equals(element.TextContent.Trim(), text, StringComparison.Ordinal);

    /// <summary>Creates a Task through <see cref="TaskService"/> on the given harness with the given fields, defaulting to no assignee - mirrors <see cref="TaskDetailTests"/>'s own helper.</summary>
    /// <param name="harness">The stack to create the Task in.</param>
    /// <param name="title">The Task's title.</param>
    /// <param name="priority">The Task's priority.</param>
    /// <param name="assignee">The Task's assignee Name, or <see langword="null"/> for none.</param>
    private static TaskItem CreateTask(TaskToolHarness harness, string title = "T", TaskPriority priority = TaskPriority.Medium, string? assignee = null)
    {
        TaskResult result = harness.Service.Create(
            new TaskDraft(title, "Platform", null, Priority: priority, Assignee: assignee),
            TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Renders <c>TaskDetail</c> in Panel mode for <paramref name="id"/>, inside <see cref="MudBunitContext.RenderWithPopovers"/> - mirrors <see cref="TaskDetailTests"/>'s own helper, with an optional <c>OnClose</c> so a test can observe whether the Panel actually closed.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPanel(MudBunitContext ctx, TaskId id, EventCallback? onClose = null)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            if (onClose is { } callback)
            {
                builder.AddAttribute(3, nameof(TaskDetail.OnClose), callback);
            }

            builder.CloseComponent();
        });
    }

    /// <summary>Renders <c>TaskDetail</c> in Expanded mode for <paramref name="id"/> - the two-column layout (Spec §13.6).</summary>
    private static IRenderedComponent<ContainerFragment> RenderExpanded(MudBunitContext ctx, TaskId id)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders <c>TaskDetail</c> in Panel mode for create mode, with <paramref name="draft"/> as its starting fields (Spec §13.6, corrections-B7 "14.4" item 2).</summary>
    private static IRenderedComponent<ContainerFragment> RenderCreate(MudBunitContext ctx, TaskDraft draft)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Draft), draft);
            builder.CloseComponent();
        });
    }

    /// <summary>Opens the title row's more-actions menu and returns its items - where Make a copy and Close/Reopen live.</summary>
    /// <param name="cut">The rendered panel.</param>
    private static async Task<IReadOnlyList<IElement>> OpenMenuItemsAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find("button[aria-label^=\"Actions for \"]").ClickAsync());
        return [.. cut.FindAll(".mud-menu-item")];
    }

    /// <summary>Opens the more-actions menu and clicks the item whose trimmed text is exactly <paramref name="text"/>.</summary>
    /// <param name="cut">The rendered panel.</param>
    /// <param name="text">The item's exact text.</param>
    private static async Task ClickMenuItemAsync(IRenderedComponent<ContainerFragment> cut, string text)
    {
        if (cut.FindAll(".mud-menu-item").Count == 0)
        {
            await cut.InvokeAsync(() => cut.Find("button[aria-label^=\"Actions for \"]").ClickAsync());
        }

        IElement item = cut.FindAll(".mud-menu-item").Single(element => string.Equals(element.TextContent.Trim(), text, StringComparison.Ordinal));
        await cut.InvokeAsync(() => item.ClickAsync());
    }

    /// <summary>
    /// Chooses <c>Close task…</c> and confirms it in the dialog it opens (audit E10). The item's handler awaits the dialog's result, so the click is
    /// started rather than awaited, and the returned task completes once the dialog has been answered and the close, if any, has happened.
    /// </summary>
    /// <param name="cut">The rendered panel.</param>
    private static async Task CloseTaskAsync(IRenderedComponent<ContainerFragment> cut)
    {
        Task closing = await StartCloseTaskAsync(cut);
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Close task", StringComparison.Ordinal)).ClickAsync());
        await closing;
    }

    /// <summary>Chooses <c>Close task…</c> and returns once the confirm dialog's buttons are rendered, with the click's task for the test to await after it answers.</summary>
    /// <param name="cut">The rendered panel.</param>
    private static async Task<Task> StartCloseTaskAsync(IRenderedComponent<ContainerFragment> cut)
    {
        if (cut.FindAll(".mud-menu-item").Count == 0)
        {
            await cut.InvokeAsync(() => cut.Find("button[aria-label^=\"Actions for \"]").ClickAsync());
        }

        IElement item = cut.FindAll(".mud-menu-item").Single(element => string.Equals(element.TextContent.Trim(), "Close task…", StringComparison.Ordinal));
        Task closing = cut.InvokeAsync(() => item.ClickAsync());
        Task shown = cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        // A click that fails (no such item, a stale handler) completes first; awaiting it surfaces that error instead of a bare wait timeout.
        await await Task.WhenAny(closing, shown);
        await shown;
        return closing;
    }

    /// <summary>
    /// The fixed instant <see cref="History_EntryDate_FormatsAsHumanLocalTimeViaInjectedClock"/>
    /// gives its <see cref="ManualTimeProvider"/>, so the Task it creates carries a known,
    /// deterministic <see cref="ChangeLogEntry.At"/>.
    /// </summary>
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// A made-up, portable +02:00 zone for the same test's <see cref="ManualTimeProvider"/> - never a
    /// named system zone, which may not exist under every OS/ICU combination.
    /// </summary>
    private static readonly TimeZoneInfo FixedZone = TimeZoneInfo.CreateCustomTimeZone("Fixed+02", TimeSpan.FromHours(2), "Fixed+02", "Fixed+02");
}
