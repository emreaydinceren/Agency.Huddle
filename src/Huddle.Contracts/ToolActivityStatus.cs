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

    /// <summary>
    /// Not a tool call: a live notice from the Adapter (such as falling back from a mode the model lacks),
    /// shown among the Turn's rows so it is never mistaken for the Agent's own words. Never logged as a
    /// tool call.
    /// </summary>
    Notice,
}
