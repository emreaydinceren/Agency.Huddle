namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// What the archive UI needs from <see cref="ArchiveService"/> (Spec 6.9, D-46): the Team and Project moves, the
/// confirmation counts and the lists of archived Teams and Projects. A seam so the Razor components can be tested
/// against a fake (the service is a sealed class with ten dependencies).
/// </summary>
internal interface IArchiveService
{
    /// <summary>What archiving the Team takes off the Boards; reads the stores and writes nothing.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    ArchiveConfirmation Confirm(string team);

    /// <summary>Archives the Team.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    /// <param name="ct">Cancels the wait for the gate.</param>
    /// <returns>Done, or the reason nothing was changed.</returns>
    Task<ArchiveOutcome> ArchiveTeamAsync(string team, CancellationToken ct);

    /// <summary>Unarchives the Team.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    /// <param name="ct">Cancels the wait for the gate.</param>
    /// <returns>Done, or the reason nothing was changed.</returns>
    Task<ArchiveOutcome> UnarchiveTeamAsync(string team, CancellationToken ct);

    /// <summary>Moves <c>Teams/&lt;team&gt;/&lt;project&gt;</c> to <c>Archive/&lt;team&gt;/&lt;project&gt;</c>, with no roster, labels or Rooms.</summary>
    /// <param name="team">The live Team, compared ignoring case.</param>
    /// <param name="project">The Project, compared ignoring case.</param>
    /// <param name="ct">Cancels the wait for the gate.</param>
    /// <returns>Done, or the reason nothing was changed.</returns>
    Task<ArchiveOutcome> ArchiveProjectAsync(string team, string project, CancellationToken ct);

    /// <summary>Moves <c>Archive/&lt;team&gt;/&lt;project&gt;</c> back under the live Team.</summary>
    /// <param name="team">The live Team, compared ignoring case.</param>
    /// <param name="project">The archived Project, compared ignoring case.</param>
    /// <param name="ct">Cancels the wait for the gate.</param>
    /// <returns>Done, or the reason nothing was changed.</returns>
    Task<ArchiveOutcome> UnarchiveProjectAsync(string team, string project, CancellationToken ct);

    /// <summary>The Teams that have <c>Archive/&lt;T&gt;/_team.md</c>, by their folder names.</summary>
    IReadOnlyList<string> ArchivedTeams();

    /// <summary>The Projects archived under <c>Archive/&lt;team&gt;/</c>, whether the Team is live or archived.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    IReadOnlyList<string> ArchivedProjects(string team);
}
