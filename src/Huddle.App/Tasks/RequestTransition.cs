namespace Agency.Huddle.App.Tasks;

/// <summary>The outcome of applying an event to a Request.</summary>
/// <param name="Allowed">Whether the event is legal in the current state.</param>
/// <param name="Error">Why it is not, or null when allowed.</param>
/// <param name="Next">The state after the event, or null when refused.</param>
/// <param name="Wake">Who to wake, in order; empty when refused.</param>
/// <param name="Closes">Whether the Request is closed by this event.</param>
internal sealed record RequestTransition(bool Allowed, string? Error, RequestState? Next, IReadOnlyList<WakeTarget> Wake, bool Closes);
