namespace Agency.Huddle.App.Feed;

/// <summary>
/// What kind of thing a Feed item reports. The <c>source_type</c> column holds the wire name from
/// <see cref="FeedSourceTypes.ToWire"/>, never the number, so reordering this enum cannot rewrite history.
/// </summary>
internal enum FeedSourceType
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

    /// <summary>A Room was created.</summary>
    RoomCreated,

    /// <summary>Someone joined a Room.</summary>
    RoomJoined,

    /// <summary>A Room was archived.</summary>
    RoomArchived,

    /// <summary>A Library document changed.</summary>
    DocumentChanged,
}
