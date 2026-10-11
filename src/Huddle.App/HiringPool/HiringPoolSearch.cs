using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.HiringPool;

/// <summary>One card of the hiring pool as the filter sees it: the role a hire stages, its job when it has one, and what it is filed under.</summary>
/// <param name="Role">The role a hire of this card stages, from the type's own list or added from the pool.</param>
/// <param name="Listed">True when the open team type lists the role itself; false for a job added from the pool.</param>
/// <param name="TeamTypes">The ids of the team types the card suits.</param>
/// <param name="Terms">The tags and skill names the second cloud filters by.</param>
internal sealed record PoolEntry(RoleSpec Role, bool Listed, IReadOnlyList<string> TeamTypes, IReadOnlyList<string> Terms);

/// <summary>
/// Builds and filters the cards of the hiring pool (the two tag clouds): the first cloud picks team types (any of them),
/// the second picks tags and skill names (all of them), and the two combine with AND, as the avatar picker's clouds do.
/// </summary>
internal static class HiringPoolSearch
{
    /// <summary>Builds the cards for a team type from its roles, looking each job up for its tags, skills and suited types.</summary>
    /// <param name="roles">The type's roles, listed and pooled.</param>
    /// <param name="typeId">The id of the open team type.</param>
    /// <param name="pool">The hiring pool that holds the jobs.</param>
    /// <returns>One card per role; a role with no job is filed under the open type with no terms.</returns>
    public static IReadOnlyList<PoolEntry> Entries(IReadOnlyList<RoleSpec> roles, string typeId, HiringPoolCatalog pool)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(typeId);
        ArgumentNullException.ThrowIfNull(pool);
        List<PoolEntry> entries = [];
        foreach (RoleSpec role in roles)
        {
            PoolJob? job = role.Job is null ? null : pool.FindJob(role.Job);
            IReadOnlyList<string> types = job is null ? [typeId] : job.TeamTypes;
            IReadOnlyList<string> terms = job is null ? [] : [.. job.Tags.Concat(job.Skills).Distinct(StringComparer.OrdinalIgnoreCase)];
            entries.Add(new PoolEntry(role, !role.Pooled, types, terms));
        }

        return entries;
    }

    /// <summary>Tells whether a card passes the two clouds.</summary>
    /// <param name="entry">The card.</param>
    /// <param name="typeIds">The selected team type ids; empty means every type.</param>
    /// <param name="terms">The selected tags and skill names; the card needs every one.</param>
    /// <param name="openTypeId">The id of the open team type, whose own listed roles always suit it.</param>
    /// <returns>True when the card shows.</returns>
    public static bool Matches(PoolEntry entry, IReadOnlyList<string> typeIds, IReadOnlyList<string> terms, string openTypeId)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(typeIds);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(openTypeId);
        bool typeOk = typeIds.Count == 0
            || (entry.Listed && typeIds.Contains(openTypeId, StringComparer.Ordinal))
            || entry.TeamTypes.Any(type => typeIds.Contains(type, StringComparer.Ordinal));
        return typeOk && terms.All(term => entry.Terms.Contains(term, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Lists the tags and skill names of the cards for the second cloud, most used first.</summary>
    /// <param name="entries">Every card.</param>
    /// <returns>Each distinct term once, by count then alphabetically.</returns>
    public static IReadOnlyList<string> TermPool(IReadOnlyList<PoolEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return [.. entries
            .SelectMany(static entry => entry.Terms)
            .GroupBy(static term => term, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Key)];
    }
}
