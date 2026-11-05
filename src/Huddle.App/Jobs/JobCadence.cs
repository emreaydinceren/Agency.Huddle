namespace Agency.Huddle.App.Jobs;

/// <summary>The six shapes of a Job's timetable, one per preset the Scheduled view and <c>schedule.md</c> offer.</summary>
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

    /// <summary>On one day of every month at one time; a day past the month's end runs on its last day.</summary>
    Monthly,

    /// <summary>Every so many hours.</summary>
    EveryHours,
}
