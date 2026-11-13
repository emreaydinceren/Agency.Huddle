namespace Agency.Huddle.App.Teams;

/// <summary>One row of the rename journal (<c>team_renames</c>).</summary>
/// <param name="Id">The row's id; later renames have greater ids.</param>
/// <param name="Kind">Whether a Team or a Project was renamed.</param>
/// <param name="Team">The Team's name at the time of the rename (for a Team rename, its old name).</param>
/// <param name="OldName">The name before the rename.</param>
/// <param name="NewName">The name after the rename.</param>
/// <param name="State">Pending or Done.</param>
/// <param name="RenamedAt">When the rename began.</param>
public sealed record TeamRename(
    long Id,
    TeamRenameKind Kind,
    string Team,
    string OldName,
    string NewName,
    TeamRenameState State,
    DateTimeOffset RenamedAt);
