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
/// <param name="RoomId">The Room a Follow-up posts into, or <see langword="null"/> for an ordinary Job, which has a Job Room of its own.</param>
internal sealed record Job(
    string Id,
    JobOwner Owner,
    string Title,
    string Instruction,
    JobRecurrence When,
    string? TimeZoneId,
    string? CreatedBy,
    bool Paused,
    string? RoomId = null)
{
    /// <summary>A Follow-up: a one-off Job of an Agent that posts into a Room it names instead of a Job Room of its own (Turn Queue spec §7.1).</summary>
    /// <param name="id">The Job's id.</param>
    /// <param name="agentName">The Agent that set it, and that it is for.</param>
    /// <param name="roomId">The Room to come back to.</param>
    /// <param name="title">The title.</param>
    /// <param name="instruction">What to do then.</param>
    /// <param name="on">The date it runs.</param>
    /// <param name="at">The time of day it runs.</param>
    /// <param name="timeZoneId">The zone its time is in, or <see langword="null"/> for this machine's.</param>
    /// <returns>A Job that <see cref="JobRules.Check"/> accepts.</returns>
    public static Job FollowUp(string id, string agentName, string roomId, string title, string instruction, DateOnly on, TimeOnly at, string? timeZoneId = null) =>
        new(id, JobOwner.Agent(agentName), title, instruction, JobRecurrence.Once(on, at), timeZoneId, agentName, false, roomId);
}
