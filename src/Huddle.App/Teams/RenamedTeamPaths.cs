namespace Agency.Huddle.App.Teams;

/// <summary>Maps a path under the Teams root that still names a Team, or a Project, by an old name onto today's names.</summary>
internal static class RenamedTeamPaths
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>Replaces the first segment under the Teams root through <see cref="ITeamRenames.Resolve"/>, and a Project second segment through <see cref="ITeamRenames.ResolveProject"/>.</summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="path">The path, with either separator.</param>
    /// <param name="renames">The rename journal.</param>
    /// <returns>The path with today's names and its original text and separators otherwise; the input when nothing is journalled or the path is outside the root.</returns>
    public static string Map(string teamsRoot, string path, ITeamRenames renames)
    {
        ArgumentNullException.ThrowIfNull(teamsRoot);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(renames);

        string root = teamsRoot.TrimEnd(Separators);
        if (root.Length == 0 || path.Length <= root.Length + 1
            || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || Array.IndexOf(Separators, path[root.Length]) < 0)
        {
            return path;
        }

        int teamStart = root.Length + 1;
        int teamEnd = path.IndexOfAny(Separators, teamStart);
        if (teamEnd < 0)
        {
            teamEnd = path.Length;
        }

        string team = path[teamStart..teamEnd];
        if (team.Length == 0)
        {
            return path;
        }

        string resolved = renames.Resolve(team);
        string rest = path[teamEnd..];
        if (rest.Length > 1 && TryProject(path, teamEnd + 1, team, renames, out int projectEnd, out string? project))
        {
            rest = string.Concat(path.AsSpan(teamEnd, 1), project, path.AsSpan(projectEnd));
        }

        return string.Concat(path.AsSpan(0, teamStart), resolved, rest);
    }

    private static bool TryProject(string path, int start, string team, ITeamRenames renames, out int end, out string? resolved)
    {
        end = path.IndexOfAny(Separators, start);
        if (end < 0)
        {
            end = path.Length;
        }

        string project = path[start..end];
        resolved = project.Length == 0 ? null : renames.ResolveProject(team, project);
        return resolved is not null && !string.Equals(resolved, project, StringComparison.Ordinal);
    }
}
