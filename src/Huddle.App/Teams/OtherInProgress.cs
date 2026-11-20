using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>The Task an assignee already has In Progress, which a second one would collide with (ADR-0102).</summary>
/// <param name="Assignee">The assignee, as the changed Task will name them.</param>
/// <param name="Id">The Task already In Progress.</param>
/// <param name="Title">That Task's title.</param>
internal sealed record OtherInProgress(string Assignee, TaskId Id, string Title);
