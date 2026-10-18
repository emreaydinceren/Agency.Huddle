using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.App.SkillSources;

/// <summary>The union of every role this Huddle knows, de-duplicated by id (§6.7, "Where the roles come from"). The
/// install file's appendix A, the context file and the app's own <see cref="CheckContext"/> all read it, so they cannot
/// disagree on which roles a Pack may name.</summary>
internal sealed class RoleLister(HiringPoolCatalog pool, CustomRoleStore customRoles, TeamTypeCatalog teamTypes)
{
    /// <summary>Raised when any of the three sources changes.</summary>
    public event Action? Changed
    {
        add
        {
            pool.Changed += value;
            customRoles.Changed += value;
            teamTypes.Changed += value;
        }

        remove
        {
            pool.Changed -= value;
            customRoles.Changed -= value;
            teamTypes.Changed -= value;
        }
    }

    /// <summary>Every role, ordered by id ordinal. Custom roles win a clash, then pool jobs, then team-type roles.</summary>
    public IReadOnlyList<RoleInfo> List()
    {
        Dictionary<string, RoleInfo> roles = new(StringComparer.Ordinal);
        foreach (CustomRole role in customRoles.Roles)
        {
            roles.TryAdd(role.Id, new RoleInfo(role.Id, role.Title, role.Description, []));
        }

        Dictionary<string, PoolJob> jobs = new(StringComparer.Ordinal);
        foreach (PoolJob job in pool.Jobs)
        {
            jobs.TryAdd(job.Id, job);
            roles.TryAdd(job.Id, new RoleInfo(job.Id, job.Title, job.Description, job.Packs));
        }

        foreach (RoleSpec role in teamTypes.Types.SelectMany(static type => type.Roles))
        {
            PoolJob? job = role.Job is { } id ? jobs.GetValueOrDefault(id) : null;
            roles.TryAdd(role.Id, new RoleInfo(role.Id, role.Title, job?.Description ?? string.Empty, job?.Packs ?? []));
        }

        return [.. roles.Values.OrderBy(static role => role.Id, StringComparer.Ordinal)];
    }
}
