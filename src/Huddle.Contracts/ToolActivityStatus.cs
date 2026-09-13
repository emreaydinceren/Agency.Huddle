namespace Agency.Huddle.Contracts;

/// <summary>The lifecycle state of one tool call, as shown to the Human in a Room.</summary>
public enum ToolActivityStatus
{
    /// <summary>The Agent has announced the call but not yet started it.</summary>
    Pending,

    /// <summary>The call is running.</summary>
    InProgress,

    /// <summary>The call finished without error.</summary>
    Completed,

    /// <summary>The call finished with an error.</summary>
    Failed,
}
