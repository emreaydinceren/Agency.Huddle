namespace Agency.Huddle.App.Teams;

/// <summary>The rename journal: records Team and Project renames and resolves old names to today's.</summary>
internal interface ITeamRenames
{
    /// <summary>Writes a Pending row stamped with the current time.</summary>
    /// <param name="kind">Team or Project.</param>
    /// <param name="team">The Team's name; for a Team rename, the old name.</param>
    /// <param name="oldName">The name before.</param>
    /// <param name="newName">The name after.</param>
    /// <returns>The new row's id.</returns>
    long Begin(TeamRenameKind kind, string team, string oldName, string newName);

    /// <summary>Turns a Pending row into a Done row.</summary>
    /// <param name="id">The row's id.</param>
    void Complete(long id);

    /// <summary>Deletes a Pending row; refuses a Done row.</summary>
    /// <param name="id">The row's id.</param>
    void Discard(long id);

    /// <summary>Lists the Pending rows in id order.</summary>
    /// <returns>The rows.</returns>
    IReadOnlyList<TeamRename> Pending();

    /// <summary>Lists the Done rows with an id above <paramref name="id"/>, in id order.</summary>
    /// <param name="id">The last id already seen.</param>
    /// <returns>The rows.</returns>
    IReadOnlyList<TeamRename> DoneAfter(long id);

    /// <summary>Lists the Done rows renamed at or after <paramref name="since"/>, in id order.</summary>
    /// <param name="since">The earliest rename time.</param>
    /// <returns>The rows.</returns>
    IReadOnlyList<TeamRename> DoneSince(DateTimeOffset since);

    /// <summary>Finds the newest Done row renamed before <paramref name="at"/>.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>Its id, or 0 when none.</returns>
    long LatestDoneBefore(DateTimeOffset at);

    /// <summary>Follows Done Team renames from a name to today's name.</summary>
    /// <param name="team">A Team name, possibly old.</param>
    /// <returns>Today's name, or the input when not journalled.</returns>
    string Resolve(string team);

    /// <summary>Resolves the Team, then follows Done Project renames of that Team.</summary>
    /// <param name="team">A Team name, possibly old.</param>
    /// <param name="project">A Project name, possibly old.</param>
    /// <returns>Today's Project name, or the input when not journalled.</returns>
    string ResolveProject(string team, string project);

    /// <summary>Tells whether a Team row, Pending or Done, holds the name as old or new name.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when reserved.</returns>
    bool IsReserved(string name);

    /// <summary>Tells whether a Pending Team row holds the name as old or new name.</summary>
    /// <param name="team">The name.</param>
    /// <returns><see langword="true"/> when a rename is pending.</returns>
    bool IsPending(string team);

    /// <summary>Tells whether a Project row, Pending or Done, of the Team holds the name as old or new name.</summary>
    /// <param name="team">A Team name, possibly old.</param>
    /// <param name="project">The Project name.</param>
    /// <returns><see langword="true"/> when reserved.</returns>
    bool IsProjectReserved(string team, string project);

    /// <summary>Tells whether a Pending Project row of the Team holds the name as old or new name.</summary>
    /// <param name="team">A Team name, possibly old.</param>
    /// <param name="project">The Project name.</param>
    /// <returns><see langword="true"/> when a rename is pending.</returns>
    bool IsProjectPending(string team, string project);

    /// <summary>Lists every earlier name of today's Team, from Done Team rows, in id order.</summary>
    /// <param name="team">A Team name, normally today's.</param>
    /// <returns>The earlier names; empty when none.</returns>
    IReadOnlyList<string> EarlierNames(string team);
}
