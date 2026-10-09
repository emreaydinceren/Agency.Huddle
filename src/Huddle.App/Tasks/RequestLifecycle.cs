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

    /// <summary>The receiving Team worked the Request (To Do to Review).</summary>
    Work,

    /// <summary>The receiving QA attached evidence in Review.</summary>
    EvidenceAttached,

    /// <summary>The requesting Team's QA confirmed the Request is done.</summary>
    Confirm,
}

/// <summary>Who a Request transition wakes.</summary>
internal enum WakeTarget
{
    /// <summary>The manager of the Team that filed the Request.</summary>
    RequestingManager,

    /// <summary>The manager of the Team that received the Request.</summary>
    ReceivingManager,

    /// <summary>The Chief of Staff built-in Teammate.</summary>
    ChiefOfStaff,
}

/// <summary>Where a Request stands: its Task status wire name and its triage state.</summary>
/// <param name="Status">The Task status wire name; empty before the Request is filed.</param>
/// <param name="Triage">The triage state.</param>
internal sealed record RequestState(string Status, TriageState Triage);

/// <summary>The outcome of applying an event to a Request.</summary>
/// <param name="Allowed">Whether the event is legal in the current state.</param>
/// <param name="Error">Why it is not, or null when allowed.</param>
/// <param name="Next">The state after the event, or null when refused.</param>
/// <param name="Wake">Who to wake, in order; empty when refused.</param>
/// <param name="Closes">Whether the Request is closed by this event.</param>
internal sealed record RequestTransition(bool Allowed, string? Error, RequestState? Next, IReadOnlyList<WakeTarget> Wake, bool Closes);

/// <summary>The pure state machine of the Request lifecycle table (RQ-3 to RQ-7, Spec §Lifecycle). It touches no store and sends no wake.</summary>
internal static class RequestLifecycle
{
    /// <summary>Status wire name: Backlog.</summary>
    private const string Backlog = "Backlog";

    /// <summary>Status wire name: To Do.</summary>
    private const string ToDo = "To Do";

    /// <summary>Status wire name: In Progress.</summary>
    private const string InProgress = "In Progress";

    /// <summary>Status wire name: Review.</summary>
    private const string Review = "Review";

    /// <summary>Status wire name: Done.</summary>
    private const string Done = "Done";

    /// <summary>Status wire name: Rejected.</summary>
    private const string Rejected = "Rejected";

    /// <summary>Applies an event to a Request state.</summary>
    /// <param name="current">The current status and triage state; an empty status means the Request does not exist yet.</param>
    /// <param name="ev">The event.</param>
    /// <param name="requesterQaConfirms">Whether the actor is the requesting Team's QA; only <see cref="RequestEvent.Confirm"/> reads it.</param>
    /// <returns>The transition, allowed or refused with an error naming the status and the event.</returns>
    public static RequestTransition Apply(RequestState current, RequestEvent ev, bool requesterQaConfirms = false)
    {
        ArgumentNullException.ThrowIfNull(current);

        string status = current.Status;
        bool untriagedBacklog = string.Equals(status, Backlog, StringComparison.Ordinal) && current.Triage == TriageState.None;
        bool workable = string.Equals(status, ToDo, StringComparison.Ordinal)
            || string.Equals(status, InProgress, StringComparison.Ordinal)
            || string.Equals(status, Review, StringComparison.Ordinal);

        switch (ev)
        {
            case RequestEvent.Filed when status.Length == 0:
                return Allow(Backlog, TriageState.None, WakeTarget.ReceivingManager);
            case RequestEvent.MarkNeedsInfo when untriagedBacklog:
                return Allow(Backlog, TriageState.NeedsInfo, WakeTarget.RequestingManager);
            case RequestEvent.InfoAdded when string.Equals(status, Backlog, StringComparison.Ordinal) && current.Triage == TriageState.NeedsInfo:
                return Allow(Backlog, TriageState.None, WakeTarget.ReceivingManager);
            case RequestEvent.Accept when untriagedBacklog:
                return Allow(ToDo, TriageState.Accepted, WakeTarget.RequestingManager);
            case RequestEvent.Decline when untriagedBacklog:
                return Allow(Rejected, TriageState.Declined, WakeTarget.RequestingManager, WakeTarget.ChiefOfStaff);
            case RequestEvent.Work when workable:
                return Allow(status, current.Triage, WakeTarget.RequestingManager);
            case RequestEvent.EvidenceAttached when string.Equals(status, Review, StringComparison.Ordinal):
                return Allow(Done, current.Triage, WakeTarget.RequestingManager);
            case RequestEvent.Confirm when string.Equals(status, Done, StringComparison.Ordinal):
                return requesterQaConfirms
                    ? new RequestTransition(true, null, current, [WakeTarget.ReceivingManager], true)
                    : Refuse("Only the requesting Team's QA can confirm a Request.");
            default:
                return Refuse($"A Request in {status} (triage {current.Triage}) cannot take the {ev} event.");
        }
    }

    /// <summary>Builds an allowed transition that closes nothing.</summary>
    /// <param name="status">The next status.</param>
    /// <param name="triage">The next triage state.</param>
    /// <param name="wake">Who to wake.</param>
    /// <returns>The transition.</returns>
    private static RequestTransition Allow(string status, TriageState triage, params WakeTarget[] wake) =>
        new(true, null, new RequestState(status, triage), wake, false);

    /// <summary>Builds a refused transition.</summary>
    /// <param name="error">The reason.</param>
    /// <returns>The transition.</returns>
    private static RequestTransition Refuse(string error) => new(false, error, null, [], false);
}
