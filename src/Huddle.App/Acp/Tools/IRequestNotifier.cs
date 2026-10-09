using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>Wakes the Personas a Request lifecycle event names (RQ-4, RQ-7). The Request tools call it after they write; <see cref="RequestNotifier"/> is the real wake.</summary>
internal interface IRequestNotifier
{
    /// <summary>Wakes the Personas that <paramref name="targets"/> resolve to for <paramref name="request"/>.</summary>
    /// <param name="request">The Request Task the event happened to.</param>
    /// <param name="requestEvent">The lifecycle event.</param>
    /// <param name="targets">Who the lifecycle table says to wake, in order.</param>
    /// <param name="actor">The name of the Persona who caused the event.</param>
    /// <param name="cancellationToken">Cancels the wake.</param>
    /// <returns>A task that completes when the wakes have been delivered.</returns>
    Task NotifyAsync(TaskItem request, RequestEvent requestEvent, IReadOnlyList<WakeTarget> targets, string actor, CancellationToken cancellationToken);
}
