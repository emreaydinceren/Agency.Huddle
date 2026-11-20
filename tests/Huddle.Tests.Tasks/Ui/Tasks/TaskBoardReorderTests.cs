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
/// Pins Board drag to reorder for <see cref="TaskBoard"/>: a drop inside one column saves only the
/// card's rank, a drop into another column saves only its status, reorder is off while the View sorts,
/// and a refused save snaps the card back with a Snackbar. Two MudBlazor facts the placement rests on
/// are pinned here as well: the drop's <c>IndexInZone</c> is the insertion index among the zone's cards
/// without the dragged one, and a zone keeps its order until the Board is given the Tasks in a new order.
/// </summary>
public sealed class TaskBoardReorderTests
{
    /// <summary>The ungrouped lane's key.</summary>
    private const string NoLane = "";

    /// <summary>The Spec §12.3 default columns, restated.</summary>
    private static readonly IReadOnlyList<BoardColumn> DefaultColumns =
    [
        new("Backlog", [TaskState.Backlog]),
        new("To Do", [TaskState.ToDo]),
        new("In Progress", [TaskState.InProgress]),
        new("Needs You", [TaskState.NeedsYou]),
        new("Done", [TaskState.Done]),
        new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
    ];

    /// <summary>The To Do zone's id.</summary>
    private static readonly string ToDoZone = BoardLayout.ZoneId(NoLane, TaskState.ToDo);

    /// <summary>Dropping C after A in To Do (ranks 10, 20, 30) saves rank 15 on C and nothing else, in one change.</summary>
    [Fact]
    public async Task SameColumnDrop_SavesOnlyTheRank_InOneChange()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem b = Create(harness, "B", 20);
        TaskItem c = Create(harness, "C", 30);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;
        int entries = c.ChangeLog.Count;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, b, c]);
        await rig.DropAsync(c, hoverIndex: 0, ToDoZone);

        TaskItem? saved = harness.Store.Get(c.Id);
        Assert.NotNull(saved);
        Assert.Equal(15, saved.Rank);
        Assert.Equal(TaskState.ToDo, saved.Status);
        Assert.Equal(c.Title, saved.Title);
        Assert.Equal(entries + 1, saved.ChangeLog.Count);
        Assert.Equal("rank: 30 → 15", saved.ChangeLog[^1].Summary);
        TaskChange change = Assert.Single(changes);
        Assert.Equal(TaskActorKind.Human, change.Actor.Kind);
        Assert.Equal(10, harness.Store.Get(a.Id)?.Rank);
        Assert.Equal(20, harness.Store.Get(b.Id)?.Rank);
    }

    /// <summary>
    /// MudBlazor fact 1: <c>IndexInZone</c> is the insertion index among the zone's cards <i>without</i> the dragged one -
    /// hovering a card means "drop after it", so dragging A onto C gives index 2 (after both B and C), not a position
    /// counted with A still in the list. Pinned by the rank the drop earns.
    /// </summary>
    [Fact]
    public async Task IndexInZone_IsTheInsertionIndexWithoutTheDraggedCard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem b = Create(harness, "B", 20);
        TaskItem c = Create(harness, "C", 30);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, b, c]);
        await rig.DropAsync(a, hoverIndex: 2, ToDoZone);

        Assert.Equal(40, harness.Store.Get(a.Id)?.Rank);
    }

    /// <summary>A card dropped on its own position keeps its rank: nothing is written.</summary>
    [Fact]
    public async Task DropOnOwnPosition_WritesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem b = Create(harness, "B", 20);
        TaskItem c = Create(harness, "C", 30);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, b, c]);
        await rig.DropAsync(b, hoverIndex: 1, ToDoZone);

        Assert.Empty(changes);
        Assert.Equal(20, harness.Store.Get(b.Id)?.Rank);
    }

    /// <summary>A drop on the placeholder above the first card puts the card at the top: half the first rank.</summary>
    [Fact]
    public async Task DropOnTopPlaceholder_TakesHalfTheFirstRank()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem b = Create(harness, "B", 20);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, b]);
        await rig.DropAsync(b, hoverIndex: -1, ToDoZone);

        Assert.Equal(5, harness.Store.Get(b.Id)?.Rank);
    }

    /// <summary>
    /// MudBlazor fact 2: a reorder drop does not move the cards by itself - the zone keeps its order until the Board is
    /// given the Tasks in their new order, and then the zone follows <c>Items</c> order.
    /// </summary>
    [Fact]
    public async Task Zone_KeepsItsOrderUntilTheTasksChange_ThenFollowsThem()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem b = Create(harness, "B", 20);
        TaskItem c = Create(harness, "C", 30);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, b, c]);
        await rig.DropAsync(c, hoverIndex: -1, ToDoZone);
        Assert.Equal([a.Id.ToString(), b.Id.ToString(), c.Id.ToString()], rig.CardIds(ToDoZone));

        IReadOnlyList<TaskItem> fresh = TaskQuery.Sort([.. new[] { a, b, c }.Select(t => harness.Store.Get(t.Id) ?? t)], []);
        rig.Board.Render(p => p.Add(x => x.Tasks, fresh));

        rig.Board.WaitForAssertion(() => Assert.Equal([c.Id.ToString(), a.Id.ToString(), b.Id.ToString()], rig.CardIds(ToDoZone)));
    }

    /// <summary>A drop into another column changes the status only: the rank is kept.</summary>
    [Fact]
    public async Task CrossColumnDrop_ChangesStatusOnly()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem c = Create(harness, "C", 30);
        string inProgress = BoardLayout.ZoneId(NoLane, TaskState.InProgress);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, c]);
        await rig.DropAsync(c, hoverIndex: null, inProgress);

        TaskItem? saved = harness.Store.Get(c.Id);
        Assert.NotNull(saved);
        Assert.Equal(TaskState.InProgress, saved.Status);
        Assert.Equal(30, saved.Rank);
        Assert.Equal("status: To Do → In Progress", saved.ChangeLog[^1].Summary);
    }

    /// <summary>Reorder is on for a single-state column while the View has no sort, and off for the Won't do display zone.</summary>
    [Fact]
    public async Task NoViewSort_SingleStateZonesAllowReorder_DisplayZoneDoesNot()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a]);

        Assert.True(rig.Zone(ToDoZone).AllowReorder);
        Assert.False(rig.Zone("lane:|col:5").AllowReorder);
    }

    /// <summary>With a sort on the View, reorder is off, and a same-column drop writes nothing.</summary>
    [Fact]
    public async Task ViewSortSet_ReorderOff_SameColumnDropWritesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem b = Create(harness, "B", 20);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, b], sort: [new SortKey("title", Agency.Huddle.App.Tasks.Views.SortDirection.Ascending)]);
        Assert.False(rig.Zone(ToDoZone).AllowReorder);
        await rig.DropAsync(b, hoverIndex: 0, ToDoZone);

        Assert.Empty(changes);
        Assert.Equal(20, harness.Store.Get(b.Id)?.Rank);
    }

    /// <summary>While reorder is on, dragging an assigned To Do card shows no hint in its own column and no Board-wide caption: a same-column drop saves a rank, which wakes no one (smart wakes 5.5).</summary>
    [Fact]
    public async Task DragStarted_ReorderOn_OwnColumnShowsNoHint_AndNoColumnCaption()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft("A", "Platform", null, TaskState.ToDo) { Rank = 10, Assignee = "Nova" },
            TaskActors.Human(harness.Options.Value))).Task;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a]);
        await rig.StartDragAsync(a);

        string[] hints = [
            BoardWakeHints.Entry(NoLane, TaskState.Backlog, "Nova"),
            BoardWakeHints.Entry(NoLane, TaskState.InProgress, "Nova"),
            BoardWakeHints.Entry(NoLane, TaskState.NeedsYou, "Nova"),
        ];
        rig.Board.WaitForAssertion(() => Assert.Equal(hints, BoardWakeHints.Of(rig.Board)));
        Assert.Empty(rig.Board.FindAll($".{BoardWakeHints.RetiredCaptionClass}"));
    }

    /// <summary>While the View sorts, reorder is off, and the card's own column still shows no hint; the other zones judge the status move as before.</summary>
    [Fact]
    public async Task DragStarted_ViewSorted_OwnColumnShowsNoHint()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft("A", "Platform", null, TaskState.ToDo) { Rank = 10, Assignee = "Nova" },
            TaskActors.Human(harness.Options.Value))).Task;

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a], sort: [new SortKey("title", Agency.Huddle.App.Tasks.Views.SortDirection.Ascending)]);
        await rig.StartDragAsync(a);

        string[] hints = [
            BoardWakeHints.Entry(NoLane, TaskState.Backlog, "Nova"),
            BoardWakeHints.Entry(NoLane, TaskState.InProgress, "Nova"),
            BoardWakeHints.Entry(NoLane, TaskState.NeedsYou, "Nova"),
        ];
        rig.Board.WaitForAssertion(() => Assert.Equal(hints, BoardWakeHints.Of(rig.Board)));
        Assert.Empty(rig.Board.FindAll($".{BoardWakeHints.RetiredCaptionClass}"));
    }

    /// <summary>A save the service refuses (the Task was deleted after the Board drew it) shows a Snackbar error, writes nothing and refreshes the zone.</summary>
    [Fact]
    public async Task RefusedUpdate_ShowsSnackbar_AndSnapsBack()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem a = Create(harness, "A", 10);
        TaskItem b = Create(harness, "B", 20);

        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, [a, b]);
        File.Delete(b.Path);
        harness.Store.RebuildFromWatcher();
        await rig.DropAsync(b, hoverIndex: -1, ToDoZone);

        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal($"Could not move {b.Id}: it no longer exists.", shown.Message);
        Assert.Equal(Severity.Error, shown.Severity);
        rig.Board.WaitForAssertion(() => Assert.Equal([a.Id.ToString(), b.Id.ToString()], rig.CardIds(ToDoZone)));
    }

    /// <summary>Creates a ranked To Do Task in Team Platform through <see cref="TaskService"/> as the Human, and returns it as saved.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="title">The title.</param>
    /// <param name="rank">The rank.</param>
    /// <returns>The saved Task.</returns>
    private static TaskItem Create(TaskToolHarness harness, string title, int rank) =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft(title, "Platform", null, TaskState.ToDo) { Rank = rank },
            TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>Renders the providers and the Board over <paramref name="tasks"/> (sorted as the page would) with a default Board View.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies every service.</param>
    /// <param name="tasks">The Tasks.</param>
    /// <param name="sort">The View's sort keys, if any.</param>
    /// <returns>The rig.</returns>
    private static Rig Render(MudBunitContext ctx, TaskToolHarness harness, IReadOnlyList<TaskItem> tasks, IReadOnlyList<SortKey>? sort = null)
    {
        harness.AddTo(ctx.Services);
        _ = ctx.Render<MudPopoverProvider>();
        _ = ctx.Render<MudDialogProvider>();
        TaskView view = new() { Id = "board-test", Name = "Board", Kind = ViewKind.Kanban, Columns = DefaultColumns, Sort = sort ?? [] };
        IReadOnlyList<TaskItem> ordered = TaskQuery.Sort(tasks, view.Sort);
        IRenderedComponent<TaskBoard> board = ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, view);
            p.Add(b => b.Tasks, ordered);
        });
        return new Rig(board);
    }

    /// <summary>The rendered Board, with the way to drop a card the way a browser does.</summary>
    /// <param name="board">The Board.</param>
    private sealed class Rig(IRenderedComponent<TaskBoard> board)
    {
        /// <summary>The Board.</summary>
        public IRenderedComponent<TaskBoard> Board { get; } = board;

        /// <summary>
        /// Drags <paramref name="item"/> out of its zone, hovers the card at <paramref name="hoverIndex"/> in the target zone
        /// (-1 is the placeholder above the first card; <see langword="null"/> hovers nothing), and drops on the zone.
        /// </summary>
        /// <param name="item">The card's Task.</param>
        /// <param name="hoverIndex">The hovered drop item's <c>index</c> attribute, or <see langword="null"/>.</param>
        /// <param name="targetZone">The target zone id.</param>
        /// <returns>A task that completes once the drop handler has run.</returns>
        public async Task DropAsync(TaskItem item, int? hoverIndex, string targetZone)
        {
            MudDropContainer<TaskItem> container = this.Board.FindComponent<MudDropContainer<TaskItem>>().Instance;
            string origin = BoardLayout.ZoneId(NoLane, item.Status);
            int startIndex = Array.IndexOf(this.CardIds(origin), item.Id.ToString());
            await this.Board.InvokeAsync(() => container.StartTransaction(item, origin, startIndex, static () => Task.CompletedTask, static () => Task.CompletedTask));
            IElement zone = this.Board.Find($".mud-drop-zone[identifier='{targetZone}']");
            if (hoverIndex is { } index)
            {
                IElement? hovered = zone.QuerySelector($".mud-drop-item[index='{index}']");
                Assert.NotNull(hovered);
                await this.Board.InvokeAsync(() => hovered.TriggerEventAsync("ondragenter", new DragEventArgs()));
            }

            await this.Board.InvokeAsync(() => container.CommitTransaction(targetZone, this.Zone(targetZone).AllowReorder));
        }

        /// <summary>Starts dragging <paramref name="item"/> out of its zone and leaves the drag in progress.</summary>
        /// <param name="item">The card's Task.</param>
        /// <returns>A task that completes once the drag has started.</returns>
        public async Task StartDragAsync(TaskItem item)
        {
            MudDropContainer<TaskItem> container = this.Board.FindComponent<MudDropContainer<TaskItem>>().Instance;
            string origin = BoardLayout.ZoneId(NoLane, item.Status);
            await this.Board.InvokeAsync(() => container.StartTransaction(item, origin, 0, static () => Task.CompletedTask, static () => Task.CompletedTask));
        }

        /// <summary>The zone component with <paramref name="zoneId"/>.</summary>
        /// <param name="zoneId">The zone identifier.</param>
        /// <returns>The zone.</returns>
        public MudDropZone<TaskItem> Zone(string zoneId) =>
            Assert.Single(this.Board.FindComponents<MudDropZone<TaskItem>>(), z => string.Equals(z.Instance.Identifier, zoneId, StringComparison.Ordinal)).Instance;

        /// <summary>The ids of the cards the zone shows, top to bottom.</summary>
        /// <param name="zoneId">The zone identifier.</param>
        /// <returns>The ids.</returns>
        public string[] CardIds(string zoneId) =>
            [.. Assert.Single(this.Board.FindComponents<MudDropZone<TaskItem>>(), z => string.Equals(z.Instance.Identifier, zoneId, StringComparison.Ordinal))
                .FindComponents<TaskCard>().Select(card => card.Instance.Task.Id.ToString())];
    }
}
