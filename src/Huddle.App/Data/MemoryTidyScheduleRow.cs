using Agency.Huddle.App.Scheduling;

namespace Agency.Huddle.App.Data;

/// <summary>One row of <c>memory_tidy_schedule</c>: a timetable's <see cref="DueState"/> and when it last ran.</summary>
/// <param name="Name">The timetable: <c>weekly</c> or <c>resume</c>.</param>
/// <param name="State">What the due-time rule stored.</param>
/// <param name="LastRunUtc">When a pass of this timetable last ended, for the card; <see langword="null"/> when none has.</param>
internal sealed record MemoryTidyScheduleRow(string Name, DueState State, DateTimeOffset? LastRunUtc);
