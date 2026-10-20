namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>What the Human decides on a Task waiting for them (Human gate Spec §6.6).</summary>
public enum TaskDecision
{
    /// <summary>Accept the Task: record the acceptance, and finish the Task when it has no Action.</summary>
    Accept,

    /// <summary>Request changes: say what needs to change and send the Task back to In Progress.</summary>
    RequestChanges,

    /// <summary>Decline the Task: say why, reject it and close it.</summary>
    Decline,
}
