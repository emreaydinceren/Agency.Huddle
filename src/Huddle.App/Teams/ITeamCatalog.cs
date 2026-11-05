namespace Agency.Huddle.App.Teams;

/// <summary>The live list of Teams as the pages see it, rebuilt whenever a Persona label or a Team folder changes.</summary>
internal interface ITeamCatalog
{
    /// <summary>The current Teams, sorted by Name ignoring case. Each read returns one immutable snapshot.</summary>
    IReadOnlyList<TeamSummary> Teams { get; }

    /// <summary>Raised once per source event, after <see cref="Teams"/> already holds the new snapshot.</summary>
    event Action? Changed;

    /// <summary>Finds a Team by name, ignoring case.</summary>
    /// <param name="team">The Team name to look for.</param>
    /// <returns>The Team, or <see langword="null"/> when none has that name.</returns>
    TeamSummary? Find(string team);

    /// <summary>Whether <paramref name="team"/> has a Project named <paramref name="project"/>, both compared ignoring case.</summary>
    /// <param name="team">The Team name.</param>
    /// <param name="project">The Project name.</param>
    /// <returns><see langword="true"/> when the Team exists and lists that Project.</returns>
    bool ProjectExists(string team, string project);

    /// <summary>
    /// Rebuilds <see cref="Teams"/> from the stores now, then raises <see cref="Changed"/>, instead of waiting for the debounce that follows a
    /// <c>_team.md</c> edit. A caller whose next step reads the catalog at once (the session of an Agent just hired) calls this after writing the file.
    /// A catalog that is always current has nothing to do.
    /// </summary>
    void Refresh()
    {
    }
}
