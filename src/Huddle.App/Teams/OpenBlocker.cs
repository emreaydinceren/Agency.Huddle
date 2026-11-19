using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>A Task in another's <c>blocked_by</c> that is not finished yet (#538).</summary>
/// <param name="Id">The blocking Task.</param>
/// <param name="Status">The blocking Task's current status.</param>
/// <param name="Assignee">Who holds the blocking Task, or null when nobody does.</param>
internal sealed record OpenBlocker(TaskId Id, TaskState Status, string? Assignee = null);
