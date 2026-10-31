namespace Agency.Huddle.App.Tasks;

/// <summary>
/// One successful change to a Task (Spec §9.5): what it looked like before (or <see langword="null"/>
/// for a Create), what it looks like now, what changed, who changed it, and the Change log entry
/// recorded for it.
/// </summary>
/// <param name="Before">The Task before the change, or <see langword="null"/> when it was just created.</param>
/// <param name="After">The Task after the change.</param>
/// <param name="Changes">What changed, from <see cref="TaskDiff.Compare(TaskItem, TaskItem)"/>.</param>
/// <param name="Actor">Who made the change.</param>
/// <param name="Entry">The Change log entry recorded for this change.</param>
/// <param name="Decision">The Human's decision when this change records one, otherwise <see langword="null"/>.</param>
/// <param name="Note">The note given with the decision, otherwise <see langword="null"/>.</param>
/// <param name="Reason">The reason the patch gave (a Helper's cut Verdict reason), otherwise <see langword="null"/>.</param>
internal sealed record TaskChange(
    TaskItem? Before,
    TaskItem After,
    IReadOnlyList<FieldChange> Changes,
    TaskActor Actor,
    ChangeLogEntry Entry,
    Agency.Huddle.App.Tasks.HumanGate.TaskDecision? Decision = null,
    string? Note = null,
    string? Reason = null);
