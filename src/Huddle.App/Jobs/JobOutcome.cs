namespace Agency.Huddle.App.Jobs;

/// <summary>What a Job's last run did, shown beside it in the Scheduled view.</summary>
internal enum JobOutcome
{
    /// <summary>The instruction was posted into the Job Room.</summary>
    Delivered,

    /// <summary>The Job Room's Budget was spent, so nothing was posted: the Agent could not have replied.</summary>
    BudgetSpent,

    /// <summary>The Agent the Job is for has never come online, so it has no Room to be woken in.</summary>
    NoAgent,

    /// <summary>The post failed; the run is tried again on the next minute.</summary>
    Failed,
}
