namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MudBlazor;
using MudBlazor.Extensions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins Spec §13.6's core state machine for <c>TaskDetail</c>, RED-only (14.1.t): loading a Task by
/// id, a pending edit driving the "Unsaved edits" alert, Revert, the Save label/notice following each
/// reachable <see cref="WakePreview"/> row, Save actually honouring <c>baseVersion</c>, and a save
/// failure (<see cref="IOException"/>) keeping <c>pending</c> instead of losing it. Corrections-B5 D14
/// item 2 names the id parameter <c>Id</c> (not <c>TaskId</c>); item 4 settles that a Human save can
/// never see <see cref="WakeBlock.BudgetPaused"/> (14.5.t's job) and separates the always-on "budget
/// banner" from this table; item 5 pins the exact Offline and Disabled notice wording, which differs
/// from Spec §13.6's own row text for Offline. Status, blockers, tags, dates (14.2), the conflict UI
/// (14.3), Expand/Make a copy/Close/Change log/<c>MudExitPrompt</c> (14.4) and the wake toast/AI-reacting
/// chip/budget banner (14.5) are each a later task's job and are not pinned here.
/// </summary>
public sealed class TaskDetailTests
{
    /// <summary>Rendering with an existing Task's id shows that Task's id and title (Spec §13.6 Title row).</summary>
    [Fact]
    public async Task Load_ExistingTaskId_ShowsIdAndTitle()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Ship the thing");
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal($"{task.Id}: Ship the thing", TextOf(cut, ".task-detail-title-display"));
    }

    /// <summary>Editing Priority adds it to <c>pending</c> and shows the "Unsaved edits" alert with <c>role="status"</c> (Spec §13.6).</summary>
    [Fact]
    public async Task EditingPriority_AddsToPending_ShowsUnsavedEditsAlertWithStatusRole()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        Assert.Empty(cut.FindAll(".task-detail-unsaved"));

        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));

        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
        Assert.Equal("status", cut.Find(".task-detail-unsaved").GetAttribute("role"));
    }

    /// <summary>Revert clears <c>pending</c>, hiding the "Unsaved edits" alert and restoring the loaded Priority (Spec §13.6).</summary>
    [Fact]
    public async Task Revert_ClearsPending_HidesUnsavedEditsAlert()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));
        Assert.Contains("Unsaved edits", cut.Markup, StringComparison.Ordinal);

        FindButton(cut, "Revert").Click();

        Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(TaskPriority.Medium, cut.FindComponents<MudSelect<TaskPriority>>().Single().Instance.GetState(x => x.Value));
    }

    /// <summary>An asleep assignee (Spec §13.6 table): notice "Nova is asleep and will be notified." and the "Save &amp; Notify Nova" label.</summary>
    [Fact]
    public async Task SaveLabelAndNotice_AssigneeAsleep_ShowsAsleepNoticeAndNotifyLabel()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        TaskItem task = CreateTask(harness, assignee: "Nova");
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("Nova is asleep and will be notified.", TextOf(cut, ".task-detail-wake-notice"));
        Assert.Equal("Save & Notify Nova", FindButton(cut, "Save & Notify Nova").TextContent.Trim());
    }

    /// <summary>An awake assignee (a Turn running) shows "Nova is awake and will be notified." and the same Notify label (Spec §13.6).</summary>
    [Fact]
    public async Task SaveLabelAndNotice_AssigneeAwake_ShowsAwakeNoticeAndNotifyLabel()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string novaId = harness.NovaId ?? throw new InvalidOperationException("NovaId missing");
        harness.Gateway.SetOnline(novaId);
        harness.TurnActivity.Begin(novaId, "room-1");
        TaskItem task = CreateTask(harness, assignee: "Nova");
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("Nova is awake and will be notified.", TextOf(cut, ".task-detail-wake-notice"));
        Assert.Equal("Save & Notify Nova", FindButton(cut, "Save & Notify Nova").TextContent.Trim());
    }

    /// <summary>
    /// An offline assignee (corrections-B5 D14 item 5's exact wording, which supersedes Spec §13.6's
    /// own row text): "Nova is offline, so this change won't reach them.", with the plain "Save" label
    /// (no Notify - Spec §13.6's table row).
    /// </summary>
    [Fact]
    public async Task SaveLabelAndNotice_AssigneeOffline_ShowsOfflineNoticeAndPlainSaveLabel()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, assignee: "Nova");
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("Nova is offline, so this change won't reach them.", TextOf(cut, ".task-detail-wake-notice"));
        Assert.Equal("Save", FindButton(cut, "Save").TextContent.Trim());
    }

    /// <summary>
    /// No notice and the plain "Save" label, one case per guard (settled by the delivery manager,
    /// J37): an unassigned Task (<see cref="WakeBlock.NoAssignee"/>) and a Task assigned to the Human
    /// (<see cref="WakeBlock.AssigneeIsHuman"/>) are each their own <see cref="TaskItem.Assignee"/>
    /// input, so each gets its own case rather than one exemplar for the whole Spec §13.6 row.
    /// <see cref="WakeBlock.AssigneeIsActor"/> is the row's third member but has no case here: it is
    /// structurally unreachable from <c>TaskDetail</c>, not merely untested. <c>TaskDetail</c> always
    /// calls <see cref="TaskTriggerService.Preview"/> with the Human actor (Spec §13.6), so
    /// "assignee == actor.Name" can only be true when the assignee IS the Human - exactly the
    /// <c>AssigneeIsHuman</c> input above - and <see cref="TaskTriggerService.Preview"/>'s own guard
    /// order checks <c>AssigneeIsHuman</c> first and returns before ever reaching the
    /// <c>AssigneeIsActor</c> check (<c>TaskTriggerService.cs</c>). There is no assignee value that
    /// makes <c>TaskDetail</c> see <c>AssigneeIsActor</c>; that guard only fires for an Agent's own
    /// edit, a different actor TaskDetail never passes.
    /// </summary>
    /// <param name="assignee"><see langword="null"/> for <see cref="WakeBlock.NoAssignee"/>, or the Human's own Name for <see cref="WakeBlock.AssigneeIsHuman"/>.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("You")]
    public async Task SaveLabelAndNotice_NoAssigneeOrAssigneeIsHuman_ShowsNoNoticeAndPlainSaveLabel(string? assignee)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, assignee: assignee);
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, task.Id);

        Assert.DoesNotContain("will be notified", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("won't reach them", cut.Markup, StringComparison.Ordinal);
        FindButton(cut, "Save");
    }

    /// <summary>Wake-ups turned off (corrections-B5 D14 item 5): "Wake-ups are off." and the plain "Save" label.</summary>
    [Fact]
    public async Task SaveLabelAndNotice_WakeDisabled_ShowsWakeUpsAreOffNotice()
    {
        await using DisabledWakeStack stack = await DisabledWakeStack.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = stack.CreateTask(assignee: "Nova");
        await using MudBunitContext ctx = stack.NewContext();

        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("Wake-ups are off.", TextOf(cut, ".task-detail-wake-notice"));
        Assert.Equal("Save", FindButton(cut, "Save").TextContent.Trim());
    }

    /// <summary>
    /// Saving persists the pending Priority through <see cref="TaskService.Update"/>, clears <c>pending</c>
    /// (the "Unsaved edits" alert disappears) and the newly saved Priority is what is shown - proving the
    /// Save button actually calls <see cref="TaskService"/> rather than only updating local state.
    /// </summary>
    [Fact]
    public async Task Save_PersistsPendingPriorityChange_AndClearsPending()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));

        FindButton(cut, "Save").Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));
        TaskItem? saved = harness.Store.Get(task.Id);
        Assert.Equal(TaskPriority.Urgent, saved?.Priority);
    }

    /// <summary>
    /// While <c>pending</c> holds an edit, an incoming <see cref="TaskEvents.TaskChanged"/> for the same
    /// Task (someone else's save) updates the displayed <c>loaded</c> state (the Title changes) but must
    /// not overwrite the Human's own untouched pending edit (corrections-B5 D14 item 6: "keep
    /// <c>baseVersion</c> while <c>pending</c> is non-empty").
    /// </summary>
    [Fact]
    public async Task IncomingTaskChanged_WithPendingEdits_KeepsThePendingPriorityValue()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium, title: "Original title");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));

        TaskResult outsideEdit = harness.Service.Update(
            task.Id,
            new TaskPatch { Title = "Retitled by someone else" },
            baseVersion: null,
            TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(outsideEdit);

        cut.WaitForAssertion(() => Assert.Contains("Retitled by someone else", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(TaskPriority.Urgent, cut.FindComponents<MudSelect<TaskPriority>>().Single().Instance.GetState(x => x.Value));
    }

    /// <summary>
    /// A concurrent save that touches the same field (Priority) the Human is mid-editing produces a
    /// <see cref="TaskResult.Conflict"/> when the Human then clicks Save - proving Save actually passes
    /// <c>baseVersion</c> (the version from when the panel opened, corrections-B5 D14 item 6), not
    /// <see langword="null"/> or the just-refreshed <c>loaded.Version</c>: either of those would let the
    /// Save silently overwrite the concurrent change instead of conflicting. 14.3.t owns the conflict
    /// resolution UI; this only asserts the edit was not silently saved (the "Unsaved edits" alert is
    /// still shown).
    /// </summary>
    [Fact]
    public async Task Save_WithConcurrentPriorityChange_DoesNotSilentlyOverwrite()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium, assignee: null);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));

        TaskResult outsideEdit = harness.Service.Update(
            task.Id,
            new TaskPatch { Priority = TaskPriority.Low },
            baseVersion: null,
            TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(outsideEdit);
        cut.WaitForAssertion(() => Assert.Contains("Unsaved edits", cut.Markup, StringComparison.Ordinal));

        FindButton(cut, "Save").Click();

        Assert.Contains("Unsaved edits", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(TaskPriority.Low, harness.Store.Get(task.Id)?.Priority);
    }

    /// <summary>
    /// The Task disappearing from the store (deleted, or its file removed outside Huddle) is reported as
    /// "This Task was deleted", with Save disabled (corrections-B5 D14 item 6).
    /// </summary>
    [Fact]
    public async Task IncomingTaskChanged_TaskNoLongerFound_ShowsDeletedMessageAndDisablesSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        File.Delete(task.Path);
        harness.Store.RebuildFromWatcher();

        cut.WaitForAssertion(() => Assert.Equal("This Task was deleted", TextOf(cut, ".task-detail-deleted")));
        Assert.True(cut.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), "Save", StringComparison.Ordinal)).HasAttribute("disabled"));
    }

    /// <summary>
    /// A save that fails with <see cref="IOException"/> (the file is locked by another handle, the same
    /// failure mode <see cref="TaskStore"/>'s own atomic write can hit) keeps <c>pending</c> - the
    /// "Unsaved edits" alert stays - and shows the error in a <see cref="MudAlert"/> with
    /// <c>role="alert"</c>, following the <c>TeammateCard.SaveAsync</c> precedent (Spec §13.6 "Save
    /// errors"). Windows-only: mandatory file locking via <see cref="FileShare.None"/> is not enforced on
    /// Linux (the same guard <c>TaskStoreTests.Constructor_UnreadableFile_RejectedNotThrown</c> uses).
    /// </summary>
    [Fact]
    public async Task Save_IOExceptionFromLockedFile_KeepsPendingAndShowsAlertRole()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Linux does not mandatory-lock files opened with FileShare.None");
            return;
        }

        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));

        using FileStream lockHandle = new(task.Path, FileMode.Open, FileAccess.Read, FileShare.None);

        FindButton(cut, "Save").Click();

        // The IOException's own message is OS/locale-dependent (a locked-file wording .NET supplies),
        // so only the alert's role is asserted exactly here; its element existing at all, uniquely,
        // already proves the error surfaced.
        cut.WaitForAssertion(() => Assert.Equal("alert", cut.Find(".task-detail-save-error").GetAttribute("role")));
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>
    /// Title (Spec §13.6 Title row: "a pencil button turns into a <c>MudTextField</c>"): clicking the
    /// pencil reveals the text field, and editing it adds to <c>pending</c> - the "Unsaved edits" alert
    /// appears, the same as Priority's own pending-edit test.
    /// </summary>
    [Fact]
    public async Task EditingTitle_AddsToPending_ShowsUnsavedEditsAlert()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Original title");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        Assert.Empty(cut.FindAll(".task-detail-unsaved"));

        cut.Find("button[aria-label=\"Edit title\"]").Click();
        IRenderedComponent<MudTextField<string>> titleField = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-title", StringComparison.Ordinal));
        await cut.InvokeAsync(() => titleField.Instance.ValueChanged.InvokeAsync("Retitled by the Human"));

        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
        Assert.Equal("Retitled by the Human", titleField.Instance.GetState(x => x.Value));
    }

    /// <summary>
    /// Description (Spec §13.6 Description row, corrections-B5 D14 item 3): the Edit-mode text field
    /// is <c>Lines="10" Sizing="InputSizing.Auto" MaxLines="30"</c> (no <c>AutoGrow</c>, which does not
    /// exist in MudBlazor 9.10), and editing it adds to <c>pending</c>.
    /// </summary>
    [Fact]
    public async Task EditingDescription_AddsToPending_AndUsesTheSpecifiedTextFieldParameters()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        IRenderedComponent<MudTextField<string>> descriptionField = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-description", StringComparison.Ordinal));
        Assert.Equal(10, descriptionField.Instance.Lines);
        Assert.Equal(InputSizing.Auto, descriptionField.Instance.Sizing);
        Assert.Equal(30, descriptionField.Instance.MaxLines);

        await cut.InvokeAsync(() => descriptionField.Instance.ValueChanged.InvokeAsync("Some **bold** plan."));

        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>
    /// Description Edit/Preview toggle (Spec §13.6, corrections-B5 D14 item 13): switching to Preview
    /// renders the pending Description through <see cref="MarkdownRenderer.ToHtml(string)"/> as a
    /// <c>MarkupString</c> - <c>**bold**</c> becomes <c>&lt;strong&gt;bold&lt;/strong&gt;</c>,
    /// not the literal asterisks.
    /// </summary>
    [Fact]
    public async Task DescriptionPreview_ShowsRenderedMarkdown()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudTextField<string>> descriptionField = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-description", StringComparison.Ordinal));
        await cut.InvokeAsync(() => descriptionField.Instance.ValueChanged.InvokeAsync("Some **bold** plan."));

        FindButton(cut, "Preview").Click();

        Assert.Equal("bold", TextOf(cut, ".task-detail-description-preview strong"));
        Assert.DoesNotContain("**bold**", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Budget banner (corrections-B5 D14 item 4, settled to 14.1 by the delivery manager, J38):
    /// once <see cref="TaskActivity.Budget(TaskId)"/> is <see cref="WakeBudget.Exhausted"/>, the exact
    /// Spec §13.6 <c>BudgetPaused</c> text appears with an "Allow 10 more" button, and the Save label
    /// stays the plain "Save" - never "Save &amp; Notify", since (as item 4 states) a Human save can
    /// never itself see <see cref="WakeBlock.BudgetPaused"/> from <see cref="TaskTriggerService.Preview"/>.
    /// </summary>
    [Fact]
    public async Task BudgetBanner_Exhausted_ShowsExactTextAndAllowMoreButton_SaveLabelStaysPlain()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, assignee: "Nova");
        ExhaustBudget(harness, task.Id);
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, task.Id);

        Assert.Equal("Wake-ups for this task are paused after 10 changes by Teammates.", TextOf(cut, ".task-detail-budget-banner-text"));
        Assert.Equal("Allow 10 more", FindButton(cut, "Allow 10 more").TextContent.Trim());
        Assert.Equal("Save", FindButton(cut, "Save").TextContent.Trim());
    }

    /// <summary>Clicking "Allow 10 more" calls <see cref="TaskActivity.Grant(TaskId)"/>: the budget is no longer exhausted, and the banner disappears.</summary>
    [Fact]
    public async Task BudgetBanner_ClickAllowMore_CallsGrant_AndTheBannerDisappears()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, assignee: "Nova");
        ExhaustBudget(harness, task.Id);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        Assert.Equal("Wake-ups for this task are paused after 10 changes by Teammates.", TextOf(cut, ".task-detail-budget-banner-text"));

        FindButton(cut, "Allow 10 more").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".task-detail-budget-banner")));
        Assert.False(harness.TaskActivity.Budget(task.Id).Exhausted);
    }

    /// <summary>No banner while the budget is not exhausted (the default for a freshly created Task).</summary>
    [Fact]
    public async Task BudgetBanner_NotExhausted_ShowsNoBanner()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, assignee: "Nova");
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, task.Id);

        Assert.DoesNotContain("Wake-ups for this task are paused", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Allow", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Counts <see cref="TeamOptions.Tasks"/>' <see cref="TasksOptions.AgentWakeBudget"/> Agent-made wakes against <paramref name="id"/>, exhausting its budget.</summary>
    /// <param name="harness">The stack whose <see cref="TaskActivity"/> is exhausted.</param>
    /// <param name="id">The Task to exhaust the budget for.</param>
    private static void ExhaustBudget(TaskToolHarness harness, TaskId id)
    {
        for (int i = 0; i < harness.Options.Value.Tasks.AgentWakeBudget; i++)
        {
            harness.TaskActivity.CountAgentWake(id);
        }
    }

    /// <summary>Creates a Task through <see cref="TaskService"/> on the given harness with the given fields, defaulting to no assignee.</summary>
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

    /// <summary>Registers the harness's services into a fresh <see cref="MudBunitContext"/>, the same pattern <c>TasksPageTests</c> uses.</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Renders <c>TaskDetail</c> in Panel mode for <paramref name="id"/>, inside <see cref="MudBunitContext.RenderWithPopovers"/> (the Won't-do menu and pickers are popovers).</summary>
    private static IRenderedComponent<ContainerFragment> RenderPanel(MudBunitContext ctx, TaskId id)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Panel);
            builder.CloseComponent();
        });
    }

    /// <summary>Finds the one <c>&lt;button&gt;</c> whose trimmed text is exactly <paramref name="text"/>.</summary>
    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>The trimmed text content of the one element matching <paramref name="cssSelector"/> - for pinning an exact text against its own element rather than a substring of the whole rendered markup.</summary>
    private static string TextOf(IRenderedComponent<ContainerFragment> cut, string cssSelector) =>
        cut.Find(cssSelector).TextContent.Trim();

    /// <summary>
    /// A from-scratch Tasks stack, independent of <see cref="TaskToolHarness"/>, whose <see cref="TeamOptions"/>
    /// has <see cref="TasksOptions.WakeEnabled"/> off - the one option <see cref="TaskToolHarness"/> cannot
    /// vary, since it always builds its own fixed <see cref="TeamOptions"/>. Mirrors the harness's own
    /// construction (corrections-B4 D10 item 2's harness, and the "Constructing a real ChatService in a
    /// test" pattern) rather than editing the shared harness, which every other D12-D14 wave's agent is
    /// touching concurrently this run.
    /// </summary>
    private sealed class DisabledWakeStack : IAsyncDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly PersonaStore personas;
        private readonly TaskStore store;
        private readonly TaskService service;
        private readonly TaskEvents events;
        private readonly TaskTriggerService triggers;
        private readonly TaskActivity taskActivity;
        private readonly SqliteTeamDirectory directory;
        private readonly IOptions<TeamOptions> options;

        private DisabledWakeStack()
        {
            this.options = Options.Create(new TeamOptions { DataDir = this.dir.Path, Tasks = new TasksOptions { WakeEnabled = false } });

            this.personas = new PersonaStore(
                this.options,
                new PersonaModelStore(this.options),
                new PersonaEffortStore(this.options),
                NullLogger<PersonaStore>.Instance);
            this.personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Platform"]), "You are Nova.");

            this.store = new TaskStore(this.options, this.personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
            TaskIdAllocator ids = new(this.options);
            this.events = new TaskEvents();
            this.service = new TaskService(this.store, ids, this.events, this.personas, this.options, TimeProvider.System, NullLogger<TaskService>.Instance);

            TurnActivity turnActivity = new();
            this.taskActivity = new TaskActivity(this.options);
            PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
            Agency.Huddle.Tests.Ui.FakeAgentGateway gateway = new();

            this.directory = new SqliteTeamDirectory(this.options);
            FileChatStore chatStore = new(this.options, NullLogger<FileChatStore>.Instance);
            RoomEvents roomEvents = new(NullLogger<RoomEvents>.Instance);
            ProposalStore proposals = new(roomEvents);
            ChatService chat = new(this.directory, chatStore, roomEvents, new FakeMentionAliasSource(), this.options, proposals, NullLogger<ChatService>.Instance);
            OwnPosts ownPosts = new(this.options);

            this.triggers = new TaskTriggerService(
                this.events,
                this.store,
                this.taskActivity,
                turnActivity,
                chat,
                this.directory,
                this.personas,
                gateway,
                health,
                new FakePromptSource(),
                this.options,
                TimeProvider.System,
                NullLogger<TaskTriggerService>.Instance,
                ownPosts);
        }

        /// <summary>Builds the stack and seeds its <see cref="SqliteTeamDirectory"/> with the Human row.</summary>
        /// <param name="ct">Cancels the seeding call.</param>
        public static async Task<DisabledWakeStack> CreateAsync(CancellationToken ct)
        {
            DisabledWakeStack stack = new();
            await stack.directory.InitializeAsync("You", ct);
            return stack;
        }

        /// <summary>Creates a Task through this stack's own <see cref="TaskService"/>.</summary>
        /// <param name="assignee">The Task's assignee Name, or <see langword="null"/> for none.</param>
        public TaskItem CreateTask(string? assignee)
        {
            TaskResult result = this.service.Create(new TaskDraft("T", "Platform", null, Assignee: assignee), TaskActors.Human(this.options.Value));
            return Assert.IsType<TaskResult.Saved>(result).Task;
        }

        /// <summary>Registers this stack's own services - not <see cref="TaskToolHarness"/>'s - into a fresh <see cref="MudBunitContext"/>.</summary>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.options);
            ctx.Services.AddSingleton(this.personas);
            ctx.Services.AddSingleton(this.store);
            ctx.Services.AddSingleton(this.events);
            ctx.Services.AddSingleton(this.service);
            ctx.Services.AddSingleton(this.triggers);
            ctx.Services.AddSingleton(this.taskActivity);
            return ctx;
        }

        /// <summary>Disposes every disposable service above, then the temp directory itself.</summary>
        public ValueTask DisposeAsync()
        {
            this.triggers.Dispose();
            this.service.Dispose();
            this.store.Dispose();
            this.personas.Dispose();
            this.dir.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
