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
/// Pins Spec §13.6's buttons, Expand, Make a copy, Close/Reopen and the Change log, RED-only (14.4.t):
/// Expand closes the Panel and opens <c>TaskDetailDialog</c> with only the id, guarded by
/// <c>ConfirmDiscardAsync</c>'s "Discard changes?" message box when edits are pending (corrections-B5
/// D14 item 7); Cancel discards and closes unconditionally, with no confirmation, since the whole point
/// of clicking Cancel is to discard (corrections-B7 "14.4" item 1, Spec §13.6 Buttons); Make a copy opens
/// a new dialog in create mode from the Panel's own <em>effective</em> (pending-included) fields and
/// writes nothing until that copy is itself saved (Spec §13.6); Close task saves any pending edit and
/// closes as one <c>Update</c> then <c>Close</c>, stopping instead of closing on a <c>Conflict</c> or
/// <c>Refused</c> (corrections-B5 D14 item 9); a Closed Task shows Reopen; the Change log is a collapsed
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
    /// <summary>Expand with nothing pending opens <c>TaskDetailDialog</c> with only the id - no confirmation, no <c>Draft</c> - and it independently loads the same Task (Spec §13.6).</summary>
    [Fact]
    public async Task Expand_NoPendingEdits_OpensDialogWithOnlyId_AndClosesThePanel()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Ship the thing");
        bool closed = false;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id, onClose: EventCallback.Factory.Create(this, () => closed = true));

        await cut.InvokeAsync(() => cut.Find(".task-detail-expand").Click());

        IRenderedComponent<TaskDetailDialog> dialog = cut.FindComponent<TaskDetailDialog>();
        Assert.Equal(task.Id, dialog.Instance.Id);
        Assert.Null(dialog.Instance.Draft);
        Assert.Equal(TaskDetailMode.Expanded, dialog.FindComponent<TaskDetail>().Instance.Mode);
        Assert.Equal($"{task.Id}: Ship the thing", TextOf(dialog, ".task-detail-title-display"));
        Assert.True(closed);
    }

    /// <summary>Expand with a pending edit asks "Discard changes?" first (corrections-B5 D14 item 7); Cancel on that message box keeps the Panel open with the edit intact and never opens the dialog.</summary>
    [Fact]
    public async Task Expand_WithPendingEdits_AsksDiscardChangesFirst_CancelKeepsThePanelAndPending()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        bool closed = false;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id, onClose: EventCallback.Factory.Create(this, () => closed = true));
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        _ = cut.InvokeAsync(() => cut.Find(".task-detail-expand").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        Assert.Equal("Discard changes?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Your unsaved edits to this task will be lost.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(["Cancel", "Discard"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).Click());

        Assert.Empty(cut.FindComponents<TaskDetailDialog>());
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
        Assert.False(closed);
    }

    /// <summary>Confirming "Discard changes?" from Expand clears <c>pending</c>, opens the dialog with only the id, and closes the Panel.</summary>
    [Fact]
    public async Task Expand_WithPendingEdits_ConfirmDiscard_OpensDialogAndClosesThePanel()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        bool closed = false;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id, onClose: EventCallback.Factory.Create(this, () => closed = true));
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        _ = cut.InvokeAsync(() => cut.Find(".task-detail-expand").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        Assert.Equal("Discard changes?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Your unsaved edits to this task will be lost.", cut.Find(".mud-dialog-content").TextContent.Trim());
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Discard", StringComparison.Ordinal)).Click());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<TaskDetailDialog>()));
        Assert.Equal(task.Id, cut.FindComponent<TaskDetailDialog>().Instance.Id);
        Assert.Equal(TaskPriority.Medium, harness.Store.Get(task.Id)?.Priority);
        Assert.True(closed);
    }

    /// <summary><see cref="TaskDetail.ConfirmDiscardAsync"/> (the method the page calls before switching <c>Id</c>, corrections-B5 D14 item 7) returns <see langword="true"/> immediately, with no message box, when nothing is pending.</summary>
    [Fact]
    public async Task ConfirmDiscardAsync_NoPending_ReturnsTrueWithNoMessageBox()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
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
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id, onClose: EventCallback.Factory.Create(this, () => closed = true));
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        await cut.InvokeAsync(() => cut.Find(".task-detail-cancel").Click());

        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
        Assert.Equal(TaskPriority.Medium, harness.Store.Get(task.Id)?.Priority);
        Assert.True(closed);
    }

    /// <summary>Make a copy opens a new <c>TaskDetailDialog</c> in create mode from the Panel's own effective (pending-included) fields: <c>Copy of</c> title, Backlog status, the assignee kept - and writes nothing to the Store (Spec §13.6).</summary>
    [Fact]
    public async Task MakeACopy_OpensCreateModeDialog_FromEffectiveFields_AndWritesNothing()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, title: "Ship the thing", priority: TaskPriority.Medium, assignee: "Nova");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);
        int tasksBefore = harness.Store.All.Count;

        await cut.InvokeAsync(() => cut.Find(".task-detail-copy").Click());

        IRenderedComponent<TaskDetailDialog> dialog = cut.FindComponent<TaskDetailDialog>();
        Assert.Null(dialog.Instance.Id);
        TaskDraft draft = dialog.Instance.Draft ?? throw new InvalidOperationException("Draft missing.");
        Assert.Equal("Copy of Ship the thing", draft.Title);
        Assert.Equal(TaskState.Backlog, draft.Status);
        Assert.Equal(TaskPriority.Urgent, draft.Priority);
        Assert.Equal("Nova", draft.Assignee);
        Assert.Equal(task.Location.Team, draft.Team);
        Assert.Equal(tasksBefore, harness.Store.All.Count);
        Assert.Equal(TaskPriority.Medium, harness.Store.Get(task.Id)?.Priority);
    }

    /// <summary>Close task with nothing pending calls only <see cref="TaskService.Close"/>: one new "closed" Change log entry, and the button becomes Reopen.</summary>
    [Fact]
    public async Task CloseTask_NoPendingEdits_ClosesTheTask_AndButtonBecomesReopen()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        int logCountBefore = task.ChangeLog.Count;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        await cut.InvokeAsync(() => FindButton(cut, "Close task").Click());

        TaskItem closed = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        Assert.True(closed.Location.Closed);
        Assert.Equal(logCountBefore + 1, closed.ChangeLog.Count);
        Assert.Equal("closed", closed.ChangeLog[^1].Summary);
        cut.WaitForAssertion(() => FindButton(cut, "Reopen"));
    }

    /// <summary>Close task with a pending edit saves it first, then closes, as one <see cref="TaskService.Update"/> then <see cref="TaskService.Close"/> - two new Change log entries (corrections-B5 D14 item 9).</summary>
    [Fact]
    public async Task CloseTask_WithPendingEdits_SavesThenCloses_AsOneUpdateThenClose()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        int logCountBefore = task.ChangeLog.Count;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        await cut.InvokeAsync(() => FindButton(cut, "Close task").Click());

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
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);
        TaskResult outsideEdit = harness.Service.Update(task.Id, new TaskPatch { Priority = TaskPriority.Low }, baseVersion: null, TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(outsideEdit);

        await cut.InvokeAsync(() => FindButton(cut, "Close task").Click());

        Assert.False(harness.Store.Get(task.Id)?.Location.Closed);
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>When the pending edit's <see cref="TaskService.Update"/> half is refused (an empty Title), Close task stops there: the Task stays open, the problem is shown, and the edit is kept.</summary>
    [Fact]
    public async Task CloseTask_UpdateRefused_DoesNotClose_ShowsTheProblem()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        cut.Find("button[aria-label='Edit title']").Click();
        IRenderedComponent<MudTextField<string>> titleField = cut.FindComponents<MudTextField<string>>().Single(HasClass<MudTextField<string>>("task-detail-title"));
        await cut.InvokeAsync(() => titleField.Instance.ValueChanged.InvokeAsync(""));

        await cut.InvokeAsync(() => FindButton(cut, "Close task").Click());

        Assert.False(harness.Store.Get(task.Id)?.Location.Closed);
        Assert.Equal("Title is empty.", TextOf(cut, ".task-detail-save-error-problem"));
    }

    /// <summary>A Closed Task shows Reopen instead of Close task; clicking it reopens the Task with a "reopened" Change log entry.</summary>
    [Fact]
    public async Task ReopenButton_OnClosedTask_ReopensIt()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        TaskResult closeResult = harness.Service.Close(task.Id, TaskActors.Human(harness.Options.Value));
        TaskItem closed = Assert.IsType<TaskResult.Saved>(closeResult).Task;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        Assert.DoesNotContain(cut.FindAll("button"), HasText("Close task"));

        FindButton(cut, "Reopen").Click();

        TaskItem reopened = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        Assert.False(reopened.Location.Closed);
        Assert.Equal("reopened", reopened.ChangeLog[^1].Summary);
        Assert.Equal(closed.ChangeLog.Count + 1, reopened.ChangeLog.Count);
    }

    /// <summary>
    /// The Change log's header names the entry count (Spec §13.6); expanding it (corrections-B5 D14
    /// item 10, "expand the panel before asserting") then shows one entry per Change log record, each
    /// a <c>Size.Small</c> <see cref="MudTimelineItem"/>. MudBlazor 9.10's <c>MudExpansionPanel</c>
    /// keeps its content in the DOM even while collapsed (only <c>MudCollapse</c>'s own CSS hides it),
    /// so this doesn't assert absence beforehand - only what's true after expanding.
    /// </summary>
    [Fact]
    public async Task ChangeLog_CollapsedByDefault_HeaderNamesTheCount_ExpandingRevealsEntries()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("Change log (1)", cut.Find(".task-detail-changelog .mud-expand-panel-text").TextContent.Trim());

        cut.Find(".task-detail-changelog .mud-expand-panel-header").Click();

        Assert.Single(cut.FindAll(".task-detail-changelog-entry"));
        Assert.Equal($"{harness.Options.Value.HumanName}: created", TextOf(cut, ".task-detail-changelog-entry"));
        Assert.All(cut.FindComponents<MudTimelineItem>(), item => Assert.Equal(Size.Small, item.Instance.Size));
    }

    /// <summary>The Change log lists every entry newest first, as a whole ordered list (R6: never by membership) - one save adds a second, newer entry ahead of "created".</summary>
    [Fact]
    public async Task ChangeLog_ListsEveryEntry_NewestFirst()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Original", priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);
        FindButton(cut, "Save").Click();
        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));

        cut.Find(".task-detail-changelog .mud-expand-panel-header").Click();

        string human = harness.Options.Value.HumanName;
        Assert.Equal(
            [$"{human}: priority: Medium → Urgent", $"{human}: created"],
            cut.FindAll(".task-detail-changelog-entry").Select(e => e.TextContent.Trim()).ToList());
    }

    /// <summary>
    /// A Change log entry's date renders as the Human's local time through the injected
    /// <see cref="TimeProvider"/> (J51), formatted <c>yyyy-MM-dd HH:mm</c> invariant - not
    /// <see cref="DateTimeOffset.ToLocalTime"/>'s machine time zone, so a fixed
    /// <see cref="TimeProvider.LocalTimeZone"/> in a test (a <see cref="ManualTimeProvider"/> built with
    /// <see cref="FixedUtcNow"/> and <see cref="FixedZone"/>, given to <see cref="TaskToolHarness"/>'s
    /// clock parameter) makes it deterministic. The expected text is a literal, not computed from the
    /// same <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/> call the component
    /// itself would use, so a bug in that conversion can't cancel out in both places.
    /// </summary>
    [Fact]
    public async Task ChangeLog_EntryDate_FormatsAsHumanLocalTimeViaInjectedClock()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(
            ct, new ManualTimeProvider(TaskDetailActionsTests.FixedUtcNow, TaskDetailActionsTests.FixedZone));
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        cut.Find(".task-detail-changelog .mud-expand-panel-header").Click();

        Assert.Equal("2026-01-15 12:30", cut.Find(".task-detail-changelog-date").TextContent.Trim());
    }

    /// <summary><c>MudExitPrompt</c> is disabled with nothing pending, and enabled once an edit is made (Spec §13.6 "Leaving with unsaved edits").</summary>
    [Fact]
    public async Task ExitPrompt_DisabledUntilPending_ThenEnabled()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        Assert.True(cut.FindComponent<MudExitPrompt>().Instance.Disabled);

        await EditPriorityAsync(cut, TaskPriority.Urgent);

        Assert.False(cut.FindComponent<MudExitPrompt>().Instance.Disabled);
    }

    /// <summary>
    /// Expanded is a two-column layout (Spec §13.6, J-follow-up review): Priority, Status and Assignee
    /// are inside <c>.task-detail-col-fields</c>, and the Description editor plus Origin are inside
    /// <c>.task-detail-col-side</c> - asserted by DOM containment (<see cref="INode.Contains"/>), the
    /// facts file's own pattern for "is this element inside that one".
    /// </summary>
    [Fact]
    public async Task Expanded_TwoColumnLayout_FieldsLeft_DescriptionAndOriginRight()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = harness.Service.Create(
            new TaskDraft("T", "Platform", null, Assignee: "Nova", OriginRoomId: "room-1"),
            TaskActors.Human(harness.Options.Value)) is TaskResult.Saved saved
            ? saved.Task
            : throw new InvalidOperationException("Create failed.");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderExpanded(ctx, task.Id);

        IElement fieldsColumn = cut.Find(".task-detail-col-fields");
        IElement sideColumn = cut.Find(".task-detail-col-side");

        Assert.True(fieldsColumn.Contains(cut.Find(".task-detail-priority")));
        Assert.True(fieldsColumn.Contains(cut.Find(".task-detail-status")));
        Assert.True(fieldsColumn.Contains(cut.Find(".task-detail-assignee")));
        Assert.True(sideColumn.Contains(cut.Find(".task-detail-description")));
        Assert.True(sideColumn.Contains(cut.Find(".task-detail-origin")));
        Assert.False(sideColumn.Contains(cut.Find(".task-detail-priority")));
        Assert.False(fieldsColumn.Contains(cut.Find(".task-detail-description")));
    }

    /// <summary>Panel is a single column (Spec §13.6): neither column hook renders, but the same controls do.</summary>
    [Fact]
    public async Task Panel_SingleColumn_NoColumnHooksRendered()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        Assert.Empty(cut.FindAll(".task-detail-col-fields"));
        Assert.Empty(cut.FindAll(".task-detail-col-side"));
        Assert.NotEmpty(cut.FindAll(".task-detail-priority"));
        Assert.NotEmpty(cut.FindAll(".task-detail-status"));
        Assert.NotEmpty(cut.FindAll(".task-detail-description"));
    }

    /// <summary>The Budget banner carries <c>role="status"</c> (corrections-B7 "14.4" item 4 - it supersedes the plain <c>MudAlert</c> 14.1.i shipped with no role).</summary>
    [Fact]
    public async Task BudgetBanner_HasStatusRole()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        for (int i = 0; i < harness.Options.Value.Tasks.AgentWakeBudget; i++)
        {
            harness.TaskActivity.CountAgentWake(task.Id);
        }

        await using MudBunitContext ctx = NewContext(harness);
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
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        await cut.InvokeAsync(() => cut.Find(".task-detail-description-toggle").QuerySelectorAll("button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Preview", StringComparison.Ordinal)).Click());

        IElement link = cut.Find($"a[href='/tasks/item/{other.Id}']");
        Assert.Equal(other.Id.ToString(), link.TextContent.Trim());
    }

    /// <summary>A non-create Save that comes back <see cref="TaskResult.Refused"/> shows its problems in the same alert as an <see cref="IOException"/> (Spec §13.6 "Save errors"), keeping <c>pending</c> instead of losing it.</summary>
    [Fact]
    public async Task Save_Refused_ShowsProblemsInTheSaveErrorAlert_KeepsPending()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        cut.Find("button[aria-label='Edit title']").Click();
        IRenderedComponent<MudTextField<string>> titleField = cut.FindComponents<MudTextField<string>>().Single(HasClass<MudTextField<string>>("task-detail-title"));
        await cut.InvokeAsync(() => titleField.Instance.ValueChanged.InvokeAsync(""));

        FindButton(cut, "Save").Click();

        Assert.Equal("Title is empty.", TextOf(cut, ".task-detail-save-error-problem"));
        Assert.Equal("alert", cut.Find(".task-detail-save-error").GetAttribute("role"));
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>Create mode's Save calls <see cref="TaskService.Create"/> with the draft's fields and shows the created Task (corrections-B7 "14.4" item 2 - the path manual test TASKS-01 exercises).</summary>
    [Fact]
    public async Task CreateMode_Save_Success_CallsCreate_AndShowsTheCreatedTask()
    {
        using TaskToolHarness harness = new();
        TaskDraft draft = new("Copy of Ship the thing", "Platform", null, Priority: TaskPriority.Urgent);
        int tasksBefore = harness.Store.All.Count;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderCreate(ctx, draft);

        FindButton(cut, "Save").Click();

        Assert.Equal(tasksBefore + 1, harness.Store.All.Count);
        TaskItem created = harness.Store.All.Single(t => string.Equals(t.Title, "Copy of Ship the thing", StringComparison.Ordinal));
        Assert.Equal(TaskPriority.Urgent, created.Priority);
        Assert.Equal(TaskState.Backlog, created.Status);
        Assert.Equal($"{created.Id}: Copy of Ship the thing", TextOf(cut, ".task-detail-title-display"));
    }

    /// <summary>Create mode's Save shows a <see cref="TaskResult.Refused"/>'s problems exactly and creates nothing (corrections-B7 "14.4" item 2).</summary>
    [Fact]
    public async Task CreateMode_Save_Refused_ShowsProblemsExactly_CreatesNothing()
    {
        using TaskToolHarness harness = new();
        TaskDraft draft = new("", "Platform", null);
        int tasksBefore = harness.Store.All.Count;
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderCreate(ctx, draft);

        FindButton(cut, "Save").Click();

        Assert.Equal(tasksBefore, harness.Store.All.Count);
        Assert.Equal("Title is empty.", TextOf(cut, ".task-detail-save-error-problem"));
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

    /// <summary>A predicate matching a rendered <see cref="IElement"/> button whose trimmed text equals <paramref name="text"/> - for asserting absence with <c>Where</c>/<c>Empty</c> rather than <see cref="FindButton"/>'s single-match lookup.</summary>
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

    /// <summary>Registers the harness's services into a fresh <see cref="MudBunitContext"/> - mirrors <see cref="TaskDetailTests"/>'s own helper.</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Renders <c>TaskDetail</c> in Panel mode for <paramref name="id"/>, inside <see cref="MudBunitContext.RenderWithPopovers"/> - mirrors <see cref="TaskDetailTests"/>'s own helper, with an optional <c>OnClose</c> so a test can observe whether the Panel actually closed.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPanel(MudBunitContext ctx, TaskId id, EventCallback? onClose = null)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Panel);
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
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Expanded);
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
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Panel);
            builder.CloseComponent();
        });
    }

    /// <summary>Finds the one <c>&lt;button&gt;</c> whose trimmed text is exactly <paramref name="text"/> - mirrors <see cref="TaskDetailTests"/>'s own helper.</summary>
    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>The trimmed text content of the one element matching <paramref name="cssSelector"/> - mirrors <see cref="TaskDetailTests"/>'s own helper.</summary>
    private static string TextOf(IRenderedComponent<ContainerFragment> cut, string cssSelector) =>
        cut.Find(cssSelector).TextContent.Trim();

    /// <summary>The trimmed text content of the one element matching <paramref name="cssSelector"/>, inside a dialog rendered through the real <see cref="IDialogService"/> rather than the Panel's own root.</summary>
    private static string TextOf(IRenderedComponent<TaskDetailDialog> dialog, string cssSelector) =>
        dialog.Find(cssSelector).TextContent.Trim();

    /// <summary>
    /// The fixed instant <see cref="ChangeLog_EntryDate_FormatsAsHumanLocalTimeViaInjectedClock"/>
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
