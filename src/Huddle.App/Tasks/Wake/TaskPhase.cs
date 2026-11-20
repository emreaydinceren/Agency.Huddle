namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>The phase of a status (Smart wakes spec 3.4): the eight statuses fall into four.</summary>
internal enum TaskPhase
{
    /// <summary>Backlog and To Do: work nobody has started.</summary>
    Planning,

    /// <summary>In Progress and Needs You: work in flight.</summary>
    Active,

    /// <summary>Done.</summary>
    Done,

    /// <summary>Cancelled, Duplicate and Rejected: work nobody will do.</summary>
    WontDo,
}
