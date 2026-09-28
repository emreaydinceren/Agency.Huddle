using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Start-up guards for the Library feature's folder layout (Spec §6.3, fourth bullet; ADR-0030
/// Consequences).
/// </summary>
internal static class LayoutGuard
{
    /// <summary>
    /// Throws when <c>Team:Teams:Dir</c> and <c>Team:Acp:TeammatesDir</c>, resolved under
    /// <see cref="TeamOptions.DataDir"/>, are the same directory or either contains the other,
    /// comparing with <see cref="FolderSnapshot.PathComparer"/> on separator-terminated prefixes.
    /// </summary>
    /// <param name="options">The bound <see cref="TeamOptions"/> to validate.</param>
    internal static void ValidateTeamsAndTeammates(TeamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string teamsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir)));
        string teammatesRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Acp.TeammatesDir)));

        string teamsPrefix = teamsRoot + Path.DirectorySeparatorChar;
        string teammatesPrefix = teammatesRoot + Path.DirectorySeparatorChar;

        bool equal = FolderSnapshot.PathComparer.Equals(teamsRoot, teammatesRoot);
        bool teamsInsideTeammates = HasPrefix(teamsPrefix, teammatesPrefix);
        bool teammatesInsideTeams = HasPrefix(teammatesPrefix, teamsPrefix);

        if (equal || teamsInsideTeammates || teammatesInsideTeams)
        {
            throw new InvalidOperationException(
                $"'Team:Teams:Dir' ({teamsRoot}) and 'Team:Acp:TeammatesDir' ({teammatesRoot}) must not overlap.");
        }
    }

    /// <summary>True when <paramref name="value"/> starts with <paramref name="prefix"/>, compared with <see cref="FolderSnapshot.PathComparer"/>.</summary>
    private static bool HasPrefix(string value, string prefix) =>
        value.Length >= prefix.Length && FolderSnapshot.PathComparer.Equals(value[..prefix.Length], prefix);
}
