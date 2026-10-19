namespace Agency.Huddle.App.Tasks;

/// <summary>One recorded wake attempt for a Task, driving the "AI reacting" toast.</summary>
/// <param name="TaskId">The Task the wake was for.</param>
/// <param name="Assignee">The name of the Agent the wake targeted.</param>
/// <param name="RoomId">The Room the wake was posted in, or <see langword="null"/> when none was chosen.</param>
/// <param name="RoomName">The name of <paramref name="RoomId"/>'s Room, or <see langword="null"/> when none was chosen.</param>
/// <param name="Outcome">What the wake attempt did.</param>
/// <param name="At">When the wake attempt happened.</param>
public sealed record WakeRecord(TaskId TaskId, string Assignee, string? RoomId, string? RoomName, WakeOutcome Outcome, DateTimeOffset At);
