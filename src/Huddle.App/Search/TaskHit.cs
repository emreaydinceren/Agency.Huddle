using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Search;

/// <summary>A Task hit: its id, status and assignee are what its card shows. <see cref="AssigneeTitle"/> is the assignee Persona's title, or null when unassigned or the Persona is gone.</summary>
public sealed record TaskHit(TaskId Id, string TaskTitle, TaskState Status, string? Assignee, string Team, bool IsClosed, MatchTier Tier, string? AssigneeTitle = null)
    : SearchHit(TaskTitle, Team, IsClosed, Tier);
