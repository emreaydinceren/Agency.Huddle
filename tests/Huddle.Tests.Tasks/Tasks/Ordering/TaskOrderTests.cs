using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Ordering;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks.Ordering;

/// <summary>Tests for <see cref="TaskOrder.Compute"/>: groups, placement, cycles, scope and determinism (Spec TaskOrder §4, §7).</summary>
public sealed class TaskOrderTests
{
    /// <summary>The DEV id with the given number.</summary>
    private static TaskId Id(int number) => new("DEV", number);

    /// <summary>A DEV Task with the given number, status, rank and blockers.</summary>
    private static TaskItem Make(int number, TaskState status = TaskState.ToDo, int? rank = null, int[]? blockedBy = null) =>
        TestTasks.Make(id: Id(number).ToString(), status: status, blockedBy: [.. (blockedBy ?? []).Select(Id)]) with { Rank = rank };

    /// <summary>A lookup over <paramref name="tasks"/>.</summary>
    private static Func<TaskId, TaskItem?> Lookup(params TaskItem[] tasks) =>
        id => tasks.FirstOrDefault(task => task.Id == id);

    /// <summary>Computes the order of <paramref name="tasks"/> with a lookup over the same Tasks.</summary>
    private static TaskOrderResult Order(params TaskItem[] tasks) => TaskOrder.Compute(tasks, Lookup(tasks));

    /// <summary>The listed ids of <paramref name="result"/>, in order.</summary>
    private static string[] Ids(TaskOrderResult result) => [.. result.Entries.Select(entry => entry.Task.Id.ToString())];

    /// <summary>The entry of the Task with the given number.</summary>
    private static TaskOrderEntry Entry(TaskOrderResult result, int number) => result.Entries.Single(entry => entry.Task.Id == Id(number));

    /// <summary>The worked example of §4.5 lists DEV-3, DEV-9, DEV-5, DEV-1, DEV-2, DEV-7 with the stated groups, reasons and Next.</summary>
    [Fact]
    public void Compute_WorkedExample_MatchesTheSpecTable()
    {
        TaskOrderResult result = Order(
            Make(1, rank: 1, blockedBy: [5]),
            Make(2, rank: 2),
            Make(5, rank: 40, blockedBy: [9]),
            Make(9, TaskState.Backlog),
            Make(3, TaskState.InProgress),
            Make(7, TaskState.Backlog, rank: 5));

        Assert.Equal(["DEV-0003", "DEV-0009", "DEV-0005", "DEV-0001", "DEV-0002", "DEV-0007"], Ids(result));
        Assert.Equal(
            [TaskOrderGroup.Started, TaskOrderGroup.Next, TaskOrderGroup.Next, TaskOrderGroup.Next, TaskOrderGroup.Next, TaskOrderGroup.Backlog],
            result.Entries.Select(entry => entry.Group));
        Assert.Equal(Id(5), Entry(result, 9).Blocks);
        Assert.Equal(Id(1), Entry(result, 5).Blocks);
        Assert.Null(Entry(result, 1).Blocks);
        Assert.Equal(TaskState.Backlog, Assert.Single(Entry(result, 5).BlockedBy).Status);
        Assert.Equal(Id(2), result.Next?.Task.Id);
        Assert.False(Entry(result, 1).IsStartable);
        Assert.False(Entry(result, 3).IsStartable);
    }

    /// <summary>A chain A blocks B blocks T is listed A, B, T, each saying what it blocks.</summary>
    [Fact]
    public void Compute_ChainOfThree_ListsBlockersAheadOfWhatTheyBlock()
    {
        TaskOrderResult result = Order(Make(1, rank: 1, blockedBy: [2]), Make(2, rank: 2, blockedBy: [3]), Make(3, rank: 3));

        Assert.Equal(["DEV-0003", "DEV-0002", "DEV-0001"], Ids(result));
        Assert.Equal(Id(2), Entry(result, 3).Blocks);
        Assert.Equal(Id(1), Entry(result, 2).Blocks);
        Assert.Null(Entry(result, 1).Blocks);
        Assert.Equal(Id(3), result.Next?.Task.Id);
    }

    /// <summary>A blocker of two Tasks is placed at the better-placed one and not moved again.</summary>
    [Fact]
    public void Compute_BlockerOfTwoTasks_PlacedAtTheBetterOne()
    {
        TaskOrderResult result = Order(
            Make(1, rank: 1, blockedBy: [9]),
            Make(2, rank: 2, blockedBy: [9]),
            Make(9, rank: 50));

        Assert.Equal(["DEV-0009", "DEV-0001", "DEV-0002"], Ids(result));
        Assert.Equal(Id(1), Entry(result, 9).Blocks);
    }

    /// <summary>A two-Task cycle is reported on both, neither is startable, and the walk ends.</summary>
    [Fact]
    public void Compute_TwoTaskCycle_ReportedAndNotStartable()
    {
        TaskOrderResult result = Order(Make(1, rank: 1, blockedBy: [2]), Make(2, rank: 2, blockedBy: [1]));

        Assert.Equal(2, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Equal([Id(1), Id(2)], entry.Cycle));
        Assert.All(result.Entries, entry => Assert.False(entry.IsStartable));
        Assert.Null(result.Next);
    }

    /// <summary>A three-Task cycle gives every member the same list in walk order and none is startable.</summary>
    [Fact]
    public void Compute_ThreeTaskCycle_ReportedAndNotStartable()
    {
        TaskOrderResult result = Order(
            Make(1, rank: 1, blockedBy: [3]),
            Make(2, rank: 2, blockedBy: [1]),
            Make(3, rank: 3, blockedBy: [2]));

        Assert.Equal(3, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Equal([Id(1), Id(3), Id(2)], entry.Cycle));
        Assert.All(result.Entries, entry => Assert.False(entry.IsStartable));
        Assert.Null(result.Next);
    }

    /// <summary>A blocker outside the scope is never listed; it appears only in the BlockedBy of what it blocks, with its assignee.</summary>
    [Fact]
    public void Compute_OutOfScopeBlocker_OnlyInBlockedBy()
    {
        TaskItem outside = TestTasks.Make(id: "OTH-0001", status: TaskState.InProgress, assignee: "Ann");
        TaskItem task = TestTasks.Make(id: "DEV-0001", blockedBy: [outside.Id]);

        TaskOrderResult result = TaskOrder.Compute([task], Lookup(task, outside));

        TaskOrderEntry entry = Assert.Single(result.Entries);
        OpenBlocker blocker = Assert.Single(entry.BlockedBy);
        Assert.Equal(outside.Id, blocker.Id);
        Assert.Equal("Ann", blocker.Assignee);
        Assert.False(entry.IsStartable);
        Assert.Null(result.Next);
    }

    /// <summary>An In Progress blocker stays in Started, is walked through to its own Backlog blocker, and the blocked Task shows it.</summary>
    [Fact]
    public void Compute_StartedBlocker_StaysInStarted()
    {
        TaskOrderResult result = Order(
            Make(1, blockedBy: [2]),
            Make(2, TaskState.InProgress, blockedBy: [3]),
            Make(3, TaskState.Backlog));

        Assert.Equal(["DEV-0002", "DEV-0003", "DEV-0001"], Ids(result));
        Assert.Equal(TaskOrderGroup.Started, Entry(result, 2).Group);
        Assert.Null(Entry(result, 2).Blocks);
        Assert.Equal(TaskOrderGroup.Next, Entry(result, 3).Group);
        Assert.Equal(Id(2), Entry(result, 3).Blocks);
        Assert.Equal(Id(2), Assert.Single(Entry(result, 1).BlockedBy).Id);
        Assert.Null(result.Next);
    }

    /// <summary>Shuffling the scope and reversing the blocked_by lists leaves the result unchanged.</summary>
    [Fact]
    public void Compute_ShuffledScopeAndReversedBlockedBy_SameResult()
    {
        TaskItem[] tasks =
        [
            Make(1, rank: 1, blockedBy: [5, 6]),
            Make(2, rank: 2),
            Make(5, rank: 40, blockedBy: [9]),
            Make(6, TaskState.Backlog, rank: 3),
            Make(9, TaskState.Backlog),
            Make(3, TaskState.InProgress),
            Make(7, TaskState.Backlog, rank: 5),
        ];
        TaskItem[] shuffled = [tasks[4], tasks[0], tasks[6], tasks[2], tasks[5], tasks[1], tasks[3]];
        TaskItem[] reversed = [.. shuffled.Select(task => task with { BlockedBy = [.. task.BlockedBy.Reverse()] })];

        TaskOrderResult expected = TaskOrder.Compute(tasks, Lookup(tasks));
        TaskOrderResult actual = TaskOrder.Compute(reversed, Lookup(reversed));

        Assert.Equal(Describe(expected), Describe(actual));
        Assert.Contains(expected.Entries, entry => entry.BlockedBy.Count > 1);
    }

    /// <summary>Each entry as its id, group, blocks, startability, blocker ids and cycle ids, for comparing two results.</summary>
    private static string[] Describe(TaskOrderResult result) =>
    [
        .. result.Entries.Select(entry => string.Join(
            '|',
            entry.Task.Id,
            entry.Group,
            entry.Blocks,
            entry.IsStartable,
            string.Join(',', entry.BlockedBy.Select(blocker => blocker.Id)),
            string.Join(',', entry.Cycle))),
    ];

    /// <summary>Seeds are walked in rank order whatever the order of the scope: a scope listed worst-first still lists the best Task first.</summary>
    [Fact]
    public void Compute_ScopeListedWorstFirst_ListsByRank()
    {
        TaskOrderResult result = Order(
            Make(3, TaskState.Backlog, rank: 9),
            Make(2, rank: 2),
            Make(1, rank: 1),
            Make(4, TaskState.Backlog, rank: 4));

        Assert.Equal(["DEV-0001", "DEV-0002", "DEV-0004", "DEV-0003"], Ids(result));
    }

    /// <summary>A blocker the lookup cannot find blocks nothing.</summary>
    [Fact]
    public void Compute_DeletedBlocker_BlocksNothing()
    {
        TaskItem task = Make(1, blockedBy: [77]);

        TaskOrderResult result = TaskOrder.Compute([task], Lookup(task));

        TaskOrderEntry entry = Assert.Single(result.Entries);
        Assert.Empty(entry.BlockedBy);
        Assert.True(entry.IsStartable);
    }

    /// <summary>A terminal Task in scope is ignored, and as a blocker it no longer blocks.</summary>
    [Fact]
    public void Compute_TerminalTaskInScope_IsIgnored()
    {
        TaskOrderResult result = Order(Make(1, blockedBy: [2]), Make(2, TaskState.Done));

        TaskOrderEntry entry = Assert.Single(result.Entries);
        Assert.Equal(Id(1), entry.Task.Id);
        Assert.Empty(entry.BlockedBy);
        Assert.True(entry.IsStartable);
    }

    /// <summary>The same id twice in the scope is listed once.</summary>
    [Fact]
    public void Compute_DuplicateIds_AreCollapsed()
    {
        TaskItem task = Make(1);

        TaskOrderResult result = TaskOrder.Compute([task, task], Lookup(task));

        Assert.Single(result.Entries);
    }

    /// <summary>When every To Do Task is blocked, Next is null.</summary>
    [Fact]
    public void Compute_NothingStartable_NextIsNull()
    {
        TaskOrderResult result = Order(Make(1, blockedBy: [2]), Make(2, TaskState.Backlog), Make(3, TaskState.InProgress));

        Assert.Null(result.Next);
        Assert.Equal(3, result.Entries.Count);
    }

    /// <summary>The gate's blocker test and the order agree on blocked or not for each state of the blocker.</summary>
    [Theory]
    [InlineData(TaskState.Backlog, true)]
    [InlineData(TaskState.ToDo, true)]
    [InlineData(TaskState.InProgress, true)]
    [InlineData(TaskState.NeedsYou, true)]
    [InlineData(TaskState.Done, false)]
    [InlineData(TaskState.Cancelled, false)]
    [InlineData(TaskState.Duplicate, false)]
    [InlineData(TaskState.Rejected, false)]
    public void Compute_AgreesWithTheGateOnBlocked(TaskState blockerState, bool blocked)
    {
        TaskItem blocker = Make(2, blockerState);
        TaskItem task = Make(1, blockedBy: [2]);
        Func<TaskId, TaskItem?> lookup = Lookup(task, blocker);

        bool gateBlocked = TaskBlockers.Open(task, lookup).Count > 0;
        TaskOrderEntry entry = Entry(TaskOrder.Compute([task, blocker], lookup), 1);

        Assert.Equal(blocked, gateBlocked);
        Assert.Equal(gateBlocked, entry.BlockedBy.Count > 0);
        Assert.Equal(!gateBlocked, entry.IsStartable);
    }
}
