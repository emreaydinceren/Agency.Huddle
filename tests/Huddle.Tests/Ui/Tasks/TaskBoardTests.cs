namespace Agency.Huddle.Tests.Ui.Tasks;

using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins the Spec §13.4 <i>Zones</i> paragraph for <see cref="TaskBoard"/> (plan 12.2.t) and the
/// corrections-B5 D12 items that land on the Board's container, zones and cards (D12-4, -5, -6, -9, -10).
/// <para>
/// Markup contract fixed here (plan R4: the <c>.t</c> fixes class names the Spec doesn't give):
/// one <c>div.task-board-column-header</c> per visible column, each holding a
/// <c>.task-board-column-label</c> and a <c>.task-board-column-count</c> (Tasks in that column across
/// all lanes); one <c>div.task-board-lane</c> per <see cref="BoardLane"/>, with a
/// <c>.task-board-lane-label</c> only when the View groups; ghost buckets are
/// <c>MudDropZone OnlyZone="true" Class="task-ghost-bucket"</c> nested in their multi-state column's
/// display zone; the hidden-count chip carries class <c>task-board-hidden-count</c>. A multi-state
/// column's display zone id is <c>lane:{Base64Url(lane key)}|col:{index among visible columns}</c>.
/// </para>
/// <para>
/// Drag start/end: MudBlazor 9.10's <c>MudDropContainer.StartTransaction</c> raises
/// <c>TransactionStarted</c> synchronously, and <c>CancelTransaction</c>/<c>CommitTransaction</c>
/// raise <c>TransactionEnded</c> (checked in the package IL), so these tests drive the real events
/// rather than calling the Board's <c>OnDragStarted</c> directly.
/// </para>
/// </summary>
public sealed class TaskBoardTests
{
    /// <summary>The ungrouped lane's key, as <see cref="BoardLayout.Build"/> gives it.</summary>
    private const string NoLane = "";

    /// <summary>The Spec §12.3 default columns, restated (plan R4) rather than read from <see cref="BoardLayout.DefaultColumns"/>.</summary>
    private static readonly IReadOnlyList<BoardColumn> DefaultColumns =
    [
        new("Backlog", [TaskState.Backlog]),
        new("To Do", [TaskState.ToDo]),
        new("In Progress", [TaskState.InProgress]),
        new("Review", [TaskState.Review]),
        new("Done", [TaskState.Done]),
        new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
    ];

    /// <summary>Plan 12.2.t "Columns follow BoardLayout.Build": one header per visible column, in View order, each with its label and its count of Tasks across every lane.</summary>
    [Fact]
    public async Task Columns_FollowVisibleColumnsInOrder_WithLabelsAndCounts()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem[] tasks =
        [
            TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo),
            TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo),
            TestTasks.Make(id: "PLAT-0003", status: TaskState.InProgress),
            TestTasks.Make(id: "PLAT-0004", status: TaskState.Duplicate),
            TestTasks.Make(id: "PLAT-0005", status: TaskState.Rejected),
        ];

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), tasks);

        IReadOnlyList<IElement> headers = cut.FindAll("div.task-board-column-header");
        string[] labels = [.. headers.Select(h => h.QuerySelector(".task-board-column-label")?.TextContent.Trim() ?? "")];
        string[] counts = [.. headers.Select(h => h.QuerySelector(".task-board-column-count")?.TextContent.Trim() ?? "")];
        string[] expectedLabels = ["Backlog", "To Do", "In Progress", "Review", "Done", "Won't do"];
        string[] expectedCounts = ["0", "2", "1", "0", "0", "2"];
        Assert.Equal(expectedLabels, labels);
        Assert.Equal(expectedCounts, counts);
    }

    /// <summary>Spec §12.6 / <see cref="BoardModel.VisibleColumns"/>: a hidden column gets no header and no zone.</summary>
    [Fact]
    public async Task HiddenColumn_NotRendered()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = BoardView(columns: WithDoneHidden());

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, view, [TestTasks.Make(id: "PLAT-0001", status: TaskState.Done)]);

        string[] labels = [.. cut.FindAll("div.task-board-column-header .task-board-column-label").Select(e => e.TextContent.Trim())];
        Assert.DoesNotContain("Done", labels);
        Assert.Equal(5, labels.Length);
        Assert.DoesNotContain(BoardLayout.ZoneId(NoLane, TaskState.Done), ZoneIds(cut));
    }

    /// <summary>Plan 12.2.t "lanes follow BoardLayout.Build": grouping by assignee gives one lane per <see cref="BoardLane"/>, in its order (Kai, Nova, then the null group last), each labelled.</summary>
    [Fact]
    public async Task Lanes_OnePerBoardLayoutLane_InOrderWithLabels()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem[] tasks =
        [
            TestTasks.Make(id: "PLAT-0001", assignee: "Nova"),
            TestTasks.Make(id: "PLAT-0002", assignee: "Kai"),
            TestTasks.Make(id: "PLAT-0003", assignee: null),
        ];
        TaskView view = BoardView(grouping: [TaskGroupField.Assignee]);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, view, tasks);

        BoardModel model = BoardLayout.Build(tasks, view);
        string[] rendered = [.. cut.FindAll("div.task-board-lane").Select(l => l.QuerySelector(".task-board-lane-label")?.TextContent.Trim() ?? "")];
        string[] expected = [.. model.Lanes.Select(l => l.Label ?? "")];
        Assert.Equal(expected, rendered);
        Assert.Equal("Kai", rendered[0]);
        Assert.Equal("Nova", rendered[1]);
        Assert.Equal(3, rendered.Length);
    }

    /// <summary>With no grouping there is exactly one lane, and it has no lane label.</summary>
    [Fact]
    public async Task Ungrouped_OneLane_NoLaneLabel()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", assignee: "Nova")]);

        Assert.Single(cut.FindAll("div.task-board-lane"));
        Assert.Empty(cut.FindAll(".task-board-lane-label"));
    }

    /// <summary>
    /// Plan 12.2.t "a single-state column has one zone with id ZoneId(lane, state)" and "a
    /// multi-state column has no ghost buckets at rest": an ungrouped default Board has exactly six
    /// zones - one per single-state column, and the Won't do display zone.
    /// </summary>
    [Fact]
    public async Task AtRest_DefaultBoard_ZoneIdsAreExactlyOnePerColumn()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001")]);

        string[] expected =
        [
            BoardLayout.ZoneId(NoLane, TaskState.Backlog),
            BoardLayout.ZoneId(NoLane, TaskState.ToDo),
            BoardLayout.ZoneId(NoLane, TaskState.InProgress),
            BoardLayout.ZoneId(NoLane, TaskState.Review),
            BoardLayout.ZoneId(NoLane, TaskState.Done),
            "lane:|col:5",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), ZoneIds(cut).Order(StringComparer.Ordinal));
        Assert.Equal("lane:|state:To Do", BoardLayout.ZoneId(NoLane, TaskState.ToDo));
    }

    /// <summary>Spec §13.4: a single-state column's zone in a lane is <c>ZoneId(lane, state)</c>, and it shows that lane's cards for that state.</summary>
    [Fact]
    public async Task SingleStateColumn_ZoneIdIsZoneIdOfLaneAndState_ShowsItsCards()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem nova = TestTasks.Make(id: "PLAT-0001", status: TaskState.InProgress, assignee: "Nova");
        TaskItem kai = TestTasks.Make(id: "PLAT-0002", status: TaskState.InProgress, assignee: "Kai");

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), [nova, kai]);

        IRenderedComponent<MudDropZone<TaskItem>> novaZone = Zone(cut, BoardLayout.ZoneId("Nova", TaskState.InProgress));
        Assert.Equal(["PLAT-0001"], CardIds(novaZone));
        Assert.Single(cut.FindComponents<MudDropZone<TaskItem>>(), z => string.Equals(z.Instance.Identifier, BoardLayout.ZoneId("Kai", TaskState.InProgress), StringComparison.Ordinal));
    }

    /// <summary>corrections-B5 D12-5: a multi-state column's display zone id is <c>lane:{b64}|col:{i}</c> - here <c>lane:Tm92YQ|col:5</c> for Nova's Won't do - which <see cref="BoardLayout.TryParseZone"/> rejects.</summary>
    [Fact]
    public async Task MultiStateColumn_DisplayZoneId_EncodesLaneAndColumnIndex()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), [TestTasks.Make(id: "PLAT-0001", assignee: "Nova")]);

        Assert.Contains("lane:Tm92YQ|col:5", ZoneIds(cut));
        Assert.False(BoardLayout.TryParseZone("lane:Tm92YQ|col:5", out _, out _));
    }

    /// <summary>Spec §13.4: the multi-state display zone shows its cards (every state it holds) and refuses drops - <c>CanDrop</c> is false for it.</summary>
    [Fact]
    public async Task MultiStateColumn_ShowsItsCards_AndRefusesDrops()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem duplicate = TestTasks.Make(id: "PLAT-0001", status: TaskState.Duplicate);
        TaskItem cancelled = TestTasks.Make(id: "PLAT-0002", status: TaskState.Cancelled);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0003", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [duplicate, cancelled, toDo]);

        IRenderedComponent<MudDropZone<TaskItem>> display = Zone(cut, "lane:|col:5");
        Assert.Equal(["PLAT-0001", "PLAT-0002"], CardIds(display));
        Func<TaskItem, string, bool> canDrop = CanDrop(cut);
        Assert.False(canDrop(toDo, "lane:|col:5"));
        Assert.False(canDrop(duplicate, "lane:|col:5"));
    }

    /// <summary>Plan 12.2.t: a multi-state column has no ghost buckets at rest - no zone for Cancelled, Duplicate or Rejected, and no <c>.task-ghost-bucket</c>.</summary>
    [Fact]
    public async Task MultiStateColumn_NoGhostBucketsAtRest()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001")]);

        Assert.Empty(cut.FindAll(".task-ghost-bucket"));
        string[] ids = ZoneIds(cut);
        Assert.DoesNotContain(BoardLayout.ZoneId(NoLane, TaskState.Cancelled), ids);
        Assert.DoesNotContain(BoardLayout.ZoneId(NoLane, TaskState.Duplicate), ids);
        Assert.DoesNotContain(BoardLayout.ZoneId(NoLane, TaskState.Rejected), ids);
    }

    /// <summary>
    /// Plan 12.2.t: after <c>StartTransaction</c> (which raises <c>TransactionStarted</c>), three
    /// ghost buckets appear - one <c>OnlyZone</c> drop zone per state with id <c>ZoneId(lane, state)</c>,
    /// class <c>task-ghost-bucket</c>, nested inside the Won't do display zone.
    /// </summary>
    [Fact]
    public async Task DragStarted_ThreeGhostBucketsInsideTheMultiStateColumn()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [toDo]);
        await StartDragAsync(cut, toDo, BoardLayout.ZoneId(NoLane, TaskState.ToDo));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".task-ghost-bucket").Count));
        foreach (TaskState state in (TaskState[])[TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected])
        {
            IRenderedComponent<MudDropZone<TaskItem>> bucket = Zone(cut, BoardLayout.ZoneId(NoLane, state));
            Assert.True(bucket.Instance.OnlyZone, $"The {state} bucket must be OnlyZone.");
            Assert.Contains("task-ghost-bucket", (bucket.Instance.Class ?? "").Split(' '), StringComparer.Ordinal);
        }

        IRenderedComponent<MudDropZone<TaskItem>> display = Zone(cut, "lane:|col:5");
        Assert.Equal(3, display.FindAll(".task-ghost-bucket").Count);
    }

    /// <summary>Spec §13.4: each bucket shows its state's name and icon - Cancelled <c>Outlined.Block</c>, Duplicate <c>Outlined.ContentCopy</c>, Rejected <c>Outlined.DoNotDisturbOn</c>.</summary>
    /// <param name="state">The bucket's state.</param>
    /// <param name="name">The name the bucket shows.</param>
    /// <param name="icon">The icon the bucket shows.</param>
    [Theory]
    [InlineData(TaskState.Cancelled, "Cancelled", Icons.Material.Outlined.Block)]
    [InlineData(TaskState.Duplicate, "Duplicate", Icons.Material.Outlined.ContentCopy)]
    [InlineData(TaskState.Rejected, "Rejected", Icons.Material.Outlined.DoNotDisturbOn)]
    public async Task GhostBucket_ShowsStateNameAndIcon(TaskState state, string name, string icon)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [toDo]);
        await StartDragAsync(cut, toDo, BoardLayout.ZoneId(NoLane, TaskState.ToDo));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".task-ghost-bucket").Count));
        IRenderedComponent<MudDropZone<TaskItem>> bucket = Zone(cut, BoardLayout.ZoneId(NoLane, state));
        Assert.Equal(name, bucket.Find(".task-ghost-bucket-label").TextContent.Trim());
        Assert.Equal(icon, Assert.Single(bucket.FindComponents<MudIcon>()).Instance.Icon);
    }

    /// <summary>12.2.i: a bucket for a state the Spec gives no icon for (here Review, in a custom Review + Done column) still shows its name, with the fallback icon <c>Outlined.Label</c>.</summary>
    [Fact]
    public async Task GhostBucket_OtherState_ShowsNameAndFallbackIcon()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        IReadOnlyList<BoardColumn> columns =
        [
            new("Backlog", [TaskState.Backlog]),
            new("To Do", [TaskState.ToDo]),
            new("In Progress", [TaskState.InProgress]),
            new("Finished", [TaskState.Review, TaskState.Done]),
            new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
        ];
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(columns: columns), [toDo]);
        await StartDragAsync(cut, toDo, BoardLayout.ZoneId(NoLane, TaskState.ToDo));

        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".task-ghost-bucket").Count));
        IRenderedComponent<MudDropZone<TaskItem>> bucket = Zone(cut, BoardLayout.ZoneId(NoLane, TaskState.Review));
        Assert.Equal("Review", bucket.Find(".task-ghost-bucket-label").TextContent.Trim());
        Assert.Equal(Icons.Material.Outlined.Label, Assert.Single(bucket.FindComponents<MudIcon>()).Instance.Icon);
    }

    /// <summary>12.2.i: an unassigned Task's card has no presence badge.</summary>
    [Fact]
    public async Task UnassignedCard_NoPresenceBadge()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", assignee: null)]);

        Assert.Empty(cut.FindComponents<MudBadge>());
        Assert.Equal(["PLAT-0001"], CardIds(cut));
    }

    /// <summary>Spec §13.4: <c>TransactionEnded</c> (raised by <c>CancelTransaction</c>) clears the drag, and the buckets disappear again.</summary>
    [Fact]
    public async Task DragEnded_GhostBucketsDisappear()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [toDo]);
        await StartDragAsync(cut, toDo, BoardLayout.ZoneId(NoLane, TaskState.ToDo));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".task-ghost-bucket").Count));

        IRenderedComponent<MudDropContainer<TaskItem>> container = cut.FindComponent<MudDropContainer<TaskItem>>();
        await cut.InvokeAsync(() => container.Instance.CancelTransaction());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".task-ghost-bucket")));
        Assert.Equal(6, ZoneIds(cut).Length);
    }

    /// <summary>Spec §13.4: buckets belong to multi-state columns only - with Cancelled in its own column and Duplicate + Rejected sharing one, a drag shows exactly two buckets (Duplicate, Rejected), and Cancelled keeps its single zone.</summary>
    [Fact]
    public async Task DragStarted_OnlyMultiStateColumnsGetBuckets()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        IReadOnlyList<BoardColumn> columns =
        [
            new("Backlog", [TaskState.Backlog]),
            new("To Do", [TaskState.ToDo]),
            new("In Progress", [TaskState.InProgress]),
            new("Review", [TaskState.Review]),
            new("Done", [TaskState.Done]),
            new("Cancelled", [TaskState.Cancelled]),
            new("Closed out", [TaskState.Duplicate, TaskState.Rejected]),
        ];
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(columns: columns), [toDo]);
        await StartDragAsync(cut, toDo, BoardLayout.ZoneId(NoLane, TaskState.ToDo));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".task-ghost-bucket").Count));
        Assert.False(Zone(cut, BoardLayout.ZoneId(NoLane, TaskState.Cancelled)).Instance.OnlyZone);
        Assert.Equal(2, Zone(cut, "lane:|col:6").FindAll(".task-ghost-bucket").Count);
    }

    /// <summary>Spec §13.4: in a grouped Board every lane's multi-state column gets its own buckets, identified by that lane.</summary>
    [Fact]
    public async Task DragStarted_GroupedBoard_EachLaneGetsItsOwnBuckets()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem nova = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Nova");
        TaskItem kai = TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo, assignee: "Kai");

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), [nova, kai]);
        await StartDragAsync(cut, nova, BoardLayout.ZoneId("Nova", TaskState.ToDo));

        cut.WaitForAssertion(() => Assert.Equal(6, cut.FindAll(".task-ghost-bucket").Count));
        string[] ids = ZoneIds(cut);
        foreach (string lane in (string[])["Nova", "Kai"])
        {
            Assert.Contains(BoardLayout.ZoneId(lane, TaskState.Cancelled), ids);
            Assert.Contains(BoardLayout.ZoneId(lane, TaskState.Duplicate), ids);
            Assert.Contains(BoardLayout.ZoneId(lane, TaskState.Rejected), ids);
        }
    }

    /// <summary>corrections-B5 D12-5: <c>ItemsSelector</c> puts a single-state card in exactly its own lane/state zone and no other.</summary>
    [Fact]
    public async Task ItemsSelector_CardMatchesOnlyItsOwnZone()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem nova = TestTasks.Make(id: "PLAT-0001", status: TaskState.InProgress, assignee: "Nova");
        TaskItem kai = TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo, assignee: "Kai");

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), [nova, kai]);

        Func<TaskItem, string, bool> selector = ItemsSelector(cut);
        Assert.True(selector(nova, BoardLayout.ZoneId("Nova", TaskState.InProgress)));
        Assert.False(selector(nova, BoardLayout.ZoneId("Nova", TaskState.ToDo)));
        Assert.False(selector(nova, BoardLayout.ZoneId("Kai", TaskState.InProgress)));
        Assert.True(selector(kai, BoardLayout.ZoneId("Kai", TaskState.ToDo)));
    }

    /// <summary>corrections-B5 D12-5: a card in a multi-state column matches the column's display zone and never one of its ghost buckets, so a drag never shows cards inside a bucket.</summary>
    [Fact]
    public async Task ItemsSelector_MultiStateCard_MatchesDisplayZone_NeverABucket()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem duplicate = TestTasks.Make(id: "PLAT-0001", status: TaskState.Duplicate);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [duplicate]);

        Func<TaskItem, string, bool> selector = ItemsSelector(cut);
        Assert.True(selector(duplicate, "lane:|col:5"));
        Assert.False(selector(duplicate, BoardLayout.ZoneId(NoLane, TaskState.Duplicate)));
    }

    /// <summary>corrections-B5 D12-5: a Task in a hidden column matches no zone, so its card isn't rendered anywhere.</summary>
    [Fact]
    public async Task ItemsSelector_TaskInHiddenColumn_MatchesNoZone()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem done = TestTasks.Make(id: "PLAT-0009", status: TaskState.Done);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(columns: WithDoneHidden()), [done, toDo]);

        Func<TaskItem, string, bool> selector = ItemsSelector(cut);
        Assert.All(ZoneIds(cut), zone => Assert.False(selector(done, zone)));
        Assert.False(selector(done, BoardLayout.ZoneId(NoLane, TaskState.Done)));
        Assert.Equal(["PLAT-0001"], CardIds(cut));
    }

    /// <summary>Plan 12.2.t / Spec §13.4 "cards can't move between lanes": <c>CanDrop</c> refuses a zone in another lane.</summary>
    [Fact]
    public async Task CanDrop_OtherLane_Refused()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem nova = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Nova");
        TaskItem kai = TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo, assignee: "Kai");

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), [nova, kai]);

        Assert.False(CanDrop(cut)(nova, BoardLayout.ZoneId("Kai", TaskState.InProgress)));
    }

    /// <summary>The permissive side of the lane rule: a single-state zone in the card's own lane accepts it.</summary>
    [Fact]
    public async Task CanDrop_SameLaneSingleStateZone_Allowed()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem nova = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Nova");
        TaskItem kai = TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo, assignee: "Kai");

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), [nova, kai]);

        Assert.True(CanDrop(cut)(nova, BoardLayout.ZoneId("Nova", TaskState.InProgress)));
    }

    /// <summary>Spec §13.4: the container's <c>CanDrop</c> covers the buckets too - a bucket in the card's own lane accepts it, one in another lane refuses it.</summary>
    [Fact]
    public async Task CanDrop_GhostBucket_OwnLaneAllowed_OtherLaneRefused()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem nova = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Nova");
        TaskItem kai = TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo, assignee: "Kai");

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), [nova, kai]);

        Func<TaskItem, string, bool> canDrop = CanDrop(cut);
        Assert.True(canDrop(nova, BoardLayout.ZoneId("Nova", TaskState.Rejected)));
        Assert.False(canDrop(nova, BoardLayout.ZoneId("Kai", TaskState.Rejected)));
    }

    /// <summary>corrections-B5 D12-5: a zone id that <see cref="BoardLayout.TryParseZone"/> rejects is refused.</summary>
    [Fact]
    public async Task CanDrop_UnparseableZone_Refused()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [toDo]);

        Assert.False(CanDrop(cut)(toDo, "nonsense"));
    }

    /// <summary>Spec §13.4 <i>Columns</i>: with columns hidden, a chip reads "4 tasks in hidden columns" (the Spec's own text).</summary>
    [Fact]
    public async Task HiddenCount_Chip_ReadsNTasksInHiddenColumns()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem[] tasks =
        [
            TestTasks.Make(id: "PLAT-0001", status: TaskState.Done),
            TestTasks.Make(id: "PLAT-0002", status: TaskState.Done),
            TestTasks.Make(id: "PLAT-0003", status: TaskState.Done),
            TestTasks.Make(id: "PLAT-0004", status: TaskState.Done),
            TestTasks.Make(id: "PLAT-0005", status: TaskState.ToDo),
        ];

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(columns: WithDoneHidden()), tasks);

        Assert.Equal("4 tasks in hidden columns", cut.Find(".task-board-hidden-count").TextContent.Trim());
    }

    /// <summary>Provisional text (flagged): one hidden Task reads "1 task in hidden columns", singular.</summary>
    [Fact]
    public async Task HiddenCount_OneTask_Singular()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(columns: WithDoneHidden()), [TestTasks.Make(id: "PLAT-0001", status: TaskState.Done)]);

        Assert.Equal("1 task in hidden columns", cut.Find(".task-board-hidden-count").TextContent.Trim());
    }

    /// <summary>No hidden columns: no hidden-count chip.</summary>
    [Fact]
    public async Task NoHiddenColumns_NoChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", status: TaskState.Done)]);

        Assert.Empty(cut.FindAll(".task-board-hidden-count"));
        Assert.Single(cut.FindAll("div.task-board-lane"));
    }

    /// <summary>Provisional (flagged): a hidden column that holds no Task hides nothing, so no chip is shown.</summary>
    [Fact]
    public async Task HiddenColumnWithNoTasks_NoChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(columns: WithDoneHidden()), [TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo)]);

        Assert.Empty(cut.FindAll(".task-board-hidden-count"));
        Assert.Equal(["PLAT-0001"], CardIds(cut));
    }

    /// <summary>Spec §13.4 "subscribe in OnAfterRender(firstRender)": the Board subscribes to the container's <c>TransactionStarted</c> and <c>TransactionEnded</c> exactly once, however often it re-renders.</summary>
    [Fact]
    public async Task Container_SubscribedExactlyOnce_AcrossRerenders()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [toDo]);
        cut.Render(p => p.Add(b => b.Search, "x"));
        cut.Render(p => p.Add(b => b.Search, "y"));

        MudDropContainer<TaskItem> container = cut.FindComponent<MudDropContainer<TaskItem>>().Instance;
        Assert.Equal(1, BoardHandlerCount(container, nameof(MudDropContainer<TaskItem>.TransactionStarted)));
        Assert.Equal(1, BoardHandlerCount(container, nameof(MudDropContainer<TaskItem>.TransactionEnded)));
    }

    /// <summary>corrections-B5 D12-6: switching to another View replaces the container (<c>@key</c> per View); the Board follows it, so a drag on the new container still shows buckets.</summary>
    [Fact]
    public async Task ViewSwitch_NewContainer_DragStillShowsBuckets()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [toDo]);
        MudDropContainer<TaskItem> first = cut.FindComponent<MudDropContainer<TaskItem>>().Instance;
        cut.Render(p => p.Add(b => b.View, BoardView() with { Id = "other-board", Name = "Other" }));
        MudDropContainer<TaskItem> second = cut.FindComponent<MudDropContainer<TaskItem>>().Instance;

        await StartDragAsync(cut, toDo, BoardLayout.ZoneId(NoLane, TaskState.ToDo));

        Assert.NotSame(first, second);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".task-ghost-bucket").Count));
        Assert.Equal(0, BoardHandlerCount(first, nameof(MudDropContainer<TaskItem>.TransactionStarted)));
    }

    /// <summary>corrections-B5 D12-6: a Board first rendered with no Tasks, then given some, still subscribes to whatever container it now has.</summary>
    [Fact]
    public async Task EmptyBoardThenTasks_DragShowsBuckets()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem toDo = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), []);
        cut.Render(p => p.Add(b => b.Tasks, (IReadOnlyList<TaskItem>)[toDo]));
        await StartDragAsync(cut, toDo, BoardLayout.ZoneId(NoLane, TaskState.ToDo));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".task-ghost-bucket").Count));
    }

    /// <summary>corrections-B5 D12-9: when the Tasks change (a requery), the container is refreshed and a card shows in its new zone, not its old one.</summary>
    [Fact]
    public async Task TasksChanged_CardMovesToItsNewZone()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem before = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);
        TaskItem after = before with { Status = TaskState.InProgress };

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [before]);
        cut.Render(p => p.Add(b => b.Tasks, (IReadOnlyList<TaskItem>)[after]));

        cut.WaitForAssertion(() => Assert.Equal(["PLAT-0001"], CardIds(Zone(cut, BoardLayout.ZoneId(NoLane, TaskState.InProgress)))));
        Assert.Empty(CardIds(Zone(cut, BoardLayout.ZoneId(NoLane, TaskState.ToDo))));
    }

    /// <summary>
    /// Spec §13.4 "unsubscribe in Dispose" and corrections-B5 D12-4: disposing the Board removes its
    /// handlers from the container's two transaction events and from <c>IAgentGateway.PresenceChanged</c>,
    /// <c>PersonaHealth.Changed</c>, <c>TurnActivity.Changed</c> and <c>TaskActivity.Changed</c>, each
    /// of which it had joined while rendered.
    /// </summary>
    [Fact]
    public async Task Dispose_UnsubscribesContainerAndServiceEvents()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        int presence = SubscriberCount(harness.Gateway, nameof(harness.Gateway.PresenceChanged));
        int health = SubscriberCount(harness.Health, nameof(PersonaHealth.Changed));
        int turns = SubscriberCount(harness.TurnActivity, nameof(harness.TurnActivity.Changed));
        int activity = SubscriberCount(harness.TaskActivity, nameof(harness.TaskActivity.Changed));

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", assignee: "Nova")]);
        MudDropContainer<TaskItem> container = cut.FindComponent<MudDropContainer<TaskItem>>().Instance;
        int[] whileRendered =
        [
            SubscriberCount(harness.Gateway, nameof(harness.Gateway.PresenceChanged)) - presence,
            SubscriberCount(harness.Health, nameof(PersonaHealth.Changed)) - health,
            SubscriberCount(harness.TurnActivity, nameof(harness.TurnActivity.Changed)) - turns,
            SubscriberCount(harness.TaskActivity, nameof(harness.TaskActivity.Changed)) - activity,
        ];

        await ctx.DisposeComponentsAsync();

        int[] oneEach = [1, 1, 1, 1];
        Assert.Equal(oneEach, whileRendered);
        Assert.Equal(presence, SubscriberCount(harness.Gateway, nameof(harness.Gateway.PresenceChanged)));
        Assert.Equal(health, SubscriberCount(harness.Health, nameof(PersonaHealth.Changed)));
        Assert.Equal(turns, SubscriberCount(harness.TurnActivity, nameof(harness.TurnActivity.Changed)));
        Assert.Equal(activity, SubscriberCount(harness.TaskActivity, nameof(harness.TaskActivity.Changed)));
        Assert.Equal(0, BoardHandlerCount(container, nameof(MudDropContainer<TaskItem>.TransactionStarted)));
        Assert.Equal(0, BoardHandlerCount(container, nameof(MudDropContainer<TaskItem>.TransactionEnded)));
    }

    /// <summary>corrections-B5 D12-4: each card's presence comes from <c>TaskPresence.For</c> - an assignee who isn't connected shows "Nova is Offline".</summary>
    [Fact]
    public async Task Card_ShowsAssigneePresence()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", assignee: "Nova")]);

        Assert.Equal("Nova is Offline", PresenceLabel(cut));
    }

    /// <summary>corrections-B5 D12-4: <c>IAgentGateway.PresenceChanged</c> re-renders the cards - Nova connecting turns her badge to "Nova is Asleep".</summary>
    [Fact]
    public async Task PresenceChanged_CardPresenceUpdates()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string novaId = RequireId(harness.NovaId);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", assignee: "Nova")]);
        await cut.InvokeAsync(() => harness.Gateway.SetOnline(novaId));

        cut.WaitForAssertion(() => Assert.Equal("Nova is Asleep", PresenceLabel(cut)));
    }

    /// <summary>corrections-B5 D12-4: <c>PersonaHealth.Changed</c> re-renders the cards - a connected Nova reported Offline shows "Nova is Offline".</summary>
    [Fact]
    public async Task HealthChanged_CardPresenceUpdates()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Gateway.SetOnline(RequireId(harness.NovaId));

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", assignee: "Nova")]);
        Assert.Equal("Nova is Asleep", PresenceLabel(cut));
        await cut.InvokeAsync(() => harness.Health.Report("Nova", PersonaState.Offline, "crashed"));

        cut.WaitForAssertion(() => Assert.Equal("Nova is Offline", PresenceLabel(cut)));
    }

    /// <summary>corrections-B5 D12-4: <c>TurnActivity.Changed</c> re-renders the cards - a Turn starting shows "Nova is Awake".</summary>
    [Fact]
    public async Task TurnActivityChanged_CardShowsAwake()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string novaId = RequireId(harness.NovaId);
        harness.Gateway.SetOnline(novaId);

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", assignee: "Nova")]);
        await cut.InvokeAsync(() => harness.TurnActivity.Begin(novaId, "room-1"));

        cut.WaitForAssertion(() => Assert.Equal("Nova is Awake", PresenceLabel(cut)));
    }

    /// <summary>
    /// corrections-B5 D12-4 (B3 D9-20): a card is "AI reacting" when its <c>LastWake</c> Room has the
    /// assignee's Turn running (<c>IsBusyIn(agentId, LastWake.RoomId)</c>), and <c>TaskActivity.Changed</c>
    /// re-renders it: the Awake link to <c>/rooms/room-1</c> appears.
    /// </summary>
    [Fact]
    public async Task TaskActivityChanged_AwakeLinkForLastWakeRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string novaId = RequireId(harness.NovaId);
        harness.Gateway.SetOnline(novaId);
        harness.TurnActivity.Begin(novaId, "room-1");
        TaskItem task = TestTasks.Make(id: "PLAT-0001", assignee: "Nova");

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [task]);
        Assert.Empty(cut.FindAll("a.task-card-awake-link"));
        await cut.InvokeAsync(() =>
        {
            harness.TaskActivity.Record(new WakeRecord(task.Id, "Nova", "room-1", "General", WakeOutcome.Woken, DateTimeOffset.UtcNow));
            harness.TaskActivity.ResetForHuman(task.Id);
        });

        cut.WaitForAssertion(() => Assert.Equal("/rooms/room-1", cut.Find("a.task-card-awake-link").GetAttribute("href")));
    }

    /// <summary>corrections-B5 D12-4: busy in a different Room than the last wake's is not "AI reacting" - no Awake link.</summary>
    [Fact]
    public async Task BusyInAnotherRoom_NoAwakeLink()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string novaId = RequireId(harness.NovaId);
        harness.Gateway.SetOnline(novaId);
        harness.TurnActivity.Begin(novaId, "room-2");
        TaskItem task = TestTasks.Make(id: "PLAT-0001", assignee: "Nova");
        harness.TaskActivity.Record(new WakeRecord(task.Id, "Nova", "room-1", "General", WakeOutcome.Woken, DateTimeOffset.UtcNow));

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [task]);

        Assert.Empty(cut.FindAll("a.task-card-awake-link"));
        Assert.Equal("Nova is Awake", PresenceLabel(cut));
    }

    /// <summary>The Board hands each card the View's Fields and the search term: a due date named in Fields is shown, and the search is highlighted.</summary>
    [Fact]
    public async Task Cards_GetViewFieldsAndSearch()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Widget Alpha", dueDate: new DateOnly(2030, 1, 2));

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(fields: ["due_date"]), [task], search: "Alpha");

        Assert.Single(cut.FindAll(".task-card-field-due-date"));
        Assert.Equal("Alpha", cut.Find(".task-card-title mark").TextContent.Trim());
    }

    /// <summary>Spec §13.4 "Clicking a card opens the detail panel": the card's open button raises <c>OnOpenTask</c> with that Task's id.</summary>
    [Fact]
    public async Task CardOpen_RaisesOnOpenTaskWithId()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = TestTasks.Make(id: "PLAT-0007");
        List<TaskId> opened = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [task], onOpenTask: opened.Add);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Open PLAT-0007']").ClickAsync());

        Assert.Equal("PLAT-0007", Assert.Single(opened).ToString());
    }

    /// <summary>Builds a Board View with the restated default columns unless given others.</summary>
    /// <param name="columns">The columns; the Spec §12.3 defaults when null.</param>
    /// <param name="grouping">The lane grouping; none when null.</param>
    /// <param name="fields">The extra card fields; none when null.</param>
    /// <returns>The View.</returns>
    private static TaskView BoardView(
        IReadOnlyList<BoardColumn>? columns = null,
        IReadOnlyList<TaskGroupField>? grouping = null,
        IReadOnlyList<string>? fields = null) => new()
        {
            Id = "board-test",
            Name = "Board",
            Kind = ViewKind.Board,
            Columns = columns ?? DefaultColumns,
            Grouping = grouping ?? [],
            Fields = fields ?? [],
        };

    /// <summary>The default columns with Done hidden.</summary>
    /// <returns>The columns.</returns>
    private static IReadOnlyList<BoardColumn> WithDoneHidden() =>
        [.. DefaultColumns.Select(c => string.Equals(c.Label, "Done", StringComparison.Ordinal) ? c with { Hidden = true } : c)];

    /// <summary>Registers the harness's services and a popover provider, then renders the Board.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies every service the Board and its cards inject.</param>
    /// <param name="view">The Board View.</param>
    /// <param name="tasks">The already-filtered, sorted Tasks.</param>
    /// <param name="search">The active search term.</param>
    /// <param name="onOpenTask">Receives the id of an opened card.</param>
    /// <returns>The rendered Board.</returns>
    private static IRenderedComponent<TaskBoard> RenderBoard(
        MudBunitContext ctx,
        TaskToolHarness harness,
        TaskView view,
        IReadOnlyList<TaskItem> tasks,
        string? search = null,
        Action<TaskId>? onOpenTask = null)
    {
        harness.AddTo(ctx.Services);
        _ = ctx.Render<MudPopoverProvider>();
        return ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, view);
            p.Add(b => b.Tasks, tasks);
            p.Add(b => b.Search, search);
            if (onOpenTask is not null)
            {
                p.Add(b => b.OnOpenTask, onOpenTask);
            }
        });
    }

    /// <summary>Starts a drag of <paramref name="item"/> from <paramref name="originZone"/> on the Board's container (corrections-B5 D12-10).</summary>
    /// <param name="cut">The rendered Board.</param>
    /// <param name="item">The dragged Task.</param>
    /// <param name="originZone">The zone it starts in.</param>
    /// <returns>A task that completes once the transaction has started.</returns>
    private static Task StartDragAsync(IRenderedComponent<TaskBoard> cut, TaskItem item, string originZone)
    {
        IRenderedComponent<MudDropContainer<TaskItem>> container = cut.FindComponent<MudDropContainer<TaskItem>>();
        return cut.InvokeAsync(() => container.Instance.StartTransaction(item, originZone, 0, static () => Task.CompletedTask, static () => Task.CompletedTask));
    }

    /// <summary>Every drop zone's identifier, as rendered now.</summary>
    /// <param name="cut">The rendered Board.</param>
    /// <returns>The identifiers.</returns>
    private static string[] ZoneIds(IRenderedComponent<TaskBoard> cut) =>
        [.. cut.FindComponents<MudDropZone<TaskItem>>().Select(z => z.Instance.Identifier)];

    /// <summary>The single drop zone with <paramref name="id"/>.</summary>
    /// <param name="cut">The rendered Board.</param>
    /// <param name="id">The zone identifier.</param>
    /// <returns>The zone.</returns>
    private static IRenderedComponent<MudDropZone<TaskItem>> Zone(IRenderedComponent<TaskBoard> cut, string id) =>
        Assert.Single(cut.FindComponents<MudDropZone<TaskItem>>(), z => string.Equals(z.Instance.Identifier, id, StringComparison.Ordinal));

    /// <summary>The ids of every card rendered inside <paramref name="fragment"/>, in render order - the whole list, so an extra or missing card is caught the way membership Contains/DoesNotContain checks cannot.</summary>
    /// <typeparam name="T">The rendered component's type.</typeparam>
    /// <param name="fragment">The zone (or the Board itself) to read cards from.</param>
    private static string[] CardIds<T>(IRenderedComponent<T> fragment)
        where T : Microsoft.AspNetCore.Components.IComponent =>
        [.. fragment.FindAll(".task-card-id").Select(e => e.TextContent.Trim())];

    /// <summary>The container's <c>CanDrop</c>, which must be set.</summary>
    /// <param name="cut">The rendered Board.</param>
    /// <returns>The function.</returns>
    private static Func<TaskItem, string, bool> CanDrop(IRenderedComponent<TaskBoard> cut)
    {
        Func<TaskItem, string, bool>? canDrop = cut.FindComponent<MudDropContainer<TaskItem>>().Instance.CanDrop;
        Assert.NotNull(canDrop);
        return canDrop;
    }

    /// <summary>The container's <c>ItemsSelector</c>, which must be set.</summary>
    /// <param name="cut">The rendered Board.</param>
    /// <returns>The function.</returns>
    private static Func<TaskItem, string, bool> ItemsSelector(IRenderedComponent<TaskBoard> cut)
    {
        Func<TaskItem, string, bool>? selector = cut.FindComponent<MudDropContainer<TaskItem>>().Instance.ItemsSelector;
        Assert.NotNull(selector);
        return selector;
    }

    /// <summary>The single card's presence badge text (its <c>BadgeAriaLabel</c>).</summary>
    /// <param name="cut">The rendered Board.</param>
    /// <returns>The label.</returns>
    private static string? PresenceLabel(IRenderedComponent<TaskBoard> cut) =>
        Assert.Single(cut.FindComponents<MudBadge>()).Instance.BadgeAriaLabel;

    /// <summary>Proves a harness user id was seeded.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The id, non-null.</returns>
    private static string RequireId(string? id)
    {
        Assert.NotNull(id);
        return id;
    }

    /// <summary>Counts <paramref name="source"/>'s subscribers to the event named <paramref name="eventName"/>, via its compiler-generated backing field (facts R4).</summary>
    /// <param name="source">The event's owner.</param>
    /// <param name="eventName">The event's name.</param>
    /// <returns>The invocation-list length.</returns>
    private static int SubscriberCount(object source, string eventName)
    {
        FieldInfo field = source.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{source.GetType().Name}' has no backing field for '{eventName}'.");

        return (field.GetValue(source) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    /// <summary>
    /// Counts the <see cref="TaskBoard"/>'s own handlers on a container event. The container's drop
    /// zones subscribe to its transaction events too, so the raw invocation-list length isn't the
    /// Board's count.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="eventName">The event's name.</param>
    /// <returns>How many handlers target a <see cref="TaskBoard"/>.</returns>
    private static int BoardHandlerCount(MudDropContainer<TaskItem> container, string eventName)
    {
        FieldInfo field = container.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{container.GetType().Name}' has no backing field for '{eventName}'.");

        return (field.GetValue(container) as Delegate)?.GetInvocationList().Count(d => d.Target is TaskBoard) ?? 0;
    }
}
