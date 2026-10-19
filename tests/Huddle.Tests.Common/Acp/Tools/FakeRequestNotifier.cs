using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>An <see cref="IRequestNotifier"/> that records every call so a test can read what a Request tool asked to wake.</summary>
internal sealed class FakeRequestNotifier : IRequestNotifier
{
    /// <summary>One recorded call.</summary>
    /// <param name="TaskId">The Request's id.</param>
    /// <param name="Event">The lifecycle event.</param>
    /// <param name="Targets">Who the event wakes.</param>
    /// <param name="Actor">The Persona who caused the event.</param>
    internal sealed record Call(string TaskId, RequestEvent Event, IReadOnlyList<WakeTarget> Targets, string Actor);

    /// <summary>The calls so far, in order.</summary>
    public List<Call> Calls { get; } = [];

    /// <inheritdoc />
    public Task NotifyAsync(TaskItem request, RequestEvent requestEvent, IReadOnlyList<WakeTarget> targets, string actor, CancellationToken cancellationToken)
    {
        this.Calls.Add(new Call(request.Id.ToString(), requestEvent, targets, actor));
        return Task.CompletedTask;
    }
}
