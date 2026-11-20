namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>What every wake rule reads about one fired batch.</summary>
/// <param name="Latest">The Task's latest state.</param>
/// <param name="Actor">Who made the batch's changes.</param>
/// <param name="RequestStatusWakes">Whether the batch holds a status move that wakes a Request's requesting manager (C-5, see <see cref="WakeRelevance.RequestStatusWakes"/>).</param>
/// <param name="AssigneePreview">The result of <see cref="TaskTriggerService.Preview(TaskItem?, TaskItem, TaskActor)"/> for the assignee, which only the assignee rule reads.</param>
/// <param name="WakesAssignee">Whether any change in the batch wakes the assignee (<see cref="WakeRelevance"/>); the assignee rule skips with no block when it is <see langword="false"/>.</param>
/// <param name="WakesManager">Whether any change in the batch wakes the Team's manager; the manager rule skips when it is <see langword="false"/>.</param>
/// <param name="Mentioned">The Agent names the batch's comments Mention, or <see langword="null"/> when none were resolved (slice 2 fills it).</param>
internal sealed record TaskWakeRound(
    TaskItem Latest,
    TaskActor Actor,
    bool RequestStatusWakes,
    WakePreview AssigneePreview,
    bool WakesAssignee = true,
    bool WakesManager = true,
    IReadOnlyList<string>? Mentioned = null);
