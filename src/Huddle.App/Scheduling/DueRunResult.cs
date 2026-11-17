namespace Agency.Huddle.App.Scheduling;

/// <summary>How a run ended, as the timetable sees it.</summary>
internal enum DueRunResult
{
    /// <summary>The run happened; that due time is over.</summary>
    Ran,

    /// <summary>The run was held back; the due time is kept and the try does not count as a failure.</summary>
    Held,

    /// <summary>The run failed; the due time is kept and the failure is counted.</summary>
    Failed,
}
