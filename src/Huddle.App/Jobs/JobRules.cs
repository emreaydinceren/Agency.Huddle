namespace Agency.Huddle.App.Jobs;

/// <summary>The rules a Job obeys wherever it is written: <c>schedule.md</c> and the <c>jobs</c> tool both ask here.</summary>
internal static class JobRules
{
    /// <summary>The refusal for a Follow-up given a recurring timetable.</summary>
    public const string FollowUpRunsOnce = "A Follow-up runs once; delete it and create a Job instead";

    /// <summary>Whether a Job is allowed as it stands.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The refusal, or <see langword="null"/> when the Job is allowed.</returns>
    public static string? Check(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return job.RoomId is not null && job.When.Cadence != JobCadence.Once ? FollowUpRunsOnce : null;
    }
}
