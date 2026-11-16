namespace Agency.Huddle.App.Teams;

/// <summary>One hire: a resolved card for a role of a team type, to be written as an Agent of a Team.</summary>
/// <param name="Team">The Team the Agent joins.</param>
/// <param name="TypeId">The team type id.</param>
/// <param name="RoleId">The role id within the type.</param>
/// <param name="Card">The resolved card carrying the name, model pick, skills and packs.</param>
/// <param name="Effort">The effort to store for the Agent, or <see langword="null"/> for the model's default.</param>
/// <param name="WorkMode">The Work Mode to store for the Agent, or <see langword="null"/> for the Adapter's own mode.</param>
/// <param name="BrowserOff"><see langword="true"/> to hire the Agent with its browser switched off (a Team duplicate copying a member's "off"); written in the same write as the hire.</param>
internal sealed record HireRequest(string Team, string TypeId, string RoleId, CardState Card, string? Effort = null, string? WorkMode = null, bool BrowserOff = false);
