using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Ordering;

namespace Agency.Huddle.Tests.Tasks.Ordering;

/// <summary>
/// Tests for <see cref="TaskOrder.Compute"/> with an <see cref="ITaskGroupRule"/> (TaskOrder spec §10.3, ADR-0103): where the
/// rule is on, one Task per group is startable and Next; the others keep their place and say what they wait for.
/// </summary>
public sealed class TaskOrderGroupRuleTests
{
    /// <summary>A Task of the Platform Team in <paramref name="project"/> (or none), in the given state.</summary>
    private static TaskItem Make(int number, string? project, TaskState status = TaskState.ToDo, int? rank = null) =>
        TestTasks.Make(id: $"PLAT-{number:D4}", status: status, location: new TaskLocation("Platform", project, false)) with { Rank = rank };

    /// <summary>Orders <paramref name="tasks"/> under <paramref name="rule"/>.</summary>
    private static TaskOrderResult Order(ITaskGroupRule? rule, params TaskItem[] tasks) =>
        TaskOrder.Compute(tasks, id => tasks.FirstOrDefault(task => task.Id == id), rule);

    private static TaskOrderEntry Entry(TaskOrderResult result, int number) =>
        result.Entries.Single(entry => entry.Task.Id == new TaskId("PLAT", number));

    /// <summary>With the rule on, only the first startable Task of a Project is startable and Next; the others wait for it, keeping their place.</summary>
    [Fact]
    public void Compute_RuleOn_OnlyTheFirstStartableTaskOfAProjectCounts()
    {
        StubRule rule = new() { On = ["API"] };

        TaskOrderResult result = Order(rule, Make(1, "API", rank: 1), Make(2, "API", rank: 2), Make(3, "API", rank: 3));

        Assert.Equal(["PLAT-0001", "PLAT-0002", "PLAT-0003"], result.Entries.Select(static entry => entry.Task.Id.ToString()));
        Assert.Equal(new TaskId("PLAT", 1), result.Next?.Task.Id);
        Assert.Equal(1, result.Entries.Count(static entry => entry.IsStartable));
        Assert.Equal(new TaskId("PLAT", 1), Entry(result, 2).WaitsFor);
        Assert.Equal(new TaskId("PLAT", 1), Entry(result, 3).WaitsFor);
        Assert.Null(Entry(result, 1).WaitsFor);
    }

    /// <summary>With the rule off, or no rule at all, every startable Task counts exactly as before.</summary>
    [Fact]
    public void Compute_RuleOff_IsTodaysResult()
    {
        TaskItem[] tasks = [Make(1, "API", rank: 1), Make(2, "API", rank: 2)];

        TaskOrderResult withRule = Order(new StubRule(), tasks);
        TaskOrderResult without = Order(null, tasks);

        Assert.Equal(2, withRule.Entries.Count(static entry => entry.IsStartable));
        Assert.Equal(without.Entries.Select(static entry => entry.IsStartable), withRule.Entries.Select(static entry => entry.IsStartable));
        Assert.All(withRule.Entries, static entry => Assert.Null(entry.WaitsFor));
    }

    /// <summary>Each group has its own Next: two Projects and the Team's no-Project Tasks each count one Task.</summary>
    [Fact]
    public void Compute_RuleOn_EachGroupHasItsOwnNext()
    {
        StubRule rule = new() { On = ["API", "Web", null] };

        TaskOrderResult result = Order(
            rule,
            Make(1, "API", rank: 1),
            Make(2, "Web", rank: 2),
            Make(3, null, rank: 3),
            Make(4, "API", rank: 4),
            Make(5, "Web", rank: 5),
            Make(6, null, rank: 6));

        Assert.Equal(
            ["PLAT-0001", "PLAT-0002", "PLAT-0003"],
            result.Entries.Where(static entry => entry.IsStartable).Select(static entry => entry.Task.Id.ToString()));
        Assert.Equal(new TaskId("PLAT", 1), Entry(result, 4).WaitsFor);
        Assert.Equal(new TaskId("PLAT", 2), Entry(result, 5).WaitsFor);
        Assert.Equal(new TaskId("PLAT", 3), Entry(result, 6).WaitsFor);
    }

    /// <summary>A group that already has a Task In Progress has no startable Task, even when that Task is outside the scope; each waits for it.</summary>
    [Fact]
    public void Compute_RuleOn_GroupWithATaskInProgress_HasNoStartableTask()
    {
        TaskItem busy = Make(9, "API", TaskState.InProgress);
        StubRule rule = new() { On = ["API"], Busy = busy };

        TaskOrderResult result = Order(rule, Make(1, "API", rank: 1), Make(2, "API", rank: 2));

        Assert.Null(result.Next);
        Assert.Equal(new TaskId("PLAT", 9), Entry(result, 1).WaitsFor);
        Assert.Equal(new TaskId("PLAT", 9), Entry(result, 2).WaitsFor);
    }

    /// <summary>A Task that is blocked is not held by the rule: it keeps its blocker reason and does not take the group's turn.</summary>
    [Fact]
    public void Compute_RuleOn_ABlockedTaskDoesNotTakeTheTurn()
    {
        StubRule rule = new() { On = ["API"] };
        TaskItem blocker = Make(5, "Web", rank: 9);
        TaskItem blocked = TestTasks.Make(id: "PLAT-0001", location: new TaskLocation("Platform", "API", false), blockedBy: [blocker.Id]) with { Rank = 1 };

        TaskOrderResult result = Order(rule, blocked, Make(2, "API", rank: 2), blocker);

        Assert.Equal(new TaskId("PLAT", 2), result.Entries.Single(static entry => entry.Task.Location.Project == "API" && entry.IsStartable).Task.Id);
        Assert.Null(Entry(result, 1).WaitsFor);
        Assert.Single(Entry(result, 1).BlockedBy);
    }

    /// <summary>A hand-set <see cref="ITaskGroupRule"/>: the groups whose rule is on (a null name is the no-Project group) and the Task In Progress.</summary>
    private sealed class StubRule : ITaskGroupRule
    {
        /// <summary>The Projects whose rule is on; <see langword="null"/> stands for the Team's no-Project group.</summary>
        public HashSet<string?> On { get; init; } = [];

        /// <summary>The Task In Progress in every group, or <see langword="null"/>.</summary>
        public TaskItem? Busy { get; init; }

        /// <inheritdoc />
        public bool IsOn(string team, string? project) => this.On.Contains(project);

        /// <inheritdoc />
        public TaskItem? InProgressTask(string team, string? project) =>
            this.Busy is { } busy && string.Equals(busy.Location.Project, project, StringComparison.Ordinal) ? busy : null;
    }
}
