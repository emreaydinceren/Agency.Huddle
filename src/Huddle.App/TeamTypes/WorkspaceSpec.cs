namespace Agency.Huddle.App.TeamTypes;

/// <summary>The workspace a team type needs.</summary>
/// <param name="Kind">The workspace kind.</param>
/// <param name="Label">The label shown to the user.</param>
/// <param name="Required">Whether a Team must have a workspace before it starts.</param>
internal sealed record WorkspaceSpec(WorkspaceKind Kind, string Label, bool Required);
