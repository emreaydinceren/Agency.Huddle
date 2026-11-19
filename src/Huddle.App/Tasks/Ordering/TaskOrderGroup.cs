namespace Agency.Huddle.App.Tasks.Ordering;

/// <summary>The part of the Task order an entry is listed in.</summary>
internal enum TaskOrderGroup
{
    /// <summary>In Progress or Needs You: already started, never startable.</summary>
    Started,

    /// <summary>To Do, plus any Backlog Task that blocks one of them.</summary>
    Next,

    /// <summary>The remaining Backlog Tasks.</summary>
    Backlog,
}
