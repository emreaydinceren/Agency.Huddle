namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>Who a wake rule is judging for. Only Status moves (Smart wakes spec 3.4) tell the two apart.</summary>
internal enum WakeRecipient
{
    /// <summary>The Task's assignee.</summary>
    Assignee,

    /// <summary>The manager of the Team that owns the Task.</summary>
    Manager,
}
