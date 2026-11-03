namespace Agency.Huddle.App.Jobs;

/// <summary>Runs one Job now; <see cref="JobRunner"/> posts it into its Job Room.</summary>
internal interface IJobRunner
{
    /// <summary>Runs a Job now.</summary>
    /// <param name="job">The Job.</param>
    /// <param name="ranAt">When it runs.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>What the run did.</returns>
    Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, CancellationToken ct);
}
