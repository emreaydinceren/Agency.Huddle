namespace Agency.Huddle.App.Jobs;

/// <summary>
/// An instruction that runs on a timetable for its owner: one <c>##</c> section of the owner's <c>schedule.md</c>. Its
/// firing state (next and last run) is not here but in <c>team.db</c>, so editing the file never fires a Job twice.
/// </summary>
/// <param name="Id">The Job's id, stable across renames and edits, for example <c>job-7f3a2c</c>.</param>
/// <param name="Owner">Whose schedule it is on, which is also who it is for.</param>
/// <param name="Title">The section heading.</param>
/// <param name="Instruction">The text posted into the Job Room when it runs.</param>
/// <param name="When">When it runs.</param>
/// <param name="TimeZoneId">The time zone its times are in, or <see langword="null"/> for this machine's.</param>
/// <param name="CreatedBy">Who scheduled it, by Name, or <see langword="null"/> when unknown.</param>
/// <param name="Paused">Whether it is paused.</param>
internal sealed record Job(
    string Id,
    JobOwner Owner,
    string Title,
    string Instruction,
    JobRecurrence When,
    string? TimeZoneId,
    string? CreatedBy,
    bool Paused);
