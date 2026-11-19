namespace Agency.Huddle.App.Tasks;

/// <summary>
/// What saving a Task change would do to its assignee (Spec §10.1): the Name it would notify, that
/// Name's presence if the wake would actually happen, and which guard - if any - stops it. Every UI
/// label about who will be woken (a Save button, a drag hint, a banner) reads this record rather than
/// re-deriving the guards in Spec §10.2 itself, so the interface can never disagree with
/// <see cref="TaskTriggerService.Preview(TaskItem?, TaskItem, TaskActor)"/>.
/// </summary>
/// <param name="AssigneeName">The Task's assignee Name (<c>After.Assignee</c>), or <see langword="null"/> when the Task has none.</param>
/// <param name="Presence">The assignee's presence badge, resolved only when <paramref name="Block"/> is <see cref="WakeBlock.None"/>; otherwise <see langword="null"/>.</param>
/// <param name="Block">Which guard (Spec §10.2) stops the wake, or <see cref="WakeBlock.None"/> when none does.</param>
public sealed record WakePreview(string? AssigneeName, PresenceState? Presence, WakeBlock Block);
