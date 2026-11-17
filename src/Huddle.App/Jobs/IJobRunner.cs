namespace Agency.Huddle.App.Jobs;

/// <summary>Runs one Job now; <see cref="JobRunner"/> posts it into its Job Room.</summary>
internal interface IJobRunner
{
    /// <summary>Runs a Job now, on time.</summary>
    /// <param name="job">The Job.</param>
    /// <param name="ranAt">When it runs.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>What the run did.</returns>
    Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, CancellationToken ct) => this.RunAsync(job, ranAt, null, ct);

    /// <summary>Runs a Job now, saying in the post that it is late when it is.</summary>
    /// <param name="job">The Job.</param>
    /// <param name="ranAt">When it runs.</param>
    /// <param name="lateDue">When it was due, in the time zone to show it in, or <see langword="null"/> for a run on time.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>What the run did.</returns>
    Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct);
}
