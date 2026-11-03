namespace Agency.Huddle.App.Jobs;

/// <summary>Configuration for scheduled Jobs, under <c>Team:Jobs</c>.</summary>
public sealed class JobsOptions
{
    /// <summary>Whether Jobs are enabled. When false, the Scheduled view is hidden, the <c>jobs</c> tool is not offered and nothing fires.</summary>
    public bool Enabled { get; set; } = true;
}
