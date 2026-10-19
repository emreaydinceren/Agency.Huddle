namespace Agency.Huddle.App.References;

/// <summary>A Room that exists; an Archived Room still does.</summary>
/// <param name="Id">The Room's id.</param>
/// <param name="Name">Its Name.</param>
/// <param name="MemberCount">How many Members it has.</param>
/// <param name="Team">The Team it is scoped to, or null for a root Room.</param>
/// <param name="IsTeamRoom">Whether it is its Team's Team Room.</param>
internal sealed record ResolvedRoom(string Id, string Name, int MemberCount, string? Team, bool IsTeamRoom) : ResolvedReference;
