namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using MudBlazor;
using MudBlazor.Extensions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins Spec §13.7 (the conflict UI) and Spec §9.3 (merge vs. conflict), RED-only (14.3.t): a
/// <see cref="TaskResult.Conflict"/> shows a warning alert and one <see cref="MudSimpleTable"/> row
/// per conflicting field, each with a <c>MudRadioGroup&lt;ConflictChoice&gt;</c>; Save stays disabled
/// until every row is resolved; "Take theirs" drops that field from <c>pending</c> so the current
/// (server) value shows again; resolving a row advances <c>baseVersion</c> to
/// <c>conflict.Current.Version</c>, so a clean re-save goes through without conflicting again. Also
/// pins corrections-B5 D14 item 8's composite drop rule (<c>Location</c> drops both Team and Project;
/// <c>Status</c> drops Status, Reason and DuplicateOf together), now testable since 14.2's fields are
/// merged into this worktree. Also pins corrections-B7 "14.3" item 1: <c>SaveAsync</c>'s result switch
/// must not silently drop <see cref="TaskResult.Refused"/> (its problems belong in the same
/// save-error alert as an <see cref="IOException"/>) or <see cref="TaskResult.NotFound"/> (the same
/// "This Task was deleted" state the watcher path already shows, Spec §13.6).
///
/// The actor name in the conflict banner ("{Name} changed this task while you were editing: …") is
/// read from <c>conflict.Current</c>'s own last Change log entry - the Task's own record of who most
/// recently touched it - since <see cref="TaskResult.Conflict"/> itself carries no actor. Every test
/// below makes that concurrent edit as an Agent actor named "Nova", the Spec §13.7 example's own name,
/// so the banner text is pinned exactly rather than to a substring.
///
/// Settled by the delivery manager (J50): both the banner's field list and each row's Field cell use
/// <see cref="TaskDiff.FieldLabel(TaskField)"/> - the same wire-key map (<c>title</c>, <c>due_date</c>,
/// <c>blocked_by</c>, …) a Change log entry's own summary already shows, not the bare enum name (a
/// Human would otherwise see "DueDate"), joined with ", " for more than one field. Revised by J55:
/// <see cref="TaskField.Location"/> labels as <c>team/project</c> (not <c>location</c>, which means
/// nothing to a Human), and <see cref="TaskField.Description"/> still falls back to its lowercase enum
/// name (<c>description</c>), having no wire key of its own. This dispatch's own tests never call
/// <see cref="TaskDiff.FieldLabel(TaskField)"/> to build an expected string - every expected label below
/// is a literal, independent of the production code under test.
/// </summary>
public sealed class TaskDetailConflictTests
{
    /// <summary>
    /// Manual test TASKS-07 finding F15: the conflict banner used to read "You changed this task while
    /// you were editing: priority." for an outside edit, because <see cref="TaskActorKind.OutsideHuddle"/>
    /// carries the Human's own Name. An outside edit - a hand edit on disk, picked up through
    /// <see cref="TaskStore.RebuildFromWatcher"/>, which stamps its Change log entry's Summary
    /// "edited outside Huddle: …" (the same mechanism TASKS-02 exercises) - must instead say "This task
    /// was changed outside Huddle while you were editing: {fields}.".
    /// </summary>
    [Fact]
    public async Task Conflict_OutsideEdit_BannerSaysChangedOutsideHuddle_NotYou()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        TaskItem current = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task not found.");
        File.WriteAllText(current.Path, TaskFileFormat.Compose(current with { Priority = TaskPriority.Low }));
        harness.Store.RebuildFromWatcher();

        await ClickSaveAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(
            "This task was changed outside Huddle while you were editing: priority.",
            TextOf(cut, ".task-detail-conflict-alert")));
    }

    /// <summary>
    /// Manual test TASKS-07 finding F17: at Panel width (~300 px) the conflict table's four columns
    /// overflow horizontally, pushing the radios off-screen. The Panel must use the stacked
    /// <c>.task-detail-conflict-stacked</c> layout instead of a <see cref="MudSimpleTable"/>; the
    /// Expanded layout, which has room for the columns, keeps the table.
    /// </summary>
    [Fact]
    public async Task Conflict_PanelUsesStackedLayout_ExpandedKeepsTable()
    {
        using TaskToolHarness harness = new();
        TaskItem panelTask = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext panelCtx = NewContext(harness);
        var panelCut = RenderPanel(panelCtx, panelTask.Id);
        await CausePriorityConflictAsync(panelCut, harness, panelTask, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        Assert.Single(panelCut.FindAll(".task-detail-conflict-stacked"));
        Assert.Empty(panelCut.FindAll(".task-detail-conflict-table"));

        TaskItem expandedTask = CreateTask(harness, title: "Other", priority: TaskPriority.Medium);
        await using MudBunitContext expandedCtx = NewContext(harness);
        var expandedCut = RenderExpanded(expandedCtx, expandedTask.Id);
        await CausePriorityConflictAsync(expandedCut, harness, expandedTask, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        Assert.Single(expandedCut.FindAll(".task-detail-conflict-table"));
        Assert.Empty(expandedCut.FindAll(".task-detail-conflict-stacked"));
    }

    /// <summary>Contrast to <see cref="Conflict_OutsideEdit_BannerSaysChangedOutsideHuddle_NotYou"/>: an Agent's concurrent edit keeps the original "{Name} changed…" wording exactly.</summary>
    [Fact]
    public async Task Conflict_AgentEdit_BannerKeepsNameChangedWording()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        Assert.Equal("Nova changed this task while you were editing: priority.", TextOf(cut, ".task-detail-conflict-alert"));
    }

    /// <summary>
    /// Manual test TASKS-07 finding F16: a fresh conflict row's <c>MudRadioGroup&lt;ConflictChoice&gt;</c>
    /// used to start at <c>default(ConflictChoice)</c> ("Mine"), so "Keep mine" rendered as checked
    /// while <c>TaskDetail</c> still treated the row as unresolved and kept Save disabled - a choice
    /// the Human never made, with a dead Save button. The group must start with nothing checked, and a
    /// real DOM click on "Keep mine" (not the 14.3 suite's own <c>ValueChanged.InvokeAsync</c>
    /// shortcut, which is why this slipped) must both check it and enable Save.
    /// </summary>
    [Fact]
    public async Task Conflict_FreshRow_NoRadioChecked_AndRealClickChecksItAndEnablesSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        IReadOnlyList<bool> checkedStates = [.. cut.FindAll(".task-detail-conflict-stacked input[type=\"radio\"]").Select(r => r.HasAttribute("checked"))];
        Assert.Equal([false, false], checkedStates);
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));

        IElement keepMineInput = cut.FindAll(".task-detail-conflict-stacked label.mud-radio").Single(l => l.TextContent.Contains("Keep mine", StringComparison.Ordinal)).QuerySelector("input[type=\"radio\"]")
            ?? throw new InvalidOperationException("Keep mine radio input not found.");
        await cut.InvokeAsync(() => keepMineInput.Click());

        Assert.True(cut.Find(".task-detail-conflict-stacked input[type=\"radio\"]").HasAttribute("checked"));
        Assert.False(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>Spec §13.7: a <see cref="TaskResult.Conflict"/> shows a warning alert with <c>role="alert"</c> and the exact banner text for one conflicting field.</summary>
    [Fact]
    public async Task Conflict_OneFieldConflicts_ShowsWarningAlertWithRoleAndExactText()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        Assert.Equal("alert", cut.Find(".task-detail-conflict-alert").GetAttribute("role"));
        Assert.Equal("Nova changed this task while you were editing: priority.", TextOf(cut, ".task-detail-conflict-alert"));
    }

    /// <summary>Spec §13.7: the table has one row for the conflicting field, with Field/Theirs/Yours cells and a <c>MudRadioGroup&lt;ConflictChoice&gt;</c>.</summary>
    [Fact]
    public async Task Conflict_OneFieldConflicts_ShowsOneRowWithFieldTheirsYoursAndRadioGroup()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);

        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        IElement row = Assert.Single(cut.FindAll(".task-detail-conflict-row"));
        Assert.Equal("priority", row.QuerySelector(".task-detail-conflict-field")?.TextContent.Trim());
        Assert.Equal("Low", row.QuerySelector(".task-detail-conflict-theirs")?.TextContent.Trim());
        Assert.Equal("Urgent", row.QuerySelector(".task-detail-conflict-yours")?.TextContent.Trim());
        Assert.Single(cut.FindComponents<MudRadioGroup<ConflictChoice?>>());
    }

    /// <summary>Spec §13.7: two conflicting fields (Title and Priority) each get their own row, and the banner lists both wire-key labels, in <see cref="TaskDiff.Compare(TaskItem, TaskItem)"/>'s own field order.</summary>
    [Fact]
    public async Task Conflict_TwoFieldsConflict_ShowsRowPerFieldAndListsBothInBanner()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Original title", priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditTitleAsync(cut, "Retitled by the Human");
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        harness.Service.Update(
            task.Id,
            new TaskPatch { Title = "Retitled by Nova", Priority = TaskPriority.Low },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("Nova changed this task while you were editing: title, priority.", TextOf(cut, ".task-detail-conflict-alert")));
        Assert.Equal(2, cut.FindAll(".task-detail-conflict-row").Count);
        Assert.Equal(2, cut.FindComponents<MudRadioGroup<ConflictChoice?>>().Count);
    }

    /// <summary>
    /// Settled J50: the field label is the wire key, not the enum name - pinned with a genuinely
    /// multi-word field (<c>DueDate</c> → <c>due_date</c>), which would read "DueDate" under the enum-name
    /// choice this dispatch originally pinned.
    /// </summary>
    [Fact]
    public async Task Conflict_DueDateFieldConflicts_UsesTheWireKeyLabel()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        DateOnly mine = new(2026, 11, 3);
        DateOnly theirs = new(2026, 12, 1);
        await EditDueDateAsync(cut, mine);

        harness.Service.Update(
            task.Id,
            new TaskPatch { DueDate = Optional<DateOnly?>.Set(theirs) },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("Nova changed this task while you were editing: due_date.", TextOf(cut, ".task-detail-conflict-alert")));
        IElement row = cut.FindAll(".task-detail-conflict-row").Single(r => r.TextContent.Contains("due_date", StringComparison.Ordinal));
        Assert.Equal("due_date", row.QuerySelector(".task-detail-conflict-field")?.TextContent.Trim());
        Assert.Equal("2026-12-01", row.QuerySelector(".task-detail-conflict-theirs")?.TextContent.Trim());
        Assert.Equal("2026-11-03", row.QuerySelector(".task-detail-conflict-yours")?.TextContent.Trim());
    }

    /// <summary>Spec §13.7: the Description row shows two read-only, six-line <see cref="MudTextField{T}"/> boxes (Theirs and Yours) rather than plain cell text.</summary>
    [Fact]
    public async Task Conflict_DescriptionField_ShowsTwoReadOnlySixLineTextFieldsInsteadOfPlainCells()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudTextField<string>> descriptionField = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-description", StringComparison.Ordinal));
        await cut.InvokeAsync(() => descriptionField.Instance.ValueChanged.InvokeAsync("Mine: some plan."));

        harness.Service.Update(
            task.Id,
            new TaskPatch { Description = "Theirs: a different plan." },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".task-detail-conflict-row"), r => r.TextContent.Contains("description", StringComparison.Ordinal)));
        IRenderedComponent<MudTextField<string>> theirsBox = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-conflict-theirs", StringComparison.Ordinal));
        IRenderedComponent<MudTextField<string>> yoursBox = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-conflict-yours", StringComparison.Ordinal));
        Assert.True(theirsBox.Instance.ReadOnly);
        Assert.True(yoursBox.Instance.ReadOnly);
        Assert.Equal(6, theirsBox.Instance.Lines);
        Assert.Equal(6, yoursBox.Instance.Lines);
        Assert.Equal("Theirs: a different plan.", theirsBox.Instance.GetState(x => x.Value));
        Assert.Equal("Mine: some plan.", yoursBox.Instance.GetState(x => x.Value));
    }

    /// <summary>Spec §13.7: "Save is enabled again only when every conflicting field has been resolved" - with one row, Save is disabled until that row is chosen.</summary>
    [Fact]
    public async Task Conflict_OneFieldConflicts_SaveDisabledUntilRowChosen_ThenEnabled()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));

        await ChooseConflictAsync(cut, "priority", ConflictChoice.Mine);

        Assert.False(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>Spec §13.7: with two conflicting rows, Save stays disabled after only one is chosen, and enables once both are.</summary>
    [Fact]
    public async Task Conflict_TwoFieldsConflict_SaveDisabledUntilBothRowsChosen()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Original title", priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditTitleAsync(cut, "Retitled by the Human");
        await EditPriorityAsync(cut, TaskPriority.Urgent);
        harness.Service.Update(
            task.Id,
            new TaskPatch { Title = "Retitled by Nova", Priority = TaskPriority.Low },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".task-detail-conflict-alert")));
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));

        await ChooseConflictAsync(cut, "title", ConflictChoice.Mine);
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));

        await ChooseConflictAsync(cut, "priority", ConflictChoice.Theirs);
        Assert.False(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>Spec §13.7: "Take theirs" drops that field from <c>pending</c> - the Priority control reverts to the server's current value, not the Human's edit.</summary>
    [Fact]
    public async Task Conflict_TakeTheirs_DropsFieldFromPending_ControlShowsCurrentValue()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        await ChooseConflictAsync(cut, "priority", ConflictChoice.Theirs);

        Assert.Equal(TaskPriority.Low, cut.FindComponents<MudSelect<TaskPriority>>().Single().Instance.GetState(x => x.Value));
    }

    /// <summary>Spec §13.7: "Keep mine" leaves the Human's edit in <c>pending</c> - the control still shows the Human's value.</summary>
    [Fact]
    public async Task Conflict_KeepMine_LeavesPendingValue_ControlStillShowsTheHumansEdit()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);

        await ChooseConflictAsync(cut, "priority", ConflictChoice.Mine);

        Assert.Equal(TaskPriority.Urgent, cut.FindComponents<MudSelect<TaskPriority>>().Single().Instance.GetState(x => x.Value));
    }

    /// <summary>
    /// Spec §13.7: "Resolving updates pending and sets baseVersion = conflict.Current.Version." Proven
    /// by re-saving after resolving with no further concurrent edit: the second Save goes straight
    /// through (no repeat conflict), the "Keep mine" value is what's written, and the conflict UI is
    /// gone.
    /// </summary>
    [Fact]
    public async Task Conflict_ResolveThenSave_AdvancesBaseVersion_SoTheReSaveSucceeds()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await CausePriorityConflictAsync(cut, harness, task, mine: TaskPriority.Urgent, theirs: TaskPriority.Low);
        await ChooseConflictAsync(cut, "priority", ConflictChoice.Mine);

        await ClickSaveAsync(cut);

        Assert.Empty(cut.FindAll(".task-detail-conflict-alert"));
        Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(TaskPriority.Urgent, harness.Store.Get(task.Id)?.Priority);
    }

    /// <summary>
    /// Corrections-B5 D14 item 8: "A re-save may conflict again (same flow)." Resolving one of two
    /// conflicting fields (Priority, via "Take theirs") leaves the other (Title) unresolved, so Save
    /// stays disabled; resolving Title too ("Keep mine") advances <c>baseVersion</c>, but a further
    /// concurrent edit to that same still-pending Title field before the Human clicks Save conflicts
    /// again, the same way the first one did.
    /// </summary>
    [Fact]
    public async Task Conflict_FurtherConcurrentEditAfterResolving_ConflictsAgain()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Original title", priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditTitleAsync(cut, "Retitled by the Human");
        await EditPriorityAsync(cut, TaskPriority.Urgent);
        harness.Service.Update(
            task.Id,
            new TaskPatch { Title = "Retitled by Nova", Priority = TaskPriority.Low },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".task-detail-conflict-alert")));
        await ChooseConflictAsync(cut, "priority", ConflictChoice.Theirs);
        await ChooseConflictAsync(cut, "title", ConflictChoice.Mine);

        harness.Service.Update(
            task.Id,
            new TaskPatch { Title = "Retitled by Nova again" },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("Nova changed this task while you were editing: title.", TextOf(cut, ".task-detail-conflict-alert")));
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>
    /// Corrections-B5 D14 item 8: "Take theirs" on the composite <c>Location</c> field drops both
    /// <c>Team</c> and <c>Project</c> from <c>pending</c>, even though only Project actually conflicted
    /// with Nova's concurrent edit - proven by pending changing BOTH Team (to a second known Team,
    /// "Design") and Project, so a component that dropped only the one field the diff itself flagged
    /// would leave "Design" behind.
    /// </summary>
    [Fact]
    public async Task Conflict_TakeTheirs_OnLocationField_DropsBothTeamAndProject()
    {
        using TaskToolHarness harness = new();
        harness.Personas.Add(new PersonaIdentity("Zed", "Zed", "Zed", ["Design"]), "You are Zed.");
        TaskItem task = CreateTask(harness, project: "Alpha");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditTeamAsync(cut, "Design");
        await EditProjectAsync(cut, "Beta");

        harness.Service.Update(
            task.Id,
            new TaskPatch { Project = Optional<string?>.Set("Gamma") },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);
        cut.WaitForAssertion(() => Assert.Equal("Nova changed this task while you were editing: team/project.", TextOf(cut, ".task-detail-conflict-alert")));
        Assert.Single(cut.FindAll(".task-detail-conflict-row"));

        await ChooseConflictAsync(cut, "team/project", ConflictChoice.Theirs);

        Assert.Equal("Platform", cut.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-team", StringComparison.Ordinal)).Instance.GetState(x => x.Value));
        Assert.Equal("Gamma", cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal)).Instance.GetState(x => x.Value));
    }

    /// <summary>
    /// Corrections-B5 D14 item 8: "Take theirs" on the composite <c>Status</c> field also drops
    /// <c>DuplicateOf</c> from <c>pending</c> - proven by re-selecting Duplicate afterwards without
    /// picking a new target: a component that only cleared Status itself would leave the earlier
    /// target parked in <c>pending</c>, so Save would come back already enabled instead of needing a
    /// fresh pick.
    /// </summary>
    [Fact]
    public async Task Conflict_TakeTheirs_OnStatusField_AlsoClearsDuplicateOf()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, status: TaskState.ToDo);
        TaskItem other = CreateTask(harness, title: "Other task");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await ClickWontDoItem(cut, TaskState.Duplicate.ToWire());
        IRenderedComponent<MudAutocomplete<TaskItem>> duplicateOf = cut.FindComponents<MudAutocomplete<TaskItem>>().Single(m => string.Equals(m.Instance.Class, "task-detail-duplicate-of", StringComparison.Ordinal));
        TaskItem chosen = await SearchAndPickFirst(cut, duplicateOf, "Other");
        Assert.Equal(other.Id, chosen.Id);

        harness.Service.Update(
            task.Id,
            new TaskPatch { Status = TaskState.InProgress },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);
        cut.WaitForAssertion(() => Assert.Equal("Nova changed this task while you were editing: status.", TextOf(cut, ".task-detail-conflict-alert")));
        Assert.Single(cut.FindAll(".task-detail-conflict-row"));

        await ChooseConflictAsync(cut, "status", ConflictChoice.Theirs);

        Assert.Empty(cut.FindAll(".task-detail-duplicate-of"));
        await ClickWontDoItem(cut, TaskState.Duplicate.ToWire());
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>
    /// Corrections-B5 D14 item 8: "Take theirs" on the composite <c>Status</c> field also drops
    /// <c>Reason</c> from <c>pending</c> - proven by re-selecting Cancelled afterwards: a component that
    /// only cleared Status itself would leave the earlier Reason text parked in <c>pending</c>, so the
    /// Reason box would come back pre-filled instead of empty.
    /// </summary>
    [Fact]
    public async Task Conflict_TakeTheirs_OnStatusField_AlsoClearsReason()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, status: TaskState.ToDo);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await ClickWontDoItem(cut, TaskState.Cancelled.ToWire());
        IRenderedComponent<MudTextField<string>> reason = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-reason", StringComparison.Ordinal));
        await cut.InvokeAsync(() => reason.Instance.ValueChanged.InvokeAsync("no longer needed"));

        harness.Service.Update(
            task.Id,
            new TaskPatch { Status = TaskState.InProgress },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        await ClickSaveAsync(cut);
        cut.WaitForAssertion(() => Assert.Equal("Nova changed this task while you were editing: status.", TextOf(cut, ".task-detail-conflict-alert")));
        Assert.Single(cut.FindAll(".task-detail-conflict-row"));

        await ChooseConflictAsync(cut, "status", ConflictChoice.Theirs);

        Assert.Empty(cut.FindAll(".task-detail-reason"));
        await ClickWontDoItem(cut, TaskState.Cancelled.ToWire());
        IRenderedComponent<MudTextField<string>> reasonAfter = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-reason", StringComparison.Ordinal));
        Assert.Equal(string.Empty, reasonAfter.Instance.GetState(x => x.Value));
    }

    /// <summary>
    /// Corrections-B7 "14.3" item 1: a <see cref="TaskResult.Refused"/> - triggered here by clearing
    /// the Title, "Title is empty." (Spec §9.2) - lists its problem in the same
    /// <c>.task-detail-save-error role="alert"</c> alert an <see cref="IOException"/> uses, and keeps
    /// <c>pending</c> (the "Unsaved edits" alert stays) instead of the switch's <c>default:</c> silently
    /// dropping the result.
    /// </summary>
    [Fact]
    public async Task Save_RefusedResult_ListsProblemInSaveErrorAlert_KeepsPending()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Original title");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditTitleAsync(cut, "   ");

        await ClickSaveAsync(cut);

        Assert.Equal("alert", cut.Find(".task-detail-save-error").GetAttribute("role"));
        Assert.Equal("Title is empty.", TextOf(cut, ".task-detail-save-error"));
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>
    /// Corrections-B7 "14.3" item 1: a <see cref="TaskResult.NotFound"/> from <c>SaveAsync</c> itself
    /// (the Task vanished from the store between the panel's last refresh and the click) shows the same
    /// "This Task was deleted" state Spec §13.6 already defines for the watcher path, rather than the
    /// switch's <c>default:</c> silently doing nothing and leaving the stale pending edit up.
    ///
    /// Deliberately deletes the file directly and never calls <c>harness.Store.RebuildFromWatcher()</c>
    /// (contrast <see cref="CausePriorityConflictAsync"/>, which needs the watcher-raised
    /// <c>TaskChanged</c>/<c>IndexChanged</c> to reach the running component): the Store's in-memory index
    /// still reports the Task present, so no <c>TasksReloaded</c> fires and <c>TaskDetail</c>'s own
    /// <c>DispatchRefresh</c> never runs - there is nothing left to race against. Clicking Save then
    /// reaches <c>TaskService.UpdateCore</c> with a live in-memory <c>current</c> but a missing file on
    /// disk, which is <c>TaskService.cs</c>'s own second <c>NotFound</c> check (<c>ReadText</c> returning
    /// <see langword="null"/>) - so this proves <c>SaveAsync</c>'s own <see cref="TaskResult.NotFound"/>
    /// handling deterministically, on every run, rather than by winning a race against the unrelated
    /// watcher-driven Refresh path <c>TaskDetailTests</c> already covers.
    /// </summary>
    [Fact]
    public async Task Save_NotFoundResult_ShowsDeletedState()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderPanel(ctx, task.Id);
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        File.Delete(task.Path);

        await ClickSaveAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("This Task was deleted", TextOf(cut, ".task-detail-deleted")));
    }

    /// <summary>Edits Priority to <paramref name="mine"/>, then has "Nova" concurrently save <paramref name="theirs"/> with a null <c>baseVersion</c> (merges, Spec §9.3 rule 1), then clicks Save - which must now conflict, since the Human's own patch also touches Priority.</summary>
    private static async Task CausePriorityConflictAsync(IRenderedComponent<ContainerFragment> cut, TaskToolHarness harness, TaskItem task, TaskPriority mine, TaskPriority theirs)
    {
        await EditPriorityAsync(cut, mine);
        TaskResult outsideEdit = harness.Service.Update(
            task.Id,
            new TaskPatch { Priority = theirs },
            baseVersion: null,
            new TaskActor(TaskActorKind.Agent, "Nova", null));
        Assert.IsType<TaskResult.Saved>(outsideEdit);

        await ClickSaveAsync(cut);

        // Nova's edit above raised TaskChanged/IndexChanged synchronously, but TaskDetail's own
        // handler dispatches its requery fire-and-forget (DispatchRefresh, never awaited by anything
        // here) - under full-suite thread-pool pressure that dispatched continuation can still be
        // pending when this returns, so wait for the conflict banner rather than asserting immediately
        // (the same race TaskDetailTests.IncomingTaskChanged_... already guards with WaitForAssertion).
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".task-detail-conflict-alert")));
    }

    /// <summary>Raises the Priority <see cref="MudSelect{T}"/>'s <c>ValueChanged</c>, adding it to <c>pending</c>.</summary>
    private static async Task EditPriorityAsync(IRenderedComponent<ContainerFragment> cut, TaskPriority value)
    {
        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(value));
    }

    /// <summary>Opens the Title editor and raises its <c>ValueChanged</c>, adding it to <c>pending</c> (Spec §13.6 Title row).</summary>
    private static async Task EditTitleAsync(IRenderedComponent<ContainerFragment> cut, string value)
    {
        cut.Find("button[aria-label=\"Edit title\"]").Click();
        IRenderedComponent<MudTextField<string>> titleField = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-title", StringComparison.Ordinal));
        await cut.InvokeAsync(() => titleField.Instance.ValueChanged.InvokeAsync(value));
    }

    /// <summary>Raises the Due date <see cref="MudDatePicker"/>'s <c>DateChanged</c>, adding it to <c>pending</c> (corrections-B5 D14 item 11).</summary>
    private static async Task EditDueDateAsync(IRenderedComponent<ContainerFragment> cut, DateOnly value)
    {
        IRenderedComponent<MudDatePicker> picker = cut.FindComponents<MudDatePicker>().Single(m => string.Equals(m.Instance.Class, "task-detail-due-date", StringComparison.Ordinal));
        await cut.InvokeAsync(() => picker.Instance.DateChanged.InvokeAsync(value.ToDateTime(TimeOnly.MinValue)));
    }

    /// <summary>Raises the Team <see cref="MudSelect{T}"/>'s <c>ValueChanged</c>, adding it to <c>pending</c> (Spec §13.6 Team/Project row).</summary>
    private static async Task EditTeamAsync(IRenderedComponent<ContainerFragment> cut, string value)
    {
        IRenderedComponent<MudSelect<string>> team = cut.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-team", StringComparison.Ordinal));
        await cut.InvokeAsync(() => team.Instance.ValueChanged.InvokeAsync(value));
    }

    /// <summary>Raises the Project <see cref="MudAutocomplete{T}"/>'s <c>ValueChanged</c>, adding it to <c>pending</c> (Spec §13.6 Team/Project row).</summary>
    private static async Task EditProjectAsync(IRenderedComponent<ContainerFragment> cut, string value)
    {
        IRenderedComponent<MudAutocomplete<string>> project = cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal));
        await cut.InvokeAsync(() => project.Instance.ValueChanged.InvokeAsync(value));
    }

    /// <summary>
    /// Opens the Won't-do <c>MudMenu</c> and clicks the item whose text is <paramref name="wireName"/> -
    /// the <c>TaskDetailFieldsTests.ClickWontDoItem</c> pattern, with the same fix that pattern needed:
    /// each Find+Click is its own <c>InvokeAsync</c> (the <c>ClickSaveAsync</c> pattern below), because a
    /// fire-and-forget dispatched re-render can otherwise land between the two plain synchronous clicks
    /// under parallel test-exe load and throw <c>UnknownEventHandlerIdException</c>.
    /// </summary>
    private static async Task ClickWontDoItem(IRenderedComponent<ContainerFragment> cut, string wireName)
    {
        await cut.InvokeAsync(() => cut.Find("button[aria-label=\"Won't do\"]").Click());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(i => string.Equals(i.TextContent.Trim(), wireName, StringComparison.Ordinal)).Click());
    }

    /// <summary>Awaits <paramref name="autocomplete"/>'s own <c>SearchFunc</c> for <paramref name="query"/>, then invokes <c>ValueChanged</c> with the first result - the <c>TaskDetailFieldsTests.SearchAndPickFirst</c> pattern; never constructs a <typeparamref name="T"/> by hand.</summary>
    private static async Task<T> SearchAndPickFirst<T>(IRenderedComponent<ContainerFragment> cut, IRenderedComponent<MudAutocomplete<T>> autocomplete, string query)
    {
        Func<string?, CancellationToken, Task<IEnumerable<T>>?> search = autocomplete.Instance.SearchFunc
            ?? throw new InvalidOperationException("Autocomplete has no SearchFunc.");
        Task<IEnumerable<T>>? searchTask = search(query, Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        T first = (await searchTask).First();
        await cut.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(first));
        return first;
    }

    /// <summary>
    /// Resolves the conflict row whose own text contains <paramref name="fieldLabel"/> (a
    /// <see cref="TaskDiff.FieldLabel(TaskField)"/> wire key) with <paramref name="choice"/>, by
    /// invoking that row's own <c>MudRadioGroup&lt;ConflictChoice&gt;</c> instance directly - the
    /// established <c>Instance.ValueChanged.InvokeAsync(...)</c> pattern this suite uses for every
    /// other MudBlazor input, rather than a raw DOM click, since the radio groups are recreated by
    /// <c>@foreach</c> on every conflict-state re-render.
    /// </summary>
    private static async Task ChooseConflictAsync(IRenderedComponent<ContainerFragment> cut, string fieldLabel, ConflictChoice choice)
    {
        List<IElement> rows = [.. cut.FindAll(".task-detail-conflict-row")];
        int index = rows.FindIndex(r => r.TextContent.Contains(fieldLabel, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException($"No conflict row for '{fieldLabel}'.");
        }

        IRenderedComponent<MudRadioGroup<ConflictChoice?>> group = cut.FindComponents<MudRadioGroup<ConflictChoice?>>()[index];
        await cut.InvokeAsync(() => group.Instance.ValueChanged.InvokeAsync(choice));
    }

    /// <summary>Creates a Task through <see cref="TaskService"/> on the given harness, defaulting to no assignee - mirrors <c>TaskDetailTests.CreateTask</c>.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title = "T", TaskPriority priority = TaskPriority.Medium, string? project = null, TaskState status = TaskState.ToDo)
    {
        TaskResult result = harness.Service.Create(
            new TaskDraft(title, "Platform", project, Status: status, Priority: priority),
            TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Registers the harness's services into a fresh <see cref="MudBunitContext"/> - mirrors <c>TaskDetailTests.NewContext</c>.</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Renders <c>TaskDetail</c> in Panel mode for <paramref name="id"/> - mirrors <c>TaskDetailTests.RenderPanel</c>.</summary>
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

    /// <summary>Renders <c>TaskDetail</c> in Expanded mode for <paramref name="id"/> - for finding F17's Panel-vs-Expanded conflict layout markup test.</summary>
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

    /// <summary>Finds the one <c>&lt;button&gt;</c> whose trimmed text is exactly <paramref name="text"/> - mirrors <c>TaskDetailTests.FindButton</c>.</summary>
    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>
    /// Clicks the Save button via <c>cut.InvokeAsync</c>, so the Find and the Click happen as one atomic
    /// unit on the renderer's synchronization context - the root cause of a once-seen full-suite flake
    /// (<c>Conflict_TakeTheirs_OnStatusField_AlsoClearsDuplicateOf</c>, and reproduced here against
    /// <c>Conflict_TwoFieldsConflict_SaveDisabledUntilBothRowsChosen</c> during a 10-loop): Nova's
    /// concurrent edit raises <c>TaskChanged</c>/<c>IndexChanged</c> synchronously, but <c>TaskDetail</c>'s
    /// own handler dispatches its requery fire-and-forget (<c>DispatchRefresh</c>, never awaited by
    /// anything the test can hook). Under thread-pool pressure that dispatched re-render can land between
    /// a plain <c>FindButton(cut, "Save").Click()</c> call's Find and its Click, replacing the very button
    /// instance the Find captured and throwing <c>UnknownEventHandlerIdException</c> instead of running
    /// <c>SaveAsync</c> at all - bUnit's own exception message names this exact fix.
    /// </summary>
    private static Task ClickSaveAsync(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(() => FindButton(cut, "Save").Click());

    /// <summary>The trimmed text content of the one element matching <paramref name="cssSelector"/> - mirrors <c>TaskDetailTests.TextOf</c>.</summary>
    private static string TextOf(IRenderedComponent<ContainerFragment> cut, string cssSelector) =>
        cut.Find(cssSelector).TextContent.Trim();
}
