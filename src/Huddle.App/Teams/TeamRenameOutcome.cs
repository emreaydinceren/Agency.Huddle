namespace Agency.Huddle.App.Teams;

/// <summary>What <see cref="TeamRenameService.RenameTeamAsync"/> did.</summary>
/// <param name="Renamed">Whether the rename finished (the journal row is Done).</param>
/// <param name="Error">A plain sentence for the dialog when it did not; <see langword="null"/> otherwise.</param>
internal sealed record TeamRenameOutcome(bool Renamed, string? Error);
