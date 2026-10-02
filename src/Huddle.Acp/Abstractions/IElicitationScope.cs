namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// Whatever answers a session's <see cref="ElicitationRequest"/>s: the one place the Human's form is
/// shown. Bound to a session through <see cref="IAgentSession.BindElicitationScope"/>.
/// </summary>
public interface IElicitationScope
{
    /// <summary>
    /// Presents <paramref name="request"/> and waits for how it ends. May wait as long as the Human
    /// takes, so the token matters: it is cancelled when the Turn is stopped or ends, the session is
    /// disposed, or the agent connection is lost, and cancellation is answered as
    /// <see cref="ElicitationCancelled"/>.
    /// </summary>
    /// <param name="request">The form to present.</param>
    /// <param name="cancellationToken">Cancelled when the request can no longer be answered.</param>
    /// <returns>How the request ended.</returns>
    Task<ElicitationResult> ElicitAsync(ElicitationRequest request, CancellationToken cancellationToken);
}
