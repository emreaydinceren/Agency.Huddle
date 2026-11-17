namespace Agency.Huddle.App.Jobs;

/// <summary>Leaves a trace when a one-off Job is removed without posting (Turn Queue spec §7.5), so it never vanishes without a word.</summary>
internal interface IJobGaveUpNotice
{
    /// <summary>Writes the <c>job-gave-up</c> Feed item and, for an Agent's Job, one Huddle line.</summary>
    /// <param name="job">The Job that was removed.</param>
    /// <param name="reason">Why it did not run, as the tail of the sentence: "its Room is gone".</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns>A task that ends when the trace is written.</returns>
    Task NoteAsync(Job job, string reason, CancellationToken ct);
}
