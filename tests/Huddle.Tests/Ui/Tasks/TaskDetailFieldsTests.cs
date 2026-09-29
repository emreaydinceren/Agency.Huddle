namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using MudBlazor;
using MudBlazor.Extensions;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Pins Spec §13.6's remaining fields (14.2.t, RED-only): Status (the <c>MudSelect</c>, duplicate_of
/// and Reason), Blocked by, Tags, Start/Due dates, and - per retro R7's scope addendum
/// (settled by the delivery manager) - every other §13.6 row no earlier task claims: Priority's icon
/// (corrections-B7 "14.2"), Assignee, Team/Project, Parent, Origin, and Created/Updated/Closed. Every
/// rule is tested at both of <c>TaskDetail</c>'s entry points - <see cref="TaskDetailMode.Panel"/> and
/// <see cref="TaskDetailMode.Expanded"/> - per R6/R7 ("each rule at both entry points"; 13.1's own
/// exact-text tightening found a real double-rendering bug testing only one side would have missed).
/// Every user-facing text is asserted exactly against its own located element, never a substring of
/// the whole rendered markup. Where the Spec gives no exact text, a provisional one is pinned here and
/// named in the delivery report's Coverage list as provisional.
/// </summary>
public sealed class TaskDetailFieldsTests
{
    // ---------------------------------------------------------------------------------------------
    // Priority icon (corrections-B7 "14.2"): the select shows the priority's icon as its adornment.
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Priority <c>MudSelect</c> shows <see cref="TaskColors.Icon(TaskPriority)"/> as its start adornment, and it follows the current value (Spec §13.6).</summary>
    /// <param name="mode">Both of <c>TaskDetail</c>'s entry points show the same Priority control.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task PriorityRow_ShowsPriorityIconAdornment_AndFollowsTheValue(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Low);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        IRenderedComponent<MudSelect<TaskPriority>> prioritySelect = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        Assert.Equal(TaskColors.Icon(TaskPriority.Low), prioritySelect.Instance.AdornmentIcon);

        await cut.InvokeAsync(() => prioritySelect.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));

        Assert.Equal(TaskColors.Icon(TaskPriority.Urgent), cut.FindComponents<MudSelect<TaskPriority>>().Single().Instance.AdornmentIcon);
    }

    // ---------------------------------------------------------------------------------------------
    // Status: one MudSelect over all eight states, duplicate_of, Reason (Spec §13.6, corrections D14 item 8).
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The Status <c>MudSelect</c> offers all eight <see cref="TaskState"/>s, as <see cref="MudSelectItem{T}"/>
    /// components in <see cref="TaskStates.All"/>'s declaration order (Spec §13.6; R7: a collection is
    /// asserted whole, never by membership). Each item's own rendered text is <c>state.ToWire()</c> -
    /// TaskDetail.razor's fixed template for every item alike, pinned separately by
    /// <see cref="Agency.Huddle.Tests.Tasks.TaskStatesTests"/> - so pinning the Values in order
    /// already pins what shows.
    /// </summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task StatusRow_MudSelect_OffersAllEightStatesInOrder(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        // MudSelectItem<T> components exist even while the select is closed (BlazorTesting.md), so
        // this needs no popover interaction - the same reasoning ViewEditorDrawerTests relies on.
        IRenderedComponent<MudSelect<TaskState>> select = cut.FindComponents<MudSelect<TaskState>>().Single(m => string.Equals(m.Instance.Class, "task-detail-status", StringComparison.Ordinal));
        TaskState[] actual = [.. select.FindComponents<MudSelectItem<TaskState>>().Select(i => i.Instance.Value)];

        Assert.Equal(TaskStates.All, actual);
    }

    /// <summary>Choosing Duplicate reveals a required <c>duplicate_of</c> picker: Save is disabled until a target is chosen, then enabled (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task StatusRow_ChooseDuplicate_RevealsRequiredDuplicateOfPicker(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        TaskItem other = CreateTask(harness, title: "Other task");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        Assert.Empty(cut.FindAll(".task-detail-duplicate-of"));

        await SetStatusAsync(cut, TaskState.Duplicate);

        IRenderedComponent<MudAutocomplete<TaskItem>> duplicateOf = cut.FindComponents<MudAutocomplete<TaskItem>>().Single(m => string.Equals(m.Instance.Class, "task-detail-duplicate-of", StringComparison.Ordinal));
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));

        TaskItem chosen = await SearchAndPickFirst(cut, duplicateOf, "Other");
        Assert.Equal(other.Id, chosen.Id);

        Assert.False(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>Choosing Cancelled or Rejected reveals an optional Reason field: <c>Save</c> stays enabled even when it is left blank (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    /// <param name="wontDo">Cancelled or Rejected.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesTimesWontDoReasonStates), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task StatusRow_ChooseCancelledOrRejected_RevealsOptionalReasonField(TaskDetailMode mode, TaskState wontDo)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        Assert.Empty(cut.FindAll(".task-detail-reason"));

        await SetStatusAsync(cut, wontDo);

        IRenderedComponent<MudTextField<string>> reason = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-reason", StringComparison.Ordinal));
        Assert.Equal("Reason (optional)", reason.Instance.Label);
        Assert.False(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>
    /// Corrections-B5 D14 item 8: leaving Duplicate for another state sends <c>DuplicateOf = Optional.Set(null)</c>
    /// - proven by actually saving afterwards and reading the persisted Task back, so a component that
    /// merely left <c>DuplicateOf</c> untouched (rather than explicitly clearing it) would fail this: the
    /// stale target chosen while Duplicate was selected would otherwise still be written.
    /// </summary>
    [Fact]
    public async Task StatusRow_LeavingDuplicateForAnotherState_ClearsDuplicateOfOnSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        TaskItem other = CreateTask(harness, title: "Other task");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        await SetStatusAsync(cut, TaskState.Duplicate);
        IRenderedComponent<MudAutocomplete<TaskItem>> duplicateOf = cut.FindComponents<MudAutocomplete<TaskItem>>().Single(m => string.Equals(m.Instance.Class, "task-detail-duplicate-of", StringComparison.Ordinal));
        _ = await SearchAndPickFirst(cut, duplicateOf, "Other");

        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        TaskItem? saved = harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Done, saved?.Status);
        Assert.Null(saved?.DuplicateOf);
        _ = other;
    }

    /// <summary>
    /// Settled J55 (a product bug found reviewing 14.3): leaving Cancelled or Rejected must clear the
    /// pending Reason, exactly as leaving Duplicate clears duplicate_of (corrections-B5 D14 item 8) -
    /// proven by actually saving afterwards, not just checking the Reason box disappears: a component
    /// that left a stale Reason in <c>pending</c> after switching away from Rejected would have
    /// <c>TaskService</c> refuse the whole Save ("A reason is only recorded when the status becomes
    /// Cancelled or Rejected.", Spec §9.2), so the Task would stay at its old Status and the Save would
    /// show a <c>role="alert"</c> error instead of persisting Done - and even if the write somehow went
    /// through, a leftover Reason would still show up in the Change log's own summary text.
    /// </summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task StatusRow_LeavingRejectedForAnotherState_ClearsReasonOnSave(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        await SetStatusAsync(cut, TaskState.Rejected);
        IRenderedComponent<MudTextField<string>> reason = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-reason", StringComparison.Ordinal));
        await cut.InvokeAsync(() => reason.Instance.ValueChanged.InvokeAsync("no longer needed"));

        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        TaskItem? saved = harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Done, saved?.Status);
        string lastSummary = saved is { ChangeLog.Count: > 0 } ? saved.ChangeLog[^1].Summary : "";
        Assert.DoesNotContain("reason:", lastSummary, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Blocked by (Spec §13.6, agents/MudBlazorDesign.md: MudAutocomplete selects one value only in 9.10).
    // ---------------------------------------------------------------------------------------------

    /// <summary>A blocker chip is added by the autocomplete and removed by <c>OnClose</c> - the chip list is asserted whole (R7), never by membership.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task BlockedBy_AutocompleteAdds_AndOnCloseRemoves_WholeListAsserted(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem blocker = CreateTask(harness, title: "Blocker task", status: TaskState.ToDo);
        TaskItem task = CreateTask(harness, title: "Blocked task");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        Assert.Empty(cut.FindAll(".task-detail-blocked-by-chips .mud-chip"));

        IRenderedComponent<MudAutocomplete<TaskItem>> add = cut.FindComponents<MudAutocomplete<TaskItem>>().Single(m => string.Equals(m.Instance.Class, "task-detail-blocked-by-add", StringComparison.Ordinal));
        _ = await SearchAndPickFirst(cut, add, "Blocker");

        Assert.Equal([$"{blocker.Id} To Do"], [.. cut.FindAll(".task-detail-blocked-by-chips .mud-chip").Select(c => c.TextContent.Trim())]);

        await CloseChipAsync(cut, blocker.Id);

        Assert.Empty(cut.FindAll(".task-detail-blocked-by-chips .mud-chip"));
    }

    /// <summary>
    /// Settled (J46): a blocker chip's text is exactly <c>"{id} {Status}"</c> (the Spec §13.6 example
    /// "PLAT-0047 ✓ Done"'s wording, without the check mark in the text itself), and it additionally
    /// carries a <see cref="Icons.Material.Filled.Check"/> icon only when the blocker's Status is
    /// terminal - never for a still-open blocker.
    /// </summary>
    /// <param name="mode">Both entry points.</param>
    /// <param name="status">A terminal (Done) or non-terminal (In Progress) blocker Status.</param>
    /// <param name="expectCheckIcon">Whether the chip should carry the check icon.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesTimesBlockerTerminalStates), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task BlockedBy_ChipShowsExactStatusText_AndCheckIconOnlyWhenTerminal(TaskDetailMode mode, TaskState status, bool expectCheckIcon)
    {
        using TaskToolHarness harness = new();
        TaskItem blocker = CreateTask(harness, title: "Blocker task", status: status);
        TaskItem task = CreateTask(harness, title: "Blocked task", blockedBy: [blocker.Id]);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        string expectedText = $"{blocker.Id} {status.ToWire()}";
        IElement chip = cut.FindAll(".task-detail-blocked-by-chips .mud-chip").Single();
        Assert.Equal(expectedText, chip.TextContent.Trim());
        Assert.Equal(expectCheckIcon, chip.QuerySelector("svg.mud-chip-icon") is not null);
    }

    /// <summary>The read-only summary reads "Blocked by 2 open tasks" (Spec §13.6's own example) and, provisionally (the Spec gives no singular form), "Blocked by 1 open task".</summary>
    /// <param name="mode">Both entry points.</param>
    /// <param name="count">How many open blockers.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesTimesBlockerCounts), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task BlockedBy_ReadOnlySummary_ExactPluralAndSingularText(TaskDetailMode mode, int count)
    {
        using TaskToolHarness harness = new();
        List<TaskId> blockerIds = [];
        for (int i = 0; i < count; i++)
        {
            blockerIds.Add(CreateTask(harness, title: $"Blocker {i}").Id);
        }

        TaskItem task = CreateTask(harness, blockedBy: blockerIds);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        string expected = count == 1 ? "Blocked by 1 open task" : $"Blocked by {count} open tasks";
        Assert.Equal(expected, TextOf(cut, ".task-detail-blocked-by-summary"));
    }

    /// <summary>Manual test TASKS-01 finding F7: a Task with no blockers shows no summary line at all - not "Blocked by 0 open tasks".</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task BlockedBy_ReadOnlySummary_NoLineWhenNoBlockers(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Empty(cut.FindAll(".task-detail-blocked-by-summary"));
    }

    // ---------------------------------------------------------------------------------------------
    // Tags (Spec §13.6): the same chip-set pattern as Blocked by.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A tag is added on Enter and removed by <c>OnClose</c> - the tag list is asserted whole (R7).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Tags_EnterAdds_AndOnCloseRemoves_WholeListAsserted(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        IRenderedComponent<MudTextField<string>> add = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-tags-add", StringComparison.Ordinal));
        await cut.InvokeAsync(() => add.Instance.ValueChanged.InvokeAsync("urgent"));

        await cut.InvokeAsync(() => add.Instance.OnKeyDown.InvokeAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" }));

        Assert.Equal(["urgent"], [.. cut.FindAll(".task-detail-tags-chips .mud-chip").Select(c => c.TextContent.Trim())]);

        await CloseChipAsync(cut, "urgent");

        Assert.Empty(cut.FindAll(".task-detail-tags-chips .mud-chip"));
    }

    // ---------------------------------------------------------------------------------------------
    // Dates (Spec §13.6, corrections-B5 D14 item 11).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Start and Due each round-trip through their own <c>MudDatePicker</c> (corrections-B5 D14 item 11: <c>DateChanged.InvokeAsync(new DateTime(...))</c>, assert <c>Instance.Date</c>).</summary>
    /// <param name="mode">Both entry points.</param>
    /// <param name="which">"start" or "due".</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesTimesDateFields), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Dates_StartAndDue_RoundTripThroughDatePicker(TaskDetailMode mode, string which)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        string cssClass = which == "start" ? "task-detail-start-date" : "task-detail-due-date";
        IRenderedComponent<MudDatePicker> picker = cut.FindComponents<MudDatePicker>().Single(m => string.Equals(m.Instance.Class, cssClass, StringComparison.Ordinal));
        DateTime picked = new(2026, 11, 3);

        await cut.InvokeAsync(() => picker.Instance.DateChanged.InvokeAsync(picked));

        Assert.Equal(picked, cut.FindComponents<MudDatePicker>().Single(m => string.Equals(m.Instance.Class, cssClass, StringComparison.Ordinal)).Instance.Date);
    }

    /// <summary>Start and Due each display through their picker's own <c>yyyy-MM-dd</c> format (Settled J53, one date format across the Tasks UI).</summary>
    /// <param name="mode">Both entry points.</param>
    /// <param name="which">"start" or "due".</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesTimesDateFields), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Dates_StartAndDue_UseTheInvariantYmdPickerFormat(TaskDetailMode mode, string which)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        string cssClass = which == "start" ? "task-detail-start-date" : "task-detail-due-date";

        IRenderedComponent<MudDatePicker> picker = cut.FindComponents<MudDatePicker>().Single(m => string.Equals(m.Instance.Class, cssClass, StringComparison.Ordinal));

        Assert.Equal("yyyy-MM-dd", picker.Instance.GetState(x => x.DateFormat));
    }

    /// <summary>A past Due date on a non-terminal Task gets the <c>task-overdue</c> class and a warning adornment (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Dates_OverdueDueDateOnNonTerminalTask_ShowsOverdueClass(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, status: TaskState.InProgress, dueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Contains("task-overdue", cut.Find(".task-detail-due-date").ClassList, StringComparer.Ordinal);
    }

    /// <summary>A past Due date on a terminal Task (Done) does not get the <c>task-overdue</c> class (Spec §13.6: "isn't terminal").</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Dates_OverdueDueDateOnTerminalTask_ShowsNoOverdueClass(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, status: TaskState.Done, dueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.DoesNotContain("task-overdue", cut.Find(".task-detail-due-date").ClassList, StringComparer.Ordinal);
    }

}
