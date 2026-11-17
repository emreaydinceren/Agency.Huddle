namespace Agency.Huddle.App.Jobs;

/// <summary>Configuration for scheduled Jobs, under <c>Team:Jobs</c>.</summary>
public sealed class JobsOptions
{
    /// <summary>Whether Jobs are enabled. When false, the Scheduled view is hidden, the <c>jobs</c> tool is not offered and nothing fires.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether a run missed by more than two minutes (Huddle was stopped or asleep) runs once, late. When false it is skipped, as before.
    /// Rollback switch. With <c>Team:TurnQueue:Enabled</c> false it behaves as false for an Agent's Job.
    /// </summary>
    public bool RunOverdue { get; set; } = true;

    /// <summary>How late a one-off may still run; a later one is removed with a Feed item instead of posting. Default one day.</summary>
    public TimeSpan OverdueOneOffWithin { get; set; } = TimeSpan.FromDays(1);
}
