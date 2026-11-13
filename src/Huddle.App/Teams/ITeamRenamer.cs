namespace Agency.Huddle.App.Teams;

/// <summary>
/// What the rename UI asks of <see cref="TeamRenameService"/> (a sealed class with many dependencies, so the components
/// and their tests take this seam, as they take <c>IArchiveService</c>).
/// </summary>
internal interface ITeamRenamer
{
    /// <summary>Renames the Team.</summary>
    /// <param name="oldName">The Team's name.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Renamed, or the plain sentence saying why not.</returns>
    Task<TeamRenameOutcome> RenameTeamAsync(string oldName, string newName, CancellationToken ct);

    /// <summary>Renames a Project of a Team.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="oldProject">The Project's current name.</param>
    /// <param name="newProject">The new name.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Renamed, or the plain sentence saying why not.</returns>
    Task<TeamRenameOutcome> RenameProjectAsync(string team, string oldProject, string newProject, CancellationToken ct);

    /// <summary>The refusal <see cref="RenameTeamAsync"/> would give for <paramref name="newName"/>, without renaming anything.</summary>
    /// <param name="oldName">The Team's name.</param>
    /// <param name="newName">The name being typed.</param>
    /// <returns>The refusal sentence, or <see langword="null"/> when the name is acceptable.</returns>
    string? CheckTeamName(string oldName, string newName);

    /// <summary>The refusal <see cref="RenameProjectAsync"/> would give for <paramref name="newProject"/>, without renaming anything.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="oldProject">The Project's current name.</param>
    /// <param name="newProject">The name being typed.</param>
    /// <returns>The refusal sentence, or <see langword="null"/> when the name is acceptable.</returns>
    string? CheckProjectName(string team, string oldProject, string newProject);

    /// <summary>The names of the Team's members whose Agent has a Turn running in any Room right now.</summary>
    /// <param name="team">The Team.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The member names, in member order.</returns>
    Task<IReadOnlyList<string>> MidTurnMembersAsync(string team, CancellationToken ct);
}
