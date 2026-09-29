namespace Agency.Huddle.App.Teams;

/// <summary>
/// Pure operations on a list of team labels, comparing case-insensitively by team name.
/// </summary>
internal static class TeamLabels
{
    /// <summary>
    /// Checks if a team is in the list, ignoring case.
    /// </summary>
    /// <param name="current">The current list of teams.</param>
    /// <param name="team">The team name to check for.</param>
    /// <returns>True if the team is in the list (case-insensitive), false otherwise.</returns>
    public static bool Contains(IReadOnlyList<string> current, string team)
    {
        return current.Any(t => string.Equals(t, team, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a team to the list if not already present (case-insensitive check).
    /// </summary>
    /// <param name="current">The current list of teams.</param>
    /// <param name="team">The team to add.</param>
    /// <returns>A new list containing the original teams plus the new team if not already present, or a copy if already present.</returns>
    public static IReadOnlyList<string> Add(IReadOnlyList<string> current, string team)
    {
        if (Contains(current, team))
        {
            return current.ToList();
        }

        List<string> result = current.ToList();
        result.Add(team);
        return result;
    }

    /// <summary>
    /// Removes all case variants of a team from the list, preserving the order of remaining teams.
    /// </summary>
    /// <param name="current">The current list of teams.</param>
    /// <param name="team">The team to remove (case-insensitive).</param>
    /// <returns>A new list with all case variants of the team removed.</returns>
    public static IReadOnlyList<string> Remove(IReadOnlyList<string> current, string team)
    {
        return current
            .Where(t => !string.Equals(t, team, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
