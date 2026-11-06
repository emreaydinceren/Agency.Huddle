namespace Agency.Huddle.App.Tasks;

/// <summary>Which Spec §10.2 guard, if any, stops a Task change from waking its assignee.</summary>
public enum WakeBlock
{
    /// <summary>No guard applies: the change would wake the assignee.</summary>
    None,

    /// <summary>The Task has no assignee, or its assignee isn't a known Persona (guards 2 and 5).</summary>
    NoAssignee,

    /// <summary>The assignee is the Human (guard 3).</summary>
    AssigneeIsHuman,

    /// <summary>The assignee made the change themselves (guard 4, the self-edit guard).</summary>
    AssigneeIsActor,

    /// <summary>The change was made by an Agent, and the Task's wake budget (Spec §10.6) is spent (guard 6).</summary>
    BudgetPaused,

    /// <summary>Tasks, or wake-ups specifically, are turned off (guard 1).</summary>
    Disabled,

    /// <summary>The post would go into a Room whose Budget is spent; the Task's own wake budget is untouched.</summary>
    RoomBudgetSpent,
}
