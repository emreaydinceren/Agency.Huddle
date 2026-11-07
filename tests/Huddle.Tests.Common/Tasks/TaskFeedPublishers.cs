using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Builds the <see cref="TaskFeedPublisher"/> that the real host builds, for a test that gives a <see cref="TaskService"/> a fake Feed.</summary>
internal static class TaskFeedPublishers
{
    /// <summary>A publisher subscribed to <paramref name="events"/>, naming the Human as the options do. The caller disposes it.</summary>
    /// <param name="events">The hub the Task service raises its events on.</param>
    /// <param name="feed">Receives the items.</param>
    /// <param name="options">Supplies the Human's name.</param>
    /// <param name="proof">Supplies a Task's Proof, or <see langword="null"/> for an empty, complete Proof.</param>
    /// <returns>The subscribed publisher.</returns>
    public static TaskFeedPublisher Over(TaskEvents events, IFeedPublisher feed, IOptions<TeamOptions> options, IProofSource? proof = null) =>
        new(events, feed, new ConfiguredHumanIdentity(options.Value.HumanName), NullLogger<TaskFeedPublisher>.Instance, proofs: proof);
}
