namespace Agency.Huddle.App.Jobs;

/// <summary>The five shapes of a Job's timetable, one per preset the Scheduled view and <c>schedule.md</c> offer.</summary>
internal enum JobCadence
{
    /// <summary>Once, on one date at one time.</summary>
    Once,

    /// <summary>Every day at one time.</summary>
    Daily,

    /// <summary>Monday to Friday at one time.</summary>
    Weekdays,

    /// <summary>On the chosen days of the week at one time.</summary>
    Weekly,

    /// <summary>Every so many hours.</summary>
    EveryHours,
}
