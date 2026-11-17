using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Jobs;

/// <summary>Where a Job's run is posted, and what stops it.</summary>
internal interface IJobDestination
{
    /// <summary>Whether this destination serves <paramref name="job"/>.</summary>
    /// <param name="job">The Job.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    bool Serves(Job job);

    /// <summary>Finds or makes the Room, or says why there is none.</summary>
    /// <param name="job">The Job.</param>
    /// <param name="agent">The Agent it is for, or <see langword="null"/> for the Human.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The Room to post into, or the outcome that stops the run.</returns>
    Task<JobTarget> ResolveAsync(Job job, User? agent, CancellationToken ct);

    /// <summary>The text of the post.</summary>
    /// <param name="job">The Job.</param>
    /// <param name="agent">The Agent it is for, or <see langword="null"/> for the Human.</param>
    /// <param name="lateDue">When it was due, in the zone to show, or <see langword="null"/> for a run on time.</param>
    /// <returns>The text.</returns>
    string TextOf(Job job, User? agent, DateTimeOffset? lateDue);
}
