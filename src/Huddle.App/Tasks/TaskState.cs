namespace Agency.Huddle.App.Tasks;

/// <summary>The lifecycle state of a Task.</summary>
public enum TaskState
{
    Backlog,
    ToDo,
    InProgress,
    Review,
    NeedsYou,
    Done,
    Cancelled,
    Duplicate,
    Rejected,
}
