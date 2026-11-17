namespace Agency.Huddle.App.Scheduling;

/// <summary>What a scheduler has stored about one timetable.</summary>
/// <param name="WhenKey">What the next run was worked out from; a change means the timetable was edited.</param>
/// <param name="NextRunUtc">The stored next run, or null when none is left.</param>
/// <param name="FailedTries">How many runs of this due time have failed. Defaults to 0 for a caller whose table has no such column.</param>
/// <param name="EverRan">Whether it has ever run, for a one-off first seen after its time. Defaults to false likewise.</param>
internal sealed record DueState(string WhenKey, DateTimeOffset? NextRunUtc, int FailedTries = 0, bool EverRan = false);
