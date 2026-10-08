using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>What the team check bar shows and whether Start Team is enabled (HR-8, HR-9, NF-6).</summary>
/// <param name="Coverage">One entry per coverage step of the type, true when a hired role covers it.</param>
/// <param name="VendorCount">The distinct adapters among the hired cards.</param>
/// <param name="ProcessCount">The hired members; each runs as one process.</param>
/// <param name="CanStart">Whether every card is Ready and the manager and every required role are hired.</param>
/// <param name="Blockers">Each reason Start Team is disabled, in plain words.</param>
internal sealed record TeamCheck(IReadOnlyDictionary<string, bool> Coverage, int VendorCount, int ProcessCount, bool CanStart, IReadOnlyList<string> Blockers);

/// <summary>Computes the team check bar and the pool limits (HR-4, HR-8, HR-9, NF-6).</summary>
internal static class TeamCheckBar
{
    /// <summary>Computes coverage, vendor count, process count and whether the Team can start.</summary>
    /// <param name="type">The Team's type.</param>
    /// <param name="hired">The cards hired so far.</param>
    /// <returns>The check bar state.</returns>
    public static TeamCheck Compute(TeamTypeManifest type, IReadOnlyList<CardState> hired)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(hired);

        HashSet<string> hiredRoles = new(hired.Select(static card => card.RoleId), StringComparer.Ordinal);
        Dictionary<string, bool> coverage = new(StringComparer.Ordinal);
        foreach (string step in type.Coverage)
        {
            coverage[step] = type.Roles.Any(role => hiredRoles.Contains(role.Id) && role.Covers.Contains(step, StringComparer.Ordinal));
        }

        int vendors = hired.Select(static card => card.Pick?.AdapterId).OfType<string>().Distinct(StringComparer.Ordinal).Count();

        List<string> blockers = [];
        foreach (CardState card in hired.Where(static card => card.Readiness != CardReadiness.Ready))
        {
            blockers.Add($"{card.Name} needs setup: {string.Join("; ", card.Reasons)}.");
        }

        foreach (RoleSpec role in type.Roles.Where(role => role.Manager && !hiredRoles.Contains(role.Id)))
        {
            blockers.Add($"Hire the manager ({role.Title}).");
        }

        foreach (RoleSpec role in type.Roles.Where(role => role.Required && !role.Manager && !hiredRoles.Contains(role.Id)))
        {
            blockers.Add($"Hire a {role.Title}; the Team requires one.");
        }

        return new TeamCheck(coverage, vendors, hired.Count, blockers.Count == 0, blockers);
    }

    /// <summary>Tells whether a role has reached its limit, so its pool card greys out (HR-4).</summary>
    /// <param name="role">The role.</param>
    /// <param name="hiredCount">How many of the role are hired.</param>
    /// <returns><see langword="true"/> when <c>MaxPerTeam</c> is above zero and reached.</returns>
    public static bool IsRoleFull(RoleSpec role, int hiredCount)
    {
        ArgumentNullException.ThrowIfNull(role);
        return role.MaxPerTeam > 0 && hiredCount >= role.MaxPerTeam;
    }
}
