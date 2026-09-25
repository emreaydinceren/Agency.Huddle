namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins the Spec §13.4 <i>Dropping</i> and <i>Keyboard</i> paragraphs for <see cref="TaskBoard"/>
/// (plan 12.3.t) and corrections-B5 D12-7, -8, -11 and -12. Every rule is exercised through both
/// entry points - a drop on the Board (<c>StartTransaction</c> + <c>CommitTransaction</c>, D12-10) and
/// the card's ⋮ <i>Move to</i> menu - because the Spec says the menu "takes the same path as a drop".
/// <para>
/// Provisional texts (flagged; the Spec gives none): the Duplicate picker's title
/// <c>Mark {id} as a duplicate</c>, its field label <c>Duplicate of</c> and buttons <c>Cancel</c> /
/// <c>Save</c>; the reason dialog's title <c>Mark {id} as {State}</c>, its field label
/// <c>Reason (optional)</c> and buttons <c>Skip</c> / <c>Save</c> plus the dialog's close (X) button;
/// the Snackbar errors <c>Could not move {id}: {problems}</c> and
/// <c>Could not move {id}: it no longer exists.</c>; the drag caption <c>{Name} will be notified</c>
/// (the Spec's own example) in <c>.task-board-drag-caption</c> under every column header except the
/// dragged card's own, and no caption when <c>Preview</c> blocks the wake.
/// </para>
/// </summary>
public sealed class TaskBoardDropTests
{
    /// <summary>The ungrouped lane's key.</summary>
    private const string NoLane = "";

    /// <summary>The ungrouped Won't do display zone's id.</summary>
    private const string WontDoZone = "lane:|col:5";

    /// <summary>The Spec §12.3 default columns, restated (plan R4).</summary>
    private static readonly IReadOnlyList<BoardColumn> DefaultColumns =
    [
        new("Backlog", [TaskState.Backlog]),
        new("To Do", [TaskState.ToDo]),
        new("In Progress", [TaskState.InProgress]),
        new("Review", [TaskState.Review]),
        new("Done", [TaskState.Done]),
        new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
    ];

    /// <summary>
    /// Plan 12.3.t bullet 1 and Spec §13.4 step 4: a move to In Progress calls <c>TaskService.Update</c>
    /// as the Human with <c>baseVersion: null</c> - so even a stale card (someone moved the Task to
    /// Review after the Board rendered it, touching the very field the drop changes) saves, last write
    /// wins, instead of conflicting - and it is saved at once (D-20).
    /// </summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveToInProgress_SavesStatusAsHuman_WithNullBaseVersion(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem stale = CreateTask(harness, "Ship it", TaskState.ToDo);
        _ = Saved(harness.Service.Update(stale.Id, new TaskPatch { Title = "Ship it now", Status = TaskState.Review }, null, Human(harness)));
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [stale]);
        await rig.MoveAsync(stale, TaskState.InProgress, viaMenu);

        TaskItem? saved = harness.Store.Get(stale.Id);
        Assert.NotNull(saved);
        Assert.Equal(TaskState.InProgress, saved.Status);
        Assert.Equal("Ship it now", saved.Title);
        Assert.Equal("status: Review → In Progress", saved.ChangeLog[^1].Summary);
        TaskChange change = Assert.Single(changes);
        Assert.Equal(TaskActorKind.Human, change.Actor.Kind);
        Assert.Equal("You", change.Actor.Name);
    }

    /// <summary>corrections-B5 D12-7 and Spec §13.4: moving a card to the state it already has saves nothing and opens nothing.</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveToSameState_SavesNothing(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, "Ship it", TaskState.ToDo);
        int entries = task.ChangeLog.Count;
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [task]);
        await rig.MoveAsync(task, TaskState.ToDo, viaMenu);

        Assert.Empty(changes);
        Assert.Equal(entries, harness.Store.Get(task.Id)?.ChangeLog.Count);
        Assert.Empty(rig.Dialogs.FindAll(".mud-dialog"));
        // contains-ok: the card's id inside the To Do zone's markup is the placement being asserted.
        Assert.Contains(task.Id.ToString(), rig.ZoneMarkup(BoardLayout.ZoneId(NoLane, TaskState.ToDo)), StringComparison.Ordinal);
    }

    /// <summary>corrections-B5 D12-7: moving a Rejected card to Rejected (its own bucket, or the menu) is a no-op before any dialog - the reason dialog never opens and nothing is saved.</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveToSameWontDoState_OpensNoDialog(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, "Ship it", TaskState.ToDo);
        task = Saved(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Rejected }, null, Human(harness)));
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [task]);
        await rig.StartMoveAsync(task, TaskState.Rejected, viaMenu).WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Empty(rig.Dialogs.FindComponents<ReasonDialog>());
        Assert.Empty(changes);
    }

    /// <summary>
    /// Spec §13.4 step 2 and corrections-B5 D12-12: Duplicate opens <c>DuplicatePickerDialog</c>
    /// excluding the moved Task - its title, its <c>MudAutocomplete&lt;TaskItem&gt;</c> (Strict, labelled
    /// "Duplicate of"), and a <c>SearchFunc</c> that is <c>TaskQuery.Suggest</c> minus the excluded id.
    /// </summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveToDuplicate_OpensPickerExcludingTheMovedTask(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Other thing", TaskState.ToDo);
        TaskItem target = CreateTask(harness, "Other task", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved, target]);
        Task move = rig.StartMoveAsync(moved, TaskState.Duplicate, viaMenu);

        IRenderedComponent<DuplicatePickerDialog> dialog = rig.WaitForDialog<DuplicatePickerDialog>();
        Assert.Equal(moved.Id, dialog.Instance.Excluding);
        Assert.Equal($"Mark {moved.Id} as a duplicate", rig.Dialogs.Find(".mud-dialog-title").TextContent.Trim());
        IRenderedComponent<MudAutocomplete<TaskItem>> picker = rig.Dialogs.FindComponent<MudAutocomplete<TaskItem>>();
        Assert.True(picker.Instance.Strict);
        Assert.Equal("Duplicate of", picker.Instance.Label);
        Func<string?, CancellationToken, Task<IEnumerable<TaskItem>>?>? search = picker.Instance.SearchFunc;
        Assert.NotNull(search);
        Task<IEnumerable<TaskItem>>? searching = search("Other", ct);
        Assert.NotNull(searching);
        TaskId[] found = [.. (await searching).Select(t => t.Id)];
        TaskId[] expected = [target.Id];
        Assert.Equal(expected, found);

        rig.Button("Cancel").Click();
        await move;
    }

    /// <summary>Spec §13.4 step 2: cancelling the Duplicate picker saves nothing, and the card snaps back to its zone (<c>Refresh()</c>).</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicatePicker_Cancel_SavesNothing_CardStaysPut(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Other thing", TaskState.ToDo);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, TaskState.Duplicate, viaMenu);
        _ = rig.WaitForDialog<DuplicatePickerDialog>();
        rig.Button("Cancel").Click();
        await move;

        Assert.Empty(changes);
        Assert.Equal(TaskState.ToDo, harness.Store.Get(moved.Id)?.Status);
        // contains-ok: the card's id inside the To Do zone's markup is the snap-back being asserted.
        rig.Board.WaitForAssertion(() => Assert.Contains(moved.Id.ToString(), rig.ZoneMarkup(BoardLayout.ZoneId(NoLane, TaskState.ToDo)), StringComparison.Ordinal));
    }

    /// <summary>Spec §13.4 steps 2 and 4: choosing a Task in the picker and saving sets Duplicate with <c>DuplicateOf</c> = that Task.</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicatePicker_Choose_SavesDuplicateOf(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Other thing", TaskState.ToDo);
        TaskItem target = CreateTask(harness, "Other task", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved, target]);
        Task move = rig.StartMoveAsync(moved, TaskState.Duplicate, viaMenu);
        _ = rig.WaitForDialog<DuplicatePickerDialog>();
        IRenderedComponent<MudAutocomplete<TaskItem>> picker = rig.Dialogs.FindComponent<MudAutocomplete<TaskItem>>();
        await rig.Dialogs.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(target));
        rig.Button("Save").Click();
        await move;

        TaskItem? saved = harness.Store.Get(moved.Id);
        Assert.NotNull(saved);
        Assert.Equal(TaskState.Duplicate, saved.Status);
        Assert.Equal(target.Id, saved.DuplicateOf);
    }

    /// <summary>
    /// Spec §13.4 step 3 and corrections-B5 D12-12: Cancelled and Rejected open <c>ReasonDialog</c>
    /// (title, optional reason field), and <b>Skip</b> saves the state with no reason.
    /// </summary>
    /// <param name="state">Cancelled or Rejected.</param>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(TaskState.Cancelled, false)]
    [InlineData(TaskState.Cancelled, true)]
    [InlineData(TaskState.Rejected, false)]
    [InlineData(TaskState.Rejected, true)]
    public async Task ReasonDialog_Skip_SavesWithNoReason(TaskState state, bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, state, viaMenu);
        _ = rig.WaitForDialog<ReasonDialog>();
        Assert.Equal($"Mark {moved.Id} as {state.ToWire()}", rig.Dialogs.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Reason (optional)", rig.Dialogs.FindComponent<MudTextField<string>>().Instance.Label);
        rig.Button("Skip").Click();
        await move;

        TaskItem? saved = harness.Store.Get(moved.Id);
        Assert.NotNull(saved);
        Assert.Equal(state, saved.Status);
        Assert.Equal($"status: To Do → {state.ToWire()}", saved.ChangeLog[^1].Summary);
    }

    /// <summary>Spec §13.4 step 3: a reason typed in <c>ReasonDialog</c> and saved is recorded with the change (<c>(reason: …)</c>, Spec §9.4).</summary>
    /// <param name="state">Cancelled or Rejected.</param>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(TaskState.Cancelled, false)]
    [InlineData(TaskState.Rejected, true)]
    public async Task ReasonDialog_Save_RecordsTheReason(TaskState state, bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, state, viaMenu);
        _ = rig.WaitForDialog<ReasonDialog>();
        IRenderedComponent<MudTextField<string>> field = rig.Dialogs.FindComponent<MudTextField<string>>();
        await rig.Dialogs.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("Out of scope"));
        rig.Button("Save").Click();
        await move;

        TaskItem? saved = harness.Store.Get(moved.Id);
        Assert.NotNull(saved);
        Assert.Equal(state, saved.Status);
        Assert.Equal($"status: To Do → {state.ToWire()} (reason: Out of scope)", saved.ChangeLog[^1].Summary);
    }

    /// <summary>corrections-B5 D12-12: closing <c>ReasonDialog</c> with its X cancels the move - nothing is saved.</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReasonDialog_Close_CancelsTheMove(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, TaskState.Rejected, viaMenu);
        _ = rig.WaitForDialog<ReasonDialog>();
        rig.Dialogs.Find("button.mud-button-close").Click();
        await move;

        Assert.Empty(changes);
        Assert.Equal(TaskState.ToDo, harness.Store.Get(moved.Id)?.Status);
    }

    /// <summary>
    /// Plan 12.3.t and Spec §13.4 step 5: a <c>Refused</c> result (here a 201-character reason) shows a
    /// Snackbar error naming the Task and the problem, saves nothing, and the card snaps back.
    /// </summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refused_ShowsSnackbarError_SavesNothing(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, TaskState.Rejected, viaMenu);
        _ = rig.WaitForDialog<ReasonDialog>();
        IRenderedComponent<MudTextField<string>> field = rig.Dialogs.FindComponent<MudTextField<string>>();
        await rig.Dialogs.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(new string('x', 201)));
        rig.Button("Save").Click();
        await move;

        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal($"Could not move {moved.Id}: Reason is 201 characters; the limit is 200.", shown.Message);
        Assert.Equal(Severity.Error, shown.Severity);
        Assert.Equal(TaskState.ToDo, harness.Store.Get(moved.Id)?.Status);
        // contains-ok: the card's id inside the To Do zone's markup is the snap-back being asserted.
        rig.Board.WaitForAssertion(() => Assert.Contains(moved.Id.ToString(), rig.ZoneMarkup(BoardLayout.ZoneId(NoLane, TaskState.ToDo)), StringComparison.Ordinal));
    }

    /// <summary>A Task deleted after the Board rendered it (<c>NotFound</c>) shows a Snackbar error rather than failing silently.</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotFound_ShowsSnackbarError(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        File.Delete(moved.Path);
        harness.Store.RebuildFromWatcher();
        await rig.MoveAsync(moved, TaskState.InProgress, viaMenu);

        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal($"Could not move {moved.Id}: it no longer exists.", shown.Message);
        Assert.Equal(Severity.Error, shown.Severity);
    }

    /// <summary>corrections-B5 D12-8: leaving Duplicate also clears <c>DuplicateOf</c> (<c>Optional.Set(null)</c>), so the move saves instead of being refused.</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeavingDuplicate_ClearsDuplicateOf(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem target = CreateTask(harness, "Original", TaskState.ToDo);
        TaskItem moved = CreateTask(harness, "Copy", TaskState.ToDo);
        moved = Saved(harness.Service.Update(
            moved.Id,
            new TaskPatch { Status = TaskState.Duplicate, DuplicateOf = Optional<TaskId?>.Set(target.Id) },
            null,
            Human(harness)));

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [target, moved]);
        await rig.MoveAsync(moved, TaskState.ToDo, viaMenu);

        TaskItem? saved = harness.Store.Get(moved.Id);
        Assert.NotNull(saved);
        Assert.Equal(TaskState.ToDo, saved.Status);
        Assert.Null(saved.DuplicateOf);
        Assert.Empty(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
    }

    /// <summary>corrections-B5 D12-7: the drop handler clears the drag first, so the ghost buckets are gone while a dialog is open.</summary>
    [Fact]
    public async Task DialogOpenDuringDrop_GhostBucketsHidden()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, TaskState.Rejected, viaMenu: false);
        _ = rig.WaitForDialog<ReasonDialog>();

        rig.Board.WaitForAssertion(() => Assert.Empty(rig.Board.FindAll(".task-ghost-bucket")));
        rig.Button("Skip").Click();
        await move;
    }

    /// <summary>
    /// Plan 12.3.t and corrections-B5 D12-11: while dragging, a caption with the <c>Preview</c> text -
    /// "Nova will be notified" - shows under every column that can take the drop, i.e. every column
    /// header but the dragged card's own.
    /// </summary>
    [Fact]
    public async Task DragStarted_CaptionUnderEveryOtherColumn()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo, assignee: "Nova");

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        await rig.StartDragAsync(moved);

        rig.Board.WaitForAssertion(() => Assert.Equal(5, rig.Board.FindAll(".task-board-drag-caption").Count));
        Assert.All(rig.Board.FindAll(".task-board-drag-caption"), c => Assert.Equal("Nova will be notified", c.TextContent.Trim()));
        IReadOnlyList<IElement> headers = rig.Board.FindAll("div.task-board-column-header");
        Assert.Null(headers[1].QuerySelector(".task-board-drag-caption"));
        Assert.NotNull(headers[2].QuerySelector(".task-board-drag-caption"));
    }

    /// <summary>Provisional (flagged): when <c>Preview</c> blocks the wake - here an unassigned card - no caption is shown.</summary>
    [Fact]
    public async Task DragStarted_UnassignedCard_NoCaption()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo, assignee: null);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        await rig.StartDragAsync(moved);

        rig.Board.WaitForAssertion(() => Assert.Equal(3, rig.Board.FindAll(".task-ghost-bucket").Count));
        Assert.Empty(rig.Board.FindAll(".task-board-drag-caption"));
    }

    /// <summary>corrections-B5 D12-11: the caption is computed once, at drag start - turning wake-ups off mid-drag and re-rendering doesn't change it.</summary>
    [Fact]
    public async Task Caption_ComputedOnceAtDragStart()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo, assignee: "Nova");
        string novaId = harness.NovaId ?? throw new InvalidOperationException("Nova was not seeded.");

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        await rig.StartDragAsync(moved);
        rig.Board.WaitForAssertion(() => Assert.Equal(5, rig.Board.FindAll(".task-board-drag-caption").Count));
        harness.Options.Value.Tasks.WakeEnabled = false;
        await rig.Board.InvokeAsync(() => harness.Gateway.SetOnline(novaId));

        rig.Board.WaitForAssertion(() => Assert.Equal("Nova is Asleep", rig.Board.FindComponent<MudBadge>().Instance.BadgeAriaLabel));
        Assert.Equal(5, rig.Board.FindAll(".task-board-drag-caption").Count);
        Assert.Equal("Nova will be notified", rig.Board.Find(".task-board-drag-caption").TextContent.Trim());
    }

    /// <summary>The caption goes away when the drag ends.</summary>
    [Fact]
    public async Task DragEnded_CaptionGone()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo, assignee: "Nova");

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        await rig.StartDragAsync(moved);
        rig.Board.WaitForAssertion(() => Assert.Equal(5, rig.Board.FindAll(".task-board-drag-caption").Count));
        await rig.Board.InvokeAsync(() => rig.Container.CancelTransaction());

        rig.Board.WaitForAssertion(() => Assert.Empty(rig.Board.FindAll(".task-board-drag-caption")));
    }

    /// <summary>12.3.i: <c>CommitTransaction</c> doesn't re-check <c>CanDrop</c>, so a drop that lands on a multi-state column's display zone (not a state zone) moves nothing.</summary>
    [Fact]
    public async Task DropOnDisplayZone_SavesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        await rig.StartDragAsync(moved);
        MudDropContainer<TaskItem> container = rig.Container;
        await rig.Board.InvokeAsync(() => container.CommitTransaction(WontDoZone, false));

        Assert.Empty(changes);
        Assert.Equal(TaskState.ToDo, harness.Store.Get(moved.Id)?.Status);
        Assert.Empty(rig.Dialogs.FindAll(".mud-dialog"));
    }

    /// <summary>12.3.i: the Duplicate picker's Save stays disabled until a Task is chosen.</summary>
    [Fact]
    public async Task DuplicatePicker_NothingChosen_SaveDisabled()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Other thing", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, TaskState.Duplicate, viaMenu: false);
        _ = rig.WaitForDialog<DuplicatePickerDialog>();

        Assert.True(rig.Button("Save").HasAttribute("disabled"));
        Assert.False(rig.Button("Cancel").HasAttribute("disabled"));
        rig.Button("Cancel").Click();
        await move;
    }

    /// <summary>12.3.i: <b>Save</b> in the reason dialog with the field left blank records no reason, the same as Skip.</summary>
    [Fact]
    public async Task ReasonDialog_SaveBlank_SavesWithNoReason()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [moved]);
        Task move = rig.StartMoveAsync(moved, TaskState.Cancelled, viaMenu: false);
        _ = rig.WaitForDialog<ReasonDialog>();
        IRenderedComponent<MudTextField<string>> field = rig.Dialogs.FindComponent<MudTextField<string>>();
        await rig.Dialogs.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("   "));
        rig.Button("Save").Click();
        await move;

        TaskItem? saved = harness.Store.Get(moved.Id);
        Assert.NotNull(saved);
        Assert.Equal(TaskState.Cancelled, saved.Status);
        Assert.Equal("status: To Do → Cancelled", saved.ChangeLog[^1].Summary);
    }

    /// <summary>Creates a Task through <see cref="TaskService"/> as the Human, in Team Platform, and returns it as saved.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="title">The title.</param>
    /// <param name="status">The initial status (not Duplicate).</param>
    /// <param name="assignee">The assignee.</param>
    /// <returns>The saved Task.</returns>
    private static TaskItem CreateTask(TaskToolHarness harness, string title, TaskState status, string? assignee = "Nova") =>
        Saved(harness.Service.Create(new TaskDraft(title, "Platform", null, status, Assignee: assignee), Human(harness)));

    /// <summary>The Human actor.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The actor.</returns>
    private static TaskActor Human(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Asserts <paramref name="result"/> is <c>Saved</c> and returns its Task.</summary>
    /// <param name="result">The service result.</param>
    /// <returns>The saved Task.</returns>
    private static TaskItem Saved(TaskResult result) => Assert.IsType<TaskResult.Saved>(result).Task;

    /// <summary>Renders the providers and the Board over <paramref name="tasks"/> with a default Board View.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies every service.</param>
    /// <param name="tasks">The Tasks.</param>
    /// <returns>The rig.</returns>
    private static Rig Render(MudBunitContext ctx, TaskToolHarness harness, IReadOnlyList<TaskItem> tasks)
    {
        harness.AddTo(ctx.Services);
        IRenderedComponent<MudPopoverProvider> popovers = ctx.Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = ctx.Render<MudDialogProvider>();
        TaskView view = new() { Id = "board-test", Name = "Board", Kind = ViewKind.Board, Columns = DefaultColumns };
        IRenderedComponent<TaskBoard> board = ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, view);
            p.Add(b => b.Tasks, tasks);
        });
        return new Rig(board, popovers, dialogs);
    }

    /// <summary>The rendered Board, its popover provider (the card menus) and its dialog provider, with the two ways to move a card.</summary>
    /// <param name="board">The Board.</param>
    /// <param name="popovers">Where the card menus render.</param>
    /// <param name="dialogs">Where the dialogs render.</param>
    private sealed class Rig(IRenderedComponent<TaskBoard> board, IRenderedComponent<MudPopoverProvider> popovers, IRenderedComponent<MudDialogProvider> dialogs)
    {
        /// <summary>The Board.</summary>
        public IRenderedComponent<TaskBoard> Board { get; } = board;

        /// <summary>Where the card menus render.</summary>
        public IRenderedComponent<MudPopoverProvider> Popovers { get; } = popovers;

        /// <summary>Where the dialogs render.</summary>
        public IRenderedComponent<MudDialogProvider> Dialogs { get; } = dialogs;

        /// <summary>The Board's drop container.</summary>
        public MudDropContainer<TaskItem> Container => this.Board.FindComponent<MudDropContainer<TaskItem>>().Instance;

        /// <summary>Starts a drag of <paramref name="item"/> from the zone it sits in.</summary>
        /// <param name="item">The card's Task.</param>
        /// <returns>A task that completes once the transaction has started.</returns>
        public Task StartDragAsync(TaskItem item)
        {
            MudDropContainer<TaskItem> container = this.Container;
            string origin = OriginZone(item);
            return this.Board.InvokeAsync(() => container.StartTransaction(item, origin, 0, static () => Task.CompletedTask, static () => Task.CompletedTask));
        }

        /// <summary>
        /// Moves <paramref name="item"/> to <paramref name="state"/> by a drop (drag start, then commit to
        /// the state's zone - a bucket for a Won't do state) or by the card's Move to menu. A test that
        /// expects a dialog holds the returned task without awaiting it, answers the dialog, then awaits.
        /// </summary>
        /// <param name="item">The card's Task.</param>
        /// <param name="state">The target state.</param>
        /// <param name="viaMenu">Whether to use the card's Move to menu.</param>
        /// <returns>The move, still running while a dialog is open.</returns>
        public async Task StartMoveAsync(TaskItem item, TaskState state, bool viaMenu)
        {
            if (viaMenu)
            {
                this.Board.Find($"button[aria-label='Move {item.Id}']").Click();
                IElement menuItem = this.Popovers.FindAll("div.mud-menu-item")
                    .First(e => string.Equals(e.TextContent.Trim(), state.ToWire(), StringComparison.Ordinal));
                await menuItem.ClickAsync(new MouseEventArgs());
                return;
            }

            await this.StartDragAsync(item);
            string target = BoardLayout.ZoneId(NoLane, state);
            if (state is TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected)
            {
                this.Board.WaitForAssertion(() => Assert.NotEmpty(this.Board.FindAll(".task-ghost-bucket")));
            }

            MudDropContainer<TaskItem> container = this.Container;
            await this.Board.InvokeAsync(() => container.CommitTransaction(target, false));
        }

        /// <summary>Moves <paramref name="item"/> to <paramref name="state"/> when no dialog is expected.</summary>
        /// <param name="item">The card's Task.</param>
        /// <param name="state">The target state.</param>
        /// <param name="viaMenu">Whether to use the card's Move to menu.</param>
        /// <returns>A task that completes when the move is done.</returns>
        public Task MoveAsync(TaskItem item, TaskState state, bool viaMenu) => this.StartMoveAsync(item, state, viaMenu);

        /// <summary>Waits until a dialog of type <typeparamref name="TDialog"/> is open, and returns it.</summary>
        /// <typeparam name="TDialog">The dialog component.</typeparam>
        /// <returns>The open dialog.</returns>
        public IRenderedComponent<TDialog> WaitForDialog<TDialog>()
            where TDialog : Microsoft.AspNetCore.Components.IComponent
        {
            this.Dialogs.WaitForAssertion(() => Assert.NotEmpty(this.Dialogs.FindComponents<TDialog>()));
            return this.Dialogs.FindComponent<TDialog>();
        }

        /// <summary>The open dialog's button with exactly <paramref name="text"/>.</summary>
        /// <param name="text">The button's text.</param>
        /// <returns>The button.</returns>
        public IElement Button(string text) =>
            this.Dialogs.FindAll("button").First(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

        /// <summary>The markup of the zone with <paramref name="zoneId"/>.</summary>
        /// <param name="zoneId">The zone identifier.</param>
        /// <returns>Its markup.</returns>
        public string ZoneMarkup(string zoneId) =>
            Assert.Single(this.Board.FindComponents<MudDropZone<TaskItem>>(), z => string.Equals(z.Instance.Identifier, zoneId, StringComparison.Ordinal)).Markup;

        /// <summary>The zone a card sits in on the ungrouped default Board: its state's zone, or the Won't do display zone.</summary>
        /// <param name="item">The card's Task.</param>
        /// <returns>The zone id.</returns>
        private static string OriginZone(TaskItem item) =>
            item.Status is TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected ? WontDoZone : BoardLayout.ZoneId(NoLane, item.Status);
    }
}
