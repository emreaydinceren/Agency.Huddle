namespace Agency.Huddle.App.Tasks;

/// <summary>An event that moves a Request through its lifecycle (Spec §Lifecycle table).</summary>
internal enum RequestEvent
{
    /// <summary>The requesting manager filed the Request.</summary>
    Filed,

    /// <summary>The receiving manager asked the requester for more information.</summary>
    MarkNeedsInfo,

    /// <summary>The requesting manager added the missing information.</summary>
    InfoAdded,

    /// <summary>The receiving manager accepted the Request.</summary>
    Accept,

    /// <summary>The receiving manager declined the Request with a reason.</summary>
    Decline,

    /// <summary>The receiving Team worked the Request (To Do or In Progress; the status is unchanged).</summary>
    Work,

    /// <summary>The requesting Team's QA confirmed the Request is done.</summary>
    Confirm,
}
