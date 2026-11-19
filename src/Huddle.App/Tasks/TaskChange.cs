namespace Agency.Huddle.App.Tasks;

/// <summary>
/// One successful change to a Task (Spec §9.5): what it looked like before (or <see langword="null"/>
/// for a Create), what it looks like now, what changed, who changed it, and the Change log entry
/// recorded for it. It is the event written to the timeline, except for Evidence and comments: their timeline entry is the
/// Evidence or comment itself, and this entry is only an in-memory summary ("evidence added by X") for wakes and feeds.
/// </summary>
/// <param name="Before">The Task before the change, or <see langword="null"/> when it was just created.</param>
/// <param name="After">The Task after the change.</param>
/// <param name="Changes">What changed, from <see cref="TaskDiff.Compare(TaskItem, TaskItem)"/>.</param>
/// <param name="Actor">Who made the change.</param>
/// <param name="Entry">The event recorded for this change; for Evidence and comments an in-memory summary that is not written to the file.</param>
/// <param name="Decision">The Human's decision when this change records one, otherwise <see langword="null"/>.</param>
/// <param name="Note">The note given with the decision, otherwise <see langword="null"/>.</param>
/// <param name="Reason">The reason the patch gave (a Helper's cut Verdict reason), otherwise <see langword="null"/>.</param>
/// <param name="Kind">What the change is, for a change whose <paramref name="Changes"/> list is empty; <see cref="TaskChangeKind.Edit"/> for every change that has fields.</param>
/// <param name="Comment">The trimmed comment text for a <see cref="TaskChangeKind.Comment"/>, otherwise <see langword="null"/>.</param>
internal sealed record TaskChange(
    TaskItem? Before,
    TaskItem After,
    IReadOnlyList<FieldChange> Changes,
    TaskActor Actor,
    ChangeLogEntry Entry,
    Agency.Huddle.App.Tasks.HumanGate.TaskDecision? Decision = null,
    string? Note = null,
    string? Reason = null,
    TaskChangeKind Kind = TaskChangeKind.Edit,
    string? Comment = null);
