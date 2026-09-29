namespace Agency.Huddle.App.Library;

/// <summary>
/// Creates the folders behind a Team and its Projects on demand, for the Team pages. Every refusal
/// is a <see cref="LibraryResult{T}"/> with a user-facing reason; nothing is ever renamed or deleted.
/// </summary>
/// <remarks>
/// A folder created here is visible in the Team catalog only after the Task store's rebuild, about
/// 500 ms later. A caller that navigates to the new Team must poll <c>ITeamCatalog.Find</c> first, or
/// the page flashes "There is no Team named".
/// </remarks>
internal interface ITeamFolders
{
    /// <summary>Creates <c>Teams/&lt;name&gt;/</c> after the Team-name rules and a duplicate check (ignoring case) against the catalog and the disk.</summary>
    /// <param name="name">The new Team's name.</param>
    /// <returns>The new Team folder's path, or the refusal reason.</returns>
    LibraryResult<LibraryPath> EnsureTeam(string name);

    /// <summary>Creates <c>Teams/&lt;team&gt;/&lt;project&gt;/</c>, creating the Team folder first when the Team exists only as a Persona label.</summary>
    /// <param name="team">The Team's name, matched ignoring case.</param>
    /// <param name="project">The new Project's name.</param>
    /// <returns>The new Project folder's path, or the refusal reason.</returns>
    LibraryResult<LibraryPath> EnsureProjectIn(string team, string project);
}
