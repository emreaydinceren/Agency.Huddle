namespace Agency.Huddle.App.Feed;

/// <summary>
/// What kind of thing a Feed item reports. The <c>source_type</c> column holds the wire name from
/// <see cref="FeedSourceTypes.ToWire"/>, never the number, so reordering this enum cannot rewrite history.
/// </summary>
public enum FeedSourceType
{
    /// <summary>A Task was created.</summary>
    TaskCreated,

    /// <summary>A process gate was switched on for a Task.</summary>
    TaskGateOn,

    /// <summary>A Task needs the Human.</summary>
    TaskNeedsYou,

    /// <summary>A Task was assigned to the Human.</summary>
    TaskAssignedToYou,

    /// <summary>A Request awaits the Human.</summary>
    RequestAwaitsYou,

    /// <summary>A gate refused an Agent's move and the Human must decide.</summary>
    GateDecisionDue,

    /// <summary>The Human was mentioned in a Room.</summary>
    RoomMention,

    /// <summary>A Room holds a question for the Human.</summary>
    RoomQuestion,

    /// <summary>A Room holds a form for the Human.</summary>
    RoomForm,

    /// <summary>A Room ran out of budget.</summary>
    RoomBudget,

    /// <summary>A scheduled Job for the Human ran in its Room.</summary>
    RoomJob,

    /// <summary>A Room was created.</summary>
    RoomCreated,

    /// <summary>Someone joined a Room.</summary>
    RoomJoined,

    /// <summary>A Room was archived.</summary>
    RoomArchived,

    /// <summary>A Library document changed.</summary>
    DocumentChanged,

    /// <summary>A Helper stopped judging because its daily cap was reached.</summary>
    HelperPaused,

    /// <summary>A Helper failed several times in a row.</summary>
    HelperFailing,

    /// <summary>The Team Memory Helper did not file a member's proposal, or dropped it: a record, not a Notification.</summary>
    TeamMemoryNotFiled,

    /// <summary>The AGENTS.md Changes Helper judged a change not worth telling Agents about, so none were told: a record, not a Notification.</summary>
    AgentsMdChangeSkipped,

    /// <summary>A device asked to pair and waits for approval.</summary>
    PairingRequested,

    /// <summary>A device asked for admin access.</summary>
    AdminRequested,

    /// <summary>A device was paired from somewhere other than this machine.</summary>
    DevicePaired,

    /// <summary>A device was revoked, removed or had its token rotated.</summary>
    DeviceRevoked,

    /// <summary>A Memory or outputs folder holds more files than Agents are shown.</summary>
    SecondBrainOverCap,

    /// <summary>A Task reached its limit of wake-ups made by Agents, so a hand-off waits until the Human allows more.</summary>
    TaskWakesPaused,

    /// <summary>A Team was archived.</summary>
    TeamArchived,

    /// <summary>A new Chief of Staff text ships, but the Human edited theirs, so it was not replaced.</summary>
    BuiltinAgentUpdateAvailable,

    /// <summary>The Human switched account connectors on or off for an Agent.</summary>
    AgentConnectorsChanged,
}
