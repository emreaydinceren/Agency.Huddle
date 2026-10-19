namespace Agency.Huddle.App.Tasks;

/// <summary>The Tasks UI's presence badge for an assignee, per Spec §10.8.</summary>
public enum PresenceState
{
    /// <summary>The assignee has a Turn running right now.</summary>
    Awake,

    /// <summary>The assignee is online but has no Turn running.</summary>
    Asleep,

    /// <summary>The assignee is not online.</summary>
    Offline,
}
