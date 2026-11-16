using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Creates the folders behind a Team and its Projects on demand, for the Team pages. Every refusal
/// is a <see cref="LibraryResult{T}"/> with a user-facing reason; nothing is ever renamed or deleted.
/// </summary>
/// <remarks>
/// A folder created here is visible in the Team catalog only after the Task store's rebuild, about
/// 500 ms later. A caller that navigates to the new Team waits on <c>ITeamCatalog.Changed</c> (see
/// <c>TeamsNav</c>) first, or the page flashes "There is no Team named".
/// </remarks>
internal interface ITeamFolders
{
    /// <summary>Creates <c>Teams/&lt;name&gt;/</c> after the Team-name rules and a duplicate check (ignoring case) against the catalog and the disk.</summary>
    /// <param name="name">The new Team's name.</param>
    /// <returns>The new Team folder's path, or the refusal reason.</returns>
    LibraryResult<LibraryPath> EnsureTeam(string name);

    /// <summary>Finds the folder of a Team that already exists on disk; unlike <see cref="EnsureTeam"/> it never creates and never refuses an existing Team.</summary>
    /// <param name="name">The Team's name.</param>
    /// <returns>The Team folder's path, or the reason there is none.</returns>
    LibraryResult<LibraryPath> FindTeam(string name);

    /// <summary>Creates <c>Teams/&lt;team&gt;/&lt;project&gt;/</c>, creating the Team folder first when the Team exists only as a Persona label.</summary>
    /// <param name="team">The Team's name, matched ignoring case.</param>
    /// <param name="project">The new Project's name.</param>
    /// <returns>The new Project folder's path, or the refusal reason.</returns>
    LibraryResult<LibraryPath> EnsureProjectIn(string team, string project);

    /// <summary>
    /// Creates the Team folder when it is missing and writes its <c>_team.md</c> atomically. Never
    /// overwrites an existing <c>_team.md</c>; a Team folder that has none (a Persona-label Team) receives one.
    /// </summary>
    /// <param name="team">The Team's name, validated with the Team-name rules.</param>
    /// <param name="file">The team file to write.</param>
    /// <param name="leaveExistingAlone">
    /// Only the archive passes <see langword="true"/>: a Team folder that exists already receives its <c>_team.md</c>
    /// and no Second Brain folders beside it. Everyone else gets <c>raw</c>, <c>memory</c> and <c>outputs</c> ensured.
    /// </param>
    /// <returns>The outcome and, on success, the path of the written file.</returns>
    TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false);

    /// <summary>Rewrites a Team's <c>_team.md</c> through <paramref name="change"/>, atomically.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="change">Maps the current file to the new one.</param>
    /// <returns><see langword="false"/> when the Team has no readable <c>_team.md</c>; <paramref name="change"/> is then not called.</returns>
    bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change);

    /// <summary>
    /// Writes a Project's avatar into its folder's <c>_project.md</c> atomically (ADR-0083), creating the file when it is missing and
    /// keeping any other line and the notes of one that exists. A Project's avatar has no image, so <paramref name="avatar"/>'s image is
    /// not written. An avatar equal to the stored one (<see cref="Avatars.Avatar.None"/> for a Project with no file) writes nothing and
    /// returns <see langword="true"/>. The default writes nothing.
    /// </summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    /// <param name="avatar">The avatar to keep: a label and a background.</param>
    /// <returns><see langword="false"/> when the Project folder does not exist or cannot be written.</returns>
    bool UpdateProjectAvatar(string team, string project, Avatars.Avatar avatar) => false;

    /// <summary>Reads a Project's <c>_project.md</c>. The default knows no Project file.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    /// <returns>The parsed file, or <see langword="null"/> when the Project folder or its file is missing or unreadable.</returns>
    ProjectFile? ReadProjectFile(string team, string project) => null;

    /// <summary>
    /// Rewrites a Project's <c>_project.md</c> through <paramref name="change"/>, atomically, creating the file when the Project folder has none.
    /// The default writes nothing.
    /// </summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    /// <param name="change">Maps the current file (<see cref="ProjectFile.Empty"/> when there is none) to the new one.</param>
    /// <returns><see langword="false"/> when the Project folder does not exist or cannot be written; <paramref name="change"/> is then not called.</returns>
    bool UpdateProjectFile(string team, string project, Func<ProjectFile, ProjectFile> change) => false;

    /// <summary>Runs <paramref name="action"/> while no label sync or folder creation can touch the Teams root. The default runs it unguarded.</summary>
    /// <typeparam name="T">The action's result.</typeparam>
    /// <param name="action">The folder move; it must not call <c>ITeamMembership</c>.</param>
    /// <returns>What <paramref name="action"/> returned.</returns>
    T RunExclusive<T>(Func<T> action) => action();

    /// <summary>The names of the archived Teams (a folder under the Archive root holding <c>_team.md</c>), for refusing a new Team of the same name up front. The default knows none.</summary>
    /// <returns>The archived names in their on-disk spelling.</returns>
    IReadOnlyList<string> ArchivedTeamNames() => [];

    /// <summary>Reads a Team's <c>_team.md</c>.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>The parsed file, or <see langword="null"/> when the Team or its file is missing or unreadable.</returns>
    TeamFile? ReadTeamFile(string team);
}
