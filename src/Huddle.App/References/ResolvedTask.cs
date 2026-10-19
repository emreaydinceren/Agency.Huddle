using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.References;

/// <summary>A Task that exists.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">Its title now.</param>
/// <param name="Status">Its status now.</param>
/// <param name="Assignee">Its assignee, or null when unassigned.</param>
/// <param name="Team">The Team folder it lives in.</param>
internal sealed record ResolvedTask(TaskId Id, string Title, TaskState Status, string? Assignee, string Team) : ResolvedReference;
