using Agency.Huddle.App.Jobs;

namespace Agency.Huddle.App.Data;

/// <summary>A Job's firing state, kept in <c>team.db</c> apart from its definition in <c>schedule.md</c>.</summary>
/// <param name="JobId">The Job's id.</param>
/// <param name="WhenKey">The timetable the next run was computed from (its <c>when</c> and time zone); a different key means the timetable was changed, so the next run is computed again.</param>
/// <param name="NextRunUtc">When it runs next, or <see langword="null"/> when it never will.</param>
/// <param name="LastRunUtc">When it last ran, or <see langword="null"/> when it never has.</param>
/// <param name="LastOutcome">What its last run did, or <see langword="null"/> when it never has.</param>
internal sealed record JobState(string JobId, string WhenKey, DateTimeOffset? NextRunUtc, DateTimeOffset? LastRunUtc, JobOutcome? LastOutcome);
