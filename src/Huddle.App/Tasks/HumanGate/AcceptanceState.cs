namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>Whether the Human has accepted a Task as it reads now (Human gate Spec §6.5).</summary>
public enum AcceptanceState
{
    /// <summary>No acceptance counts: the Task is not in Needs You or the Human has not accepted since it entered.</summary>
    None,

    /// <summary>The latest acceptance carries the hash of the Task's current title, Action and description.</summary>
    Valid,

    /// <summary>The Task was accepted, then its title, Action or description changed.</summary>
    Stale,
}
