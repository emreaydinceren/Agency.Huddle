using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The App service that shows an agent's <see cref="ElicitationRequest"/> to the Human and waits for how
/// it ends. A <see cref="RoomSession"/> hands every request it leases to this and nothing else: the
/// lease, the idle-watchdog pause and the upper bound on waiting are already settled by the time a
/// request arrives here.
/// </summary>
internal interface IElicitationBridge
{
    /// <summary>
    /// Presents <paramref name="request"/> in <paramref name="context"/>'s Room and waits for the Human.
    /// <paramref name="cancellationToken"/> is cancelled when the request can no longer be answered (a
    /// Stop, the Turn ending, a shutdown, the bound running out); the implementation stops waiting and
    /// drops anything it is showing for the request.
    /// </summary>
    /// <param name="context">The Room and Agent the request was made for.</param>
    /// <param name="request">The form the agent asked for.</param>
    /// <param name="cancellationToken">Cancelled when the request can no longer be answered.</param>
    /// <returns>How the request ended.</returns>
    Task<ElicitationResult> RequestAsync(ElicitationContext context, ElicitationRequest request, CancellationToken cancellationToken);
}
