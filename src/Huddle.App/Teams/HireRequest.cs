namespace Agency.Huddle.App.Teams;

/// <summary>One hire: a resolved card for a role of a team type, to be written as an Agent of a Team.</summary>
/// <param name="Team">The Team the Agent joins.</param>
/// <param name="TypeId">The team type id.</param>
/// <param name="RoleId">The role id within the type.</param>
/// <param name="Card">The resolved card carrying the name, model pick, skills and packs.</param>
internal sealed record HireRequest(string Team, string TypeId, string RoleId, CardState Card);
