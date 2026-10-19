namespace Agency.Huddle.App.Teams;

/// <summary>One Task that changed since the cutoff, as the retro sees it.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Status">The Task's current status, by its wire name.</param>
/// <param name="Assignee">The assignee, or <see langword="null"/> when unassigned.</param>
internal sealed record TaskRecord(string Id, string Title, string Status, string? Assignee);
