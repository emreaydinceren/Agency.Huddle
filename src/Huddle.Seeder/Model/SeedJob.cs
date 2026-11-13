using Agency.Huddle.App.Jobs;

namespace Agency.Huddle.Seeder.Model;

/// <summary>
/// A Job: one section of its owner's <c>schedule.md</c>, its firing state in <c>job_state</c>, and, once it has run, its Job Room.
/// Seeder spec §5 (The plan a scenario returns); SEED-SC-19.
/// </summary>
/// <param name="Id">The Job's id, <c>job-</c> and six hex digits.</param>
/// <param name="Owner">The Agent it is for, by Name, or <see langword="null"/> for the Human.</param>
/// <param name="Title">The section heading, which also names the Job Room.</param>
/// <param name="When">The timetable as <c>schedule.md</c> writes it, such as <c>weekdays at 09:00</c>.</param>
/// <param name="Instruction">The text each run posts.</param>
internal sealed record SeedJob(string Id, string? Owner, string Title, string When, string Instruction)
{
    /// <summary>The IANA or Windows time zone id, or <see langword="null"/> for the machine's own.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Who scheduled it, by Name, or <see langword="null"/> when unknown.</summary>
    public string? By { get; init; }

    /// <summary>Whether it is paused.</summary>
    public bool Paused { get; init; }

    /// <summary>Its past runs, oldest first; empty means it has never run and has no Room.</summary>
    public IReadOnlyList<SeedJobRun> Runs { get; init; } = [];

    /// <summary>
    /// What the last run did. Anything but <see cref="JobOutcome.Delivered"/> means the last run posted nothing, so it carries
    /// no replies; ignored when there are no <see cref="Runs"/>.
    /// </summary>
    public JobOutcome LastOutcome { get; init; } = JobOutcome.Delivered;

    /// <summary>Whether its Job Room is archived; the next run brings it back.</summary>
    public bool RoomArchived { get; init; }

    /// <summary>The key its Room's Message ids are derived from, apart from every <see cref="SeedRoom.Key"/>.</summary>
    internal string RoomKey => "job:" + this.Id;
}
