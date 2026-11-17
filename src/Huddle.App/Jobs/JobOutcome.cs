namespace Agency.Huddle.App.Jobs;

/// <summary>What a Job's last run did, shown beside it in the Scheduled view.</summary>
internal enum JobOutcome
{
    /// <summary>The instruction was posted into the Job Room.</summary>
    Delivered,

    /// <summary>The Job Room's Budget was spent, so nothing was posted: the Agent could not have replied.</summary>
    BudgetSpent,

    /// <summary>The Agent the Job is for is no longer available (its Persona was removed or it never came online), so nothing was posted.</summary>
    NoAgent,

    /// <summary>The post failed; the run is tried again on the next minute, up to three times.</summary>
    Failed,

    /// <summary>The instruction was posted, but later than two minutes after it was due, with "ran late" in the post.</summary>
    DeliveredLate,

    /// <summary>A one-off could not post now and is kept to try again; a recurring Job reports <see cref="BudgetSpent"/> instead.</summary>
    Held,

    /// <summary>The Room the Job posts into is gone, so nothing was posted and a one-off is removed.</summary>
    NoRoom,
}
