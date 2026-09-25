namespace Agency.Huddle.Tests.Ui.Tasks;

using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins Spec §13.6's remaining fields (14.2.t, RED-only): Status (the five toggles, the Won't do menu,
/// duplicate_of and Reason), Blocked by, Tags, Start/Due dates, and - per retro R7's scope addendum
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
    private static readonly TaskDetailMode[] BothModes = [TaskDetailMode.Panel, TaskDetailMode.Expanded];

    // ---------------------------------------------------------------------------------------------
    // Priority icon (corrections-B7 "14.2"): the select shows the priority's icon as its adornment.
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Priority <c>MudSelect</c> shows <see cref="TaskColors.Icon(TaskPriority)"/> as its start adornment, and it follows the current value (Spec §13.6).</summary>
    /// <param name="mode">Both of <c>TaskDetail</c>'s entry points show the same Priority control.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
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
    // Status: the five toggles, the Won't do menu, duplicate_of, Reason (Spec §13.6, corrections D14 item 8).
    // ---------------------------------------------------------------------------------------------

    /// <summary>The five <c>MudToggleItem</c>s show <see cref="TaskStates.ToWire(TaskState)"/>'s exact wire names, in order (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task StatusRow_FiveToggleItems_ShowExactWireNamesInOrder(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        IReadOnlyList<TaskState> shown = [TaskState.Backlog, TaskState.ToDo, TaskState.InProgress, TaskState.Review, TaskState.Done];
        string[] expected = [.. shown.Select(s => s.ToWire())];
        string[] actual = [.. cut.Find(".task-detail-status").QuerySelectorAll("button").Select(b => b.TextContent.Trim())];

        Assert.Equal(expected, actual);
    }

    /// <summary>The Won't do menu offers exactly Cancelled, Duplicate and Rejected, as a whole list (R7: a collection is asserted whole, never by membership).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task StatusRow_WontDoMenu_OffersExactlyCancelledDuplicateRejected(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        IReadOnlyList<string> items = OpenWontDoMenu(cut);

        Assert.Equal([TaskState.Cancelled.ToWire(), TaskState.Duplicate.ToWire(), TaskState.Rejected.ToWire()], items);
    }

    /// <summary>Choosing Duplicate reveals a required <c>duplicate_of</c> picker: Save is disabled until a target is chosen, then enabled (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task StatusRow_ChooseDuplicate_RevealsRequiredDuplicateOfPicker(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        TaskItem other = CreateTask(harness, title: "Other task");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        Assert.Empty(cut.FindAll(".task-detail-duplicate-of"));

        await ClickWontDoItem(cut, TaskState.Duplicate.ToWire());

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
    [MemberData(nameof(BothModesTimesWontDoReasonStates))]
    public async Task StatusRow_ChooseCancelledOrRejected_RevealsOptionalReasonField(TaskDetailMode mode, TaskState wontDo)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        Assert.Empty(cut.FindAll(".task-detail-reason"));

        await ClickWontDoItem(cut, wontDo.ToWire());

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
        await ClickWontDoItem(cut, TaskState.Duplicate.ToWire());
        IRenderedComponent<MudAutocomplete<TaskItem>> duplicateOf = cut.FindComponents<MudAutocomplete<TaskItem>>().Single(m => string.Equals(m.Instance.Class, "task-detail-duplicate-of", StringComparison.Ordinal));
        _ = await SearchAndPickFirst(cut, duplicateOf, "Other");

        ClickStatusToggle(cut, TaskState.Done);
        FindButton(cut, "Save").Click();

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
    [MemberData(nameof(BothModesData))]
    public async Task StatusRow_LeavingRejectedForAnotherState_ClearsReasonOnSave(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        await ClickWontDoItem(cut, TaskState.Rejected.ToWire());
        IRenderedComponent<MudTextField<string>> reason = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-reason", StringComparison.Ordinal));
        await cut.InvokeAsync(() => reason.Instance.ValueChanged.InvokeAsync("no longer needed"));

        ClickStatusToggle(cut, TaskState.Done);
        FindButton(cut, "Save").Click();

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
    [MemberData(nameof(BothModesData))]
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
    [MemberData(nameof(BothModesTimesBlockerTerminalStates))]
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
    [MemberData(nameof(BothModesTimesBlockerCounts))]
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

    // ---------------------------------------------------------------------------------------------
    // Tags (Spec §13.6): the same chip-set pattern as Blocked by.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A tag is added on Enter and removed by <c>OnClose</c> - the tag list is asserted whole (R7).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
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
    [MemberData(nameof(BothModesTimesDateFields))]
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
    [MemberData(nameof(BothModesTimesDateFields))]
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
    [MemberData(nameof(BothModesData))]
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
    [MemberData(nameof(BothModesData))]
    public async Task Dates_OverdueDueDateOnTerminalTask_ShowsNoOverdueClass(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, status: TaskState.Done, dueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.DoesNotContain("task-overdue", cut.Find(".task-detail-due-date").ClassList, StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Assignee (Spec §13.6; scope addendum R7).
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Assignee picker is a <c>Strict</c> autocomplete (Spec §13.6) - this alone forces the new <c>AssigneeOption</c> item type's red.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task Assignee_AutocompleteIsStrict(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));

        Assert.True(assignee.Instance.Strict);
    }

    /// <summary>
    /// Settled (R7 addendum, J46, assigned to 14.2 explicitly): an Assignee option's <c>ItemTemplate</c>
    /// renders a <c>TeammateAvatar</c> nested inside a presence <c>MudBadge</c> whose <c>BadgeAriaLabel</c>
    /// states the presence in words - the exact <c>TaskCard.razor</c> wording, "{Name} is {Presence}" - and
    /// the option's own Name text exactly. Renders the template directly from a real
    /// <see cref="AssigneeOption"/> returned by the autocomplete's own <c>SearchFunc</c> (never
    /// hand-constructed), rather than opening the popover, since bUnit cannot easily drive a debounced
    /// popover open from a plain text change.
    /// </summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task Assignee_OptionTemplate_ShowsTeammateAvatarInPresenceBadge_AndExactName(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));
        Func<string?, CancellationToken, Task<IEnumerable<AssigneeOption>>?> search = assignee.Instance.SearchFunc
            ?? throw new InvalidOperationException("Assignee autocomplete has no SearchFunc.");
        Task<IEnumerable<AssigneeOption>>? searchTask = search("Nova", Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        AssigneeOption nova = (await searchTask).Single();
        RenderFragment<AssigneeOption>? itemTemplate = assignee.Instance.ItemTemplate;
        Assert.NotNull(itemTemplate);

        var itemCut = ctx.Render(itemTemplate!(nova));

        IRenderedComponent<MudBadge> badge = itemCut.FindComponent<MudBadge>();
        IRenderedComponent<TeammateAvatar> avatar = badge.FindComponent<TeammateAvatar>();
        Assert.Equal("Nova", avatar.Instance.Name);
        Assert.Equal("Nova is Asleep", badge.Instance.BadgeAriaLabel);
        Assert.Equal("Nova", TextOf(itemCut, ".task-detail-assignee-option-name"));
    }

    /// <summary>Selecting a known Persona from the Assignee autocomplete's own <c>SearchFunc</c> results sets the pending Assignee, proven by saving it (never constructing an <c>AssigneeOption</c> by hand - its shape is 14.2.i's own decision).</summary>
    [Fact]
    public async Task Assignee_SelectingFromSearchFuncResults_SetsAssignee_ProvenBySaving()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));
        Func<string?, CancellationToken, Task<IEnumerable<AssigneeOption>>?> search = assignee.Instance.SearchFunc
            ?? throw new InvalidOperationException("Assignee autocomplete has no SearchFunc.");
        Task<IEnumerable<AssigneeOption>>? searchTask = search("Nova", Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        AssigneeOption nova = (await searchTask).Single();

        await cut.InvokeAsync(() => assignee.Instance.ValueChanged.InvokeAsync(nova));
        FindButton(cut, "Save & Notify Nova").Click();

        Assert.Equal("Nova", harness.Store.Get(task.Id)?.Assignee);
    }

    /// <summary>When the Task has a <c>LastWake</c>, a line underneath the Assignee reads exactly "Woken in Room: {name}" (Spec §13.6) and links to the Room.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task Assignee_WithLastWake_ShowsWokenInRoomExactText(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, assignee: "Nova");
        harness.TaskActivity.Record(new WakeRecord(task.Id, "Nova", "room-1", "SAML Integration", WakeOutcome.Woken, DateTimeOffset.UtcNow));
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Equal("Woken in Room: SAML Integration", TextOf(cut, ".task-detail-woken-link"));
        Assert.Equal("/rooms/room-1", cut.Find(".task-detail-woken-link").GetAttribute("href"));
    }

    // ---------------------------------------------------------------------------------------------
    // Team, Project (Spec §13.6: the Panel combines them; Expanded keeps them separate).
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Panel combines Team and Project into one "Team / Project" control (Spec §13.6).</summary>
    [Fact]
    public async Task TeamProject_Panel_ShowsCombinedLabel()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Equal("Team / Project", TextOf(cut, ".task-detail-team-project-label"));
    }

    /// <summary>The Expanded layout keeps Team and Project as two separately-labelled controls (Spec §13.6, the Panel-only combining implies Expanded does not).</summary>
    [Fact]
    public async Task TeamProject_Expanded_ShowsSeparateLabels()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Expanded);

        IRenderedComponent<MudSelect<string>> team = cut.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-team", StringComparison.Ordinal));
        IRenderedComponent<MudAutocomplete<string>> project = cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal));
        Assert.Equal("Team", team.Instance.Label);
        Assert.Equal("Project", project.Instance.Label);
    }

    /// <summary>Typing a new Project name (<c>CoerceValue</c>) and saving creates that Project's folder (Spec §13.6).</summary>
    [Fact]
    public async Task Project_CoerceValue_NewNameCreatesFolderOnSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        IRenderedComponent<MudAutocomplete<string>> project = cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal));
        Assert.True(project.Instance.CoerceValue);

        await cut.InvokeAsync(() => project.Instance.ValueChanged.InvokeAsync("NewProject"));
        FindButton(cut, "Save").Click();

        Assert.True(Directory.Exists(Path.Combine(harness.TasksDirPath, "Platform", "NewProject")));
    }

    // ---------------------------------------------------------------------------------------------
    // Parent (Spec §13.6).
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Parent picker is <c>Strict</c> and its <c>ToStringFunc</c> gives "PLAT-0030 · title" (Spec §13.6's own example format).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task Parent_AutocompleteStrict_ToStringFuncFormatsIdMiddleDotTitle(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        IRenderedComponent<MudAutocomplete<TaskItem>> parent = cut.FindComponents<MudAutocomplete<TaskItem>>().Single(m => string.Equals(m.Instance.Class, "task-detail-parent", StringComparison.Ordinal));
        Assert.True(parent.Instance.Strict);

        TaskItem sample = TestTasks.Make(id: "PLAT-0030", title: "Sample title");
        Func<TaskItem?, string?>? toString = parent.Instance.ToStringFunc;
        Assert.NotNull(toString);
        Assert.Equal("PLAT-0030 · Sample title", toString(sample));
    }

    // ---------------------------------------------------------------------------------------------
    // Origin (Spec §13.6).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Origin shows a link to <c>/rooms/{id}</c> when the origin Room still exists (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task Origin_RoomStillExists_ShowsLinkToRoom(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        Room room = await harness.Directory.CreateRoomAsync("Origin room", [KnownIds.Human, harness.NovaId!], Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, originRoomId: room.Id);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Equal($"/rooms/{room.Id}", cut.Find(".task-detail-origin a").GetAttribute("href"));
    }

    /// <summary>Origin shows the exact Spec §13.6 text "(Room no longer exists)" when the origin Room is gone.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task Origin_RoomNoLongerExists_ShowsExactParenthetical(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, originRoomId: "no-such-room");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Equal("(Room no longer exists)", TextOf(cut, ".task-detail-origin"));
    }

    // ---------------------------------------------------------------------------------------------
    // Created, Updated, Closed (Spec §13.6: read-only, derived).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Created and Updated show <see cref="TaskItem.Created"/>/<see cref="TaskItem.Updated"/>, formatted (provisionally, the Spec gives no format) as <c>yyyy-MM-dd</c>, invariant.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task CreatedUpdated_ShowDerivedDatesInIsoFormat(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        DateTimeOffset created = task.Created ?? throw new InvalidOperationException("Task has no Created date.");
        DateTimeOffset updated = task.Updated ?? throw new InvalidOperationException("Task has no Updated date.");
        string expectedCreated = created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string expectedUpdated = updated.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(expectedCreated, TextOf(cut, ".task-detail-created"));
        Assert.Equal(expectedUpdated, TextOf(cut, ".task-detail-updated"));
    }

    /// <summary>Closed shows only when the Task has a <see cref="TaskItem.ClosedAt"/> (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(BothModesData))]
    public async Task Closed_ShownOnlyWhenClosedAtIsSet(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem openTask = CreateTask(harness, title: "Still open");
        await using MudBunitContext openCtx = NewContext(harness);
        var openCut = RenderDetail(openCtx, openTask.Id, mode);
        Assert.Empty(openCut.FindAll(".task-detail-closed"));

        TaskResult closeResult = harness.Service.Close(openTask.Id, TaskActors.Human(harness.Options.Value));
        TaskItem closedTask = Assert.IsType<TaskResult.Saved>(closeResult).Task;
        await using MudBunitContext closedCtx = NewContext(harness);
        var closedCut = RenderDetail(closedCtx, closedTask.Id, mode);

        DateTimeOffset closedAt = closedTask.ClosedAt ?? throw new InvalidOperationException("Task has no ClosedAt date.");
        string expected = closedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(expected, TextOf(closedCut, ".task-detail-closed"));
    }

    // ---------------------------------------------------------------------------------------------
    // Shared test data and helpers.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Both of <c>TaskDetail</c>'s entry points, for a <c>[Theory]</c> that repeats one rule at each (R6/R7).</summary>
    public static IEnumerable<object[]> BothModesData() => BothModes.Select(m => new object[] { m });

    /// <summary>The cross-product of both entry points and the two Won't-do states that show a Reason field.</summary>
    public static IEnumerable<object[]> BothModesTimesWontDoReasonStates()
    {
        foreach (TaskDetailMode mode in BothModes)
        {
            yield return [mode, TaskState.Cancelled];
            yield return [mode, TaskState.Rejected];
        }
    }

    /// <summary>The cross-product of both entry points and one vs. two open blockers (singular vs. plural summary text).</summary>
    public static IEnumerable<object[]> BothModesTimesBlockerCounts()
    {
        foreach (TaskDetailMode mode in BothModes)
        {
            yield return [mode, 1];
            yield return [mode, 2];
        }
    }

    /// <summary>The cross-product of both entry points and a terminal vs. non-terminal blocker Status (J46: the check icon only for terminal).</summary>
    public static IEnumerable<object[]> BothModesTimesBlockerTerminalStates()
    {
        foreach (TaskDetailMode mode in BothModes)
        {
            yield return [mode, TaskState.Done, true];
            yield return [mode, TaskState.InProgress, false];
        }
    }

    /// <summary>The cross-product of both entry points and the Start/Due date fields.</summary>
    public static IEnumerable<object[]> BothModesTimesDateFields()
    {
        foreach (TaskDetailMode mode in BothModes)
        {
            yield return [mode, "start"];
            yield return [mode, "due"];
        }
    }

    /// <summary>Creates a Task through <see cref="TaskService"/> with every field this file's tests need to control.</summary>
    private static TaskItem CreateTask(
        TaskToolHarness harness,
        string title = "T",
        TaskState status = TaskState.ToDo,
        TaskPriority priority = TaskPriority.Medium,
        string? assignee = null,
        string? originRoomId = null,
        IReadOnlyList<TaskId>? blockedBy = null,
        DateOnly? dueDate = null)
    {
        TaskResult result = harness.Service.Create(
            new TaskDraft(title, "Platform", null, Status: status, Priority: priority, Assignee: assignee, OriginRoomId: originRoomId, BlockedBy: blockedBy, DueDate: dueDate),
            TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Registers the harness's services into a fresh <see cref="MudBunitContext"/> (the <c>TaskDetailTests</c> pattern).</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Renders <c>TaskDetail</c> for <paramref name="id"/> in <paramref name="mode"/>, inside <see cref="MudBunitContext.RenderWithPopovers"/> (the Won't-do menu and pickers are popovers).</summary>
    private static IRenderedComponent<ContainerFragment> RenderDetail(MudBunitContext ctx, TaskId id, TaskDetailMode mode)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.AddAttribute(2, nameof(TaskDetail.Mode), mode);
            builder.CloseComponent();
        });
    }

    /// <summary>Finds the one <c>&lt;button&gt;</c> whose trimmed text is exactly <paramref name="text"/>.</summary>
    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>The trimmed text content of the one element matching <paramref name="cssSelector"/>.</summary>
    private static string TextOf(IRenderedComponent<ContainerFragment> cut, string cssSelector) =>
        cut.Find(cssSelector).TextContent.Trim();

    /// <summary>Opens the Won't-do <c>MudMenu</c> (activator <c>button[aria-label="Won't do"]</c>, the <c>RoomListTests</c> pattern) and returns its items' trimmed texts, in order.</summary>
    private static IReadOnlyList<string> OpenWontDoMenu(IRenderedComponent<ContainerFragment> cut)
    {
        cut.Find("button[aria-label=\"Won't do\"]").Click();
        return [.. cut.FindAll("div.mud-menu-item").Select(i => i.TextContent.Trim())];
    }

    /// <summary>
    /// Opens the Won't-do menu and clicks the item whose text is <paramref name="wireName"/>. Each
    /// Find+Click is its own <c>InvokeAsync</c> (the <c>TaskDetailConflictTests.ClickSaveAsync</c>
    /// pattern) - found by 14.3 under parallel test-exe load: a fire-and-forget dispatched re-render
    /// (<c>TaskDetail.DispatchRefresh</c>) can land between the plain synchronous <c>Find</c> and
    /// <c>Click</c> that opens the menu and the one that picks an item, detaching the element bUnit
    /// found and throwing <c>UnknownEventHandlerIdException</c> on the second click.
    /// </summary>
    private static async Task ClickWontDoItem(IRenderedComponent<ContainerFragment> cut, string wireName)
    {
        await cut.InvokeAsync(() => cut.Find("button[aria-label=\"Won't do\"]").Click());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(i => string.Equals(i.TextContent.Trim(), wireName, StringComparison.Ordinal)).Click());
    }

    /// <summary>Clicks the Status toggle item for <paramref name="state"/> (one of the five ordinary states, not a Won't-do one).</summary>
    private static void ClickStatusToggle(IRenderedComponent<ContainerFragment> cut, TaskState state)
    {
        cut.Find(".task-detail-status").QuerySelectorAll("button").Single(b => string.Equals(b.TextContent.Trim(), state.ToWire(), StringComparison.Ordinal)).Click();
    }

    /// <summary>
    /// Closes the <c>MudChip{T}</c> whose <c>Value</c> equals <paramref name="value"/> by invoking that
    /// real rendered chip's own <c>OnClose</c> callback with itself - the same event <c>MudChipSet</c>'s
    /// close icon would raise. Never constructs a <c>MudChip{T}</c> by hand (MudBlazor's own analyzer,
    /// <c>BL0005</c>, forbids setting a component parameter outside its component) and does not depend on
    /// the close icon's exact DOM shape, which bUnit's static render does not reproduce identically to a
    /// live browser.
    /// </summary>
    /// <typeparam name="T">The chip's value type.</typeparam>
    /// <param name="cut">The rendered <c>TaskDetail</c>.</param>
    /// <param name="value">The <c>Value</c> of the chip to close.</param>
    private static async Task CloseChipAsync<T>(IRenderedComponent<ContainerFragment> cut, T value)
    {
        IRenderedComponent<MudChip<T>> chip = cut.FindComponents<MudChip<T>>().Single(c => Equals(c.Instance.Value, value));
        await cut.InvokeAsync(() => chip.Instance.OnClose.InvokeAsync(chip.Instance));
    }

    /// <summary>Awaits <paramref name="autocomplete"/>'s own <c>SearchFunc</c> for <paramref name="query"/>, then invokes <c>ValueChanged</c> with the first result - never constructing a <typeparamref name="T"/> by hand.</summary>
    /// <typeparam name="T">The autocomplete's item type.</typeparam>
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
}
