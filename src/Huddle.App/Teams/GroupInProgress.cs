using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>Another Task In Progress in the group a change would put a Task into: the same Team and Project, outside <c>_closed/</c> (ADR-0103).</summary>
/// <param name="Project">The group's Project, or <see langword="null"/> for the Team's Tasks that have none.</param>
/// <param name="Id">The other Task's id.</param>
/// <param name="Title">The other Task's title.</param>
internal sealed record GroupInProgress(string? Project, TaskId Id, string Title);
