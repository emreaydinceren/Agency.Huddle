namespace Agency.Huddle.App.Scheduling;

/// <summary>What to do now. Every decision but NotDue carries the whole state to save, so no caller assembles it.</summary>
internal abstract record DueDecision
{
    /// <summary>Nothing is due: do nothing and save nothing.</summary>
    internal sealed record NotDue : DueDecision;

    /// <summary>The timetable is new or was edited: save its baseline and do not run.</summary>
    /// <param name="Save">The state to store.</param>
    internal sealed record FirstSight(DueState Save) : DueDecision;

    /// <summary>Run now, after storing the state.</summary>
    /// <param name="Save">The state to store before the run.</param>
    /// <param name="Late">Whether the run is later than the grace time.</param>
    internal sealed record Run(DueState Save, bool Late) : DueDecision;

    /// <summary>This due time is over without a run: store the state.</summary>
    /// <param name="Save">The state to store.</param>
    internal sealed record Skip(DueState Save) : DueDecision;

    /// <summary>Stop trying this due time; store the state, and remove a one-off.</summary>
    /// <param name="Save">The state to store.</param>
    /// <param name="Remove">Whether the timetable itself is finished and should be removed.</param>
    internal sealed record GiveUp(DueState Save, bool Remove) : DueDecision;
}
