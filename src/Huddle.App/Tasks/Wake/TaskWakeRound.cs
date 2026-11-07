namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>What every wake rule reads about one fired batch.</summary>
/// <param name="Latest">The Task's latest state.</param>
/// <param name="Actor">Who made the batch's changes.</param>
/// <param name="StatusChanged">Whether the batch changed the status.</param>
/// <param name="MovedToReview">Whether the batch moved the Task into Review.</param>
/// <param name="AssigneePreview">The result of <see cref="TaskTriggerService.Preview"/> for the assignee, which only the assignee rule reads.</param>
internal sealed record TaskWakeRound(TaskItem Latest, TaskActor Actor, bool StatusChanged, bool MovedToReview, WakePreview AssigneePreview);
