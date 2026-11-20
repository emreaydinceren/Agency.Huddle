using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// The status table of Smart wakes spec 3.4, written out move by move rather than computed from <see cref="WakeRelevance.PhaseOf"/>
/// (so a wrong phase cannot hide a wrong verdict): all 64 ordered pairs of the eight statuses for both recipients, the phase of each
/// status, and the narrower Request rule of C-5.
/// </summary>
public sealed class WakeRelevanceStatusTests
{
    /// <summary>The four phases of 3.4, one per status in <see cref="TaskStates.All"/> order: Backlog, To Do, In Progress, Needs You, Done, Cancelled, Duplicate, Rejected.</summary>
    [Fact]
    public void PhaseOf_EveryStatus_IsItsPhase()
    {
        TaskPhase[] expected =
        [
            TaskPhase.Planning,
            TaskPhase.Planning,
            TaskPhase.Active,
            TaskPhase.Active,
            TaskPhase.Done,
            TaskPhase.WontDo,
            TaskPhase.WontDo,
            TaskPhase.WontDo,
        ];

        Assert.Equal(expected, TaskStates.All.Select(WakeRelevance.PhaseOf));
    }

    /// <summary>
    /// Every ordered pair of statuses against 3.4: whether the move wakes the assignee and whether it wakes the manager. A move to the
    /// same status is no move and is silent. The rows follow the spec's table in order: Planning to Planning, Planning to Active,
    /// Active to Active, Active to Planning, Active to Won't do, Active to Done, Done or Won't do back to Planning or Active,
    /// Planning to Won't do or Done, then Done and Won't do among themselves.
    /// </summary>
    [Theory]
    [InlineData(TaskState.Backlog, TaskState.Backlog, false, false)]
    [InlineData(TaskState.Backlog, TaskState.ToDo, true, false)]
    [InlineData(TaskState.Backlog, TaskState.InProgress, true, false)]
    [InlineData(TaskState.Backlog, TaskState.NeedsYou, true, false)]
    [InlineData(TaskState.Backlog, TaskState.Done, false, false)]
    [InlineData(TaskState.Backlog, TaskState.Cancelled, false, false)]
    [InlineData(TaskState.Backlog, TaskState.Duplicate, false, false)]
    [InlineData(TaskState.Backlog, TaskState.Rejected, false, false)]
    [InlineData(TaskState.ToDo, TaskState.Backlog, true, false)]
    [InlineData(TaskState.ToDo, TaskState.ToDo, false, false)]
    [InlineData(TaskState.ToDo, TaskState.InProgress, true, false)]
    [InlineData(TaskState.ToDo, TaskState.NeedsYou, true, false)]
    [InlineData(TaskState.ToDo, TaskState.Done, false, false)]
    [InlineData(TaskState.ToDo, TaskState.Cancelled, false, false)]
    [InlineData(TaskState.ToDo, TaskState.Duplicate, false, false)]
    [InlineData(TaskState.ToDo, TaskState.Rejected, false, false)]
    [InlineData(TaskState.InProgress, TaskState.Backlog, true, true)]
    [InlineData(TaskState.InProgress, TaskState.ToDo, true, true)]
    [InlineData(TaskState.InProgress, TaskState.InProgress, false, false)]
    [InlineData(TaskState.InProgress, TaskState.NeedsYou, false, false)]
    [InlineData(TaskState.InProgress, TaskState.Done, true, true)]
    [InlineData(TaskState.InProgress, TaskState.Cancelled, true, true)]
    [InlineData(TaskState.InProgress, TaskState.Duplicate, true, true)]
    [InlineData(TaskState.InProgress, TaskState.Rejected, true, true)]
    [InlineData(TaskState.NeedsYou, TaskState.Backlog, true, true)]
    [InlineData(TaskState.NeedsYou, TaskState.ToDo, true, true)]
    [InlineData(TaskState.NeedsYou, TaskState.InProgress, true, false)]
    [InlineData(TaskState.NeedsYou, TaskState.NeedsYou, false, false)]
    [InlineData(TaskState.NeedsYou, TaskState.Done, true, true)]
    [InlineData(TaskState.NeedsYou, TaskState.Cancelled, true, true)]
    [InlineData(TaskState.NeedsYou, TaskState.Duplicate, true, true)]
    [InlineData(TaskState.NeedsYou, TaskState.Rejected, true, true)]
    [InlineData(TaskState.Done, TaskState.Backlog, true, true)]
    [InlineData(TaskState.Done, TaskState.ToDo, true, true)]
    [InlineData(TaskState.Done, TaskState.InProgress, true, true)]
    [InlineData(TaskState.Done, TaskState.NeedsYou, true, true)]
    [InlineData(TaskState.Done, TaskState.Done, false, false)]
    [InlineData(TaskState.Done, TaskState.Cancelled, false, false)]
    [InlineData(TaskState.Done, TaskState.Duplicate, false, false)]
    [InlineData(TaskState.Done, TaskState.Rejected, false, false)]
    [InlineData(TaskState.Cancelled, TaskState.Backlog, true, true)]
    [InlineData(TaskState.Cancelled, TaskState.ToDo, true, true)]
    [InlineData(TaskState.Cancelled, TaskState.InProgress, true, true)]
    [InlineData(TaskState.Cancelled, TaskState.NeedsYou, true, true)]
    [InlineData(TaskState.Cancelled, TaskState.Done, false, false)]
    [InlineData(TaskState.Cancelled, TaskState.Cancelled, false, false)]
    [InlineData(TaskState.Cancelled, TaskState.Duplicate, false, false)]
    [InlineData(TaskState.Cancelled, TaskState.Rejected, false, false)]
    [InlineData(TaskState.Duplicate, TaskState.Backlog, true, true)]
    [InlineData(TaskState.Duplicate, TaskState.ToDo, true, true)]
    [InlineData(TaskState.Duplicate, TaskState.InProgress, true, true)]
    [InlineData(TaskState.Duplicate, TaskState.NeedsYou, true, true)]
    [InlineData(TaskState.Duplicate, TaskState.Done, false, false)]
    [InlineData(TaskState.Duplicate, TaskState.Cancelled, false, false)]
    [InlineData(TaskState.Duplicate, TaskState.Duplicate, false, false)]
    [InlineData(TaskState.Duplicate, TaskState.Rejected, false, false)]
    [InlineData(TaskState.Rejected, TaskState.Backlog, true, true)]
    [InlineData(TaskState.Rejected, TaskState.ToDo, true, true)]
    [InlineData(TaskState.Rejected, TaskState.InProgress, true, true)]
    [InlineData(TaskState.Rejected, TaskState.NeedsYou, true, true)]
    [InlineData(TaskState.Rejected, TaskState.Done, false, false)]
    [InlineData(TaskState.Rejected, TaskState.Cancelled, false, false)]
    [InlineData(TaskState.Rejected, TaskState.Duplicate, false, false)]
    [InlineData(TaskState.Rejected, TaskState.Rejected, false, false)]
    public void StatusWakes_EveryOrderedPair_FollowsTheTable(TaskState from, TaskState to, bool assignee, bool manager)
    {
        Assert.Equal(
            (assignee, manager),
            (WakeRelevance.StatusWakes(from, to, WakeRecipient.Assignee), WakeRelevance.StatusWakes(from, to, WakeRecipient.Manager)));
    }

    /// <summary>
    /// The Request rule of C-5 (correction 16) wakes the requesting manager on a reopen (out of a finished status into a non-finished
    /// one: To Do, Backlog, In Progress, Needs You) and on a move into Done, Cancelled or Rejected, including from another finished
    /// status; the planning and active moves of an Agent working the Request (To Do, In Progress, Needs You) are silent. A move into
    /// Duplicate is not one the requesting Team acts on, from an open status or from a finished one (Done, Cancelled or Rejected to Duplicate).
    /// </summary>
    [Theory]
    [InlineData(TaskState.Backlog, TaskState.ToDo, false)]
    [InlineData(TaskState.Backlog, TaskState.InProgress, false)]
    [InlineData(TaskState.ToDo, TaskState.InProgress, false)]
    [InlineData(TaskState.InProgress, TaskState.NeedsYou, false)]
    [InlineData(TaskState.NeedsYou, TaskState.InProgress, false)]
    [InlineData(TaskState.InProgress, TaskState.ToDo, false)]
    [InlineData(TaskState.InProgress, TaskState.Backlog, false)]
    [InlineData(TaskState.InProgress, TaskState.Duplicate, false)]
    [InlineData(TaskState.Backlog, TaskState.Duplicate, false)]
    [InlineData(TaskState.Done, TaskState.Duplicate, false)]
    [InlineData(TaskState.Cancelled, TaskState.Duplicate, false)]
    [InlineData(TaskState.Rejected, TaskState.Duplicate, false)]
    [InlineData(TaskState.InProgress, TaskState.Done, true)]
    [InlineData(TaskState.NeedsYou, TaskState.Done, true)]
    [InlineData(TaskState.Backlog, TaskState.Done, true)]
    [InlineData(TaskState.InProgress, TaskState.Cancelled, true)]
    [InlineData(TaskState.Backlog, TaskState.Cancelled, true)]
    [InlineData(TaskState.InProgress, TaskState.Rejected, true)]
    [InlineData(TaskState.Backlog, TaskState.Rejected, true)]
    [InlineData(TaskState.Done, TaskState.InProgress, true)]
    [InlineData(TaskState.Done, TaskState.Backlog, true)]
    [InlineData(TaskState.Cancelled, TaskState.ToDo, true)]
    [InlineData(TaskState.Duplicate, TaskState.InProgress, true)]
    [InlineData(TaskState.Rejected, TaskState.Backlog, true)]
    [InlineData(TaskState.Done, TaskState.Cancelled, true)]
    [InlineData(TaskState.Cancelled, TaskState.Done, true)]
    [InlineData(TaskState.Duplicate, TaskState.Rejected, true)]
    public void RequestStatusWakes_StatusMove_FollowsC5(TaskState from, TaskState to, bool wakes)
    {
        Assert.Equal(wakes, WakeRelevance.RequestStatusWakes(WakeChanges.StatusMove(from, to)));
    }

    /// <summary>A change that moves no status (a rank edit, a comment, Evidence, a Create) never wakes the requesting manager, while a move into Done in the same list does.</summary>
    [Fact]
    public void RequestStatusWakes_ChangeWithoutAStatusField_IsFalse()
    {
        TaskChange rank = WakeChanges.Edit(TaskActorKind.Agent, new FieldChange(TaskField.Rank, "2", "1"));
        TaskChange evidence = WakeChanges.Bare(TaskChangeKind.Evidence);
        TaskChange comment = WakeChanges.Make(WakeChanges.Task(), WakeChanges.Task(), [], TaskActorKind.Agent, TaskChangeKind.Comment, comment: "Hello");
        TaskChange created = WakeChanges.Make(null, WakeChanges.Task(TaskState.Done), [], TaskActorKind.Agent, TaskChangeKind.Create);

        TaskChange control = WakeChanges.StatusMove(TaskState.InProgress, TaskState.Done);
        bool[] verdicts = [.. new[] { rank, evidence, comment, created, control }.Select(WakeRelevance.RequestStatusWakes)];

        bool[] expected = [false, false, false, false, true];
        Assert.Equal(expected, verdicts);
    }

    /// <summary>The Request rule reads the status field's old and new values, not the Task's own status (correction 4).</summary>
    [Fact]
    public void RequestStatusWakes_ReadsTheFieldChangeNotTheTaskStatus()
    {
        TaskChange intoDone = WakeChanges.Make(
            WakeChanges.Task(TaskState.Backlog),
            WakeChanges.Task(TaskState.Backlog),
            [new FieldChange(TaskField.Status, "In Progress", "Done")]);
        TaskChange toInProgress = WakeChanges.Make(
            WakeChanges.Task(TaskState.InProgress),
            WakeChanges.Task(TaskState.Done),
            [new FieldChange(TaskField.Status, "To Do", "In Progress")]);

        Assert.True(WakeRelevance.RequestStatusWakes(intoDone));
        Assert.False(WakeRelevance.RequestStatusWakes(toInProgress));
    }
}
