namespace Agency.Huddle.App.HiringPool;

/// <summary>A team type as the hiring pool names it: the label and the sentence shown beside its chip and in the type dropdown.</summary>
/// <param name="Id">The type id, the same as the folder name of its <c>team-type.json</c>.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">One or two plain sentences on what the team does for its Human.</param>
internal sealed record PoolTeamType(string Id, string Name, string Description);
