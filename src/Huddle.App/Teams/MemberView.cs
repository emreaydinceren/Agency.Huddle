namespace Agency.Huddle.App.Teams;

/// <summary>A member as the Complete Setup panel sees it, for warnings.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="RoleId">The id of the member's role.</param>
/// <param name="AdapterId">The adapter the member runs on, or null when none is chosen.</param>
/// <param name="Skills">The names of the member's skills.</param>
internal sealed record MemberView(string Name, string RoleId, string? AdapterId, IReadOnlyList<string> Skills);
