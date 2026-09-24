namespace Agency.Huddle.App.Tasks;

/// <summary>A single field change in a Task.</summary>
public sealed record FieldChange(TaskField Field, string? Old, string? New);

/// <summary>The fields that can change in a Task.</summary>
public enum TaskField
{
    /// <summary>The Task's title.</summary>
    Title,

    /// <summary>The Task's state.</summary>
    Status,

    /// <summary>The Task's priority level.</summary>
    Priority,

    /// <summary>Who the Task is assigned to.</summary>
    Assignee,

    /// <summary>The Room in which the Task was created.</summary>
    Origin,

    /// <summary>The parent Task.</summary>
    Parent,

    /// <summary>Tasks that block this Task.</summary>
    BlockedBy,

    /// <summary>The Task this Task is a duplicate of.</summary>
    DuplicateOf,

    /// <summary>The Task's labels.</summary>
    Tags,

    /// <summary>The start date for the Task.</summary>
    StartDate,

    /// <summary>The due date for the Task.</summary>
    DueDate,

    /// <summary>The Task's description.</summary>
    Description,

    /// <summary>Where the Task lives in the folder structure.</summary>
    Location,

    /// <summary>A field that the parser did not recognize.</summary>
    Unknown,
}
