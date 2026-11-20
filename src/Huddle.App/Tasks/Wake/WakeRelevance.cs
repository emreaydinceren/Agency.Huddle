namespace Agency.Huddle.App.Tasks.Wake;

// Every switch below is a deliberate closed table over an enum, with no discard arm, so a new TaskField, TaskState, TaskPhase or
// TaskChangeKind fails the build (CS8509) until it is classified. CS8524 only asks for the unnamed values of the same enums, which
// no producer can reach; suppressing it is what keeps CS8509 live (the same choice as SessionUpdateMapper's switch in RoomSession).
#pragma warning disable CS8524 // Closed tables over enums: only the unnamed values go unreached, and suppressing it keeps CS8509 live (see above).

/// <summary>
/// Smart wakes spec 3 as a pure rule: from the fields and the kind of a <see cref="TaskChange"/> alone, whether it wakes the assignee and
/// whether it wakes the manager. No I/O, no clock, no options. <see cref="TaskTriggerService"/> reads it on receipt of a change and in <c>Preview</c>.
/// </summary>
internal static class WakeRelevance
{
    /// <summary>The phase of <paramref name="state"/> (spec 3.4).</summary>
    /// <param name="state">The status.</param>
    public static TaskPhase PhaseOf(TaskState state) => state switch
    {
        TaskState.Backlog or TaskState.ToDo => TaskPhase.Planning,
        TaskState.InProgress or TaskState.NeedsYou => TaskPhase.Active,
        TaskState.Done => TaskPhase.Done,
        TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected => TaskPhase.WontDo,
    };

    /// <summary>Whether moving from <paramref name="from"/> to <paramref name="to"/> wakes <paramref name="recipient"/> (the spec 3.4 table). A move to the same status is no move and is silent.</summary>
    /// <param name="from">The old status.</param>
    /// <param name="to">The new status.</param>
    /// <param name="recipient">Who is being judged.</param>
    public static bool StatusWakes(TaskState from, TaskState to, WakeRecipient recipient)
    {
        if (from == to)
        {
            return false;
        }

        return (PhaseOf(from), PhaseOf(to)) switch
        {
            (TaskPhase.Planning, TaskPhase.Planning) => recipient == WakeRecipient.Assignee,
            (TaskPhase.Planning, TaskPhase.Active) => recipient == WakeRecipient.Assignee,
            (TaskPhase.Planning, TaskPhase.Done) => false,
            (TaskPhase.Planning, TaskPhase.WontDo) => false,
            (TaskPhase.Active, TaskPhase.Planning) => true,
            (TaskPhase.Active, TaskPhase.Active) => recipient == WakeRecipient.Assignee && from == TaskState.NeedsYou,
            (TaskPhase.Active, TaskPhase.Done) => true,
            (TaskPhase.Active, TaskPhase.WontDo) => true,
            (TaskPhase.Done, TaskPhase.Planning) => true,
            (TaskPhase.Done, TaskPhase.Active) => true,
            (TaskPhase.Done, TaskPhase.Done) => false,
            (TaskPhase.Done, TaskPhase.WontDo) => false,
            (TaskPhase.WontDo, TaskPhase.Planning) => true,
            (TaskPhase.WontDo, TaskPhase.Active) => true,
            (TaskPhase.WontDo, TaskPhase.Done) => false,
            (TaskPhase.WontDo, TaskPhase.WontDo) => false,
        };
    }

    /// <summary>Whether <paramref name="changes"/>, made by an actor of <paramref name="kind"/>, hold a change that wakes <paramref name="recipient"/> (spec 3.1, 3.4).</summary>
    /// <param name="changes">The field changes.</param>
    /// <param name="kind">The kind of actor who made them.</param>
    /// <param name="recipient">Who is being judged.</param>
    public static bool Wakes(IReadOnlyList<FieldChange> changes, TaskActorKind kind, WakeRecipient recipient)
    {
        foreach (FieldChange change in changes)
        {
            if (FieldWakes(change, kind, recipient))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="change"/> wakes <paramref name="recipient"/> (spec 3.1, 3.2, 3.4). The order of the checks is the rulings of
    /// corrections 3 and 15: a decision, then a Create (no Before), then a flipped closed flag, then the kind, then the fields.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="recipient">Who is being judged.</param>
    public static bool Wakes(TaskChange change, WakeRecipient recipient)
    {
        if (change.Decision is not null || change.Before is null)
        {
            return true;
        }

        if (change.Before.Location.Closed != change.After.Location.Closed)
        {
            return IsActive(change.After.Status)
                || (change.Kind == TaskChangeKind.Edit && Wakes(change.Changes, change.Actor.Kind, recipient));
        }

        return change.Kind switch
        {
            TaskChangeKind.Create => true,
            TaskChangeKind.Evidence => true,
            TaskChangeKind.Release => true,
            TaskChangeKind.Comment => false,
            TaskChangeKind.HuddleStep => false,
            TaskChangeKind.Close => IsActive(change.After.Status),
            TaskChangeKind.Reopen => IsActive(change.After.Status),
            TaskChangeKind.Edit => Wakes(change.Changes, change.Actor.Kind, recipient),
        };
    }

    /// <summary>Whether <paramref name="change"/> is a status move of a Request that wakes its requesting manager (C-5, correction 16).</summary>
    /// <param name="change">The change.</param>
    public static bool RequestStatusWakes(TaskChange change)
    {
        foreach (FieldChange field in change.Changes)
        {
            if (field.Field == TaskField.Status && RequestMoveWakes(field))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="state"/> is In Progress or Needs You, the statuses where closing or reopening a Task stops or restarts work in flight.</summary>
    private static bool IsActive(TaskState state) => PhaseOf(state) == TaskPhase.Active;

    /// <summary>Whether one field change wakes <paramref name="recipient"/> (the spec 3.1 table). The switch has no default arm, so a new <see cref="TaskField"/> fails the build until it is classified.</summary>
    private static bool FieldWakes(FieldChange change, TaskActorKind kind, WakeRecipient recipient) => change.Field switch
    {
        TaskField.Status => StatusFieldWakes(change, recipient),
        TaskField.Title => true,
        TaskField.Assignee => true,
        TaskField.BlockedBy => true,
        TaskField.Action => true,
        TaskField.Description => true,
        TaskField.Unknown => true,
        TaskField.Location => kind != TaskActorKind.OutsideHuddle,
        TaskField.Priority => false,
        TaskField.Origin => false,
        TaskField.Parent => false,
        TaskField.DuplicateOf => false,
        TaskField.Tags => false,
        TaskField.StartDate => false,
        TaskField.DueDate => false,
        TaskField.Sprint => false,
        TaskField.Rank => false,
        TaskField.HumanGate => false,
    };

    /// <summary>A status change judged from its wire strings (correction 4); a value that does not parse wakes, since Huddle cannot judge a move it cannot read.</summary>
    private static bool StatusFieldWakes(FieldChange change, WakeRecipient recipient) =>
        !TryReadMove(change, out TaskState from, out TaskState to) || StatusWakes(from, to, recipient);

    /// <summary>The Request rule of C-5: a move into Done, Cancelled or Rejected, or a reopen out of a finished status. A value that does not parse wakes.</summary>
    private static bool RequestMoveWakes(FieldChange change)
    {
        if (!TryReadMove(change, out TaskState from, out TaskState to))
        {
            return true;
        }

        if (from == to)
        {
            return false;
        }

        return to is TaskState.Done or TaskState.Cancelled or TaskState.Rejected || (from.IsTerminal() && !to.IsTerminal());
    }

    /// <summary>Parses the old and new wire strings of a status change.</summary>
    private static bool TryReadMove(FieldChange change, out TaskState from, out TaskState to)
    {
        to = default;
        return TaskStates.TryParse(change.Old, out from) && TaskStates.TryParse(change.New, out to);
    }
}
#pragma warning restore CS8524
