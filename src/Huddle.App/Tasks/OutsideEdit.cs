namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The state of a Task before and after an edit made outside Huddle - by hand, a script, or a git
/// checkout - that <see cref="TaskStore"/>'s filesystem watcher noticed on its next debounced
/// rebuild (Spec §8.4, Spec §4 principle 3).
/// </summary>
/// <param name="Before">
/// The Task as this store last saw it, or <see langword="null"/> when the file is new - either
/// created outside Huddle, or an existing id that just moved here from a different path.
/// </param>
/// <param name="After">The Task as it now reads on disk, at whatever path the rebuild found it.</param>
internal sealed record OutsideEdit(TaskItem? Before, TaskItem After);
